using MediaBrowser.Model.Services;

namespace Emby.Plugin.Danmu.Core.Controllers.Dto
{
    [Route("/plugin/danmu2/{id}")]
    [Route("/api/danmu2/{id}")]
    public class DanmuParams2 : IReturn<DanmuResult2>
    {
        public string id { get; set; } = string.Empty;
    }
    
    public class DanmuResult2
    {
        public string value { get; set; } = string.Empty;
    }
}