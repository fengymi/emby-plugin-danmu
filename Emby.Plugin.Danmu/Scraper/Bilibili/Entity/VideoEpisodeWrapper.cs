// d:\桌面\danmuDLL\测试\emby-plugin-danmu-main\Emby.Plugin.Danmu\Scraper\Bilibili\Entity\VideoEpisodeWrapper.cs
using System.Text.Json.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Bilibili.Entity
{
    public class VideoEpisodeWrapper
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("data")]
        public VideoEpisode Data { get; set; } // 假设 data 字段直接是 VideoEpisode 对象
        // 如果 data 是一个列表，则应该是 public List<VideoEpisode> Data { get; set; }
    }
}
