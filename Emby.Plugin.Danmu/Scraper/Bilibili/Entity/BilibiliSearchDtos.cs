using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Bilibili.Entity
{
    /// <summary>
    /// DTO for the 'data' part of /x/web-interface/search/all/v2 response.
    /// </summary>
    public class BiliSearchAllV2Data
    {
        [JsonPropertyName("seid")]
        public string Seid { get; set; }

        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("pagesize")]
        public int PageSize { get; set; }

        [JsonPropertyName("numResults")]
        public int NumResults { get; set; }

        [JsonPropertyName("numPages")]
        public int NumPages { get; set; }

        [JsonPropertyName("suggest_keyword")]
        public string SuggestKeyword { get; set; }

        [JsonPropertyName("result")]
        public List<BiliSearchAllResultGroup> Result { get; set; }
    }

    /// <summary>
    /// DTO for each group in the 'result' array of /x/web-interface/search/all/v2 response (e.g., media_bangumi, media_ft).
    /// </summary>
    public class BiliSearchAllResultGroup
    {
        [JsonPropertyName("result_type")]
        public string ResultType { get; set; }

        [JsonPropertyName("data")]
        public List<Media> Data { get; set; } // Media class is in the same namespace Emby.Plugin.Danmu.Scraper.Bilibili.Entity
    }
}