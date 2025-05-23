using System;
using System.Runtime.Serialization;
using Emby.Plugin.Danmu.Core.Extensions;

namespace Emby.Plugin.Danmu.Scraper.Youku.Entity
{
    public class YoukuEpisode
    {
        private string _id;
        [DataMember(Name="id")]
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

        [DataMember(Name="seq")]
        public string Seq { get; set; }

        [DataMember(Name="duration")]
        public string Duration { get; set; }

        [DataMember(Name="title")]
        public string Title { get; set; }

        [DataMember(Name="rc_title")]
        public string RCTitle { get; set; }

        [DataMember(Name="link")]
        public string Link { get; set; }

        [DataMember(Name="category")]
        public string Category { get; set; }


        public int TotalMat
        {
            get
            {
                var duration = Duration.ToDouble();
                return (int)Math.Floor(duration / 60) + 1;
            }

        }
    }
}
