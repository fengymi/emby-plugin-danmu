using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Iqiyi.Entity
{
    public class IqiyiHtmlAlbumInfo
    {
        private static readonly Regex regLinkId = new Regex(@"v_(\w+?)\.html", RegexOptions.Compiled);

        [DataMember(Name="qipuId")]
        public long AlbumId { get; set; }

        [DataMember(Name="albumName")]
        public string albumName { get; set; }
        
        [DataMember(Name="videoCount")]
        public int VideoCount { get; set; }
    }
}
