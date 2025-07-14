using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Core.Singleton;
using Emby.Plugin.Danmu.Scraper.Tencent.Entity;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace Emby.Plugin.Danmu.Scraper.Tencent
{
    public class TencentApi : AbstractApi
    {
        protected Dictionary<string, string> defaultHeaders;
        protected string[] cookies;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="TencentApi"/> class.
        /// </summary>
        /// <param name="loggerFactory">The <see cref="ILoggerFactory"/>.</param>
        public TencentApi(ILogManager logManager, IHttpClient httpClient)
            : base(logManager.getDefaultLogger("TencentApi"), httpClient)
        {

            this.defaultHeaders = new Dictionary<string, string>
            {
                { "referer", "https://v.qq.com/" }
            };

            this.cookies = new[]
            {
                "pgv_pvid=40b67e3b06027f3d; video_platform=2; vversion_name=8.2.95; video_bucketid=4; video_omgid=0a1ff6bc9407c0b1cff86ee5d359614d"
            };
        }


        protected override Dictionary<string, string> GetDefaultHeaders()
        {
            return defaultHeaders;
        }
        
        protected override string[] GetDefaultCookies(string? url=null)
        {
            return cookies;
        }

        public async Task<List<TencentVideo>> SearchAsync(string keyword, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                return new List<TencentVideo>();
            }

            var cacheKey = $"search_{keyword}";
            var expiredOption = new MemoryCacheEntryOptions()
                { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) };
            if (!SingletonManager.IsDebug && _memoryCache.TryGetValue<List<TencentVideo>>(cacheKey, out var cacheValue))
            {
                return cacheValue;
            }

            await this.LimitRequestFrequently();

            var originPostData = new TencentSearchRequest() { Query = keyword };
            var url = $"https://pbaccess.video.qq.com/trpc.videosearch.mobile_search.HttpMobileRecall/MbSearchHttp";

            var result = new List<TencentVideo>();
            var searchResult = await httpClient.GetSelfResultAsyncWithError<TencentSearchResult>(GetDefaultHttpRequestOptions(url, null, cancellationToken), null, "POST", originPostData);
            
            if (searchResult != null && searchResult.Data != null && searchResult.Data.NormalList != null &&
                searchResult.Data.NormalList.ItemList != null)
            {
                foreach (var item in searchResult.Data.NormalList.ItemList)
                {
                    if (item.VideoInfo.Year == null || item.VideoInfo.Year == 0)
                    {
                        continue;
                    }
                    
                    if (item.VideoInfo.Title.Distance(keyword) <= 0)
                    {
                        continue;
                    }

                    var video = item.VideoInfo;
                    video.Id = item.Doc.Id;
                    result.Add(video);
                }
            }

            _memoryCache.Set<List<TencentVideo>>(cacheKey, result, expiredOption);
            return result;
        }

        public async Task<TencentVideo?> GetVideoAsync(string id, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            var cacheKey = $"media_{id}";
            var expiredOption = new MemoryCacheEntryOptions()
                { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };
            if (_memoryCache.TryGetValue<TencentVideo?>(cacheKey, out var video))
            {
                return video;
            }

            var url = $"https://pbaccess.video.qq.com/trpc.universal_backend_service.page_server_rpc.PageServer/GetPageData?video_appid=3000010&vplatform=2";
            var allEpisodes = new List<TencentEpisode>();

            // --- 切换为手动拼接分页参数的逻辑 ---
            var pageSize = 100;
            var beginNum = 1;
            var endNum = pageSize;
            var nextPageContext = string.Empty; // 用于构造请求的分页参数
            var lastId = string.Empty; // 用于防止死循环
            // --- 逻辑切换结束 ---

            try
            {
                do
                {
                    // 首次请求PageContext为空，后续请求使用手动构造的字符串
                    var pageParams = new TencentPageParams() { Cid = id, PageSize = $"{pageSize}", PageContext = nextPageContext };
                    var originPostData = new TencentEpisodeListRequest() { PageParams = pageParams };
                    var result = await httpClient.GetSelfResultAsyncWithError<TencentEpisodeListResult>(GetDefaultHttpRequestOptions(url), null, "POST", originPostData).ConfigureAwait(false);

                    nextPageContext = string.Empty; // 每次循环重置，如果需要下一页再重新赋值

                    // 使用更健壮的方式解析深层嵌套的对象
                    var itemDataLists = result?.Data?.ModuleListDatas?.FirstOrDefault()?.ModuleDatas?.FirstOrDefault()?.ItemDataLists;

                    if (itemDataLists?.ItemDatas != null && itemDataLists.ItemDatas.Any())
                    {
                        var newEpisodes = itemDataLists.ItemDatas
                            .Select(x => x.ItemParams)
                            // 增加更详细的过滤规则，过滤掉预告、彩蛋、直拍等非正片内容
                            .Where(x => x != null && x.IsTrailer != "1" && !x.Title.Contains("直拍") && !x.Title.Contains("彩蛋") && !x.Title.Contains("直播回顾"))
                            .ToList();

                        // 防死循环检查：如果本次获取的最后一集和上次的最后一集相同，则停止
                        if (newEpisodes.Any() && newEpisodes.Last().Vid == lastId)
                        {
                            _logger.Warn($"TencentApi.GetVideoAsync - 检测到重复的分页数据 (lastId: {lastId})，为避免死循环，终止获取。");
                            break;
                        }

                        allEpisodes.AddRange(newEpisodes);
                        _logger.Info($"TencentApi.GetVideoAsync - 成功为ID '{id}' 获取并解析了 {newEpisodes.Count()} 个剧集分片。当前总数: {allEpisodes.Count}。");

                        // 判断是否需要请求下一页
                        if (itemDataLists.ItemDatas.Count == pageSize)
                        {
                            beginNum += pageSize;
                            endNum += pageSize;
                            nextPageContext = $"episode_begin={beginNum}&episode_end={endNum}&episode_step={pageSize}";
                            lastId = allEpisodes.Last().Vid;

                            // 等待一段时间避免 api 请求太快
                            if (!cancellationToken.IsCancellationRequested)
                            {
                                await Task.Delay(300, cancellationToken).ConfigureAwait(false);
                            }
                        }
                    }
                    else
                    {
                        _logger.Warn($"TencentApi.GetVideoAsync - 腾讯API为ID '{id}' 的分页请求未返回有效的剧集列表。终止分页。响应: {result?.ToJson()}");
                        // nextPageContext 此时为空, 循环将自然终止
                    }
                } while (!string.IsNullOrEmpty(nextPageContext) && !cancellationToken.IsCancellationRequested);

                if (allEpisodes.Any())
                {
                    var videoInfo = new TencentVideo
                    {
                        Id = id,
                        // 某些综艺节目可能会返回重复的剧集，这里进行去重
                        EpisodeList = allEpisodes.GroupBy(e => e.Vid).Select(g => g.First()).ToList()
                    };
                    _logger.Info($"TencentApi.GetVideoAsync - ID '{id}' 的所有剧集获取完成，总计 {videoInfo.EpisodeList.Count} 个。");
                    _memoryCache.Set<TencentVideo?>(cacheKey, videoInfo, expiredOption);
                    return videoInfo;
                }

            }
            catch (Exception ex)
            {
                _logger.Error("TencentApi.GetVideoAsync - 处理ID '{0}' 时发生错误", id);
            }

            _memoryCache.Set<TencentVideo?>(cacheKey, null, expiredOption);
            return null;
        }


        public async Task<List<TencentComment>> GetDanmuContentAsync(string vid, CancellationToken cancellationToken)
        {
            var danmuList = new List<TencentComment>();
            if (string.IsNullOrEmpty(vid))
            {
                return danmuList;
            }

            var url = $"https://dm.video.qq.com/barrage/base/{vid}";
            var result = await httpClient.GetSelfResultAsyncWithError<TencentCommentResult>(GetDefaultHttpRequestOptions(url)).ConfigureAwait(false);
            if (result != null && result.SegmentIndex != null)
            {
                var start = result.SegmentStart.ToLong();
                var size = result.SegmentSpan.ToLong();
                for (long i = start; result.SegmentIndex.ContainsKey(i) && size > 0; i += size)
                {
                    var segment = result.SegmentIndex[i];
                    var segmentUrl = $"https://dm.video.qq.com/barrage/segment/{vid}/{segment.SegmentName}";

                    var segmentResult = await httpClient
                        .GetSelfResultAsyncWithError<TencentCommentSegmentResult>(GetDefaultHttpRequestOptions(segmentUrl)).ConfigureAwait(false);
                    if (segmentResult != null && segmentResult.BarrageList != null)
                    {
                        // 30秒每segment，为避免弹幕太大，从中间隔抽取最大60秒200条弹幕
                        danmuList.AddRange(segmentResult.BarrageList.ExtractToNumber(100));
                    }

                    // 等待一段时间避免api请求太快
                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                }
            }

            return danmuList;
        }

        protected async Task LimitRequestFrequently()
        {
            await Task.Delay(1000).ConfigureAwait(false);
        }
    }
}
