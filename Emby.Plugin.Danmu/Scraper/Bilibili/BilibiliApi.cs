using System;
using System.Net; 
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Net.Http; // For HttpClient, HttpResponseMessage, HttpContent, etc.
using System.Net.Http.Json; // For ReadFromJsonAsync
using System.Text.Json; // For JsonSerializerOptions
// using ComposableAsync; // For TimeLimiter (assuming it's from this package) - REMOVE THIS
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Scraper.Bilibili.Entity;
using Emby.Plugin.Danmu.Scraper.Entity;
// using MediaBrowser.Common.Net; // We will use System.Net.Http.HttpClient instead of IHttpClient for Bilibili
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Logging;
using Biliproto.Community.Service.Dm.V1; // <--- 替换成实际的命名空间
using Microsoft.Extensions.Caching.Memory;


namespace Emby.Plugin.Danmu.Scraper.Bilibili
{
    public class BilibiliApi : AbstractApi
    {
        private static readonly Regex regBiliplusVideoInfo = new Regex(@"view\((.+?)\);", RegexOptions.Compiled);

        private readonly HttpClient _httpClient; // Use standard HttpClient
        private readonly CookieContainer _cookieContainer; // Managed CookieContainer
        private readonly JsonSerializerOptions _jsonOptions; // For System.Text.Json

        // 为BilibiliApi添加特定的速率限制器和时间戳
        private static readonly SemaphoreSlim _biliApiRateLimiter = new SemaphoreSlim(1, 1);
        private static DateTime _lastBiliApiRequestTime = DateTime.MinValue;
        private static readonly TimeSpan _biliApiMinInterval = TimeSpan.FromMilliseconds(300); // B站API请求之间至少间隔3秒
        // Rate limiting similar to Jellyfin's approach

        /// <summary>
        /// Initializes a new instance of the <see cref="BilibiliApi"/> class.
        /// </summary>
        /// <param name="logManager">The <see cref="ILogManager"/>.</param>
        public BilibiliApi(ILogManager logManager, MediaBrowser.Common.Net.IHttpClient embyHttpClient) // Keep embyHttpClient for AbstractApi if needed, but BiliApi will use its own
            : base(logManager.getDefaultLogger("BilibiliApi"), embyHttpClient) // Pass embyHttpClient to base if AbstractApi requires IHttpClient
        {
            _cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler
            {
                CookieContainer = _cookieContainer,
                UseCookies = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            _httpClient = new HttpClient(handler);

            // --- 为 HttpClient 设置通用的默认请求头 (主要用于 API 调用) ---
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(HTTP_USER_AGENT); // Use User-Agent from AbstractApi
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*"); // API 通常接受 JSON 或纯文本，*/* 作为备选
            _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.8");
            _httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.bilibili.com/"); // API 调用通常需要 Referer
            
            // Sec-Fetch-* 头部，模拟浏览器 AJAX/Fetch 请求
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Site", "same-site"); // API 和主站通常是 same-site
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Mode", "cors"); // API 调用通常是 CORS 模式
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Dest", "empty"); // Fetch/XHR 的 dest 通常是 empty
            // Origin 头部，对于 CORS 请求有时是必需的
            // _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Origin", "https://www.bilibili.com");

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                // Add any other converters or options if needed
            }; // Consider adding more options if needed, like converters for specific types
        }

        /// <summary>
        /// Applies rate limiting before making a request.
        /// </summary>
        protected override async Task LimitRequestFrequently()
        {
            // 首先调用基类的通用延迟 (如果AbstractApi的延迟也需要保留的话)
            // await base.LimitRequestFrequently(); 
    
            // 然后应用针对B站API的更严格的控制
            await _biliApiRateLimiter.WaitAsync().ConfigureAwait(false);
            try
            {
                var now = DateTime.UtcNow;
                var timeSinceLastRequest = now - _lastBiliApiRequestTime;
                if (timeSinceLastRequest < _biliApiMinInterval)
                {
                    await Task.Delay(_biliApiMinInterval - timeSinceLastRequest).ConfigureAwait(false);
                }
                _lastBiliApiRequestTime = DateTime.UtcNow; // 更新最后请求时间
            }
            finally
            {
                _biliApiRateLimiter.Release();
            }
        }

        public async Task<SearchResult> SearchAsync(string keyword, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                return new SearchResult();
            }

