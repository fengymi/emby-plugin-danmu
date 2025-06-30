using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Scrapers.Mgtv.Entity;
using Emby.Plugin.Danmu.Scraper;
using MediaBrowser.Common.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using MediaBrowser.Model.Logging;

namespace Emby.Plugin.Danmu.Scrapers.Mgtv
{
    public class MgtvApi : AbstractApi
    {
        private static readonly SemaphoreSlim _mgtvApiRateLimiter = new SemaphoreSlim(1, 1);
        private static DateTime _lastMgtvApiRequestTime = DateTime.MinValue;
        private static readonly TimeSpan _mgtvApiMinInterval = TimeSpan.FromMilliseconds(500);
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="MgtvApi"/> class.
        /// </summary>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/>.</param>
        public MgtvApi(ILogManager logManager, IHttpClient httpClient)
            : base(logManager.getDefaultLogger("MgtvApi"), httpClient)
        {
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            _httpClient = new HttpClient(handler);
            // --- 为 HttpClient 设置通用的默认请求头 (主要用于 API 调用) ---
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(HTTP_USER_AGENT);
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
            _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.8");
            _httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.mgtv.com/");

            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Site", "same-site");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Mode", "cors");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Dest", "empty");
        }


        public async Task<List<MgtvSearchItem>> SearchAsync(string keyword, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                return new List<MgtvSearchItem>();
            }

            var cacheKey = $"search_{keyword}";
            var expiredOption = new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
            if (_memoryCache.TryGetValue<List<MgtvSearchItem>>(cacheKey, out var cacheValue))
            {
                return cacheValue;
            }

            await this.LimitRequestFrequently();

            keyword = HttpUtility.UrlEncode(keyword);
            var url = $"https://mobileso.bz.mgtv.com/msite/search/v2?q={keyword}&pc=30&pn=1&sort=-99&ty=0&du=0&pt=0&corr=1&abroad=0&_support=10000000000000000";
            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var result = new List<MgtvSearchItem>();
            var searchResult = await DeserializeJsonResponseAsync<MgtvSearchResult>(response, cancellationToken).ConfigureAwait(false);
            if (searchResult != null && searchResult.Data != null && searchResult.Data.Contents != null)
            {
                foreach (var content in searchResult.Data.Contents)
                {
                    if (content.Type != "media")
                    {
                        continue;
                    }
                    foreach (var item in content.Data)
                    {
                        if (string.IsNullOrEmpty(item.Id))
                        {
                            continue;
                        }

                        result.Add(item);
                    }
                }
            }

