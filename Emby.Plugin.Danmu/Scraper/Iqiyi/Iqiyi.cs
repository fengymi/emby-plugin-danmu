using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Scraper.Entity;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Logging;

namespace Emby.Plugin.Danmu.Scraper.Iqiyi
{
    public class Iqiyi : AbstractScraper
    {
        public const string ScraperProviderName = "爱奇艺";
        public const string ScraperProviderId = "IqiyiID";

        private readonly IqiyiApi _api;

        public Iqiyi(IHttpClient httpClient, ILogManager logManager) : base(logManager.getDefaultLogger("Iqiyi"))
        {
            _api = new IqiyiApi(logManager, httpClient);
        }

        public override int DefaultOrder => 4;

        public override bool DefaultEnable => true;

        public override string Name => ScraperProviderName;

        public override string ProviderName => ScraperProviderName;

        public override string ProviderId => ScraperProviderId;

        public override async Task<List<ScraperSearchInfo>> Search(BaseItem item)
        {
            var list = new List<ScraperSearchInfo>();
            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            var searchName = this.NormalizeSearchName(item.Name);
            var videos = await this._api.SearchAsync(searchName, CancellationToken.None).ConfigureAwait(false);
            foreach (var video in videos)
            {
                if (isMovieItemType && video.ChannelName != "电影")
                {
                    continue;
                }

                if (!isMovieItemType && video.ChannelName == "电影")
                {
                    continue;
                }

                list.Add(new ScraperSearchInfo()
                {
                    Id = $"{video.LinkId}",
                    Name = video.Name,
                    Category = video.ChannelName,
                    Year = video.Year,
                    EpisodeSize = video.ItemTotalNumber,
                });
            }


            return list;
        }

        public override async Task<string?> SearchMediaId(BaseItem item)
        {
            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            var searchName = this.NormalizeSearchName(item.Name);
            var videos = await this._api.SearchAsync(searchName, CancellationToken.None).ConfigureAwait(false);
            foreach (var video in videos)
            {
                var title = video.Name;
                var pubYear = video.Year;

                if (isMovieItemType && video.ChannelName != "电影")
                {
                    continue;
                }

                if (!isMovieItemType && video.ChannelName == "电影")
                {
                    continue;
                }

                // 检测标题是否相似（越大越相似）
                var score = searchName.Distance(title);
                if (score < 0.7)
                {
                    log.LogDebug("[{0}] 标题差异太大，忽略处理. 搜索词：{1}, score:　{2}", title, searchName, score);
                    continue;
                }

                // 检测年份是否一致
                var itemPubYear = item.ProductionYear ?? 0;
                if (itemPubYear > 0 && pubYear > 0 && itemPubYear != pubYear)
                {
                    log.LogDebug("[{0}] 发行年份不一致，忽略处理. Iqiyi：{1} jellyfin: {2}", title, pubYear, itemPubYear);
                    continue;
                }

                return video.LinkId;
            }

            return null;
        }


        public override async Task<ScraperMedia?> GetMedia(BaseItem item, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            // id是编码后的
            var isMovieItemType = item is MediaBrowser.Controller.Entities.Movies.Movie;
            var video = await _api.GetVideoAsync(id, CancellationToken.None).ConfigureAwait(false);
            if (video == null)
            {
                log.LogInformation("[{0}]获取不到视频信息：id={1}", this.Name, id);
                return null;
            }


            var media = new ScraperMedia();
            media.Id = id; // 使用url编码后的id (LinkId)
            media.ProviderId = this.ProviderId;
            if (isMovieItemType && video.Epsodelist != null && video.Epsodelist.Count > 0)
            {
                var tvId = video.Epsodelist[0].TvId;
                if (tvId > 0) // 确保 TvId 有效
                {
                    media.CommentId = $"{tvId}";
                    log.Info($"[IQIYI] GetMedia: 电影 '{item.Name}' (LinkId: {id}) 设置 CommentId 为 TvId: {tvId}");
                }
                else
                {
                    log.Warn($"[IQIYI] GetMedia: 电影 '{item.Name}' (LinkId: {id}) 获取到的 TvId 无效 ({tvId})，media.CommentId 将为空。");
                }
            }

            if (video.Epsodelist != null && video.Epsodelist.Count > 0)
            {
                foreach (var ep in video.Epsodelist)
                {
                    var episodeTvId = ep.TvId;
                    var episodeCommentId = (episodeTvId > 0) ? $"{episodeTvId}" : string.Empty;
                    if (episodeTvId <= 0) {
                        log.Warn($"[IQIYI] GetMedia: 剧集 '{ep.Name}' (LinkId: {ep.LinkId}) 的 TvId 无效 ({episodeTvId})，ScraperEpisode.CommentId 将为空。");
                    }
                    media.Episodes.Add(new ScraperEpisode() { Id = $"{ep.LinkId}", CommentId = episodeCommentId, Title = ep.Name });
                }
            }

            return media;
        }

