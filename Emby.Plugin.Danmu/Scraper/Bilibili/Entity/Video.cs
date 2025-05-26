using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Bilibili.Entity
{
    [DataContract]
    public class VideoOwner
    {
        [DataMember(Name="mid")]
        public long Mid { get; set; }
        [DataMember(Name="name")]
        public string Name { get; set; }
        [DataMember(Name="face")]
        public string Face { get; set; }
    }

    [DataContract]
    public class VideoStat
    {
        [DataMember(Name="aid")]
        public long Aid { get; set; }
        [DataMember(Name="view")]
        public int View { get; set; }
        [DataMember(Name="danmaku")]
        public int Danmaku { get; set; }
        [DataMember(Name="reply")]
        public int Reply { get; set; }
        [DataMember(Name="favorite")]
        public int Favorite { get; set; }
        [DataMember(Name="coin")]
        public int Coin { get; set; }
        [DataMember(Name="share")]
        public int Share { get; set; }
        [DataMember(Name="like")]
        public int Like { get; set; }
        [DataMember(Name="dislike")]
        public int Dislike { get; set; }
        [DataMember(Name="his_rank")]
        public int HisRank { get; set; }
        [DataMember(Name="now_rank")]
        public int NowRank { get; set; }
    }

    [DataContract]
    public class VideoStaffInfo
    {
        [DataMember(Name="mid")]
        public long Mid { get; set; }
        [DataMember(Name="title")]
        public string Title { get; set; } // e.g., "UP主", "配音"
        [DataMember(Name="name")]
        public string Name { get; set; }
        [DataMember(Name="face")]
        public string Face { get; set; }
    }

    [DataContract]
    public class Video
    {
        [DataMember(Name="bvid")]
        public string Bvid { get; set; }
        [DataMember(Name="aid")]
        public long? Aid { get; set; } // Made nullable
        [DataMember(Name="videos")]
        public int VideosCount { get; set; } // Number of parts
        [DataMember(Name="pic")]
        public string Pic { get; set; } // Cover image
        [DataMember(Name="title")]
        public string Title { get; set; }
        [DataMember(Name="pubdate")]
        public long Pubdate { get; set; } // Unix timestamp
        [DataMember(Name="desc")]
        public string Desc { get; set; } // Description
        [DataMember(Name="cid")]
        public long? Cid { get; set; } // CID of the first part, made nullable
        [DataMember(Name="duration")]
        public long Duration { get; set; } // Total duration if single part, or duration of first part.

        [DataMember(Name="owner")]
        public VideoOwner Owner { get; set; }

        [DataMember(Name="stat")]
        public VideoStat Stat { get; set; }

        [DataMember(Name="pages")]
        public List<VideoPart> Pages { get; set; } // List of video parts

        [DataMember(Name="staff")]
        public List<VideoStaffInfo> Staff { get; set; } // Staff information for co-authored videos
    }
}