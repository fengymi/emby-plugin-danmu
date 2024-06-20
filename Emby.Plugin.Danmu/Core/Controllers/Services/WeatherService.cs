using MediaBrowser.Common;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;

namespace Emby.Plugin.Danmu.Core.Controllers.Services
{
    [Route("/Weather", "GET")]
    public class GetWeather : IReturn<WeatherInfo>
    {
        public string id { get; set; }
    }
    
    

    public class WeatherService : IService, IHasResultFactory
    {
        private readonly ILogger logger;
        private readonly IApplicationHost applicationHost;
        private readonly IJsonSerializer jsonSerializer;

        public WeatherService(IApplicationHost applicationHost, ILogManager logManager,  IJsonSerializer jsonSerializer)
        {
            this.logger = logManager.GetLogger("WeatherService");
            this.applicationHost = applicationHost;
            this.jsonSerializer = jsonSerializer;
        }

        public object Get(GetWeather request1)
        {
            logger.Info("收到请求了 request1={0}", jsonSerializer.SerializeToSpan(request1).ToString());
            
            logger.Info("收到请求 request1={0}, applicationHost={1}, headers={2}", jsonSerializer.SerializeToSpan(request1).ToString(), Request.Protocol, jsonSerializer.SerializeToSpan(Request.Headers).ToString());
            var result = new WeatherInfo();
            
            result.value = "hello world";
            return result;
        }

        public IRequest Request { get; set; }
        public IHttpResultFactory ResultFactory { get; set; }
    }

    public class WeatherInfo
    {
        public string value { get; set; }
    }
}