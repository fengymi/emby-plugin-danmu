using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;

namespace Emby.Plugin.Danmu.Core.Extensions
{
    public static class SeasonsExtension
    {
        public static List<BaseItem> GetSeasons(this Series series, User user, DtoOptions options)
        {
            InternalItemsQuery query = new InternalItemsQuery(user)
            {
                DtoOptions = options
            };
            // this.SetSeasonQueryOptions(query, user);
            return BaseItem.LibraryManager.GetItemList(query).ToList();
        }
    }
}