            _memoryCache.Set<List<MgtvSearchItem>>(cacheKey, result, expiredOption);
            return result;
        }

        public async Task<MgtvVideo?> GetVideoAsync(string id, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            var cacheKey = $"media_{id}";
            var expiredOption = new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };
            if (_memoryCache.TryGetValue<MgtvVideo?>(cacheKey, out var video))
            {
                return video;
            }

            var month = "";
            var idx = 0;
            var total = 0;
            var videoInfo = new MgtvVideo() { Id = id };
            var list = new List<MgtvEpisode>();
            do
            {
                var url = $"https://pcweb.api.mgtv.com/variety/showlist?allowedRC=1&collection_id={id}&month={month}&page=1&_support=10000000";
                var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var result = await DeserializeJsonResponseAsync<MgtvEpisodeListResult>(response, cancellationToken).ConfigureAwait(false);
                if (result != null && result.Data != null && result.Data.List != null)
                {
                    list.AddRange(result.Data.List.Where(x => x.SourceClipId == id));

                    total = result.Data.Tabs.Count;
                    idx++;
                    month = idx < total ? result.Data.Tabs[idx].Month : "";
                }

                // 等待一段时间避免api请求太快
                await Task.Delay(300, cancellationToken).ConfigureAwait(false);
            } while (idx < total && !string.IsNullOrEmpty(month));

            videoInfo.EpisodeList = list.OrderBy(x => x.VideoId).ToList();
            _memoryCache.Set<MgtvVideo?>(cacheKey, videoInfo, expiredOption);
            return videoInfo;
        }




        public async Task<List<MgtvComment>> GetDanmuContentAsync(string cid, string vid, CancellationToken cancellationToken)
        {
            var danmuList = new List<MgtvComment>();
            if (string.IsNullOrEmpty(vid))
            {
                return danmuList;
            }

            // https://galaxy.bz.mgtv.com/getctlbarrage?version=8.1.39&abroad=0&uuid=&os=10.15.7&platform=0&deviceid=42813b17-99f8-4e34-98a2-2c37537667ad&mac=&vid=21920728&pid=&cid=593455&ticket=
            var ctlbarrageUrl = $"https://galaxy.bz.mgtv.com/getctlbarrage?version=8.1.39&abroad=0&uuid=&os=10.15.7&platform=0&mac=&vid={vid}&pid=&cid={cid}&ticket=";
            var ctlbarrageResponse = await _httpClient.GetAsync(ctlbarrageUrl, cancellationToken).ConfigureAwait(false);
            ctlbarrageResponse.EnsureSuccessStatusCode();
            
            var ctlbarrageResult = await DeserializeJsonResponseAsync<MgtvControlBarrageResult>(ctlbarrageResponse, cancellationToken).ConfigureAwait(false);
            if (ctlbarrageResult != null && ctlbarrageResult.Data != null && ctlbarrageResult.Data.CdnVersion != null)
            {
                // https://pcweb.api.mgtv.com/video/info?allowedRC=1&cid=593455&vid=21920892&change=3&datatype=1&type=1&_support=10000000
                var videoInfoUrl = $"https://pcweb.api.mgtv.com/video/info?allowedRC=1&cid={cid}&vid={vid}&change=3&datatype=1&type=1&_support=10000000";
                var videoInfoResponse = await _httpClient.GetAsync(videoInfoUrl, cancellationToken).ConfigureAwait(false);
                videoInfoResponse.EnsureSuccessStatusCode();

                var videoInfoResult = await DeserializeJsonResponseAsync<MgtvVideoInfoResult>(videoInfoResponse, cancellationToken).ConfigureAwait(false);
                if (videoInfoResult != null && videoInfoResult.Data != null && videoInfoResult.Data.Info != null)
                {
                    var time = 0;
                    var totalMinutes = videoInfoResult.Data.Info.TotalMinutes;
                    while (time < totalMinutes)
                    {
                        try
                        {
                            // https://bullet-ali.hitv.com/bullet/tx/2024/12/5/093517/21920728/20.json
                            var segmentUrl = $"https://{ctlbarrageResult.Data.CdnHost}/{ctlbarrageResult.Data.CdnVersion}/{time}.json";
                            var segmentResponse = await _httpClient.GetAsync(segmentUrl, cancellationToken).ConfigureAwait(false);
                            segmentResponse.EnsureSuccessStatusCode();

                            var segmentResult = await DeserializeJsonResponseAsync<MgtvCommentSegmentResult>(segmentResponse, cancellationToken).ConfigureAwait(false);
                            if (segmentResult?.Data?.Items != null)
                            {
                                danmuList.AddRange(segmentResult.Data.Items);
                            }
                            else
                            {
                                _logger.Warn("MGTV弹幕分段数据为空或格式不正确，跳过。URL: {0}", segmentUrl);
                            }
                        }
                        catch (Exception ex)
                        {
                            // 当某个分段下载失败时（如404），记录错误并继续尝试下一个分段
                            _logger.LogError(ex, "下载MGTV弹幕分段时发生错误，将跳过此分段。Time: {0}", time);
                        }
                        time += 1;
                        // 等待一段时间避免api请求太快
                        await Task.Delay(300, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            else
            {
                danmuList = await this.GetDanmuContentByCdnAsync(cid, vid, cancellationToken).ConfigureAwait(false);
            }

            return danmuList;
        }

        private async Task<List<MgtvComment>> GetDanmuContentByCdnAsync(string cid, string vid, CancellationToken cancellationToken)
        {
            var danmuList = new List<MgtvComment>();
            if (string.IsNullOrEmpty(vid))
            {
                return danmuList;
            }


            var time = 0;
            do
            {
                try
                {
                    var segmentUrl = $"https://galaxy.bz.mgtv.com/cdn/opbarrage?vid={vid}&pid=&cid={cid}&ticket=&time={time}&allowedRC=1";
                    var segmentResponse = await _httpClient.GetAsync(segmentUrl, cancellationToken).ConfigureAwait(false);
                    segmentResponse.EnsureSuccessStatusCode();

                    var segmentResult = await DeserializeJsonResponseAsync<MgtvCommentSegmentResult>(segmentResponse, cancellationToken).ConfigureAwait(false);
                    if (segmentResult?.Data?.Items != null)
                    {
                        danmuList.AddRange(segmentResult.Data.Items);
                        time = segmentResult.Data.Next; // 仅在成功时更新时间
                    }
                    else
                    {
                        _logger.Warn("MGTV弹幕(CDN)分段数据为空或格式不正确，终止下载。URL: {0}", segmentUrl);
                        time = 0; // 终止循环
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "下载MGTV弹幕(CDN)分段时发生错误，将终止下载。Time: {0}", time);
                    time = 0; // 发生错误时终止循环
                }

                if (time > 0) {
                    await Task.Delay(300, cancellationToken).ConfigureAwait(false);
                }
            }
            while (time > 0);
        

            return danmuList;
        }

        private async Task<T> DeserializeJsonResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken) where T : class
        {
            // 检查响应头中的内容编码
            if (response.Content.Headers.ContentEncoding.Any(x => x.Equals("br", StringComparison.OrdinalIgnoreCase)))
            {
                _logger.Error("服务器返回了Brotli压缩的数据，但当前.NET框架版本不支持自动解压。请升级项目框架或添加Brotli支持库。URL: {0}", response.RequestMessage?.RequestUri);
                // 因为无法解压，所以无法继续，返回默认值以避免JSON解析错误
                return null;
            }

            string responseContentForLog = null;
            try
            {
                // 对于支持的编码（或无编码），正常解析
                // 先读取为字符串，以便在解析失败时记录日志
                responseContentForLog = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (string.IsNullOrEmpty(responseContentForLog))
                {
                    _logger.Warn("响应内容为空。URL: {0}", response.RequestMessage?.RequestUri);
                    return null;
                }
                return JsonSerializer.Deserialize<T>(responseContentForLog, _jsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.ErrorException("JSON反序列化失败。URL: {0}, 内容: {1}", ex, response.RequestMessage?.RequestUri, responseContentForLog?.Substring(0, Math.Min(500, responseContentForLog.Length)));
                return null;
            }
            catch (Exception ex)
            {
                _logger.ErrorException("反序列化时发生未知错误。URL: {0}", ex, response.RequestMessage?.RequestUri);
                return null;
            }
        }

        protected async Task LimitRequestFrequently()
        {
            await _mgtvApiRateLimiter.WaitAsync().ConfigureAwait(false);
            try
            {
                var now = DateTime.UtcNow;
                var timeSinceLastRequest = now - _lastMgtvApiRequestTime;
                if (timeSinceLastRequest < _mgtvApiMinInterval)
                {
                    await Task.Delay(_mgtvApiMinInterval - timeSinceLastRequest).ConfigureAwait(false);
                }
                _lastMgtvApiRequestTime = DateTime.UtcNow; // 更新最后请求时间
            }
            finally
            {
                _mgtvApiRateLimiter.Release();
            }
        }
    }
}