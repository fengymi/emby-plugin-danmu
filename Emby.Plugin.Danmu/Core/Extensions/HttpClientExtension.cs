using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;

namespace Emby.Plugin.Danmu.Core.Extensions
{
    public static class HttpClientExtension
    {
        public static Task<HttpResponseInfo> GetAsync(this IHttpClient httpClient,  HttpRequestOptions options)
        {
            return httpClient.GetResponse(options);
        }
    }
}