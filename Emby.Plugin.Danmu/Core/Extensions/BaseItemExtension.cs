using System.Threading;
using System.Threading.Tasks;
using Emby.Plugin.Danmu.Core.Singleton;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;

namespace Emby.Plugin.Danmu.Core.Extensions
{
    public static class BaseItemExtension
    {
        public static Task UpdateToRepositoryAsync(this BaseItem item, ItemUpdateType itemUpdateType,
            CancellationToken cancellationToken)
        {
            SingletonManager.LogManager.GetLogger("BaseItemExtension").Info("数据执行完成 item={0}, itemUpdateType={1}, cancellationToken={2}", item.ToJson(), itemUpdateType.ToJson(), cancellationToken.ToJson());
            item.UpdateToRepository(ItemUpdateType.MetadataEdit);
            return null;
        }
    }
}