            var cacheKey = $"search_{keyword}";
            var expiredOption = new MemoryCacheEntryOptions()
                { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
            if (this._memoryCache.TryGetValue<SearchResult>(cacheKey, out var searchResult))
            {
                return searchResult!; // Nullable reference type fix
            }

            await this.LimitRequestFrequently();
            bool sessionEnsured = await this.EnsureSessionCookie(cancellationToken).ConfigureAwait(false);
            if (!sessionEnsured)
            {
                _logger.Warn($"SearchAsync: 未能建立有效的B站会话 (buvid3缺失)。跳过对关键词 '{keyword}' 的搜索。");
                return new SearchResult(); // 返回空结果，不继续执行搜索
            }

            keyword = HttpUtility.UrlEncode(keyword);

            // 使用新的综合搜索接口 /x/web-interface/search/all/v2
            // platform=pc 是常用的参数，指明搜索来源平台
            // duration=0 表示不限制时长 (B站API文档中 0:全部 1:10分钟以下 2:10-30分钟 3:30-60分钟 4:60分钟以上)
            // order=totalrank 综合排序 (其他可选：click, pubdate, dm, stow)
            // 我们主要关心 media_ft 和 media_bangumi，此接口会返回多种类型，我们需要从中筛选
            var url = $"https://api.bilibili.com/x/web-interface/search/all/v2?keyword={keyword}&platform=pc&duration=0&order=totalrank";
            _logger.Info($"SearchAsync (all/v2) - 正在请求综合搜索接口，关键词：'{keyword}'，URL：{url}");

            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            _logger.Debug($"SearchAsync (all/v2) - 响应状态码: {response.StatusCode}");

            var responseContentString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            _logger.Debug($"SearchAsync (all/v2) - 接收到的搜索响应内容 (总长度: {responseContentString.Length} 字符，内容前缀): {responseContentString.Substring(0, Math.Min(responseContentString.Length, 5000))}");

            response.EnsureSuccessStatusCode();
            var apiResult = System.Text.Json.JsonSerializer.Deserialize<ApiResult<BiliSearchAllV2Data>>(responseContentString, _jsonOptions);

            var finalSearchResult = new SearchResult { Result = new List<Entity.Media>() };

            if (apiResult?.Code == 0 && apiResult.Data != null)
            {
                _logger.Info($"SearchAsync (all/v2) - 综合搜索API成功，关键词 '{keyword}'。总结果数 (所有类型): {apiResult.Data.NumResults}。");
                finalSearchResult.Page = apiResult.Data.Page;
                finalSearchResult.PageSize = apiResult.Data.PageSize;
                finalSearchResult.NumResults = apiResult.Data.NumResults; // This is total results for all types
                finalSearchResult.NumPages = apiResult.Data.NumPages;

                if (apiResult.Data.Result != null)
                {
                    foreach (var group in apiResult.Data.Result)
                    {
                        if ((group.ResultType == "media_ft" || group.ResultType == "media_bangumi") && group.Data != null)
                        {
                            _logger.Info($"SearchAsync (all/v2) - 找到类型 '{group.ResultType}' 的结果 {group.Data.Count} 条。");
                            // Assuming Entity.Media class has fields like 'ApiType' (mapped from "type")
                            // and 'SeasonTypeName' that are correctly deserialized.
                            finalSearchResult.Result.AddRange(group.Data);
                        }
                    }
                    _logger.Info($"SearchAsync (all/v2) - 从综合搜索结果中提取到影视/番剧共 {finalSearchResult.Result.Count} 条。");
                }
                else
                {
                    _logger.Info($"SearchAsync (all/v2) - 综合搜索API成功，但 data.result 列表为 null。关键词 '{keyword}'。");
                }
            }
            else
            {
                _logger.Warn($"SearchAsync (all/v2) - 综合搜索 API (关键词 '{keyword}') 返回异常或空数据：Code={apiResult?.Code}, Message={apiResult?.Message}");
                // finalSearchResult will be empty as initialized
            }

            // Update NumResults to reflect only the count of media_ft and media_bangumi items found
            finalSearchResult.NumResults = finalSearchResult.Result.Count;

            this._memoryCache.Set<SearchResult>(cacheKey, finalSearchResult, expiredOption);
            return finalSearchResult;
        }

        /// <summary>
        /// Get bilibili danmu data for a video by BVID.
        /// </summary>
        /// <param name="bvid">The Bilibili bvid.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>Task containing the danmu content as byte array.</returns>
        /// <exception cref="ArgumentNullException">Thrown if bvid is null or empty.</exception>
        /// <exception cref="Exception">Thrown if the request fails or no part is found.</exception>
        public async Task<byte[]> GetDanmuContentAsync(string bvid, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(bvid))
            {
                throw new ArgumentNullException(nameof(bvid));
            }

            // 1. Get the page list to find the CID for the first part
            // http://api.bilibili.com/x/player/pagelist?bvid={bvid}
            bvid = bvid.Trim();
            _logger.Info($"GetDanmuContentAsync (BVID) - 开始为 BVID '{bvid}' 获取视频分P列表以查找 CID。");

            var pageUrl = $"http://api.bilibili.com/x/player/pagelist?bvid={bvid}";
            var response = await _httpClient.GetAsync(pageUrl, cancellationToken).ConfigureAwait(false);
            
            _logger.Debug($"GetDanmuContentAsync (BVID) - 请求分P列表 URL: {pageUrl}, 响应状态: {response.StatusCode}");

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ApiResult<VideoPart[]>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
            if (result != null && result.Code == 0 && result.Data != null)
            {
                var part = result.Data.FirstOrDefault();
                if (part != null)
                {
                    // 2. Get danmu content using the found CID
                    _logger.Info($"GetDanmuContentAsync (BVID) - 成功获取 BVID '{bvid}' 的第一个分P信息，CID 为 {part.Cid}。将使用此 CID 获取弹幕。");
                    return await this.GetDanmuContentByCidAsync(part.Cid, cancellationToken).ConfigureAwait(false);
                }else {
                    _logger.Warn($"GetDanmuContentAsync (BVID) - BVID '{bvid}' 的分P列表数据为空，虽然API请求成功。");
                }
            }

            throw new Exception($"请求失败或未找到 BVID '{bvid}' 的视频分P信息。API响应代码: {result?.Code}, 消息: {result?.Message}");
        }


