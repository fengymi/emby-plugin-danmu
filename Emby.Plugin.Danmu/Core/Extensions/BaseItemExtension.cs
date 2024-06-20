using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Emby.Plugin.Danmu.Core.Extensions
{
    public static class BaseItemExtension
    {
        public static Task UpdateToRepositoryAsync(this BaseItem item, ItemUpdateType itemUpdateType,
            CancellationToken cancellationToken)
        {
            return null;
        }
    }
}