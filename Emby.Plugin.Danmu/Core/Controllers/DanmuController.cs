using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Core.Singleton;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;
using MediaBrowser.Controller.Api;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;

namespace Emby.Plugin.Danmu.Core.Controllers
{
    [Route("/plugin/danmu/{id}")]
    [Route("/api/danmu/{id}")]
    [Route("/api/danmu/search")]
    public class DanmuParams : IReturn<DanmuFileInfo>
    {
        public string id { get; set; } = string.Empty;
        public string keyword { get; set; } = string.Empty;
    }
    
    public class DanmuResult
    {
        public string value { get; set; } = string.Empty;
    }

    public class DanmuController : BaseApiService
    {
        private readonly ILibraryManager _libraryManager;
        private readonly LibraryManagerEventsHelper _libraryManagerEventsHelper;
        private readonly MediaBrowser.Model.IO.IFileSystem _fileSystem;
        private readonly ScraperManager _scraperManager;
        
        private readonly ILogger _logger;
        private readonly IJsonSerializer _jsonSerializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="DanmuController"/> class.
        /// </summary>
        /// <param name="fileSystem">Instance of the <see cref="IFileSystem"/> interface.</param>
        /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
        /// <param name="logManager"></param>
        public DanmuController(
            MediaBrowser.Model.IO.IFileSystem fileSystem,
            ILogManager logManager,
            LibraryManagerEventsHelper libraryManagerEventsHelper,
            ILibraryManager libraryManager,
            // ScraperManager scraperManager, 
            IJsonSerializer jsonSerializer)
        {
            
            _fileSystem = fileSystem;
            _logger = logManager.getDefaultLogger();
            _libraryManager = libraryManager;
            _libraryManagerEventsHelper = libraryManagerEventsHelper;
            _scraperManager = SingletonManager.ScraperManager;
            _jsonSerializer = SingletonManager.JsonSerializer;
            
            _logger.Info("libraryManagerEventsHelper={0}", libraryManagerEventsHelper);
        }

        /// <summary>
        /// 获取弹幕文件内容.
        /// </summary>
        /// <returns>xml弹幕文件内容</returns>
        // [Route("/plugin/danmu/{id}", "get")]
        [Route("/api/danmu/{id}", "get")]
        public async Task<object> Get(DanmuParams danmuParams)
        {
            _logger.Info("AbsoluteUri={0}, RawUrl={1}", Request.AbsoluteUri, Request.RawUrl);
            if (Request.RawUrl.StartsWith("/emby/api/danmu/search"))
            {
                return await SearchDanmu(danmuParams.keyword);
            }
            
            var id = danmuParams.id;
            if (string.IsNullOrEmpty(id))
            {
                return new DanmuFileInfo() { Url = "https://www.baidu.com/" };
            }
            
            // _libraryManager.GetItemById(id);
            var currentItem = this.GetItemById(_libraryManager, id, null);
            if (currentItem == null)
            {
                return new DanmuFileInfo();
            }
            _logger.Info("currentItem={0}", currentItem.ToJson());

            var danmuPath = Path.Combine(currentItem.ContainingFolderPath,
                currentItem.FileNameWithoutExtension + ".xml");
            var fileMeta = _fileSystem.GetFileInfo(danmuPath);
            if (!fileMeta.Exists)
            {
                return new DanmuFileInfo();
            }
            
            // var domain = Request.Scheme + System.Uri.SchemeDelimiter + Request.Host;
            var danmuFileInfo = new DanmuFileInfo() { Url = string.Format("{0}/api/danmu/{1}/raw", "domain", "id") };

            var serializeToString = _jsonSerializer.SerializeToString(danmuFileInfo);

            _logger.Info("danmuFileInfo={0}, {1}", danmuFileInfo, serializeToString);
            return "test";
        }