        public async Task<byte[]> GetDanmuContentAsync(long epId, CancellationToken cancellationToken)
        {
            if (epId <= 0)
            {
                throw new ArgumentNullException(nameof(epId));
            }

            // 1. Get episode details to find the CID
            
            _logger.Info($"GetDanmuContentAsync (EP ID) - 开始为 EP ID {epId} 获取剧集详情以查找 CID。");

            var episode = await this.GetEpisodeAsync(epId, cancellationToken).ConfigureAwait(false);
            if (episode != null)
            {
                // 2. Get danmu content using the found CID
                
                _logger.Info($"GetDanmuContentAsync (EP ID) - 成功获取 EP ID {epId} 的剧集详情，CID 为 {episode.CId}。将使用此 CID 获取弹幕。");
 
                return await this.GetDanmuContentByCidAsync(episode.CId, cancellationToken).ConfigureAwait(false);
            } else {
                 _logger.Warn($"GetDanmuContentAsync (EP ID) - 未能获取 EP ID {epId} 的剧集详情。");
                throw new Exception($"请求失败或未找到 EP ID 为 {epId} 的剧集。");
            }

            throw new Exception($"Request fail or no episode found for epId={epId}");
        }

        public async Task<byte[]> GetDanmuContentByCidAsync(long cid, CancellationToken cancellationToken)
        {
            if (cid <= 0)
            {
                throw new ArgumentNullException(nameof(cid));
            }

            var url = $"https://api.bilibili.com/x/v1/dm/list.so?oid={cid}";
            
            _logger.Info($"GetDanmuContentByCidAsync - 正在请求 CID 为 {cid} 的 XML 弹幕数据，URL：{url}");

            // Using GetAsync directly as we expect raw bytes, not a JSON object
            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            
            _logger.Debug($"GetDanmuContentByCidAsync - 请求 CID {cid} 的弹幕数据响应状态: {response.StatusCode}");

            if (!response.IsSuccessStatusCode) // Standard HttpResponseMessage has IsSuccessStatusCode
            {
                throw new Exception($"请求弹幕失败。URL: {url}, 状态码: {response.StatusCode}");
            }
            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false); // 移除 cancellationToken 参数
            if (bytes == null || bytes.Length < 2000) // Arbitrary size check, might need tuning
            {
                _logger.Warn($"GetDanmuContentByCidAsync - 获取到的 CID {cid} 的弹幕数据过小 (大小: {bytes?.Length ?? 0} 字节)。URL: {url}。可能视频已下架或弹幕稀少。将返回空弹幕。");
                return Array.Empty<byte>(); // 返回空数组而不是抛出异常，以允许插件继续处理
            }

            _logger.Info($"GetDanmuContentByCidAsync - 成功获取 CID {cid} 的 XML 弹幕数据，大小 {bytes.Length} 字节。");

