using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core.Extensions;
using Emby.Plugin.Danmu.Core.Http;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace Emby.Plugin.Danmu.Scraper
{
    public class AbstractApi : IDisposable
    {
        public const string HTTP_USER_AGENT =
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/93.0.4577.63 Safari/537.36 Edg/93.0.961.44";

        private const string CookieExpireReplace = @"expires=.*?;";
        
        protected ILogger _logger;
        // protected JsonSerializerOptions _jsonOptions = null;
        protected IHttpClient httpClient;
        protected CookieContainer _cookieContainer;
        protected IMemoryCache _memoryCache;

        public AbstractApi(ILogger log, IHttpClient httpClient)
        {
            this._logger = log;
            var handler = new HttpClientHandlerEx(); // 还原：使用 HttpClientHandlerEx 初始化 CookieContainer
            _cookieContainer = handler.CookieContainer; // 还原
            this.httpClient = httpClient;
            // httpClient.DefaultRequestHeaders.Add("user-agent", HTTP_USER_AGENT);
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
        }

        protected virtual void AddCookies(Uri uri, string cookieVal, params char[]? separator)
        {
            // 清空旧的cookie
            // var cookies = _cookieContainer.GetCookies(uri);
            // foreach (Cookie co in cookies)
            // {
            //     co.Expires = DateTime.Now.Subtract(TimeSpan.FromDays(1));
            // }

            if (cookieVal == null)
            {
                return;
            }

            DateTime maxExpiryDate = DateTime.Now.AddMinutes(60 * 6);
            DateTime minExpiryDate = DateTime.Now.AddMinutes(10);
            string[] multCookies = separator == null || separator.Length == 0
                ? new[] { cookieVal }
                : cookieVal.Split(separator);
            _logger.Info("url={0}, set cookie = {1}, separator={2}, multCookies.length={3}", uri.AbsoluteUri, cookieVal, separator, multCookies.Length);
            foreach (string c in multCookies)
            {
                string replaceCookie = Regex.Replace(c, CookieExpireReplace, string.Empty);
                _logger.Info("one cookie={0}, noBlank={1}, url={2}", c, replaceCookie, uri.AbsoluteUri);
                CookieContainer cookieContainer = new CookieContainer();
                cookieContainer.SetCookies(uri, replaceCookie);

                CookieCollection cookieCollections = cookieContainer.GetCookies(uri);
                foreach (Cookie cookie in cookieCollections)
                {
                    if (!cookie.Expired)
                    {
                        if (cookie.Expires.CompareTo(maxExpiryDate) > 0)
                        {
                            cookie.Expires = maxExpiryDate;
                        }
                        else if (cookie.Expires.CompareTo(minExpiryDate) < 0)
                        {
                            cookie.Expires = minExpiryDate;
                        }
                    }
                }
                
                this._cookieContainer.Add(cookieCollections);
            }
        }

        protected virtual Dictionary<string, string> GetDefaultHeaders() => null;

        protected virtual string[] GetDefaultCookies(string? url = null) => null;

        protected HttpRequestOptions GetDefaultHttpRequestOptions(string url, string? cookies = null, CancellationToken cancellationToken = default) // 还原：参数名 cookies
        {
            HttpRequestOptions httpRequestOptions = new HttpRequestOptions
            {
                Url = url,
                UserAgent = $"{HTTP_USER_AGENT}",
                TimeoutMs = 300000,
                EnableHttpCompression = true,
                RequestContentType = "application/json", // 还原：取消注释
                AcceptHeader = "application/json", // 还原：取消注释
                CancellationToken = cancellationToken
            };
            
            Dictionary<string,string> requestHeaders = httpRequestOptions.RequestHeaders;
            Dictionary<string,string> defaultHeaders = GetDefaultHeaders();
            if (defaultHeaders != null )
            {
                foreach (var kvp in defaultHeaders)
                {
                    requestHeaders.Add(kvp.Key, kvp.Value);
                }
            }

            // 还原：原始的 Cookie 处理逻辑
            // 1. Try to use cookies from our managed _cookieContainer
            var cookieHeaderFromManagedContainer = _cookieContainer.GetCookieHeader(new Uri(url));
            if (!string.IsNullOrEmpty(cookieHeaderFromManagedContainer))
            {
                requestHeaders["Cookie"] = cookieHeaderFromManagedContainer;
                _logger.Debug($"Using cookies from _cookieContainer for {url}: {cookieHeaderFromManagedContainer}");
            }
            
            // 2. From GetDefaultCookies(url)
            string[] defaultCookiesArr = GetDefaultCookies(url);
            if (defaultCookiesArr != null && defaultCookiesArr.Length > 0)
            {
                string cookieStr = string.Join(";", defaultCookiesArr);
                if (requestHeaders.ContainsKey("Cookie") && !string.IsNullOrEmpty(requestHeaders["Cookie"]))
                {
                    requestHeaders["Cookie"] += ";" + cookieStr;
                }
                else
                {
                    requestHeaders["Cookie"] = cookieStr;
                }
            }

            // 3. From 'cookies' parameter
            if (!string.IsNullOrEmpty(cookies)) // 还原：使用 'cookies' 参数
            {
                if (requestHeaders.ContainsKey("Cookie") && !string.IsNullOrEmpty(requestHeaders["Cookie"]))
                {
                    requestHeaders["Cookie"] += ";" + cookies;
                }
                else
                {
                    requestHeaders["Cookie"] = cookies;
                }
            }
            
            return httpRequestOptions;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _memoryCache.Dispose();
            }
        }
        
        protected virtual Task LimitRequestFrequently()
        {
            // Thread.Sleep(1000);
            // return Task.CompletedTask;
            return Task.Delay(5000); // 还原：延迟改回 3000ms
        }
    }
}