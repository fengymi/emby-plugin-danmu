## 类型说明
Movie 代表一部电影，通常是单一的媒体项目
Episode 代表一个电视剧的单个集
Series（电视系列）：一组按顺序组织起来的电视剧集，对应一个电视节目或网络系列剧。
Season（季）：属于特定 Series 的一个季度的集合，通常包含多个 Episodes。
MusicAlbum（音乐专辑）：包含音乐曲目（Tracks）的集合。
MusicArtist（音乐艺术家）：代表创作音乐的个人或团体。
MusicTrack（音乐曲目）：单个音乐作品或歌曲。
AudioBook（有声书）：配有旁白的书籍，通常以一系列音频文件的形式存在。
MusicVideo（音乐视频）：特定音乐作品对应的视频。
Book（书籍）：代表电子书或扫描的实体书籍等可阅读媒体。
PhotoAlbum（相册）：包含一系列照片的集合。
Folder（文件夹）：作为逻辑分组以包含不同类型的媒体项目。
Trailer（预告片）
LiveTV（实时电视）
Channel（频道）
Playlist（播放列表）


## 请求测试url
http://localhost:8096/emby/api/danmu/17?option=GetJsonById&X-Emby-Token=aa13a23774314e35a79688322954d9cf








## 优酷

// search
curl -H "Host: search.youku.com" -H "User-Agent: Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/93.0.4577.63 Safari/537.36 Edg/93.0.961.44" -H "traceparent: 00-1da4358abe8e5c1eb3d65ab89b284671-23c9d50a198aaf20-00" --compressed "https://search.youku.com/api/search?keyword=%E8%B5%B0%E8%B5%B0%E5%81%9C%E5%81%9C&userAgent=Mozilla%2f5.0+(Macintosh%3b+Intel+Mac+OS+X+10_15_7)+AppleWebKit%2f537.36+(KHTML%2c+like+Gecko)+Chrome%2f93.0.4577.63+Safari%2f537.36+Edg%2f93.0.961.44&site=1&categories=0&ftype=0&ob=0&pg=1"

// getcookie
https://log.mmstat.com

// shows
curl -H "Host: openapi.youku.com" -H "User-Agent: Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/93.0.4577.63 Safari/537.36 Edg/93.0.961.44" -H "traceparent: 00-daf144333d37d84ef8f81caaedc34d58-1434525820b19761-00" --compressed "https://openapi.youku.com/v2/shows/videos.json?client_id=53e6cc67237fc59a&package=com.huawei.hwvplayer.youku&ext=show&show_id=fdaaea8164164ffd98e8&page=1&count=20"
// videos
curl -H "Host: openapi.youku.com" -H "User-Agent: Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/93.0.4577.63 Safari/537.36 Edg/93.0.961.44" -H "traceparent: 00-daf144333d37d84ef8f81caaedc34d58-7481b1e11ee36000-00" -H "Cookie: _m_h5_tk=579ef0bf984c4176e0438188b116c758_1720774754775; _m_h5_tk_enc=48a308345fa5919d9748e2c5c772f9a1" --compressed "https://openapi.youku.com/v2/videos/show_basic.json?client_id=53e6cc67237fc59a&package=com.huawei.hwvplayer.youku&video_id=XNjQwMzkxNzU1Mg=="

// weakget
curl -H "Host: acs.youku.com" -H "User-Agent: Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/93.0.4577.63 Safari/537.36 Edg/93.0.961.44" -H "traceparent: 00-daf144333d37d84ef8f81caaedc34d58-3d16a5cc00bcf4c8-00" --compressed "https://acs.youku.com/h5/mtop.com.youku.aplatform.weakget/1.0/?jsv=2.5.1&appKey=24679788"
// danmu.list
curl -H "Host: acs.youku.com" -H "Referer: https://v.youku.com/" -H "User-Agent: Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/93.0.4577.63 Safari/537.36 Edg/93.0.961.44" -H "traceparent: 00-daf144333d37d84ef8f81caaedc34d58-a49071227a90be99-00" -H "Cookie: _m_h5_tk=579ef0bf984c4176e0438188b116c758_1720774754775; _m_h5_tk_enc=48a308345fa5919d9748e2c5c772f9a1" --data "data=%7B%22pid%22%3A0%2C%22ctype%22%3A10004%2C%22sver%22%3A%223.1.0%22%2C%22cver%22%3A%22v1.0%22%2C%22ctime%22%3A1720770254732%2C%22guid%22%3A%22zswXH3eBDXcCASp4SxhkiwLc%22%2C%22vid%22%3A%22XNjQwMzkxNzU1Mg%3D%3D%22%2C%22mat%22%3A0%2C%22mcount%22%3A1%2C%22type%22%3A1%2C%22msg%22%3A%22eyJjdGltZSI6MTcyMDc3MDI1NDczMiwiY3R5cGUiOjEwMDA0LCJjdmVyIjoidjEuMCIsImd1aWQiOiJ6c3dYSDNlQkRYY0NBU3A0U3hoa2l3TGMiLCJtYXQiOjAsIm1jb3VudCI6MSwicGlkIjowLCJzdmVyIjoiMy4xLjAiLCJ0eXBlIjoxLCJ2aWQiOiJYTmpRd016a3hOelUxTWc9PSJ9%22%2C%22sign%22%3A%2268d8155e77b5e5efa36f8376f01ebd07%22%7D" --compressed "https://acs.youku.com/h5/mopen.youku.danmu.list/1.0/?jsv=2.7.0&appKey=24679788&t=1720770254748&sign=f3359c90fcc405a0b953a4b1ce174d82&api=mopen.youku.danmu.list&v=1.0&type=originaljson&dataType=jsonp&timeout=20000&jsonpIncPrefix=utility"