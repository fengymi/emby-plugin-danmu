using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Emby.Plugin.Danmu.Scraper.Bilibili.Entity
{
    public class SearchResult
    {
        [DataMember(Name="page")]
        public int Page { get; set; }

        [DataMember(Name="pagesize")]
        public int PageSize { get; set; }

        [DataMember(Name="numResults")]
        public int NumResults { get; set; }

        [DataMember(Name="numPages")]
        public int NumPages { get; set; }
        
        [DataMember(Name="result")]
        public List<Media> Result { get; set; }
    }
}