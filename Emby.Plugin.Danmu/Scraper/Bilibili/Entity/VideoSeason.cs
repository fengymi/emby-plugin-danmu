using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Bilibili.Entity
{
    public class VideoSeason
    {
        [DataMember(Name="season_id")]
        public long SeasonId { get; set; }

        [DataMember(Name="title")]
        public string Title { get; set; }

        [DataMember(Name="cover")]
        public string Cover { get; set; }

        [DataMember(Name="evaluate")]
        public string Evaluate { get; set; } // Synopsis or description

        [DataMember(Name="pub_time")]
        public long PubTime { get; set; } // Unix timestamp for publish time

        [DataMember(Name="season_title")]
        public string SeasonTitle { get; set; } // e.g., "第一季"

        [DataMember(Name="total_count")]
        public int TotalCount { get; set; } // Total number of episodes in this season

        [DataMember(Name="episodes")]
        public List<VideoEpisode> Episodes { get; set; }
    }
}