        /// <inheritdoc />
        public override async Task<ScraperEpisode?> GetMediaEpisode(BaseItem item, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            // id是编码后的
            var video = await _api.GetVideoBaseAsync(id, CancellationToken.None).ConfigureAwait(false);
            if (video == null)
            {
                return null;
            }
            var tvId = video.TvId;
            if (tvId <= 0)
            {
                log.Warn($"[IQIYI] GetMediaEpisode: 对于 LinkId '{id}', 从 GetVideoBaseAsync 获取的 TvId 无效: {tvId}. 返回的 ScraperEpisode 的 CommentId 将为空。");
            }
            var commentIdForEpisode = (tvId > 0) ? $"{tvId}" : string.Empty;
            return new ScraperEpisode() { Id = id, CommentId = commentIdForEpisode, Title = video.VideoName };
        }

        public override async Task<ScraperDanmaku?> GetDanmuContent(BaseItem item, string commentId)
        {
            if (string.IsNullOrEmpty(commentId))
            {
                return null;
            }

            if (!long.TryParse(commentId, out var tvIdNumeric) || tvIdNumeric <= 0)
            {
                log.Warn($"[IQIYI] GetDanmuContent: 接收到无效的 TvId ('{commentId}') 用于获取弹幕。项目: '{item?.Name ?? "未知项目"}'.");
                return null; // TvId 无效，无法获取弹幕
            }

            var comments = await _api.GetDanmuContentAsync(commentId, CancellationToken.None).ConfigureAwait(false);
            var danmaku = new ScraperDanmaku();
            danmaku.ChatId = commentId.ToLong();
            danmaku.ChatServer = "cmts.iqiyi.com";
            danmaku.ProviderId = ProviderId;
            foreach (var comment in comments)
            {
                try
                {
                    var danmakuText = new ScraperDanmakuText();
                    danmakuText.Progress = (int)comment.ShowTime * 1000;
                    danmakuText.Mode = 1;
                    danmakuText.MidHash = $"[iqiyi]{comment.UserInfo.Uid}";
                    danmakuText.Id = comment.ContentId.ToLong();
                    danmakuText.Content = comment.Content;
                    if (uint.TryParse(comment.Color, System.Globalization.NumberStyles.HexNumber, null, out var color))
                    {
                        danmakuText.Color = color;
                    }

                    danmaku.Items.Add(danmakuText);
                }
                catch (Exception ex)
                {
                }
            }
            
            danmaku.DataSize = danmaku.Items.Count;
            return danmaku;
        }


        public override async Task<List<ScraperSearchInfo>> SearchForApi(string keyword)
        {
            var list = new List<ScraperSearchInfo>();
            var videos = await this._api.SearchAsync(keyword, CancellationToken.None).ConfigureAwait(false);
            foreach (var video in videos)
            {
                list.Add(new ScraperSearchInfo()
                {
                    Id = $"{video.LinkId}",
                    Name = video.Name,
                    Category = video.ChannelName,
                    Year = video.Year,
                    EpisodeSize = video.ItemTotalNumber,
                });
            }

            return list;
        }

        public override async Task<List<ScraperEpisode>> GetEpisodesForApi(string id)
        {
            var list = new List<ScraperEpisode>();
            var video = await this._api.GetVideoAsync(id, CancellationToken.None).ConfigureAwait(false);
            if (video == null)
            {
                return list;
            }

            if (video.Epsodelist != null && video.Epsodelist.Count > 0)
            {
                foreach (var ep in video.Epsodelist)
                {
                    list.Add(new ScraperEpisode() { Id = $"{ep.LinkId}", CommentId = $"{ep.TvId}", Title = ep.Name });
                }
            }

            return list;
        }

        public override async Task<ScraperDanmaku?> DownloadDanmuForApi(string commentId)
        {
            return await this.GetDanmuContent(null, commentId).ConfigureAwait(false);
        }
    }
}