using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Core.Singleton;
using Emby.Plugin.Danmu.Model;
using Emby.Plugin.Danmu.Scraper;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Tasks;

namespace Emby.Plugin.Danmu.ScheduledTasks
{
    public class ScanLibraryTask : IScheduledTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly ScraperManager _scraperManager;
        private readonly ILogger _logger;
        private readonly LibraryManagerEventsHelper _libraryManagerEventsHelper;


        public string Key => $"{Plugin.Instance.Name}ScanLibrary";

        public string Name => "扫描媒体库匹配弹幕";

        public string Description => $"扫描缺少弹幕的视频，搜索匹配后，再下载对应弹幕文件。";

        public string Category => Plugin.Instance.Name;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScanLibraryTask"/> class.
        /// </summary>
        /// <param name="loggerFactory">Instance of the <see cref="ILoggerFactory"/> interface.</param>
        /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
        public ScanLibraryTask(ILogManager logManager, ILibraryManager libraryManager
            // , LibraryManagerEventsHelper libraryManagerEventsHelper
            )
        {
            _logger = logManager.getDefaultLogger(GetType().ToString());
            _libraryManager = libraryManager;
            _libraryManagerEventsHelper = SingletonManager.LibraryManagerEventsHelper;
            // _libraryManagerEventsHelper = SingletonManager.LibraryManagerEventsHelper;
            _scraperManager = SingletonManager.ScraperManager;
            // _libraryManagerEventsHelper = SingletonManager.LibraryManagerEventsHelper;
        }

        // public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        // {
        //     throw new NotImplementedException();
        // }

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new List<TaskTriggerInfo>();
        }

        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            await Task.Yield();
            progress?.Report(0);

            var scrapers = this._scraperManager.All();
            var items = _libraryManager.GetItemList(new InternalItemsQuery
            {
                // MediaTypes = new[] { MediaType.Video },
                ExcludeProviderIds = this.GetScraperFilter(scrapers),
                IncludeItemTypes = new[] { "Movie", "Episode"}
            }).ToList();

            _logger.LogInformation("Scan danmu for {0} scrapers.", scrapers.Count);
            _logger.LogInformation("Scan danmu for {0} videos.", items.Count);

            var successCount = 0;
            var failCount = 0;
            
            int i = 1;
            for (int idx = 0; idx < items.Count; idx++)
            {
                BaseItem item = items[idx];
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report((double)idx / items.Count * 100);

                try
                {
                    _logger.Info("Scan danmu for type={0}, movie={1}, season={2}, Episode={3}", item.GetType().ToString(), (item is Movie), (item is Season), (item is Episode));
                    // 有epid的忽略处理（不需要再匹配）
                    if (this.HasAnyScraperProviderId(scrapers, item))
                    {
                        successCount++;
                        continue;
                    }

                    // item所在的媒体库不启用弹幕插件，忽略处理
                    if (_libraryManagerEventsHelper.IsIgnoreItem(item))
                    {
                        continue;
                    }

                    if (item is Movie)
                    {
                        var movieItem = (Movie)item;
                        await _libraryManagerEventsHelper.ProcessQueuedMovieEvents(new List<LibraryEvent>() { new LibraryEvent { Item = movieItem, EventType = EventType.Add } }, EventType.Add).ConfigureAwait(false);
                    }
                    else if (item is Season)
                    {
                        var seasonItem = (Season)item;
                        // 搜索匹配season的元数据
                        await _libraryManagerEventsHelper.ProcessQueuedSeasonEvents(new List<LibraryEvent>() { new LibraryEvent { Item = seasonItem, EventType = EventType.Add } }, EventType.Add).ConfigureAwait(false);
                        // 下载剧集弹幕
                        await _libraryManagerEventsHelper.ProcessQueuedSeasonEvents(new List<LibraryEvent>() { new LibraryEvent { Item = seasonItem, EventType = EventType.Update } }, EventType.Update).ConfigureAwait(false);
                    } else if (item is Episode)
                    {
                        var episodeItem = (Episode)item;
                        await _libraryManagerEventsHelper.ProcessQueuedSeasonEvents(new List<LibraryEvent>() { new LibraryEvent { Item = episodeItem, EventType = EventType.Update } }, EventType.Update).ConfigureAwait(false);
                    }
                    
                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Scan danmu failed for video {0}: {1}", item.Name, ex.Message);
                    failCount++;
                }
            }

            progress?.Report(100);
            _logger.LogInformation("Exectue task completed. success: {0} fail: {1}", successCount, failCount);
        }

        private bool HasAnyScraperProviderId(ReadOnlyCollection<AbstractScraper> scrapers, BaseItem item)
        {
            foreach (var scraper in scrapers)
            {
                var providerVal = item.GetProviderId(scraper.ProviderId);
                if (!string.IsNullOrEmpty(providerVal))
                {
                    return true;
                }
            }

            return false;
        }

        private Dictionary<string, string> GetScraperFilter(ReadOnlyCollection<AbstractScraper> scrapers)
        {
            var filter = new Dictionary<string, string>();
            foreach (var scraper in scrapers)
            {
                filter.Add(scraper.ProviderId, string.Empty);
            }

            return filter;
        }
    }
}