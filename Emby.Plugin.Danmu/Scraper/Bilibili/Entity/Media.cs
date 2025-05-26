using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;
using Emby.Plugin.Danmu.Core.Extensions;

namespace Emby.Plugin.Danmu.Scraper.Bilibili.Entity
{
    public class Media
    {
        static readonly Regex regHtml = new Regex(@"\<.+?\>");
        static readonly Regex regSeasonNumber = new Regex(@"第([0-9一二三四五六七八九十]+)季");

        // For System.Text.Json, use JsonPropertyName
        [JsonPropertyName("type")]
        public string? ApiType { get; set; } // E.g., "media_bangumi", "media_ft". Renamed to avoid confusion with internal TypeName.

        [JsonPropertyName("media_type")]
        public int MediaType { get; set; }

        [JsonPropertyName("media_id")]
        public long MediaId { get; set; } // Specific media_id if applicable (e.g., for movies)

        [JsonPropertyName("season_id")]
        public long SeasonId { get; set; } // season_id for PGC content

        [JsonPropertyName("pgc_season_id")] // B站搜索结果中也有这个字段，有时比season_id更准确用于PGC
        public long PgcSeasonId { get; set; }

        [JsonPropertyName("season_type")]
        public int SeasonType { get; set; }

        [JsonPropertyName("season_type_name")]
        public string? SeasonTypeName { get; set; } // This is what Bilibili.cs needs for "电影"

        // This TypeName is for internal logic in Bilibili.cs.
        // It should be populated based on SeasonTypeName or ApiType.
        // For simplicity, Bilibili.cs can directly use SeasonTypeName.
        public string? TypeName { get; set; }

        [JsonPropertyName("ep_size")]
        public int EpisodeSize { get; set; }

        [DataMember(Name="cover")]
        public string Cover { get; set; }

        [JsonPropertyName("pubtime")]
        public long PubTime { get; set; } // Unix timestamp for publish time

        [JsonPropertyName("pub_date")] // Bilibili API search result does not seem to have pub_date directly, only pubtime
        public string? PubDate { get; set; } // Formatted publish date string

        private string title;
        [DataMember(Name="title")]
        public string Title
        {
            get
            {
                // First, remove HTML tags, then decode HTML entities
                return string.IsNullOrEmpty(title) ? string.Empty : HttpUtility.HtmlDecode(regHtml.Replace(title, ""));
            }
            set
            {
                title = value;
            }
        }
        
        [JsonPropertyName("org_title")]
        public string? OrgTitle { get; set; }

        [JsonPropertyName("goto_url")]
        public string? GotoUrl { get; set; } // URL to jump to, can indicate type (av, bv, ep, ss)

        [JsonPropertyName("desc")]
        public string? Description { get; set; }

        [JsonPropertyName("areas")] // B站API返回的是 "areas":"美国/英国"
        public string? Area { get; set; } // Changed from "area" to "areas" to match common Bilibili API field name, or use JsonPropertyName if it's "area"

        [JsonPropertyName("cv")]
        public string? CV { get; set; } // Voice actors

        [JsonPropertyName("staff")]
        public string? Staff { get; set; }
        
        // This property seems to be specific to Emby's logic and not directly from Bilibili's search result item.
        // It's kept for compatibility if your plugin uses it.
        // Consider if SeasonId from Bilibili (for PGC) is more direct.
        // The 'id' field in Bilibili.cs Search method is already handling this logic.


        [JsonIgnore]
        public int SeasonNumber {
            get {
                var number = regSeasonNumber.FirstMatchGroup(title);
                
                // 替换中文数字为阿拉伯数字
                return number.Replace("一", "1").Replace("二", "2").Replace("三", "3").Replace("四", "4").Replace("五", "5").Replace("六", "6").Replace("七", "7").Replace("八", "8").Replace("九", "9").ToInt();
            }
        }

        // Jellyfin's SearchItem has a 'seasons' field of type List<VideoSeasonSimple>
        // This might be useful if a search result directly lists seasons.
        // For now, keeping it simple as per current Emby usage.
        // [DataMember(Name = "seasons")]
        // public List<VideoSeasonSimple> Seasons { get; set; } // Define VideoSeasonSimple if needed
    }
}