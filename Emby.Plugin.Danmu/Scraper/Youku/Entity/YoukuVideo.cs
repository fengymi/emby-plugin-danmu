using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Youku.Entity
{
    public class YoukuVideo
    {
        [DataMember(Name="total")]
        public int Total { get; set; }

        [DataMember(Name="videos")]
        public List<YoukuEpisode> Videos { get; set; } = new List<YoukuEpisode>();

        private string _id;
        [IgnoreDataMember]
        public string ID {
            get
            {
                if (string.IsNullOrEmpty(_id))
                {
                    return _id;
                }

                return _id.Replace("=", "_");

            }
            set { this._id = value; }
        }

        [IgnoreDataMember]
        public string Title { get; set; }

        [IgnoreDataMember]
        public int? Year { get; set; }

        [IgnoreDataMember]
        public string Type { get; set; }

    }
}
