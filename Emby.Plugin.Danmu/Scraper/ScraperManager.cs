using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Emby.Plugin.Danmu.Core.Extensions;
using MediaBrowser.Model.Logging;

namespace Emby.Plugin.Danmu.Scraper
{
    public class ScraperManager
    {
        protected ILogger log;
        private List<AbstractScraper> _scrapers = new List<AbstractScraper>();

        public ScraperManager(ILogManager logManager)
        {
            log = logManager.getDefaultLogger(this.GetType().ToString());
        }

        public void Register(AbstractScraper scraper)
        {
            this._scrapers.Add(scraper);
        }

        public void Register(IList<AbstractScraper> scrapers)
        {
            this._scrapers.AddRange(scrapers);
        }

        public ReadOnlyCollection<AbstractScraper> All()
        {
            // 存在配置时，根据配置调整源顺序，并删除不启用的源
            if (Plugin.Instance?.Configuration.Scrapers != null)
            {
                var orderScrapers = new List<AbstractScraper>();
                var scraperMap = this._scrapers.ToDictionary(x => x.Name, x => x);
                var configScrapers = Plugin.Instance.Configuration.Scrapers;
                foreach (var config in configScrapers)
                {
                    if (scraperMap.ContainsKey(config.Name) && config.Enable)
                    {
                        orderScrapers.Add(scraperMap[config.Name]);
                    }
                }
                log.Info("当前配置信息 configScrapers={0}", configScrapers.ToJson());
                
                // 添加新增并默认启用的源
                var allOldScaperNames = configScrapers.Select(o => o.Name).ToList();
                foreach (var scraper in this._scrapers)
                {
                    if (!allOldScaperNames.Contains(scraper.Name) && scraper.DefaultEnable)
                    {
                        orderScrapers.Add(scraper);
                    }
                }

                foreach (AbstractScraper abstractScraper in orderScrapers)
                {
                    log.Info("abstractScraper = {0}, ProviderId={1}, name={2}", abstractScraper, abstractScraper.ProviderId, abstractScraper.ProviderName);
                }

                return orderScrapers.AsReadOnly();
            }

            return this._scrapers.AsReadOnly();
        }
    }
}