        // /// <summary>
        // /// 获取弹幕文件内容.
        // /// </summary>
        // /// <returns>xml弹幕文件内容</returns>
        // [Route("/plugin/danmu/raw/{id}")]
        // [Route("/api/danmu/{id}/raw")]
        // [HttpGet]
        // public async Task<ActionResult> Download(string id)
        // {
        //     if (string.IsNullOrEmpty(id))
        //     {
        //         throw new ResourceNotFoundException();
        //     }
        //
        //     var currentItem = _libraryManager.GetItemById(id);
        //     if (currentItem == null)
        //     {
        //         throw new ResourceNotFoundException();
        //     }
        //
        //     var danmuPath = Path.Combine(currentItem.ContainingFolderPath,
        //         currentItem.FileNameWithoutExtension + ".xml");
        //     var fileMeta = _fileSystem.GetFileInfo(danmuPath);
        //     if (!fileMeta.Exists)
        //     {
        //         throw new ResourceNotFoundException();
        //     }
        //
        //     return File(System.IO.File.ReadAllBytes(danmuPath), "text/xml");
        // }
        //
        /// <summary>
        /// 查找弹幕
        /// </summary>
        // [Route("/api/danmu/search")]
        // [HttpGet]
        public async Task<IEnumerable<MediaInfo>> SearchDanmu(string keyword)
        {
            var list = new List<MediaInfo>();
        
            if (string.IsNullOrEmpty(keyword))
            {
                return list;
            }
        
            _logger.Info("_scraperManager.all = {0}", _scraperManager.All());
            foreach (var scraper in _scraperManager.All())
            {
                try
                {
                    var scraperId = Regex.Replace(scraper.ProviderId, "ID$", string.Empty).ToLower();
                    var result = await scraper.SearchForApi(keyword).ConfigureAwait(false);
                    foreach (var searchInfo in result)
                    {
                        list.Add(new MediaInfo()
                        {
                            Id = searchInfo.Id,
                            Name = searchInfo.Name,
                            // Category = searchInfo.Category,
                            // Year = searchInfo.Year == null ? string.Empty : searchInfo.Year.ToString(),
                            // EpisodeSize = searchInfo.EpisodeSize,
                            // Site = scraper.Name,
                            // SiteId = scraperId,
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{0}]Exception handled processing search movie [{1}]", scraper.Name,
                        keyword);
                }
            }
        
            return list;
        }
        //
        // /// <summary>
        // /// 查找弹幕
        // /// </summary>
        // [Route("/api/{site}/danmu/search")]
        // [HttpGet]
        // public async Task<IEnumerable<MediaInfo>> SearchDanmuBySite(string site, string keyword)
        // {
        //     var list = new List<MediaInfo>();
        //
        //     if (string.IsNullOrEmpty(keyword))
        //     {
        //         return list;
        //     }
        //
        //
        //     foreach (var scraper in _scraperManager.All())
        //     {
        //         try
        //         {
        //             var scraperId = Regex.Replace(scraper.ProviderId, "ID$", string.Empty).ToLower();
        //             if (scraperId != site)
        //             {
        //                 continue;
        //             }
        //
        //             var result = await scraper.SearchForApi(keyword).ConfigureAwait(false);
        //             foreach (var searchInfo in result)
        //             {
        //                 list.Add(new MediaInfo()
        //                 {
        //                     Id = searchInfo.Id,
        //                     Name = searchInfo.Name,
        //                     Category = searchInfo.Category,
        //                     Year = searchInfo.Year == null ? string.Empty : searchInfo.Year.ToString(),
        //                     EpisodeSize = searchInfo.EpisodeSize,
        //                     Site = scraper.Name,
        //                     SiteId = scraperId,
        //                 });
        //             }
        //         }
        //         catch (Exception ex)
        //         {
        //             _logger.LogError(ex, "[{0}]Exception handled processing search movie [{1}]", scraper.Name,
        //                 keyword);
        //         }
        //     }
        //
        //     return list;
        // }
        //
        // /// <summary>
        // /// 查找弹幕
        // /// </summary>
        // [Route("/api/{site}/danmu/{id}/episodes")]
        // [HttpGet]
        // public async Task<IEnumerable<EpisodeInfo>> GetDanmuEpisodesBySite(string site, string id)
        // {
        //     var list = new List<EpisodeInfo>();
        //
        //     if (string.IsNullOrEmpty(id))
        //     {
        //         return list;
        //     }
        //
        //
        //     foreach (var scraper in _scraperManager.All())
        //     {
        //         try
        //         {
        //             var scraperId = Regex.Replace(scraper.ProviderId, "ID$", string.Empty).ToLower();
        //             if (scraperId != site)
        //             {
        //                 continue;
        //             }
        //
        //             var result = await scraper.GetEpisodesForApi(id).ConfigureAwait(false);
        //             foreach (var (ep, idx) in result.WithIndex())
        //             {
        //                 list.Add(new EpisodeInfo()
        //                 {
        //                     Id = ep.Id,
        //                     CommentId = ep.CommentId,
        //                     Number = idx + 1,
        //                     Title = ep.Title,
        //                 });
        //             }
        //         }
        //         catch (Exception ex)
        //         {
        //             _logger.LogError(ex, "[{0}]Exception handled processing get episodes [{1}]", scraper.Name, id);
        //         }
        //     }
        //
        //     return list;
        // }
        //
        //
        // /// <summary>
        // /// 下载弹幕.
        // /// </summary>
        // [Route("/api/{site}/danmu/{cid}/download")]
        // [HttpGet]
        // public async Task<ActionResult> DownloadByCommentID(string site, string cid)
        // {
        //     if (string.IsNullOrEmpty(cid))
        //     {
        //         throw new ResourceNotFoundException();
        //     }
        //
        //     foreach (var scraper in this._scraperManager.All())
        //     {
        //         var scraperId = Regex.Replace(scraper.ProviderId, "ID$", string.Empty).ToLower();
        //         if (scraperId == site)
        //         {
        //             var danmaku = await scraper.DownloadDanmuForApi(cid).ConfigureAwait(false);
        //             if (danmaku != null)
        //             {
        //                 var bytes = danmaku.ToXml();
        //                 return File(bytes, "text/xml");
        //             }
        //         }
        //     }
        //
        //     throw new ResourceNotFoundException();
        // }
        //
        // /// <summary>
        // /// 跳转链接.
        // /// </summary>
        // [Route("goto")]
        // [HttpGet]
        // public RedirectResult GoTo(string provider, string id, string type)
        // {
        //     var url = $"/";
        //     switch (provider)
        //     {
        //         case "bilibili":
        //             if (id.StartsWith("BV"))
        //             {
        //                 url = $"https://www.bilibili.com/video/{id}/";
        //             }
        //             else
        //             {
        //                 if (type == "movie")
        //                 {
        //                     url = $"https://www.bilibili.com/bangumi/play/ep{id}";
        //                 }
        //                 else
        //                 {
        //                     url = $"https://www.bilibili.com/bangumi/play/ss{id}";
        //                 }
        //             }
        //
        //             break;
        //         default:
        //             break;
        //     }
        //
        //     return Redirect(url);
        // }
        //
        //
        // /// <summary>
        // /// 重新获取对应的弹幕id.
        // /// </summary>
        // /// <returns>请求结果</returns>
        // [Route("")]
        // [HttpGet]
        // public async Task<String> Refresh(string id)
        // {
        //     if (string.IsNullOrEmpty(id))
        //     {
        //         throw new ResourceNotFoundException();
        //     }
        //
        //     var item = _libraryManager.GetItemById(id);
        //     if (item == null)
        //     {
        //         throw new ResourceNotFoundException();
        //     }
        //
        //     if (item is Movie || item is Season)
        //     {
        //         _libraryManagerEventsHelper.QueueItem(item, Model.EventType.Add);
        //         _libraryManagerEventsHelper.QueueItem(item, Model.EventType.Update);
        //     }
        //
        //     if (item is Series)
        //     {
        //         var seasons = ((Series)item).GetSeasons(null, new DtoOptions(false));
        //         foreach (var season in seasons)
        //         {
        //             _libraryManagerEventsHelper.QueueItem(season, Model.EventType.Add);
        //             _libraryManagerEventsHelper.QueueItem(season, Model.EventType.Update);
        //         }
        //     }
        //
        //     return "ok";
        // }
    }
}