            return bytes; // This is typically XML data
        }

        public async Task<VideoSeason?> GetSeasonAsync(long seasonId, CancellationToken cancellationToken)
        {
            if (seasonId <= 0)
            {
                return null;
            }

            var cacheKey = $"season_{seasonId}";
            var expiredOption = new MemoryCacheEntryOptions()
                { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };
            VideoSeason? seasonData;
                       
            _logger.Debug($"GetSeasonAsync - 检查缓存中是否存在 Season ID 为 {seasonId} 的数据。");

            if (this._memoryCache.TryGetValue<VideoSeason?>(cacheKey, out seasonData))
            {
                _logger.Debug($"GetSeasonAsync - 从缓存中命中 Season ID {seasonId} 的数据。");

                return seasonData; // No ! needed if TValue in TryGetValue is nullable
            }

            await this.EnsureSessionCookie(cancellationToken).ConfigureAwait(false);

            // Use the /ep/list API which is more reliable for getting episode lists and respects Referer
            var url = $"https://api.bilibili.com/pgc/view/web/ep/list?season_id={seasonId}";
                        
            _logger.Info($"GetSeasonAsync - 正在请求 Season ID 为 {seasonId} 的剧集信息，URL：{url}");

            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

            _logger.Debug($"GetSeasonAsync - 请求 Season ID {seasonId} 的剧集信息响应状态: {response.StatusCode}");

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ApiResult<VideoSeason>>(_jsonOptions, cancellationToken).ConfigureAwait(false);

            if (result != null && result.Code == 0 && result.Result != null)
            {
                // 过滤预告片等非正片内容 (BadgeType == 1 通常表示预告)
                                
                var originalCount = result.Result.Episodes?.Count ?? 0;
                List<VideoEpisode> episodesToCache = null;

                if (result.Result.Episodes != null)
                {
                    episodesToCache = result.Result.Episodes.ToList(); // 复制一份用于缓存，避免修改原始列表影响后续过滤
                     result.Result.Episodes = result.Result.Episodes.Where(x => x.BadgeType != 1).ToList();
                }
                                
                _logger.Info($"GetSeasonAsync - 成功获取并解析 Season ID {seasonId} 的剧集信息。原始集数: {originalCount}，过滤预告后集数: {result.Result.Episodes?.Count ?? 0}。");

                // --- 新增：缓存该季度下的所有（未过滤预告前的）剧集详情 ---
                if (episodesToCache != null) {
                    foreach (var episode in episodesToCache) {
                        var individualEpisodeCacheKey = $"episode_{episode.Id}";
                        this._memoryCache.Set<VideoEpisode?>(individualEpisodeCacheKey, episode, expiredOption); // 使用与 GetEpisodeAsync 相同的过期选项或调整
                        _logger.Debug($"GetSeasonAsync - 为 Season ID {seasonId} 缓存了其下的 EP ID {episode.Id} 的详情。");
                    }
                }
                // --- 新增结束 ---

                this._memoryCache.Set<VideoSeason?>(cacheKey, result.Result, expiredOption);
                return result.Result;
            }

            this._memoryCache.Set<VideoSeason?>(cacheKey, null, expiredOption); // Cache null result to avoid repeated requests for non-existent seasons
            return null;
        }

        /// <summary>
        /// Get Bilibili episode details by episode ID.
        /// </summary>
        /// <param name="epId">The Bilibili episode ID.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>Task containing the VideoEpisode object, or null if not found.</returns>
        public async Task<VideoEpisode?> GetEpisodeAsync(long epId, CancellationToken cancellationToken)
        {
            if (epId <= 0)
            {
                return null;
            }

            var cacheKey = $"episode_{epId}";
            var expiredOption = new MemoryCacheEntryOptions()
                { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
            VideoEpisode? episodeData;
                        
            _logger.Debug($"GetEpisodeAsync - 检查缓存中是否存在 EP ID 为 {epId} 的数据。");
 
            if (this._memoryCache.TryGetValue<VideoEpisode?>(cacheKey, out episodeData))
            {
                
                _logger.Debug($"GetEpisodeAsync - 从缓存中命中 EP ID {epId} 的数据。");
 
                return episodeData; // No ! needed
            }

            await this.EnsureSessionCookie(cancellationToken).ConfigureAwait(false);

            // B站狗屎接口，无论是用ep_id还是season_id都全部返回出来
            var url = $"https://api.bilibili.com/pgc/view/web/ep/list?ep_id={epId}";
            
            _logger.Debug($"GetEpisodeAsync - EP ID {epId} - 即将发起请求。传入的 CancellationToken: {cancellationToken}, URL: {url}");

            // 创建 HttpRequestMessage 以便自定义头部
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);

            // 参考浏览器请求添加/修改头部
            // Origin 头部
            requestMessage.Headers.TryAddWithoutValidation("Origin", "https://www.bilibili.com");

            // Referer 头部 (根据epId动态构造一个更具体的Referer)
            // 注意：如果此API对Referer有严格要求，且格式必须完全匹配浏览器行为，可能需要更复杂的逻辑
            // 但通常，一个指向B站相关页面的Referer比通用的 "https://www.bilibili.com/" 更好，或与默认值一致即可。
            // 浏览器示例中的Referer是播放页，这里我们保持 _httpClient 的默认Referer，除非有明确证据表明需要更改。
            // 如果需要覆盖默认Referer:
            // requestMessage.Headers.Referrer = new Uri($"https://www.bilibili.com/bangumi/play/ep{epId}");

            // Sec-CH-UA* 头部 (Client Hints)
            // 这些值应尽可能与现代浏览器的值保持一致，可以考虑将其作为常量或从配置中读取
            requestMessage.Headers.TryAddWithoutValidation("sec-ch-ua", "\"Chromium\";v=\"136\", \"Microsoft Edge\";v=\"136\", \"Not.A/Brand\";v=\"99\"");
            requestMessage.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
            requestMessage.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
            
            // _httpClient 的 DefaultRequestHeaders 中的 Accept, Accept-Language, Accept-Encoding, User-Agent, Sec-Fetch-* 会被自动应用
            var response = await _httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            try
            {
                if (!response.IsSuccessStatusCode)
                {
                    
                    _logger.Warn($"GetEpisodeAsync - 请求 EP ID {epId} 失败，状态码: {response.StatusCode}");

                    var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    
                    return null;
                }
                
                var responseContentString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.Debug($"GetEpisodeAsync - EP ID {epId} - 原始响应内容 (前500字符): {responseContentString.Substring(0, Math.Min(responseContentString.Length, 500))}");
                ApiResult<VideoSeason> result = null;

                string cleanedResponseContent = responseContentString;
                // 检查是否以 "!" 或 UTF-8 BOM + "!" 开头 (BOM: EF BB BF)
                if (!string.IsNullOrEmpty(responseContentString) &&
                    (responseContentString.StartsWith("!") || 
                     (responseContentString.Length > 0 && responseContentString[0] == '\uFEFF' && responseContentString.Length > 1 && responseContentString[1] == '!')))
                {
                    _logger.Warn($"GetEpisodeAsync - EP ID {epId} - 响应以意外字符开头，尝试清理。");
                    int jsonStartIndex = responseContentString.IndexOfAny(new char[] { '{', '[' });
                    if (jsonStartIndex != -1) 
                    {
                        cleanedResponseContent = responseContentString.Substring(jsonStartIndex);
                        _logger.Debug($"GetEpisodeAsync - EP ID {epId} - 清理后内容前缀: {cleanedResponseContent.Substring(0, Math.Min(cleanedResponseContent.Length, 100))}");
                    } else {
                        _logger.Warn($"GetEpisodeAsync - EP ID {epId} - 清理失败，未找到JSON起始符。原始内容: {responseContentString.Substring(0, Math.Min(responseContentString.Length,100))}");
                    }
                }
                result = System.Text.Json.JsonSerializer.Deserialize<ApiResult<VideoSeason>>(cleanedResponseContent, _jsonOptions);

                if (result?.Code == 0 && result.Result?.Episodes != null)
                {
                    // --- 新增：缓存API返回的所有剧集详情 ---
                    if (result.Result.Episodes.Any()) {
                        _logger.Debug($"GetEpisodeAsync - API为 EP ID {epId} 的请求返回了 {result.Result.Episodes.Count} 个剧集，将全部缓存。");
                        foreach (var episode in result.Result.Episodes) {
                            var individualEpisodeCacheKey = $"episode_{episode.Id}";
                            // 注意：这里不过滤 BadgeType，因为 GetEpisodeAsync 的目的是获取特定 ep_id 的原始信息
                            this._memoryCache.Set<VideoEpisode?>(individualEpisodeCacheKey, episode, expiredOption);
                            _logger.Debug($"GetEpisodeAsync - 缓存了 API 返回的 EP ID {episode.Id} (来自对 EP ID {epId} 的请求)。Title: '{episode.Title}'");
                        }
                    }
                    // --- 新增结束 ---

                    // Find and return the specific episode requested
                    var requestedEpisode = result.Result.Episodes.FirstOrDefault(x => x.Id == epId);

                    if (requestedEpisode != null) {
                        _logger.Info($"GetEpisodeAsync - 成功获取并解析 EP ID {epId} 的剧集详情。");
                        // 此处不再需要单独缓存 requestedEpisode，因为它已经在上面的循环中被缓存了
                        // this._memoryCache.Set<VideoEpisode?>(cacheKey, requestedEpisode, expiredOption); 
                        return requestedEpisode;
                    } else {
                        _logger.Warn($"GetEpisodeAsync - API 响应成功但未在结果中找到请求的 EP ID {epId}。");
                        // Log the first few episodes if available for debugging
                        if (result.Result.Episodes.Any()) {
                            var firstFew = string.Join(", ", result.Result.Episodes.Take(3).Select(e => $"ID:{e.Id} Title:'{e.Title}'"));
                            _logger.Debug($"GetEpisodeAsync - API返回的episodes (前几个): {firstFew}");
                        }
                        this._memoryCache.Set<VideoEpisode?>(cacheKey, null, expiredOption); // Cache null if not found
                        return null;
                    }
                }
                                
                _logger.Warn($"GetEpisodeAsync - EP ID {epId} 的 API 响应数据不符合预期或未包含剧集信息。Code: {result?.Code}, Message: {result?.Message}");
 
                this._memoryCache.Set<VideoEpisode?>(cacheKey, null, expiredOption); // Cache null result
                return null;
            }
            catch (JsonException ex)
            {
                // 尝试获取原始响应内容，如果上面的 ReadAsStringAsync 已经执行过，这里可能不需要再次读取
                // 但为了确保在任何JsonException情况下都有内容，可以保留一个尝试
                string errorContentForLog = "[读取响应内容时也发生错误或内容为空]";
                if (response?.Content != null) {
                    try {
                        errorContentForLog = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        errorContentForLog = errorContentForLog.Substring(0, Math.Min(errorContentForLog.Length, 500)); // 截断
                    } catch {} // 忽略读取错误
                }
                _logger.Error($"GetEpisodeAsync: JSON 反序列化错误。URL: {url}, EP ID: {epId}, 内容: {errorContentForLog}", ex);

                // 重试逻辑（如果需要，之前已添加，这里不再重复，确保它使用 responseContentString 或 errorContentForLog）
                // bool isNotFoundResource = errorContentForLog.Contains("\"code\":-404") || errorContentForLog.Contains("啥都木有");
                // if (isNotFoundResource && retryCount < 1) { ... }
                // ...
                              
                this._memoryCache.Set<VideoEpisode?>(cacheKey, null, expiredOption);
 
                return null;
            }
        }

        /// <summary>
        /// Get Bilibili video details by BVID.
        /// </summary>
        /// <param name="bvid">The Bilibili BVID.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>Task containing the Video object, or null if not found.</returns>
        public async Task<Entity.Video?> GetVideoByBvidAsync(string bvid, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(bvid))
            {
                return null;
            }

            var cacheKey = $"video_{bvid}";
            var expiredOption = new MemoryCacheEntryOptions()
                { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };
            Entity.Video? videoData;
            if (this._memoryCache.TryGetValue<Entity.Video?>(cacheKey, out videoData))
            {
                return videoData; // No ! needed
            }

            await this.EnsureSessionCookie(cancellationToken).ConfigureAwait(false);

            var url = $"https://api.bilibili.com/x/web-interface/view?bvid={bvid}";
                        
            _logger.Info($"GetVideoByBvidAsync - 正在请求 BVID '{bvid}' 的视频详情，URL：{url}");
 
            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            
            _logger.Debug($"GetVideoByBvidAsync - 请求 BVID '{bvid}' 的视频详情响应状态: {response.StatusCode}");
            
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ApiResult<Entity.Video>>(_jsonOptions, cancellationToken).ConfigureAwait(false);

            if (result != null && result.Code == 0 && result.Data != null)
            {

                _logger.Info($"GetVideoByBvidAsync - 成功获取并解析 BVID '{bvid}' 的视频详情。");
  
                this._memoryCache.Set<Entity.Video?>(cacheKey, result.Data, expiredOption);
                return result.Data;
            }

            _logger.Warn($"GetVideoByBvidAsync - BVID '{bvid}' 的 API 响应数据不符合预期或未包含视频信息。Code: {result?.Code}, Message: {result?.Message}");
  
            // Ensure correct type for caching null
            this._memoryCache.Set<Entity.Video?>(cacheKey, null, expiredOption); // Cache null result
            return null;
        }

        /// <summary>
        /// Get Biliplus video details by AV ID.
        /// </summary>
        /// <param name="avid">The Bilibili AV ID.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>Task containing the BiliplusVideo object, or null if not found or parsing fails.</returns>
        public async Task<BiliplusVideo?> GetVideoByAvidAsync(string avid, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(avid))
            {
                return null;
            }

            var cacheKey = $"video_{avid}";
            var expiredOption = new MemoryCacheEntryOptions()
                { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };
            BiliplusVideo? videoData;
                        
            //_logger.Debug($"GetVideoByBvidAsync - 检查缓存中是否存在 BVID '{bvid}' 的数据。");

            if (this._memoryCache.TryGetValue<BiliplusVideo?>(cacheKey, out videoData))
            {

                //_logger.Debug($"GetVideoByBvidAsync - 从缓存中命中 BVID '{bvid}' 的数据。");

                return videoData; // No ! needed
            }

            var url = $"https://www.biliplus.com/video/{avid}/";
            BiliplusVideo? videoInfo = null;
            try
            {
                // Use GetAsync directly as we need to read the raw HTML string
                var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode(); // Ensure request was successful

                // Standard HttpContent has ReadAsStringAsync
                var htmlResult = await response.Content.ReadAsStringAsync().ConfigureAwait(false); // 移除 cancellationToken 参数
                var videoJson = regBiliplusVideoInfo.FirstMatchGroup(htmlResult); // Assuming FirstMatchGroup is an extension method

                if (!string.IsNullOrEmpty(videoJson))
                {
                    // Assuming FromJson<T> is an extension method in Emby.Plugin.Danmu.Core.Extensions
                    // If not, replace with your project's JSON deserialization method (e.g., System.Text.Json.JsonSerializer.Deserialize)
                    videoInfo = videoJson.FromJson<BiliplusVideo>();
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't re-throw, just return null
                _logger.LogError(ex, "Failed to get video info from Biliplus for avid: {0}", avid);
            }

            if (videoInfo != null)
            {
                this._memoryCache.Set<BiliplusVideo?>(cacheKey, videoInfo, expiredOption);
                return videoInfo;
            }

            this._memoryCache.Set<BiliplusVideo?>(cacheKey, null, expiredOption); // Cache null result
            return null;
        }

        /// <summary>
        /// 下载实时弹幕，返回弹幕列表 (Protobuf format).
        /// protobuf定义：https://github.com/SocialSisterYi/bilibili-API-collect/blob/master/grpc_api/bilibili/community/service/dm/v1/dm.proto
        /// </summary>
        /// <param name="aid">稿件avID</param>
        /// <param name="cid">视频CID</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>Task containing the ScraperDanmaku object, or null if download fails.</returns>
        public async Task<ScraperDanmaku?> GetDanmuContentByProtoAsync(long aid, long cid,
            CancellationToken cancellationToken)
        {
            var danmaku = new ScraperDanmaku();
            danmaku.ChatId = cid;
            danmaku.ChatServer = "api.bilibili.com"; // Or the actual server if different
            danmaku.Items = new List<ScraperDanmakuText>();

            await this.EnsureSessionCookie(cancellationToken).ConfigureAwait(false);

            try
            {
                var segmentIndex = 1; // 分包，每6分钟一包
                while (true)
                {
                    var url =
                        $"https://api.bilibili.com/x/v2/dm/web/seg.so?type=1&oid={cid}&pid={aid}&segment_index={segmentIndex}";
                    var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

                    if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
                    {
                        // 已经到最后了 (No more segments)
                        break;
                    }

                    // Check for Bilibili specific error codes in headers
                    // Standard HttpResponseMessage.Headers is HttpResponseHeaders, which has TryGetValues
                    if (response.Headers.TryGetValues("bili-status-code", out IEnumerable<string> biliStatusCodeValues))
                    {
                        var biliStatusCode = biliStatusCodeValues.FirstOrDefault();
                        if (biliStatusCode == "-352")
                        {
                            this._logger.Warn($"下载部分弹幕失败. bili-status-code: {biliStatusCode} url: {url}");
                            return danmaku; // Return whatever has been collected so far
                        }
                    }
                    response.EnsureSuccessStatusCode(); // Throw if status code is not success (and not NotModified)

                    var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false); // 移除 cancellationToken 参数

                    // Check if bytes are empty or too small before parsing
                    if (bytes == null || bytes.Length == 0)
                    {
                         // No data for this segment, might be the end or an empty segment
                         break;
                    }

                    // Assuming Biliproto.Community.Service.Dm.V1.DmSegMobileReply is correctly defined and available
                    Biliproto.Community.Service.Dm.V1.DmSegMobileReply danmuReply;
                    try
                    {
                         danmuReply = DmSegMobileReply.Parser.ParseFrom(bytes); // 如果上面的 using 正确，这里就可以简化
                    }
                    catch (Exception parseEx)
                    {
                         _logger.LogError(parseEx, "Failed to parse protobuf danmaku for url: {0}", url);
                         // Decide how to handle parse errors - skip segment, or stop?
                         // For now, log and continue to next segment
                         segmentIndex += 1;
                         await Task.Delay(200, cancellationToken); // Small delay before next segment attempt
                         continue; // Skip to next segment
                    }

                    if (danmuReply == null || danmuReply.Elems == null || danmuReply.Elems.Count <= 0)
                    {
                        // No elements in this segment
                        break; // Assume this is the last segment with data
                    }

                    var segmentList = new List<ScraperDanmakuText>();
                    foreach (var dm in danmuReply.Elems)
                    {
                        // <d p="944.95400,5,25,16707842,1657598634,0,ece5c9d1,1094775706690331648,11">今天的风儿甚是喧嚣</d>
                        // time, mode, size, color, create, pool, sender, id, weight(屏蔽等级)
                        segmentList.Add(new ScraperDanmakuText()
                        {
                            Id = dm.Id,
                            Progress = dm.Progress, // 毫秒
                            Mode = dm.Mode,
                            Fontsize = dm.Fontsize,
                            Color = dm.Color, // 整数颜色值
                            MidHash = dm.MidHash,
                            Content = dm.Content,
                            Ctime = dm.Ctime, // Unix时间戳
                            Weight = dm.Weight,
                            Pool = dm.Pool,
                            // Additional fields from proto if needed: id_str, attr
                        });
                    }

                    // 将分段中的所有弹幕添加到结果列表中。
                    // 之前的代码使用了 .ExtractToNumber(1200) 方法，这会限制每个分段最多只获取1200条弹幕，
                    // 导致弹幕下载不完整。现在我们添加所有获取到的弹幕。
                    danmaku.Items.AddRange(segmentList);

                    segmentIndex += 1;

                    // 等待一段时间避免api请求太快
                    await Task.Delay(500, cancellationToken); // Small delay between segment fetches
                }
            }
            catch (Exception ex)
            {
                // Log the error but allow returning partially downloaded danmaku
                this._logger.LogError(ex, "下载弹幕出错");
                // Optionally re-throw if any error should stop the process entirely
                // throw;
            }

            return danmaku; // Return collected danmaku (could be partial or empty on error)
        }

        /// <summary>
        /// Ensures a session cookie (like buvid3) exists by making a request to the Bilibili homepage if necessary.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>A Task that resolves to true if the session cookie is ensured, false otherwise.</returns>
        private async Task<bool> EnsureSessionCookie(CancellationToken cancellationToken)
        {
            var url = "https://www.bilibili.com";
            var uri = new Uri(url);
            var currentCookies = this._cookieContainer.GetCookies(uri);
            if (currentCookies.Cast<Cookie>().Any(c => c.Name == "buvid3" && !c.Expired))
            {
                _logger.Debug("在本地 _cookieContainer 中找到未过期的 buvid3。会话 Cookie 可能已存在。");
                // 之前这里有 /nav API 验证逻辑，可以考虑是否恢复或改进
                return true;
            }

            _logger.Info("本地未找到有效 buvid3，正在尝试通过请求B站首页获取 Bilibili 会话 Cookie (buvid3)...");

            try
            {
                // 为首页请求构造 HttpRequestMessage 以便精确控制头部
                var request = new HttpRequestMessage(HttpMethod.Get, uri);

                // --- 为获取首页的请求设置特定的导航类型头部 ---
                // 清除或覆盖可能不适用于页面导航的默认 Accept 头
                request.Headers.Accept.Clear();
                // 请根据浏览器抓包结果精确设置以下头部
                request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"); // 示例值
                request.Headers.AcceptLanguage.Clear();
                request.Headers.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.8"); // 示例值
                request.Headers.AcceptEncoding.Clear();
                request.Headers.AcceptEncoding.ParseAdd("gzip, deflate, br"); // 示例值
                
                // User-Agent 会从 _httpClient.DefaultRequestHeaders 继承，确保它是最新的浏览器UA
                // 或者在这里显式设置一个最新的浏览器UA
                // request.Headers.UserAgent.Clear();
                // request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");


                request.Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
                request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "none"); // 初始导航
                request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
                request.Headers.TryAddWithoutValidation("Sec-Fetch-User", "?1");
                request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
                
                // 关键：确保首页请求没有 Referer，或 Referer 不指向 bilibili.com 自身
                request.Headers.Referrer = null;

                // _logger.Debug($"EnsureSessionCookie - 发往B站首页 ({url}) 的请求头信息:");
                // foreach (var header in request.Headers) { _logger.Debug($"  RequestHeader - {header.Key}: {string.Join(", ", header.Value)}"); }
                // ... （其他日志）

                var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                // ... (后续的手动处理 Set-Cookie 和检查 buvid3 的逻辑保持不变) ...
                // （请参考您之前包含手动处理 Set-Cookie 的版本）
                _logger.Debug($"EnsureSessionCookie - 请求B站首页 ({url}) 响应状态: {response.StatusCode}");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.Warn($"请求B站首页 ({url}) 以获取会话 Cookie 时返回状态码: {response.StatusCode}");
                    return false;
                }

                _logger.Info("EnsureSessionCookie - 请求B站首页已完成，正在尝试手动处理 Set-Cookie 响应头。");
                // _logger.Debug($"EnsureSessionCookie - 从B站首页 ({url}) 收到的响应头信息:");
                // // foreach (var header in response.Headers) { _logger.Debug($"  ResponseHeader - {header.Key}: {string.Join(", ", header.Value)}"); }
                // // foreach (var header in response.Content.Headers) { _logger.Debug($"  ResponseContentHeader - {header.Key}: {string.Join(", ", header.Value)}"); }

                bool buvid3SetByServer = false;
                if (response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string> setCookieHeaders))
                {
                    foreach (var cookieHeaderValue in setCookieHeaders)
                    {
                        try
                        {
                            _cookieContainer.SetCookies(uri, cookieHeaderValue);
                            _logger.Debug($"EnsureSessionCookie - 已处理 Set-Cookie 响应头: {cookieHeaderValue}");
                            if (cookieHeaderValue.IndexOf("buvid3=", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                buvid3SetByServer = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"处理 Set-Cookie 响应头值时出错: {cookieHeaderValue} (URI: {uri})", ex);
                        }
                    }
                }
                else
                {
                    _logger.Warn($"从B站首页 ({url}) 的响应中未找到 Set-Cookie 响应头。");
                    // 记录响应体的前一部分，以帮助诊断返回的是什么类型的页面
                    try
                    {
                        string responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        _logger.Debug($"EnsureSessionCookie - 首页响应体 (前1000字符，当Set-Cookie未找到时): {responseBody.Substring(0, Math.Min(responseBody.Length, 1000))}");
                    }
                    catch (Exception readEx)
                    {
                        _logger.Warn($"EnsureSessionCookie - 读取首页响应体失败 (当Set-Cookie未找到时): {readEx.Message}");
                    }
                }

                var cookiesAfterHomepage = this._cookieContainer.GetCookies(uri);
                var buvid3Cookie = cookiesAfterHomepage.Cast<Cookie>().FirstOrDefault(c => c.Name == "buvid3" && !c.Expired);
                if (buvid3Cookie != null) {
                    _logger.Info($"请求首页后，在 _cookieContainer 中找到有效 buvid3 Cookie: {buvid3Cookie.Value.Substring(0, Math.Min(buvid3Cookie.Value.Length,10))}...");
                    return true;
                } else {
                    if (buvid3SetByServer) {
                        _logger.Warn($"请求首页后，服务器发送了包含buvid3的Set-Cookie，但在_cookieContainer中未找到有效buvid3。可能Cookie设置失败或已立即过期。");
                    } else {
                        _logger.Warn($"请求首页后，在 _cookieContainer 中仍未找到有效 buvid3 Cookie。服务器响应头中未包含有效的Set-Cookie指令。");
                    }
                    return false;
                } 
            }
            catch (HttpRequestException ex)
            {
                _logger.Error($"从B站首页 ({url}) 获取会话 Cookie 时发生 HttpRequestException。", ex);
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error("从B站首页获取会话 Cookie 失败。", ex);
                return false;
            }
        } 

        private async Task<bool> GetBuvidFromApi(CancellationToken cancellationToken)
        {
            var getBuvidUrl = "https://api.bilibili.com/x/web-frontend/getbuvid";
            _logger.Info($"尝试从 /x/web-frontend/getbuvid 获取 buvid3，URL: {getBuvidUrl}");

            try
            {
                var response = await _httpClient.GetAsync(getBuvidUrl, cancellationToken).ConfigureAwait(false);

                _logger.Debug($"请求 /x/web-frontend/getbuvid 响应状态: {response.StatusCode}");

                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                _logger.Debug($"从 /x/web-frontend/getbuvid 接收到的响应体: {responseBody}");

                var buvidResult = System.Text.Json.JsonSerializer.Deserialize<BuvidApiResponse>(responseBody, _jsonOptions);

                if (buvidResult?.Code == 0 && buvidResult.Data?.Buvid != null)
                {
                    var buvid = buvidResult.Data.Buvid;
                    _logger.Info($"成功从 /x/web-frontend/getbuvid 获取 buvid3: {buvid}");

                    // 手动设置 buvid3 Cookie
                    var cookie = new Cookie("buvid3", buvid, "/", ".bilibili.com");
                    _cookieContainer.Add(new Uri("https://.bilibili.com"), cookie);
                    _logger.Info($"已在 CookieContainer 中设置新的 buvid3: {buvid.Substring(0, Math.Min(buvid.Length, 10))}...");
                    return true;
                }
                else
                {
                    _logger.Warn($"/x/web-frontend/getbuvid API 返回异常或数据缺失: Code={buvidResult?.Code}");
                    return false;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.Error($"从 /x/web-frontend/getbuvid ({getBuvidUrl}) 获取 buvid3 时发生 HttpRequestException。", ex);
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error("从 /x/web-frontend/getbuvid 获取 buvid3 失败。", ex);
                return false;
            }
        } 

        // 定义用于反序列化 /x/web-frontend/getbuvid 响应的类
        private class BuvidApiResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("code")]
            public int Code { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("data")]
            public BuvidData Data { get; set; }
        }
        private class BuvidData {
            [System.Text.Json.Serialization.JsonPropertyName("buvid")]
            public string Buvid { get; set; }
        }
    }
}
