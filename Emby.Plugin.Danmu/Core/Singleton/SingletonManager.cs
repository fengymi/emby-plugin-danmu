using Emby.Plugin.Danmu.Scraper;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Serialization;

namespace Emby.Plugin.Danmu.Core.Singleton
{
    public static class SingletonManager
    {
        public static ScraperManager ScraperManager;
        public static IJsonSerializer JsonSerializer;
        public static IHttpClient HttpClient;
    }
}