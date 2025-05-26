using System.Text.Json.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Bilibili.Entity
{
    public class BilibiliBaseResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        // 根据需要可以添加 data 字段，但对于此处的有效性检查，code 和 message 通常足够
        // [JsonPropertyName("data")]
        // public object? Data { get; set; }
    }
}
