using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace Emby.Plugin.Danmu.Scraper.Iqiyi.Entity
{
    public class IqiyiHtmlVideoInfo
    {
        private static readonly Regex regLinkId = new Regex(@"v_(\w+?)\.html", RegexOptions.Compiled);

        [DataMember(Name="albumQipuId")]
        public long AlbumId { get; set; }
        [DataMember(Name="tvId")]
        public long TvId { get; set; }
        [DataMember(Name="videoName")]
        public string VideoName { get; set; }
        
        private string _videoUrl;
        [DataMember(Name="videoUrl")]
        public string VideoUrl 
        {
            get
            {
                if (this._videoUrl == null)
                {
                    return string.Empty;
                }
                if (this._videoUrl.StartsWith("http://") || this._videoUrl.StartsWith("https://"))
                {
                    return this._videoUrl;
                }
                if (this._videoUrl.StartsWith("//"))
                {
                    return "https:" + this._videoUrl;
                }
                return this._videoUrl;
            }
            set
            {
                _videoUrl = value;
            }
        }
        
        // [DataMember(Name="channelId")]
        // public int channelId { get; set; }
        
        [DataMember(Name="channelName")]
        public string channelName { get; set; }
        [DataMember(Name="duration")]
        public int Duration { get; set; }
        [DataMember(Name="videoCount")]
        public int VideoCount { get; set; }

        [IgnoreDataMember]
        public List<IqiyiEpisode> Epsodelist { get; set; }

        [IgnoreDataMember]
        public string LinkId
        {
            get
            {
                var match = regLinkId.Match(VideoUrl);
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value.Trim();
                }
                else
                {
                    return null;
                }
            }
        }

        // [IgnoreDataMember]
        // public string ChannelName
        // {
        //     get
        //     {
        //         switch (channelId)
        //         {
        //             case 1:
        //                 return "电影";
        //             case 2:
        //                 return "电视剧";
        //             case 3:
        //                 return "纪录片";
        //             case 4:
        //                 return "动漫";
        //             case 6:
        //                 return "综艺";
        //             case 15:
        //                 return "儿童";
        //             default:
        //                 return string.Empty;
        //         }
        //     }
        // }

    }
}