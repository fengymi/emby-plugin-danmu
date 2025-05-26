using System.Runtime.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Bilibili.Entity
{
    public class VideoEpisode
    {
        [DataMember(Name="id")]
        public long Id { get; set; }

        [DataMember(Name="aid")]
        public long? AId { get; set; } // Made nullable to match usage in Bilibili.cs

        [DataMember(Name="bvid")]
        public string BvId { get; set; }

        [DataMember(Name="cid")]
        public long CId { get; set; }

        [DataMember(Name="title")]
        public string Title { get; set; }

        [DataMember(Name="long_title")]
        public string LongTitle { get; set; }

        [DataMember(Name="cover")]
        public string Cover { get; set; }

        [DataMember(Name="pub_time")]
        public long PubTime { get; set; } // Unix timestamp

        [DataMember(Name="duration")]
        public long Duration { get; set; } // Duration in milliseconds for PGC episodes

        [DataMember(Name="badge")]
        public string Badge { get; set; } // e.g., "会员", "预告"

        [DataMember(Name="badge_type")]
        public int BadgeType { get; set; } // Numeric type for the badge
    }
}