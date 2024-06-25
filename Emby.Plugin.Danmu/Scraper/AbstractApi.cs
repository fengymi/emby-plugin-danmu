using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
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

        protected ILogger _logger;
        // protected JsonSerializerOptions _jsonOptions = null;
        protected IHttpClient httpClient;
        protected CookieContainer _cookieContainer;
        protected IMemoryCache _memoryCache;

        public AbstractApi(ILogger log, IHttpClient httpClient)
        {
            this._logger = log;
            var handler = new HttpClientHandlerEx();
            _cookieContainer = handler.CookieContainer;

            this.httpClient = httpClient;
            // httpClient.DefaultRequestHeaders.Add("user-agent", HTTP_USER_AGENT);
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
        }

        protected void AddCookies(string cookieVal, Uri uri)
        {
            // 清空旧的cookie
            var cookies = _cookieContainer.GetCookies(uri);
            foreach (Cookie co in cookies)
            {
                co.Expires = DateTime.Now.Subtract(TimeSpan.FromDays(1));
            }


            // 附加新的cookie
            if (!string.IsNullOrEmpty(cookieVal))
            {
                var domain = uri.GetSecondLevelHost();
                var arr = cookieVal.Split(';');
                foreach (var str in arr)
                {
                    var cookieArr = str.Split('=');
                    if (cookieArr.Length != 2)
                    {
                        continue;
                    }

                    var key = cookieArr[0].Trim();
                    var value = cookieArr[1].Trim();
                    try
                    {
                        _cookieContainer.Add(new Cookie(key, value, "/", "." + domain));
                    }
                    catch (Exception ex)
                    {
                        this._logger.Error("",  ex, ex.Message);
                    }
                }
            }
        }

        protected virtual Dictionary<string, string> GetDefaultHeaders() => null;

        protected virtual string[] GetDefaultCookies(string? url = null) => null;

        protected HttpRequestOptions GetDefaaultHttpRequestOptions(string url, string? cookies = null,
            CancellationToken cancellationToken = default)
        {
            HttpRequestOptions httpRequestOptions = new HttpRequestOptions
            {
                Url = url,
                UserAgent = $"{HTTP_USER_AGENT}",
                TimeoutMs = 30000,
                EnableHttpCompression = true,
                RequestContentType = "application/json",
                AcceptHeader = "application/json",
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

            var defaultCookies = GetDefaultCookies(url);
            if (cookies != null)
            {
                requestHeaders["Cookie"] = cookies;
            }
            else if (defaultCookies != null && defaultCookies.Length > 0)
            {
                requestHeaders["Cookie"] = string.Join(",", defaultCookies);
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
    }
}