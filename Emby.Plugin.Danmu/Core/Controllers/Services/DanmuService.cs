using Emby.Plugin.Danmu.Core.Controllers.Dto;
using MediaBrowser.Controller.Api;
using MediaBrowser.Model.Services;

namespace Emby.Plugin.Danmu.Core.Controllers.Services
{
    public class DanmuService : BaseApiService
    {
        public DanmuService()
        {
        }
        
        public DanmuResult2 Get(DanmuParams2 danmuParams)
        {
            
            return new DanmuResult2{value = danmuParams.id};
        }
    }
}