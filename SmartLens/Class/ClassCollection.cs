using Bluetooth.Services.Obex;
using ICSharpCode.SharpZipLib.Zip;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using Microsoft.Data.Sqlite;
using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.Toolkit.Uwp.Notifications;
using MimeKit;
using MimeKit.Text;
using MimeKit.Tnef;
using Newtonsoft.Json;
using SmartLens.NetEase;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Enumeration;
using Windows.Devices.WiFi;
using Windows.Media.Capture.Frames;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Networking.Connectivity;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using WinRTXamlToolkit.Controls.Extensions;

namespace SmartLens
{
    #region WIFI列表提供类
    /// <summary>
    /// 保存搜索到的WiFi的相关信息
    /// </summary>
    public sealed class WiFiInfo : INotifyPropertyChanged
    {
        private WiFiAvailableNetwork AvailableWiFi;
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }

        /// <summary>
        /// 获取WiFi的MAC地址
        /// </summary>
        public string MAC
        {
            get
            {
                return AvailableWiFi.Bssid;
            }
        }

        /// <summary>
        /// 获取WiFi的SSID名称
        /// </summary>
        public string Name
        {
            get
            {
                if (AvailableWiFi.Ssid != "")
                {
                    return AvailableWiFi.Ssid;
                }
                else return "未知设备";
            }
        }

        /// <summary>
        /// 获取此WiFi的连接状态
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// 获取或设置此WiFi的密码
        /// </summary>
        public string Password { get; set; } = "";

        /// <summary>
        /// 获取消息提示的可见性
        /// </summary>
        public Visibility MessageVisibility { get; private set; } = Visibility.Collapsed;

        /// <summary>
        /// 获取或设置是否自动连接
        /// </summary>
        public bool AutoConnect { get; set; } = true;

        /// <summary>
        /// 获取或设置消息提示
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 获取WiFi安全性描述
        /// </summary>
        public string Encryption
        {
            get
            {
                if (AvailableWiFi.SecuritySettings.NetworkAuthenticationType == NetworkAuthenticationType.Open80211 || AvailableWiFi.SecuritySettings.NetworkAuthenticationType == NetworkAuthenticationType.None)
                {
                    if (IsConnected)
                    {
                        return "已连接，开放";
                    }
                    else
                    {
                        return "开放";
                    }
                }
                else
                {
                    if (IsConnected)
                    {
                        return "已连接，安全";
                    }
                    else
                    {
                        return "安全";
                    }
                }
            }
        }

        /// <summary>
        /// 获取WiFi信号强度
        /// </summary>
        public byte SignalBar
        {
            get
            {
                return AvailableWiFi.SignalBars;
            }
        }

        /// <summary>
        /// 获取或设置WiFi是否已经被下一次扫描所更新
        /// </summary>
        public bool IsUpdated { get; set; } = false;

        /// <summary>
        /// 创建WiFiInfo实例
        /// </summary>
        /// <param name="e">WiFi网络</param>
        /// <param name="IsConnected">连接状态</param>
        public WiFiInfo(WiFiAvailableNetwork e, bool IsConnected = false)
        {
            AvailableWiFi = e;
            this.IsConnected = IsConnected;
        }

        /// <summary>
        /// 更新WiFi信息
        /// </summary>
        /// <param name="e">重新扫描WiFi获取到的对应WiFiAvailableNetwork</param>
        public void Update(WiFiAvailableNetwork e)
        {
            if (Name != e.Ssid)
            {
                OnPropertyChanged("Name");
            }
            AvailableWiFi = e;
            IsUpdated = true;
            OnPropertyChanged("SignalBar");
            OnPropertyChanged("Encryption");
        }

        /// <summary>
        /// 在UI上显示消息
        /// </summary>
        /// <param name="msg">消息</param>
        public void ShowMessage(string msg)
        {
            Message = msg;
            MessageVisibility = Visibility.Visible;
            OnPropertyChanged("Message");
            OnPropertyChanged("MessageVisibility");
        }

        /// <summary>
        /// 在UI上隐藏消息
        /// </summary>
        public void HideMessage()
        {
            Message = "";
            MessageVisibility = Visibility.Collapsed;
            OnPropertyChanged("Message");
            OnPropertyChanged("MessageVisibility");
        }

        /// <summary>
        /// 异步更改此WiFi的连接状态
        /// </summary>
        /// <param name="IsConnected">连接状态应改为已连接或未连接</param>
        /// <param name="info">指示是否要将此WiFi移动到列表最上方</param>
        public async void ChangeConnectionStateAsync(bool IsConnected, bool MoveToTop = false)
        {
            this.IsConnected = IsConnected;
            await SettingsPage.ThisPage.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (MoveToTop)
                {
                    SettingsPage.ThisPage.WiFiList.Move(SettingsPage.ThisPage.WiFiList.IndexOf(this), 0);
                    SettingsPage.ThisPage.WiFiControl.ScrollIntoView(this);
                }
                OnPropertyChanged("Encryption");
            });
        }

        /// <summary>
        /// 获取WiFiInfo内部的WiFiAvailableNetwork
        /// </summary>
        /// <returns>WiFiAvailableNetwork</returns>
        public WiFiAvailableNetwork GetWiFiAvailableNetwork()
        {
            return AvailableWiFi;
        }
    }
    #endregion

    #region WiFi信号强度数据绑定转换类
    public sealed class WifiGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (!(value is byte))
            {
                return null;
            }

            var strength = (byte)value;

            switch (strength)
            {
                case 0:
                    return "\xEC3C";
                case 1:
                    return "\xEC3C";
                case 2:
                    return "\xEC3D";
                case 3:
                    return "\xEC3E";
                case 4:
                    return "\xEC3F";
                default:
                    return "\xEC3F";
            }

        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    #endregion

    #region 存储在数据库中的WiFi信息导入类
    /// <summary>
    /// 保存从数据库中提取的WiFi信息
    /// </summary>
    public sealed class WiFiInDataBase
    {
        /// <summary>
        /// 获取WiFi的SSID名称
        /// </summary>
        public string SSID { get; private set; }

        /// <summary>
        /// 获取WiFi密码
        /// </summary>
        public string Password { get; private set; }
        private readonly string autoconnect;

        /// <summary>
        /// 获取WiFi自动连接设置
        /// </summary>
        public bool AutoConnect
        {
            get
            {
                if (autoconnect == "True")
                    return true;
                else return false;
            }
        }

        /// <summary>
        /// 创建WiFiInDataBase实例
        /// </summary>
        /// <param name="SSID">SSID名称</param>
        /// <param name="Password">密码</param>
        /// <param name="AutoConnect">是否自动连接</param>
        public WiFiInDataBase(string SSID, string Password, string AutoConnect)
        {
            this.SSID = SSID;
            this.Password = Password;
            autoconnect = AutoConnect;
        }
    }
    #endregion

    #region 联网音乐搜索提供类
    /// <summary>
    /// 提供对云端音乐API的调用
    /// </summary>
    public sealed class NeteaseMusicAPI
    {
        private readonly string _MODULUS = "00e0b509f6259df8642dbc35662901477df22677ec152b5ff68ace615bb7b725152b3ab17a876aea8a5aa76d2e417629ec4ee341f56135fccf695280104e0312ecbda92557c93870114af6c9d05c4f7f0c3685b7a46bee255932575cce10b424d813cfe4875d3e82047b97ddef52741d546b8e289dc6935b3ece0462db0a22b8e7";
        private readonly string _NONCE = "0CoJUm6Qyw8W8jud";
        private readonly string _PUBKEY = "010001";
        private readonly string _VI = "0102030405060708";
        private readonly string _USERAGENT = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/63.0.3239.132 Safari/537.36";
        private readonly string _COOKIE = "os=pc;osver=Microsoft-Windows-10-Professional-build-16299.125-64bit;appver=2.0.3.131777;channel=netease;__remember_me=true";
        private readonly string _REFERER = "http://music.163.com/";
        private readonly string _secretKey;
        private readonly string _encSecKey;
        private static NeteaseMusicAPI Netease = null;

        private NeteaseMusicAPI()
        {
            _secretKey = CreateSecretKey(16);
            _encSecKey = RSAEncode(_secretKey);
        }

        /// <summary>
        /// 获取NeteaseMusicAPI的实例
        /// </summary>
        /// <returns>实例</returns>
        public static NeteaseMusicAPI GetInstance()
        {
            return Netease ?? (Netease = new NeteaseMusicAPI());
        }

        private string CreateSecretKey(int length)
        {
            var str = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var r = "";
            var rnd = new Random();
            for (int i = 0; i < length; i++)
            {
                r += str[rnd.Next(0, str.Length)];
            }
            return r;
        }

        private Dictionary<string, string> Prepare(string raw)
        {
            Dictionary<string, string> data = new Dictionary<string, string>();
            data["params"] = AESEncode(raw, _NONCE);
            data["params"] = AESEncode(data["params"], _secretKey);
            data["encSecKey"] = _encSecKey;

            return data;
        }

        // encrypt mod
        private string RSAEncode(string text)
        {
            string srtext = new string(text.Reverse().ToArray()); ;
            var a = BCHexDec(BitConverter.ToString(Encoding.Default.GetBytes(srtext)).Replace("-", ""));
            var b = BCHexDec(_PUBKEY);
            var c = BCHexDec(_MODULUS);
            var key = BigInteger.ModPow(a, b, c).ToString("x");
            key = key.PadLeft(256, '0');
            if (key.Length > 256)
                return key.Substring(key.Length - 256, 256);
            else
                return key;
        }

        private BigInteger BCHexDec(string hex)
        {
            BigInteger dec = new BigInteger(0);
            int len = hex.Length;
            for (int i = 0; i < len; i++)
            {
                dec += BigInteger.Multiply(new BigInteger(Convert.ToInt32(hex[i].ToString(), 16)), BigInteger.Pow(new BigInteger(16), len - i - 1));
            }
            return dec;
        }

        private string AESEncode(string secretData, string secret = "TA3YiYCfY2dDJQgg")
        {
            byte[] encrypted;
            byte[] IV = Encoding.UTF8.GetBytes(_VI);

            using (var aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(secret);
                aes.IV = IV;
                aes.Mode = CipherMode.CBC;
                using (var encryptor = aes.CreateEncryptor())
                {
                    using (var stream = new MemoryStream())
                    {
                        using (var cstream = new CryptoStream(stream, encryptor, CryptoStreamMode.Write))
                        {
                            using (var sw = new StreamWriter(cstream))
                            {
                                sw.Write(secretData);
                            }
                            encrypted = stream.ToArray();
                        }
                    }
                }
            }
            return Convert.ToBase64String(encrypted);
        }

        // fake curl
        private string CURL(string url, Dictionary<string, string> parms, string method = "POST")
        {
            string result;
            using (var wc = new WebClient())
            {
                wc.Headers.Add(HttpRequestHeader.ContentType, "application/x-www-form-urlencoded");
                wc.Headers.Add(HttpRequestHeader.Referer, _REFERER);
                wc.Headers.Add(HttpRequestHeader.UserAgent, _USERAGENT);
                wc.Headers.Add(HttpRequestHeader.Cookie, _COOKIE);
                var reqparm = new System.Collections.Specialized.NameValueCollection();
                foreach (var keyPair in parms)
                {
                    reqparm.Add(keyPair.Key, keyPair.Value);
                }
            flag:
                try
                {
                    byte[] responsebytes = wc.UploadValues(url, method, reqparm);
                    result = Encoding.UTF8.GetString(responsebytes);
                }
                catch (Exception) { goto flag; }
            }
            return result;
        }

        private class SearchJson
        {
            public string s;
            public int type;
            public int limit;
            public string total = "true";
            public int offset;
            public string csrf_token = "";
        }

        public enum SearchType
        {
            Song = 1,
            Album = 10,
            Artist = 100,
            PlayList = 1000,
            User = 1002,
            Radio = 1009,
        }

        /// <summary>
        /// 搜索云端乐库
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="keyword">关键字</param>
        /// <param name="limit">搜索返回结果限制条数</param>
        /// <param name="offset">偏移</param>
        /// <param name="type">搜索类型</param>
        /// <returns></returns>
        public Task<T> SearchAsync<T>(string keyword, int limit = 30, int offset = 0, SearchType type = SearchType.Song) where T : class
        {
            return Task.Run(() =>
            {
                var url = "http://music.163.com/weapi/cloudsearch/get/web";
                var data = new SearchJson
                {
                    s = keyword,
                    type = (int)type,
                    limit = limit,
                    offset = offset,
                };

                string raw = CURL(url, Prepare(JsonConvert.SerializeObject(data)));

                var DeserialedObj = JsonConvert.DeserializeObject<T>(raw);

                return DeserialedObj;
            });
        }

        /// <summary>
        /// 获取指定ID所代表的艺术家详细信息
        /// </summary>
        /// <param name="artist_id">艺术家ID</param>
        /// <returns>ArtistResult</returns>
        public Task<ArtistResult> GetArtistAsync(long artist_id)
        {
            return Task.Run(() =>
            {
                var url = "http://music.163.com/weapi/v1/artist/" + artist_id.ToString() + "?csrf_token=";
                var data = new Dictionary<string, string>
                {
                    {"csrf_token",""}
                };
                var raw = CURL(url, Prepare(JsonConvert.SerializeObject(data)));

                var deserialedObj = JsonConvert.DeserializeObject<ArtistResult>(raw);
                return deserialedObj;

            });
        }

        /// <summary>
        /// 获取指定ID所代表的专辑详细信息
        /// </summary>
        /// <param name="album_id">专辑ID</param>
        /// <returns>AlbumResult</returns>
        public Task<AlbumResult> GetAlbumAsync(long album_id)
        {
            return Task.Run(() =>
            {
                string url = "http://music.163.com/weapi/v1/album/" + album_id.ToString() + "?csrf_token=";
                var data = new Dictionary<string, string> {
                { "csrf_token","" },
            };
                string raw = CURL(url, Prepare(JsonConvert.SerializeObject(data)));
                var deserialedObj = JsonConvert.DeserializeObject<AlbumResult>(raw);
                return deserialedObj;
            });
        }

        /// <summary>
        /// 获取指定ID所代表的歌曲的详细信息
        /// </summary>
        /// <param name="song_id">歌曲ID</param>
        /// <returns>DetailResult</returns>
        public Task<DetailResult> GetDetailAsync(long song_id)
        {
            return Task.Run(() =>
            {
                string url = "http://music.163.com/weapi/v3/song/detail?csrf_token=";
                var data = new Dictionary<string, string> {
                { "c",
                    "[" + JsonConvert.SerializeObject(new Dictionary<string, string> { //神tm 加密的json里套json mdzz (说不定一次可以查多首歌?)
                        { "id", song_id.ToString() }
                    }) + "]"
                },
                {"csrf_token",""},
            };
                string raw = CURL(url, Prepare(JsonConvert.SerializeObject(data)));

                var deserialedObj = JsonConvert.DeserializeObject<DetailResult>(raw);
                return deserialedObj;
            });
        }

        private class GetSongUrlJson
        {
            public long[] ids;
            public long br;
            public string csrf_token = "";
        }

        /// <summary>
        /// 获取指定ID所代表的歌曲的播放链接
        /// </summary>
        /// <param name="song_id">歌曲ID</param>
        /// <param name="bitrate">比特率</param>
        /// <returns>SongUrls</returns>
        public Task<SongUrls> GetSongsUrlAsync(long[] song_id, long bitrate = 999000)
        {
            return Task.Run(() =>
            {
                string url = "http://music.163.com/weapi/song/enhance/player/url?csrf_token=";
                var data = new GetSongUrlJson
                {
                    ids = song_id,
                    br = bitrate
                };

                string raw = CURL(url, Prepare(JsonConvert.SerializeObject(data)));

                var deserialedObj = JsonConvert.DeserializeObject<SongUrls>(raw);
                return deserialedObj;
            });

        }

        /// <summary>
        /// 获取指定ID所代表的歌单的详细信息
        /// </summary>
        /// <param name="playlist_id">歌单ID</param>
        /// <returns>PlayListResult</returns>
        public PlayListResult Playlist(long playlist_id)
        {
            string url = "http://music.163.com/weapi/v3/playlist/detail?csrf_token=";
            var data = new Dictionary<string, string> {
                { "id",playlist_id.ToString() },
                { "n" , "1000" },
                { "csrf_token" , "" },
            };
            string raw = CURL(url, Prepare(JsonConvert.SerializeObject(data)));

            var deserialedObj = JsonConvert.DeserializeObject<PlayListResult>(raw);
            return deserialedObj;
        }

        /// <summary>
        /// 获取指定ID所代表的歌曲的歌词信息
        /// </summary>
        /// <param name="song_id"><歌曲ID/param>
        /// <returns>LyricResult</returns>
        public Task<LyricResult> GetLyricAsync(long song_id)
        {
            return Task.Run(() =>
            {
                string url = "http://music.163.com/weapi/song/lyric?csrf_token=";
                var data = new Dictionary<string, string> {
                { "id",song_id.ToString()},
                { "os","pc" },
                { "lv","-1" },
                { "kv","-1" },
                { "tv","-1" },
                { "csrf_token","" }
            };

                string raw = CURL(url, Prepare(JsonConvert.SerializeObject(data)));
                var deserialedObj = JsonConvert.DeserializeObject<LyricResult>(raw);
                return deserialedObj;
            });

        }

        /// <summary>
        /// 获取指定ID所代表的MV信息
        /// </summary>
        /// <param name="mv_id">MV的ID</param>
        /// <returns>MVResult</returns>
        public Task<MVResult> GetMVAsync(int mv_id)
        {
            return Task.Run(() =>
            {
                string url = "http://music.163.com/weapi/mv/detail?csrf_token=";
                var data = new Dictionary<string, string> {
                { "id",mv_id.ToString() },
                { "csrf_token","" },
            };
                string raw = CURL(url, Prepare(JsonConvert.SerializeObject(data)));
                var deserialedObj = JsonConvert.DeserializeObject<MVResult>(
                    raw.Replace("\"720\"", "\"the720\"")
                       .Replace("\"480\"", "\"the480\"")
                       .Replace("\"240\"", "\"the240\"")); //不能解析数字key的解决方案
                return deserialedObj;
            });
        }

        //static url encrypt, use for pic

        public string Id2Url(int id)
        {
            byte[] magic = Encoding.ASCII.GetBytes("3go8&8*3*3h0k(2)2");
            byte[] song_id = Encoding.ASCII.GetBytes(id.ToString());

            for (int i = 0; i < song_id.Length; i++)
                song_id[i] = Convert.ToByte(song_id[i] ^ magic[i % magic.Length]);

            string result;

            using (var md5 = MD5.Create())
            {
                md5.ComputeHash(song_id);
                result = Convert.ToBase64String(md5.Hash);
            }

            result = result.Replace("/", "_");
            result = result.Replace("+", "-");
            return result;
        }
    }
    #endregion

    #region 播放模式枚举
    /// <summary>
    /// 枚举播放模式
    /// </summary>
    public enum PlayMode
    {
        /// <summary>
        /// 顺序播放
        /// </summary>
        Order = 0,

        /// <summary>
        /// 随机播放
        /// </summary>
        Shuffle = 1,

        /// <summary>
        /// 列表循环
        /// </summary>
        ListLoop = 2,

        /// <summary>
        /// 单曲循环
        /// </summary>
        Repeat = 3
    }
    #endregion

    #region 单曲搜索类
    /// <summary>
    /// 保存音乐搜索结果的类
    /// </summary>
    public sealed class SearchSingleMusic
    {
        /// <summary>
        /// 音乐名称
        /// </summary>
        public string MusicName { get; private set; }

        /// <summary>
        /// 艺术家
        /// </summary>
        public string Artist { get; private set; }

        /// <summary>
        /// 专辑
        /// </summary>
        public string Album { get; private set; }

        /// <summary>
        /// 持续时间
        /// </summary>
        public string Duration { get; private set; }

        /// <summary>
        /// 收藏按钮颜色
        /// </summary>
        public SolidColorBrush Col { get; private set; } = new SolidColorBrush(Colors.White);

        /// <summary>
        /// 收藏按钮图形
        /// </summary>
        public string Glyph { get; private set; } = "\uEB51";

        /// <summary>
        /// 音乐封面URL
        /// </summary>
        public string ImageUrl { get; private set; }

        /// <summary>
        /// 音乐ID
        /// </summary>
        public long[] SongID { get; private set; }

        /// <summary>
        /// MV的ID
        /// </summary>
        public long MVid { get; private set; }

        /// <summary>
        /// 指示该音乐是否具有MV
        /// </summary>
        public bool MVExists { get; private set; } = false;

        /// <summary>
        /// 创建SearchSingleMusic的实例
        /// </summary>
        /// <param name="MusicName">音乐名</param>
        /// <param name="Artist">艺术家</param>
        /// <param name="Album">专辑名</param>
        /// <param name="Duration">持续时间</param>
        /// <param name="SongID">音乐ID</param>
        /// <param name="Url">封面图片URL</param>
        /// <param name="MVid">MV的ID</param>
        public SearchSingleMusic(string MusicName, string Artist, string Album, string Duration, long SongID, string Url, long MVid)
        {
            this.MusicName = MusicName;
            this.Artist = Artist;
            this.Album = Album;
            this.Duration = Duration;
            this.MVid = MVid;
            if (MVid != 0)
            {
                MVExists = true;
            }

            long[] temp = new long[1];
            temp[0] = SongID;
            this.SongID = temp;

            ImageUrl = Url;
            if (MusicList.ThisPage.MusicIdDictionary.Contains(SongID))
            {
                Glyph = "\uEB52";
                Col = new SolidColorBrush(Colors.Red);
            }
        }
    }
    #endregion

    #region 歌手搜索类
    /// <summary>
    /// 保存歌手搜索结果的类
    /// </summary>
    public sealed class SearchSinger
    {
        /// <summary>
        /// 艺术家名称
        /// </summary>
        public string Singer { get; private set; }

        /// <summary>
        /// 艺术家封面
        /// </summary>
        public Uri ImageUri { get; private set; }

        /// <summary>
        /// 艺术家ID
        /// </summary>
        public string ID { get; private set; }

        /// <summary>
        /// 创建SearchSinger的实例
        /// </summary>
        /// <param name="Singer">艺术家名称</param>
        /// <param name="ImageUri">艺术家封面</param>
        /// <param name="ID">艺术家ID</param>
        public SearchSinger(string Singer, Uri ImageUri, string ID)
        {
            this.Singer = Singer;
            this.ImageUri = ImageUri;
            this.ID = ID;
        }
    }
    #endregion

    #region 专辑搜索类
    /// <summary>
    /// 保存专辑搜索结果的类
    /// </summary>
    public sealed class SearchAlbum
    {
        /// <summary>
        /// 专辑封面URL
        /// </summary>
        public Uri ImageUri { get; private set; }

        /// <summary>
        /// 专辑名称
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// 专辑所属的艺术家名称
        /// </summary>
        public string Artists { get; private set; }

        /// <summary>
        /// 专辑ID
        /// </summary>
        public long ID { get; private set; }

        /// <summary>
        /// 创建SearchAlbum实例
        /// </summary>
        /// <param name="ImageUri">专辑封面URL</param>
        /// <param name="Name">专辑名</param>
        /// <param name="Artists">艺术家名</param>
        /// <param name="ID">专辑ID</param>
        public SearchAlbum(Uri ImageUri, string Name, string Artists, long ID)
        {
            this.ImageUri = ImageUri;
            this.Name = Name;
            this.Artists = Artists;
            this.ID = ID;
        }
    }

    /// <summary>
    /// 保存歌手详情页面下的专辑信息的类
    /// </summary>
    public sealed class SingerAlbum
    {
        /// <summary>
        /// 专辑封面URL
        /// </summary>
        public Uri AlbumCover { get; private set; }

        /// <summary>
        /// 专辑名称
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// 专辑ID
        /// </summary>
        public long ID { get; private set; }

        /// <summary>
        /// 创建SingerAlbum的实例
        /// </summary>
        /// <param name="Name">专辑名</param>
        /// <param name="ID">专辑ID</param>
        /// <param name="CoverUri">封面URL</param>
        public SingerAlbum(string Name, long ID, Uri CoverUri)
        {
            this.Name = Name;
            this.ID = ID;
            AlbumCover = CoverUri;
        }
    }

    #endregion

    #region MV提供类
    /// <summary>
    /// 保存歌手详情下的MV信息的类
    /// </summary>
    public sealed class SingerMV
    {
        /// <summary>
        /// MV封面URL
        /// </summary>
        public Uri MVCover { get; private set; }

        /// <summary>
        /// MV名称
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// MV相关介绍
        /// </summary>
        public string Introduction { get; private set; }

        /// <summary>
        /// MV的ID
        /// </summary>
        public int MovieID { get; private set; }

        /// <summary>
        /// 创建SingerMV的新实例
        /// </summary>
        /// <param name="Name">MV名称</param>
        /// <param name="Introduction">MV介绍</param>
        /// <param name="MovieID">MV的ID</param>
        /// <param name="CoverUri">MV的封面URI</param>
        public SingerMV(string Name, string Introduction, int MovieID, Uri CoverUri)
        {
            this.Name = Name;
            this.Introduction = Introduction;
            this.MovieID = MovieID;
            MVCover = CoverUri;
        }
    }

    public sealed class MVSuggestion
    {
        public Uri MVCoverUri { get; private set; }

        public string Name { get; private set; }

        public string Introduction { get; private set; }

        public int MovieID { get; private set; }

        public MVSuggestion(string Name, string Introduction, int MovieID, Uri CoverUri)
        {
            this.Name = Name;
            this.Introduction = Introduction;
            this.MovieID = MovieID;
            MVCoverUri = CoverUri;
        }

    }
    #endregion

    #region 搜索提示框数据模板选择类
    public sealed class CustomDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ArtistsTemplate { get; set; }
        public DataTemplate SingleMusicTemplate { get; set; }
        public DataTemplate AlbumTemplate { get; set; }
        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is ArtistSearchResult.Artists)
                return ArtistsTemplate;
            else if (item is Song)
                return SingleMusicTemplate;
            else if (item is AlbumSearchResult.AlbumsItem)
                return AlbumTemplate;
            else return null;
        }
    }
    #endregion

    #region 播放列表对象提供类
    /// <summary>
    /// 为音乐播放提供播放列表
    /// </summary>
    public sealed class MediaPlayList
    {
        /// <summary>
        /// 收藏的音乐的播放列表
        /// </summary>
        public static MediaPlaybackList FavouriteSongList = new MediaPlaybackList()
        {
            MaxPlayedItemsToKeepOpen = 3
        };

        /// <summary>
        /// 特定歌手的热门50首歌曲播放列表
        /// </summary>
        public static MediaPlaybackList SingerHotSongList = new MediaPlaybackList()
        {
            MaxPlayedItemsToKeepOpen = 3
        };

        /// <summary>
        /// 特定专辑内所有歌曲播放列表
        /// </summary>
        public static MediaPlaybackList AlbumSongList = new MediaPlaybackList()
        {
            MaxPlayedItemsToKeepOpen = 3
        };

        /// <summary>
        /// 热门50首歌曲备份
        /// </summary>
        public static List<SearchSingleMusic> HotSongBackup = new List<SearchSingleMusic>();

        /// <summary>
        /// 专辑歌曲备份
        /// </summary>
        public static List<SearchSingleMusic> AlbumSongBackup = new List<SearchSingleMusic>();

    }
    #endregion

    #region 音乐列表信息类
    /// <summary>
    /// 对音乐收藏列表提供显示
    /// </summary>
    public sealed class PlayList : INotifyPropertyChanged
    {
        /// <summary>
        /// 音乐名称
        /// </summary>
        public string Music { get; private set; }

        /// <summary>
        /// 艺术家名称
        /// </summary>
        public string Artist { get; private set; }

        /// <summary>
        /// 所属专辑名称
        /// </summary>
        public string Album { get; private set; }

        /// <summary>
        /// 持续时间
        /// </summary>
        public string Duration { get; private set; }

        /// <summary>
        /// 封面图片URL
        /// </summary>
        public string ImageUrl { get; private set; }

        /// <summary>
        /// 音乐ID
        /// </summary>
        public long SongID { get; private set; }

        /// <summary>
        /// MV的ID
        /// </summary>
        public long MVid { get; private set; }

        /// <summary>
        /// 指示是否存在MV
        /// </summary>
        public bool MVExists { get; private set; } = false;
        private SolidColorBrush fontcolor;

        /// <summary>
        /// 当音乐处于加载状态时，为界面提供灰色或白色指示
        /// </summary>
        public SolidColorBrush FontColor
        {
            get
            {
                return fontcolor;
            }
            set
            {
                fontcolor = value;
                OnPropertyChanged("FontColor");
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 创建PlayList的实例
        /// </summary>
        /// <param name="Music">音乐名</param>
        /// <param name="Artist">艺术家名</param>
        /// <param name="Album">专辑名</param>
        /// <param name="Duration">持续时间</param>
        /// <param name="ImageUrl">封面图片URL</param>
        /// <param name="SongID">音乐ID</param>
        /// <param name="MVid">MV的ID</param>
        /// <param name="LoadAsGray">指示是否要以灰色状态加载</param>
        public PlayList(string Music, string Artist, string Album, string Duration, string ImageUrl, long SongID, long MVid, bool LoadAsGray = false)
        {
            this.Music = Music;
            this.Artist = Artist;
            this.Album = Album;
            this.Duration = Duration;
            this.ImageUrl = ImageUrl;
            this.SongID = SongID;
            this.MVid = MVid;
            if (MVid != 0)
            {
                MVExists = true;
            }
            if (LoadAsGray)
            {
                FontColor = new SolidColorBrush(Colors.Gray);
            }
            else FontColor = new SolidColorBrush(Colors.White);
        }

        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
    }
    #endregion

    #region SQLite数据库类
    /// <summary>
    /// 为SmartLens提供数据库支持
    /// </summary>
    public sealed class SQLite : IDisposable
    {
        private SqliteConnection OLEDB = new SqliteConnection("Filename=SmartLens_SQLite.db");
        private bool IsDisposed = false;
        private static SQLite SQL = null;
        private static object Lock;
        private SQLite()
        {
            OLEDB.Open();
            string Command = @"Create Table If Not Exists MusicList (MusicName Text Not Null, Artist Text Not Null, Album Text Not Null, Duration Text Not Null, ImageURL Text Not Null, SongID Int Not Null ,MVid Int Not Null, Primary Key (MusicName,Artist,Album,Duration));
                               Create Table If Not Exists WiFiRecord (SSID Text Not Null, Password Text Not Null, AutoConnect Text Not Null, Primary Key (SSID,Password,AutoConnect));
                               Create Table If Not Exists HashTable (FileName Text Not Null, HashValue Text Not Null, Primary Key (FileName,HashValue))";
            SqliteCommand CreateTable = new SqliteCommand(Command, OLEDB);
            CreateTable.ExecuteNonQuery();
        }

        /// <summary>
        /// 提供SQLite的实例
        /// </summary>
        /// <returns>SQLite</returns>
        public static SQLite GetInstance()
        {
            if (Lock == null)
            {
                Lock = new object();
            }
            lock (Lock)
            {
                return SQL ?? (SQL = new SQLite());
            }
        }

        /// <summary>
        /// 异步获取SQLite数据库中存储的所有音乐名称
        /// </summary>
        /// <returns>string[]</returns>
        public async Task<string[]> GetAllMusicNameAsync()
        {
            SqliteCommand Command = new SqliteCommand("Select MusicName From MusicList", OLEDB);
            SqliteCommand Command1 = new SqliteCommand("Select Count(*) From MusicList", OLEDB);
            int DataCount = Convert.ToInt32(await Command1.ExecuteScalarAsync());
            if (DataCount == 0)
                return null;
            string[] Names = new string[DataCount];
            SqliteDataReader query1 = await Command.ExecuteReaderAsync();
            for (int i = 0; query1.Read(); i++)
            {
                Names[i] = query1[0].ToString();
            }
            return Names;
        }

        /// <summary>
        /// 异步获取SQLite数据库中存储的所有音乐数据
        /// </summary>
        /// <returns></returns>
        public async Task GetMusicDataAsync()
        {
            SqliteCommand Command = new SqliteCommand("Select * From MusicList", OLEDB);
            SqliteDataReader query = await Command.ExecuteReaderAsync();
            while (query.Read())
            {
                MusicList.ThisPage.FavouriteMusicCollection.Add(new PlayList(query[0].ToString(), query[1].ToString(), query[2].ToString(), query[3].ToString(), query[4].ToString(), (long)query[5], (long)query[6], true));
            }
            query.Close();

            SqliteDataReader query1 = await Command.ExecuteReaderAsync();
            int Index = 0;
            while (query1.Read())
            {
                long[] SongID = new long[1];
                SongID[0] = (long)query1[5];
                MediaPlaybackItem Item = new MediaPlaybackItem(MediaSource.CreateFromUri(new Uri((await NeteaseMusicAPI.GetInstance().GetSongsUrlAsync(SongID)).Data[0].Url)));
                MediaPlayList.FavouriteSongList.Items.Add(Item);

                MediaItemDisplayProperties Props = Item.GetDisplayProperties();
                Props.Type = Windows.Media.MediaPlaybackType.Music;
                Props.MusicProperties.Title = query1[0].ToString();
                Props.MusicProperties.Artist = query1[2].ToString();
                Item.ApplyDisplayProperties(Props);
                MusicList.ThisPage.FavouriteMusicCollection[Index++].FontColor = new SolidColorBrush(Colors.White);
            }
            if (MusicList.ThisPage.FavouriteMusicCollection.Count != 0)
            {
                var bitmap = new BitmapImage();
                MusicList.ThisPage.Image1.Source = bitmap;
                bitmap.UriSource = new Uri(MusicList.ThisPage.FavouriteMusicCollection[0].ImageUrl);
            }
        }

        public async Task SetMD5ValueAsync(List<KeyValuePair<string, string>> Hash)
        {
            StringBuilder sb = new StringBuilder("Delete From HashTable;");
            foreach (var Command in from Command in Hash
                                    select "Insert Into HashTable Values ('" + Command.Key + "','" + Command.Value + "');")
            {
                sb.Append(Command);
            }
            SqliteCommand SQLCommand = new SqliteCommand(sb.ToString(), OLEDB);
            await SQLCommand.ExecuteNonQueryAsync();
        }

        public async Task<List<KeyValuePair<string, string>>> GetMD5ValueAsync()
        {
            SqliteCommand Command = new SqliteCommand("Select * From HashTable", OLEDB);
            SqliteDataReader query = await Command.ExecuteReaderAsync();
            List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
            while (query.Read())
            {
                list.Add(new KeyValuePair<string, string>(query[0].ToString(), query[1].ToString()));
            }
            return list;
        }


        /// <summary>
        /// 向SQLite数据库中异步存储音乐数据
        /// </summary>
        /// <param name="MusicName">音乐名称</param>
        /// <param name="Artist">艺术家</param>
        /// <param name="Album">专辑名</param>
        /// <param name="Duration">持续时间</param>
        /// <param name="ImageURL">封面图片URL</param>
        /// <param name="SongID">歌曲ID</param>
        /// <param name="MVid">MV的ID</param>
        /// <returns></returns>
        public async Task SetMusicDataAsync(string MusicName, string Artist, string Album, string Duration, string ImageURL, long SongID, long MVid)
        {
            SqliteCommand Command = new SqliteCommand("Insert Into MusicList Values (@MusicName,@Artist,@Album,@Duration,@ImageURL,@SongID,@MVid)", OLEDB);
            Command.Parameters.AddWithValue("@MusicName", MusicName);
            Command.Parameters.AddWithValue("@Artist", Artist);
            Command.Parameters.AddWithValue("@Album", Album);
            Command.Parameters.AddWithValue("@Duration", Duration);
            Command.Parameters.AddWithValue("@SongID", SongID);
            Command.Parameters.AddWithValue("@ImageURL", ImageURL);
            Command.Parameters.AddWithValue("@MVid", MVid);
            await Command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 异步删除SQLite数据库中的音乐条目
        /// </summary>
        /// <param name="list">需要删除的对象</param>
        /// <returns>无</returns>
        public async Task DeleteMusicAsync(PlayList list)
        {
            SqliteCommand Command = new SqliteCommand("Delete From MusicList Where MusicName=@MusicName And Artist=@Artist And Album=@Album And Duration=@Duration", OLEDB);
            Command.Parameters.AddWithValue("@MusicName", list.Music);
            Command.Parameters.AddWithValue("@Artist", list.Artist);
            Command.Parameters.AddWithValue("@Album", list.Album);
            Command.Parameters.AddWithValue("@Duration", list.Duration);
            await Command.ExecuteNonQueryAsync();
        }



        /// <summary>
        /// 从SQLite数据库中异步获取上一次保存的WiFi数据
        /// </summary>
        /// <returns>Task<List<WiFiInDataBase>></returns>
        public async Task<List<WiFiInDataBase>> GetAllWiFiDataAsync()
        {
            List<WiFiInDataBase> WiFiContainer = new List<WiFiInDataBase>();
            SqliteCommand Command = new SqliteCommand("Select * From WiFiRecord", OLEDB);
            SqliteDataReader query = await Command.ExecuteReaderAsync();
            while (query.Read())
            {
                WiFiContainer.Add(new WiFiInDataBase(query[0].ToString(), query[1].ToString(), query[2].ToString()));
            }
            return WiFiContainer;
        }

        /// <summary>
        /// 异步更新SQLite数据库中的AutoConnnect字段
        /// </summary>
        /// <param name="SSID">WiFi的SSID号</param>
        /// <param name="AutoConnect">是否自动连接</param>
        /// <returns>无</returns>
        public async Task UpdateWiFiDataAsync(string SSID, bool AutoConnect)
        {
            SqliteCommand Command = new SqliteCommand("Update WiFiRecord Set AutoConnect = @AutoConnect Where SSID = @SSID", OLEDB);
            Command.Parameters.AddWithValue("@SSID", SSID);
            Command.Parameters.AddWithValue("@AutoConnect", AutoConnect ? "True" : "False");

            await Command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 异步更新SQLite数据库中的Password字段
        /// </summary>
        /// <param name="SSID">WiFi的SSID号</param>
        /// <param name="Password">密码</param>
        /// <returns></returns>
        public async Task UpdateWiFiDataAsync(string SSID, string Password)
        {
            SqliteCommand Command = new SqliteCommand("Update WiFiRecord Set Password = @Password Where SSID = @SSID", OLEDB);
            Command.Parameters.AddWithValue("@SSID", SSID);
            Command.Parameters.AddWithValue("@Password", Password);

            await Command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 异步向SQLite数据库中保存WiFi数据
        /// </summary>
        /// <param name="SSID">WiFi的SSID号</param>
        /// <param name="Password">密码</param>
        /// <param name="AutoConnect">是否自动连接</param>
        /// <returns>无</returns>
        public async Task SetWiFiDataAsync(string SSID, string Password, bool AutoConnect)
        {
            SqliteCommand Command = new SqliteCommand("Insert Into WiFiRecord Values (@SSID , @Password , @AutoConnect)", OLEDB);
            Command.Parameters.AddWithValue("@SSID", SSID);
            Command.Parameters.AddWithValue("@Password", Password);
            Command.Parameters.AddWithValue("@AutoConnect", AutoConnect ? "True" : "False");

            await Command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// 释放SQLite数据库资源
        /// </summary>
        public void Dispose()
        {
            if (!IsDisposed)
            {
                OLEDB.Dispose();
                OLEDB = null;
            }
            SQL = null;
            Lock = null;
            IsDisposed = true;
        }

        ~SQLite()
        {
            Dispose();
        }
    }
    #endregion

    #region 美妆品牌展示类
    /// <summary>
    /// 为美妆图片和口红颜色提供支持
    /// </summary>
    public sealed class CosmeticsItem
    {
        /// <summary>
        /// 口红品牌对应的图片
        /// </summary>
        public Uri ImageUri { get; private set; }

        /// <summary>
        /// 品牌名称
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// 品牌描述
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// 口红颜色
        /// </summary>
        public Color LipColor { get; private set; }

        /// <summary>
        /// 创建CosmeticsItem的实例
        /// </summary>
        /// <param name="uri">品牌图片URL</param>
        /// <param name="Name">品牌名称</param>
        /// <param name="Description">品牌描述</param>
        /// <param name="LipColor">口红颜色</param>
        public CosmeticsItem(Uri uri, string Name, string Description, Color LipColor)
        {
            ImageUri = uri;
            this.Name = Name;
            this.Description = Description;
            this.LipColor = LipColor;
        }
    }
    #endregion

    #region 摄像头实例提供类
    /// <summary>
    /// 提供摄像头的支持
    /// </summary>
    public sealed class CameraProvider
    {
        private static CameraHelper CamHelper = null;
        private static readonly object DisposeLocker = new object();
        private static readonly object CreateLocker = new object();
        private CameraProvider() { }

        /// <summary>
        /// 释放CameraProvider资源
        /// </summary>
        public static void Dispose()
        {
            lock (DisposeLocker)
            {
                if (CamHelper != null)
                {
                    CamHelper.Dispose();
                    CamHelper = null;
                }
            }
        }

        /// <summary>
        /// 获取CameraHelper实例
        /// </summary>
        /// <returns>CameraHelper实例</returns>
        public static CameraHelper GetCameraHelperInstance()
        {
            lock (CreateLocker)
            {
                return CamHelper = CamHelper ?? new CameraHelper();
            }
        }

        /// <summary>
        /// 设置视频帧采集来源
        /// </summary>
        /// <param name="FrameSource">来源</param>
        public static void SetCameraFrameSource(MediaFrameSourceGroup FrameSource)
        {
            if (CamHelper == null)
            {
                CamHelper = new CameraHelper
                {
                    FrameSourceGroup = FrameSource
                };
            }
            else
            {
                CamHelper.FrameSourceGroup = FrameSource;
            }
        }
    }
    #endregion

    #region 地理位置JSON解析类
    public class Position
    {
        public class Location
        {
            public string lng { get; set; }
            public string lat { get; set; }
        }

        public class AddressComponent
        {
            public string country { get; set; }
            public string country_code { get; set; }
            public string country_code_iso { get; set; }
            public string country_code_iso2 { get; set; }
            public string province { get; set; }
            public string city { get; set; }
            public string city_level { get; set; }
            public string district { get; set; }
            public string town { get; set; }
            public string adcode { get; set; }
            public string street { get; set; }
            public string street_number { get; set; }
            public string direction { get; set; }
            public string distance { get; set; }
        }

        public class Pois
        {
        }

        public class Roads
        {
        }

        public class PoiRegions
        {
            public string direction_desc { get; set; }
            public string name { get; set; }
            public string tag { get; set; }
            public string uid { get; set; }
        }

        public class Result
        {
            public Location location { get; set; }
            public string formatted_address { get; set; }
            public string business { get; set; }
            public AddressComponent addressComponent { get; set; }
            public List<Pois> pois { get; set; }
            public List<Roads> roads { get; set; }
            public List<PoiRegions> poiRegions { get; set; }
            public string sematic_description { get; set; }
            public string cityCode { get; set; }
        }

        public class RootObject
        {
            public string status { get; set; }
            public Result result { get; set; }
        }
    }
    #endregion

    #region 天气JSON解析类
    public class Weather
    {
        public class CityInfo
        {
            public string city { get; set; }

            public string cityId { get; set; }

            public string parent { get; set; }

            public string updateTime { get; set; }
        }

        public class Yesterday
        {
            public string date { get; set; }

            public string ymd { get; set; }

            public string week { get; set; }

            public string sunrise { get; set; }

            public string high { get; set; }

            public string low { get; set; }

            public string sunset { get; set; }

            public float aqi { get; set; }

            public string fx { get; set; }

            public string fl { get; set; }

            public string type { get; set; }

            public string notice { get; set; }
        }

        public class ForecastItem
        {

            public string date { get; set; }

            public string ymd { get; set; }

            public string week { get; set; }

            public string sunrise { get; set; }

            public string high { get; set; }

            public string low { get; set; }

            public string sunset { get; set; }

            public float aqi { get; set; }

            public string fx { get; set; }

            public string fl { get; set; }

            public string type { get; set; }

            public string notice { get; set; }
        }

        public class Data
        {
            public string shidu { get; set; }

            public float pm25 { get; set; }

            public float pm10 { get; set; }

            public string quality { get; set; }

            public string wendu { get; set; }

            public string ganmao { get; set; }

            public Yesterday yesterday { get; set; }

            public List<ForecastItem> forecast { get; set; }
        }

        public class Root
        {
            public string time { get; set; }

            public CityInfo cityInfo { get; set; }

            public string date { get; set; }

            public string message { get; set; }

            public int status { get; set; }

            public Data data { get; set; }
        }
    }
    #endregion

    #region 蓝牙设备列表类
    /// <summary>
    /// 为蓝牙模块提供蓝牙设备信息保存功能
    /// </summary>
    public sealed class BluetoothList : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 表示蓝牙设备
        /// </summary>
        public DeviceInformation DeviceInfo { get; set; }
        public void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }

        /// <summary>
        /// 获取蓝牙设备名称
        /// </summary>
        public string Name
        {
            get
            {
                return DeviceInfo.Name;
            }
        }

        /// <summary>
        /// 获取蓝牙标识字符串
        /// </summary>
        public string Id
        {
            get
            {
                return DeviceInfo.Id;
            }
        }

        /// <summary>
        /// 获取配对情况描述字符串
        /// </summary>
        public string IsPaired
        {
            get
            {
                if (DeviceInfo.Pairing.IsPaired)
                    return "已配对";
                else return "准备配对";
            }
        }

        /// <summary>
        /// Button显示属性
        /// </summary>
        public string CancelOrPairButton
        {
            get
            {
                if (DeviceInfo.Pairing.IsPaired)
                    return "取消配对";
                else return "配对";
            }
        }

        /// <summary>
        /// 更新蓝牙设备信息
        /// </summary>
        /// <param name="DeviceInfoUpdate">蓝牙设备的更新属性</param>
        public void Update(DeviceInformationUpdate DeviceInfoUpdate)
        {
            DeviceInfo.Update(DeviceInfoUpdate);
            OnPropertyChanged("IsPaired");
            OnPropertyChanged("Name");
        }

        /// <summary>
        /// 创建BluetoothList的实例
        /// </summary>
        /// <param name="DeviceInfo">蓝牙设备</param>
        public BluetoothList(DeviceInformation DeviceInfo)
        {
            this.DeviceInfo = DeviceInfo;
        }
    }
    #endregion

    #region 蓝牙Obex协议对象类
    /// <summary>
    /// 提供蓝牙OBEX协议服务
    /// </summary>
    public sealed class ObexServiceProvider
    {
        /// <summary>
        /// OBEX协议服务
        /// </summary>
        public static ObexService ObexClient { get; private set; }

        /// <summary>
        /// 设置Obex对象的实例
        /// </summary>
        /// <param name="obex">OBEX对象</param>
        public static void SetObexInstance(ObexService obex)
        {
            ObexClient = obex;
        }

        /// <summary>
        /// 释放OBEX服务资源
        /// </summary>
        public static void Dispose()
        {
            ObexClient = null;
        }
    }
    #endregion

    #region 可移动设备StorageFile类
    /// <summary>
    /// 提供USB设备中的文件的描述
    /// </summary>
    public sealed class RemovableDeviceFile : INotifyPropertyChanged
    {
        /// <summary>
        /// 获取文件大小
        /// </summary>
        public string Size { get; private set; }
        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>
        /// 获取此文件的StorageFile对象
        /// </summary>
        public StorageFile File { get; private set; }

        /// <summary>
        /// 获取此文件的缩略图
        /// </summary>
        public BitmapImage Thumbnail { get; private set; }

        /// <summary>
        /// 创建RemovableDeviceFile实例
        /// </summary>
        /// <param name="Size">文件大小</param>
        /// <param name="File">文件StorageFile对象</param>
        /// <param name="Thumbnail">文件缩略图</param>
        public RemovableDeviceFile(string Size, StorageFile File, BitmapImage Thumbnail)
        {
            this.Size = Size;
            this.File = File;
            this.Thumbnail = Thumbnail;
        }

        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }

        /// <summary>
        /// 更新文件以及文件大小，并通知UI界面
        /// </summary>
        /// <param name="File"></param>
        /// <param name="FileSize"></param>
        public void FileUpdateRequested(StorageFile File, string FileSize)
        {
            this.File = File;
            OnPropertyChanged("DisplayName");
            Size = FileSize;
            OnPropertyChanged("Size");
        }

        /// <summary>
        /// 更新文件名称，并通知UI界面
        /// </summary>
        public void NameUpdateRequested()
        {
            OnPropertyChanged("DisplayName");
        }

        /// <summary>
        /// 更新文件大小，并通知UI界面
        /// </summary>
        /// <param name="Size"></param>
        public void SizeUpdateRequested(string Size)
        {
            this.Size = Size;
            OnPropertyChanged("Size");
        }

        /// <summary>
        /// 获取文件的文件名(不包含后缀)
        /// </summary>
        public string DisplayName
        {
            get
            {
                return File.DisplayName;
            }
        }

        /// <summary>
        /// 获取文件的完整文件名(包括后缀)
        /// </summary>
        public string Name
        {
            get
            {
                return File.Name;
            }
        }

        /// <summary>
        /// 获取文件类型描述
        /// </summary>
        public string DisplayType
        {
            get
            {
                return File.DisplayType;
            }
        }

        public string Type
        {
            get
            {
                return File.FileType;
            }
        }
    }
    #endregion

    #region Zip文件查看器显示类
    /// <summary>
    /// 提供Zip内部文件的显示
    /// </summary>
    public sealed class ZipFileDisplay
    {
        /// <summary>
        /// 获取文件名
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// 获取压缩后的大小
        /// </summary>
        public string CompresionSize { get; private set; }

        /// <summary>
        /// 获取文件实际大小
        /// </summary>
        public string ActualSize { get; private set; }

        /// <summary>
        /// 获取文件修改时间
        /// </summary>
        public string Time { get; private set; }

        /// <summary>
        /// 获取文件类型
        /// </summary>
        public string Type { get; private set; }

        /// <summary>
        /// 获取是否加密的描述
        /// </summary>
        public string IsCrypted { get; private set; }

        /// <summary>
        /// 创建ZipFileDisplay的实例
        /// </summary>
        /// <param name="Name">文件名称</param>
        /// <param name="Type">文件类型</param>
        /// <param name="CompresionSize">压缩后大小</param>
        /// <param name="ActualSize">实际大小</param>
        /// <param name="Time">修改时间</param>
        /// <param name="IsCrypted">加密描述</param>
        public ZipFileDisplay(string Name, string Type, string CompresionSize, string ActualSize, string Time, bool IsCrypted)
        {
            this.CompresionSize = CompresionSize;
            this.Name = Name;
            this.Time = Time;
            this.Type = Type;
            this.ActualSize = ActualSize;
            if (IsCrypted)
            {
                this.IsCrypted = "密码保护：是";
            }
            else
            {
                this.IsCrypted = "密码保护：否";
            }
        }
    }
    #endregion

    #region USB设备为空时的文件目录树显示类
    public sealed class EmptyDeviceDisplay
    {
        public string DisplayName { get; set; }
    }
    #endregion

    #region Zip相关枚举
    /// <summary>
    /// AES加密密钥长度枚举
    /// </summary>
    public enum KeySize
    {
        /// <summary>
        /// 无
        /// </summary>
        None = 0,

        /// <summary>
        /// AES-128bit
        /// </summary>
        AES128 = 128,

        /// <summary>
        /// AES-256bit
        /// </summary>
        AES256 = 256
    }

    /// <summary>
    /// 压缩等级枚举
    /// </summary>
    public enum CompressionLevel
    {
        /// <summary>
        /// 最大
        /// </summary>
        Max = 9,

        /// <summary>
        /// 高于标准
        /// </summary>
        AboveStandard = 7,

        /// <summary>
        /// 标准
        /// </summary>
        Standard = 5,

        /// <summary>
        /// 低于标准
        /// </summary>
        BelowStandard = 3,

        /// <summary>
        /// 仅打包
        /// </summary>
        PackOnly = 1
    }
    #endregion

    #region Zip加密界面绑定转换器
    public sealed class ZipCryptConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (!(value is bool))
            {
                return null;
            }

            var IsEnable = (bool)value;
            if (IsEnable)
            {
                return Visibility.Visible;
            }
            else
            {
                return Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    #endregion

    #region AES加密解密方法类
    /// <summary>
    /// 提供AES加密相关方法
    /// </summary>
    public sealed class AESProvider
    {
        /// <summary>
        /// 默认256位密钥
        /// </summary>
        public const string Admin256Key = "12345678876543211234567887654321";

        /// <summary>
        /// 默认128位密钥
        /// </summary>
        public const string Admin128Key = "1234567887654321";

        /// <summary>
        /// 默认IV加密向量
        /// </summary>
        private static readonly byte[] AdminIV = Encoding.UTF8.GetBytes("r7BXXKkLb8qrSNn0");

        /// <summary>
        /// 使用AES-CBC加密方式的加密算法
        /// </summary>
        /// <param name="ToEncrypt">待加密的数据</param>
        /// <param name="key">密码</param>
        /// <param name="KeySize">密钥长度</param>
        /// <returns>加密后数据</returns>
        public static byte[] Encrypt(byte[] ToEncrypt, string key, int KeySize)
        {
            if (KeySize != 256 && KeySize != 128)
            {
                throw new InvalidEnumArgumentException("AES密钥长度仅支持128或256任意一种");
            }
            byte[] KeyArray = Encoding.UTF8.GetBytes(key);
            byte[] result;
            using (RijndaelManaged Rijndael = new RijndaelManaged
            {
                KeySize = KeySize,
                Key = KeyArray,
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7,
                IV = AdminIV
            })
            {
                ICryptoTransform CryptoTransform = Rijndael.CreateEncryptor();
                result = CryptoTransform.TransformFinalBlock(ToEncrypt, 0, ToEncrypt.Length);
            }
            return result;
        }

        /// <summary>
        /// 使用AES-CBC加密方式的解密算法
        /// </summary>
        /// <param name="ToDecrypt">待解密数据</param>
        /// <param name="key">密码</param>
        /// <param name="KeySize">密钥长度</param>
        /// <returns>解密后数据</returns>
        public static byte[] Decrypt(byte[] ToDecrypt, string key, int KeySize)
        {
            if (KeySize != 256 && KeySize != 128)
            {
                throw new InvalidEnumArgumentException("AES密钥长度仅支持128或256任意一种");
            }

            byte[] KeyArray = Encoding.UTF8.GetBytes(key);
            byte[] result;
            using (RijndaelManaged Rijndael = new RijndaelManaged
            {
                KeySize = KeySize,
                Key = KeyArray,
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7,
                IV = AdminIV
            })
            {
                ICryptoTransform CryptoTransform = Rijndael.CreateDecryptor();
                result = CryptoTransform.TransformFinalBlock(ToDecrypt, 0, ToDecrypt.Length);
            }
            return result;
        }

        /// <summary>
        /// 使用AES-ECB方式的加密算法
        /// </summary>
        /// <param name="ToEncrypt">待加密数据</param>
        /// <param name="key">密码</param>
        /// <param name="KeySize">密钥长度</param>
        /// <returns></returns>
        public static byte[] EncryptForUSB(byte[] ToEncrypt, string key, int KeySize)
        {
            if (KeySize != 256 && KeySize != 128)
            {
                throw new InvalidEnumArgumentException("AES密钥长度仅支持128或256任意一种");
            }

            byte[] KeyArray = Encoding.UTF8.GetBytes(key);
            byte[] result;
            using (RijndaelManaged Rijndael = new RijndaelManaged
            {
                KeySize = KeySize,
                Key = KeyArray,
                Mode = CipherMode.ECB,
                Padding = PaddingMode.Zeros
            })
            {
                ICryptoTransform CryptoTransform = Rijndael.CreateEncryptor();
                result = CryptoTransform.TransformFinalBlock(ToEncrypt, 0, ToEncrypt.Length);
            }
            return result;
        }

        /// <summary>
        /// 使用AES-ECB方式的解密算法
        /// </summary>
        /// <param name="ToDecrypt">待解密数据</param>
        /// <param name="key">密码</param>
        /// <param name="KeySize">密钥长度</param>
        /// <returns>解密后数据</returns>
        public static byte[] DecryptForUSB(byte[] ToDecrypt, string key, int KeySize)
        {
            if (KeySize != 256 && KeySize != 128)
            {
                throw new InvalidEnumArgumentException("AES密钥长度仅支持128或256任意一种");
            }

            byte[] KeyArray = Encoding.UTF8.GetBytes(key);
            byte[] result;
            using (RijndaelManaged Rijndael = new RijndaelManaged
            {
                KeySize = KeySize,
                Key = KeyArray,
                Mode = CipherMode.ECB,
                Padding = PaddingMode.Zeros
            })
            {

                ICryptoTransform CryptoTransform = Rijndael.CreateDecryptor();

                result = CryptoTransform.TransformFinalBlock(ToDecrypt, 0, ToDecrypt.Length);
            }
            return result;
        }

    }
    #endregion

    #region ListViewBase控件平滑位移扩展方法
    public static class ListViewBaseExtensions
    {
        public static void ScrollIntoViewSmoothly(this ListViewBase listViewBase, object item)
        {
            ScrollIntoViewSmoothly(listViewBase, item, ScrollIntoViewAlignment.Default);
        }

        public static void ScrollIntoViewSmoothly(this ListViewBase listViewBase, object item, ScrollIntoViewAlignment alignment)
        {
            if (listViewBase == null)
            {
                throw new ArgumentNullException(nameof(listViewBase));
            }

            // GetFirstDescendantOfType 是 WinRTXamlToolkit 中的扩展方法，
            // 寻找该控件在可视树上第一个符合类型的子元素。
            ScrollViewer scrollViewer = listViewBase.GetFirstDescendantOfType<ScrollViewer>();

            // 记录初始位置，用于 ScrollIntoView 检测目标位置后复原。
            double originHorizontalOffset = scrollViewer.HorizontalOffset;
            double originVerticalOffset = scrollViewer.VerticalOffset;

            void layoutUpdatedHandler(object sender, object e)
            {
                listViewBase.LayoutUpdated -= layoutUpdatedHandler;

                // 获取目标位置。
                double targetHorizontalOffset = scrollViewer.HorizontalOffset;
                double targetVerticalOffset = scrollViewer.VerticalOffset;

                void scrollHandler(object s, ScrollViewerViewChangedEventArgs m)
                {
                    scrollViewer.ViewChanged -= scrollHandler;

                    // 最终目的，带平滑滚动效果滚动到 item。
                    scrollViewer.ChangeView(targetHorizontalOffset, targetVerticalOffset, null);
                }

                scrollViewer.ViewChanged += scrollHandler;

                // 复原位置，且不需要使用动画效果。
                scrollViewer.ChangeView(originHorizontalOffset, originVerticalOffset, null, true);

            }

            listViewBase.LayoutUpdated += layoutUpdatedHandler;

            listViewBase.ScrollIntoView(item, alignment);
        }
    }
    #endregion

    #region 主题切换器
    /// <summary>
    /// 提供切换主题功能
    /// </summary>
    public sealed class ThemeSwitcher
    {
        /// <summary>
        /// 获取或设置使用亮色主题或暗色主题
        /// </summary>
        public static bool IsLightEnabled
        {
            get
            {
                if (ApplicationData.Current.RoamingSettings.Values["ThemeValue"] is string Theme)
                {
                    if (Theme == "Dark")
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    ApplicationData.Current.RoamingSettings.Values["ThemeValue"] = "Dark";
                    return false;
                }
            }
            set
            {
                if (value)
                {
                    ApplicationData.Current.RoamingSettings.Values["ThemeValue"] = "Dark";
                }
                else
                {
                    ApplicationData.Current.RoamingSettings.Values["ThemeValue"] = "Light";
                }
            }
        }
    }
    #endregion

    #region 天气数据到达事件信息传递类
    /// <summary>
    /// 天气数据到达时提供数据传递方式
    /// </summary>
    public sealed class WeatherData
    {
        /// <summary>
        /// 天气数据
        /// </summary>
        public Weather.Data Data { get; private set; }

        /// <summary>
        /// 地理位置信息
        /// </summary>
        public string Location { get; private set; }

        /// <summary>
        /// 创建WeatherData的实例
        /// </summary>
        /// <param name="Data">天气数据</param>
        /// <param name="Location">地理位置信息</param>
        public WeatherData(Weather.Data Data, string Location)
        {
            this.Data = Data;
            this.Location = Location;
        }
    }
    #endregion

    #region 天气数据获取错误枚举
    /// <summary>
    /// 获取天气错误类型枚举
    /// </summary>
    public enum ErrorReason
    {
        /// <summary>
        /// 因地理位置授权被拒绝而无法获取天气
        /// </summary>
        Location = 0,

        /// <summary>
        /// 因网络问题无法获取天气
        /// </summary>
        NetWork = 1,

        /// <summary>
        /// 因天气API错误而无法获取天气
        /// </summary>
        APIError = 2
    }
    #endregion

    #region USB图片展示类
    /// <summary>
    /// 为USB图片查看提供支持
    /// </summary>
    public sealed class PhotoDisplaySupport
    {
        /// <summary>
        /// 获取Bitmap图片对象
        /// </summary>
        public BitmapImage Bitmap { get; private set; }

        /// <summary>
        /// 获取Photo文件名称
        /// </summary>
        public string FileName
        {
            get
            {
                return PhotoFile.Name;
            }
        }

        /// <summary>
        /// 获取Photo的StorageFile对象
        /// </summary>
        public StorageFile PhotoFile { get; private set; }

        /// <summary>
        /// 创建PhotoDisplaySupport的实例
        /// </summary>
        /// <param name="stream">缩略图的流</param>
        /// <param name="File">图片文件</param>
        public PhotoDisplaySupport(IRandomAccessStream stream, StorageFile File)
        {
            Bitmap = new BitmapImage();
            Bitmap.SetSource(stream);
            PhotoFile = File;
        }
    }
    #endregion

    #region Zip自定义静态数据源
    public sealed class CustomStaticDataSource : IStaticDataSource
    {
        private Stream stream;

        public Stream GetSource()
        {
            return stream;
        }

        public void SetStream(Stream inputStream)
        {
            stream = inputStream;
            stream.Position = 0;
        }
    }
    #endregion

    #region Email基础服务提供类
    public sealed class EmailProtocolServiceProvider : IDisposable
    {
        private EmailProtocolServiceProvider()
        {
            GetStorageData();
            IMAPClient = new ImapClient();
            SMTPClient = new SmtpClient();
            SMTPOprationLock = new AutoResetEvent(false);
        }

        /// <summary>
        /// 获取IMAP连接状态
        /// </summary>
        public bool IsIMAPConnected
        {
            get
            {
                return IMAPClient.IsConnected;
            }
        }

        /// <summary>
        /// 获取用户名
        /// </summary>
        public string UserName
        {
            get
            {
                return Credential.UserName;
            }
        }

        /// <summary>
        /// 昵称
        /// </summary>
        public string CallName { get; private set; }

        private bool IsEnableSSL;
        private static readonly object SyncRoot = new object();
        private ImapClient IMAPClient;
        private SmtpClient SMTPClient;
        private AutoResetEvent SMTPOprationLock;
        private NetworkCredential Credential;
        private KeyValuePair<string, int> IMAPServerAddress = default;
        private KeyValuePair<string, int> SMTPServerAddress = default;
        private static EmailProtocolServiceProvider Instance;

        /// <summary>
        /// 获取EmailProtocolServiceProvider的实例
        /// </summary>
        /// <returns>实例</returns>
        public static EmailProtocolServiceProvider GetInstance()
        {
            lock (SyncRoot)
            {
                return Instance ?? (Instance = new EmailProtocolServiceProvider());
            }
        }

        /// <summary>
        /// EmailProtocolServiceProvider初始化时加载账户数据
        /// </summary>
        private void GetStorageData()
        {
            if (ApplicationData.Current.RoamingSettings.Values["EmailCredentialName"] is byte[] Name && ApplicationData.Current.RoamingSettings.Values["EmailCredentialPassword"] is byte[] Password)
            {
                string DecryptName = Encoding.UTF8.GetString(AESProvider.Decrypt(Name, AESProvider.Admin256Key, 256));
                string DecryptPassword = Encoding.UTF8.GetString(AESProvider.Decrypt(Password, AESProvider.Admin256Key, 256));
                Credential = new NetworkCredential(DecryptName, DecryptPassword);
            }
            if (ApplicationData.Current.RoamingSettings.Values["EmailIMAPAddress"] is string IMAPAddress && ApplicationData.Current.RoamingSettings.Values["EmailIMAPPort"] is int IMAPPort)
            {
                IMAPServerAddress = new KeyValuePair<string, int>(IMAPAddress, IMAPPort);
            }
            if (ApplicationData.Current.RoamingSettings.Values["EmailSMTPAddress"] is string SMTPAddress && ApplicationData.Current.RoamingSettings.Values["EmailSMTPPort"] is int SMTPPort)
            {
                SMTPServerAddress = new KeyValuePair<string, int>(SMTPAddress, SMTPPort);
            }
            if (ApplicationData.Current.RoamingSettings.Values["EmailEnableSSL"] is bool EnableSSL)
            {
                IsEnableSSL = EnableSSL;
            }
            if (ApplicationData.Current.RoamingSettings.Values["EmailCallName"] is string CallName)
            {
                this.CallName = CallName;
            }
        }

        /// <summary>
        /// 获取IMailFolder对象
        /// </summary>
        /// <returns>IMailFolder</returns>
        public IMailFolder GetMailFolder()
        {
            return IMAPClient.Inbox;
        }

        /// <summary>
        /// 异步与IMAP和SMTP服务器建立通信连接
        /// </summary>
        /// <param name="ConnectionCancellation">取消指令对象</param>
        /// <returns>无</returns>
        public async Task ConnectAllServiceAsync(CancellationTokenSource ConnectionCancellation)
        {
            if (Credential == null)
            {
                throw new InvalidOperationException("Credential invalid,Please excute \"void SetCredential(NetworkCredential Credential)\" first");
            }
            if (IMAPServerAddress.Equals(default(KeyValuePair<string, int>)) || SMTPServerAddress.Equals(default(KeyValuePair<string, int>)))
            {
                throw new InvalidOperationException("ServerAddress invalid,Please excute \"void SetEmailServerAddress(List<KeyValuePair<EmailProtocol, KeyValuePair<string, int>>> EmailAddress)\" first");
            }

            var task = ConnectSendServiceAsync(ConnectionCancellation);

            if (!IMAPClient.IsConnected)
            {
                await IMAPClient.ConnectAsync(IMAPServerAddress.Key, IMAPServerAddress.Value, IsEnableSSL, ConnectionCancellation.Token);
            }
            if (!IMAPClient.IsAuthenticated)
            {
                await IMAPClient.AuthenticateAsync(Credential.UserName, Credential.Password, ConnectionCancellation.Token);
            }

            await task;
        }

        /// <summary>
        /// 异步与SMTP服务器建立连接
        /// </summary>
        /// <param name="ConnectionCancellation">取消指令对象</param>
        /// <returns>无</returns>
        private async Task ConnectSendServiceAsync(CancellationTokenSource ConnectionCancellation)
        {
            if (!SMTPClient.IsConnected)
            {
                await SMTPClient.ConnectAsync(SMTPServerAddress.Key, SMTPServerAddress.Value, IsEnableSSL, ConnectionCancellation.Token);
            }
            if (!SMTPClient.IsAuthenticated)
            {
                await SMTPClient.AuthenticateAsync(Credential.UserName, Credential.Password, ConnectionCancellation.Token);
            }
            SMTPOprationLock.Set();
        }

        /// <summary>
        /// 异步发送Email邮件
        /// </summary>
        /// <param name="SendMessage">需发送的邮件</param>
        /// <returns>发送成功与否</returns>
        public async Task<bool> SendEmailAsync(MimeMessage SendMessage)
        {
            if (SendMessage == null)
            {
                throw new NullReferenceException("The message could not be null");
            }
            if (!(SMTPClient.IsConnected || SMTPClient.IsAuthenticated))
            {
                await Task.Run(() =>
                {
                    SMTPOprationLock.WaitOne();
                });
            }
            try
            {
                await SMTPClient.SendAsync(SendMessage);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 设置IMAP服务器和SMTP服务器的地址和端口，仅供首次初始化邮件模块使用
        /// </summary>
        /// <param name="EmailAddress">服务器地址</param>
        /// <param name="IsEnableSSL">是否启用SSL加密</param>
        public void SetEmailServerAddress(List<KeyValuePair<EmailProtocol, KeyValuePair<string, int>>> EmailAddress, bool IsEnableSSL)
        {
            this.IsEnableSSL = IsEnableSSL;
            ApplicationData.Current.RoamingSettings.Values["EmailEnableSSL"] = IsEnableSSL;

            foreach (var Protocol in EmailAddress)
            {
                switch (Protocol.Key)
                {
                    case EmailProtocol.IMAP:
                        {
                            IMAPServerAddress = Protocol.Value;
                            ApplicationData.Current.RoamingSettings.Values["EmailIMAPAddress"] = IMAPServerAddress.Key;
                            ApplicationData.Current.RoamingSettings.Values["EmailIMAPPort"] = IMAPServerAddress.Value;
                            break;
                        }
                    case EmailProtocol.SMTP:
                        {
                            SMTPServerAddress = Protocol.Value;
                            ApplicationData.Current.RoamingSettings.Values["EmailSMTPAddress"] = SMTPServerAddress.Key;
                            ApplicationData.Current.RoamingSettings.Values["EmailSMTPPort"] = SMTPServerAddress.Value;
                            break;
                        }
                }
            }

        }

        /// <summary>
        /// 设置用户邮箱登录凭据并加密保存，仅供首次初始化邮件模块使用
        /// </summary>
        /// <param name="Credential">凭据</param>
        /// <param name="CallName">称呼</param>
        /// <returns>无</returns>
        public Task SetCredential(NetworkCredential Credential, string CallName)
        {
            return Task.Run(() =>
            {
                this.Credential = Credential;
                this.CallName = CallName;
                ApplicationData.Current.RoamingSettings.Values["EmailCallName"] = CallName;
                ApplicationData.Current.RoamingSettings.Values["EmailCredentialName"] = AESProvider.Encrypt(Encoding.UTF8.GetBytes(Credential.UserName), AESProvider.Admin256Key, 256);
                ApplicationData.Current.RoamingSettings.Values["EmailCredentialPassword"] = AESProvider.Encrypt(Encoding.UTF8.GetBytes(Credential.Password), AESProvider.Admin256Key, 256);
            });
        }

        /// <summary>
        /// 检查EmailProtocolServiceProvider实例是否被释放
        /// </summary>
        /// <returns></returns>
        public static bool CheckWhetherInstanceExist()
        {
            return Instance == null ? false : true;
        }

        /// <summary>
        /// 释放EmailProtocolServiceProvider资源
        /// </summary>
        public void Dispose()
        {
            IMAPClient?.Dispose();
            SMTPClient?.Dispose();

            SMTPOprationLock?.Dispose();
            IMAPClient = null;
            SMTPClient = null;
            SMTPOprationLock = null;
            Instance = null;
        }
    }
    #endregion

    #region Email信息内部传递包
    /// <summary>
    /// Email内部信息传递包
    /// </summary>
    public sealed class InfomationDeliver
    {
        /// <summary>
        /// 获取发件人
        /// </summary>
        public string From { get; private set; }

        /// <summary>
        /// 获取收件人
        /// </summary>
        public string To { get; private set; }

        /// <summary>
        /// 获取Email发送类型
        /// </summary>
        public EmailSendType SendType { get; private set; }

        /// <summary>
        /// 获取Email主题
        /// </summary>
        public string Title { get; private set; }

        /// <summary>
        /// 创建InfomationDeliver的实例
        /// </summary>
        /// <param name="From">发件人</param>
        /// <param name="To">收件人</param>
        /// <param name="Title">主题</param>
        /// <param name="SendType">发送类型</param>
        public InfomationDeliver(string From, string To, string Title, EmailSendType SendType)
        {
            if (SendType == EmailSendType.NormalSend)
            {
                throw new InvalidEnumArgumentException("if EmailSendType is NormalSend ,Please use another overload");
            }
            this.From = From;
            this.To = To;
            this.Title = Title;
            this.SendType = SendType;
        }

        /// <summary>
        /// 创建InfomationDeliver的实例
        /// </summary>
        /// <param name="From">发件人</param>
        /// <param name="Title">主题</param>
        public InfomationDeliver(string From, string Title)
        {
            To = null;
            this.From = From;
            this.Title = Title;
            SendType = EmailSendType.Forward;
        }

        /// <summary>
        /// 创建InfomationDeliver的实例
        /// </summary>
        /// <param name="From">发件人</param>
        public InfomationDeliver(string From)
        {
            this.From = From;
            To = null;
            Title = null;
            SendType = EmailSendType.NormalSend;
        }
    }
    #endregion

    #region Email相关枚举
    /// <summary>
    /// Email协议枚举
    /// </summary>
    public enum EmailProtocol
    {
        /// <summary>
        /// IMAP协议
        /// </summary>
        IMAP = 0,

        /// <summary>
        /// SMTP协议
        /// </summary>
        SMTP = 1
    }

    /// <summary>
    /// Email发送类型枚举
    /// </summary>
    public enum EmailSendType
    {
        /// <summary>
        /// 回复全部
        /// </summary>
        ReplyToAll = 0,

        /// <summary>
        /// 回复
        /// </summary>
        Reply = 1,

        /// <summary>
        /// 新邮件
        /// </summary>
        NormalSend = 2,

        /// <summary>
        /// 转发
        /// </summary>
        Forward = 3
    }
    #endregion

    #region Email列表展示类
    /// <summary>
    /// 为Email邮件列表提供展示
    /// </summary>
    public sealed class EmailItem : INotifyPropertyChanged
    {
        /// <summary>
        /// 获取邮件消息本体
        /// </summary>
        public MimeMessage Message { get; private set; }
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 获取附件
        /// </summary>
        public IEnumerable<MimeEntity> FileEntitys
        {
            get
            {
                return Message.Attachments;
            }
        }

        /// <summary>
        /// 获取发件人
        /// </summary>
        public string From
        {
            get
            {
                return Message.From[0].Name == "" ? Message.From[0].ToString() : Message.From[0].Name;
            }
        }

        /// <summary>
        /// 获取主题
        /// </summary>
        public string Title
        {
            get
            {
                return Message.Subject;
            }
        }

        /// <summary>
        /// 获取唯一标识符
        /// </summary>
        public UniqueId Id { get; private set; }

        /// <summary>
        /// 获取发件人首字母
        /// </summary>
        public string FirstWord
        {
            get
            {
                return From[0].ToString().ToUpper();
            }
        }

        private double Indicator;

        /// <summary>
        /// 未读指示器
        /// </summary>
        public double IsNotSeenIndicator
        {
            get
            {
                return Indicator;
            }
            private set
            {
                Indicator = value;
                OnPropertyChanged("IsNotSeenIndicator");
            }
        }

        /// <summary>
        /// 获取邮件的发件日期
        /// </summary>
        public string Date
        {
            get
            {
                return Message.Date.LocalDateTime.Year + "年" + (Message.Date.LocalDateTime.Month < 10 ? "0" + Message.Date.LocalDateTime.Month : Message.Date.LocalDateTime.Month.ToString()) + "月" + (Message.Date.LocalDateTime.Day < 10 ? "0" + Message.Date.LocalDateTime.Day : Message.Date.LocalDateTime.Day.ToString()) + "日";
            }
        }

        /// <summary>
        /// 获取圆圈的显示颜色
        /// </summary>
        public Color Color { get; private set; }

        /// <summary>
        /// 异步设置邮件未读或已读
        /// </summary>
        /// <param name="visibility">已读或未读</param>
        public async Task SetSeenIndicatorAsync(Visibility visibility)
        {
            if (visibility == Visibility.Visible)
            {
                IsNotSeenIndicator = 1;
                await EmailProtocolServiceProvider.GetInstance().GetMailFolder().RemoveFlagsAsync(Id, MessageFlags.Seen, true);
            }
            else
            {
                IsNotSeenIndicator = 0;
                await EmailProtocolServiceProvider.GetInstance().GetMailFolder().SetFlagsAsync(Id, MessageFlags.Seen, true);
            }
        }

        public EmailItem(MimeMessage Message, UniqueId Id)
        {
            this.Message = Message;
            this.Id = Id;

            //随机数来进行颜色指定
            Random random = new Random();
            switch (random.Next(1, 7))
            {
                case 1: Color = Colors.DarkSeaGreen; break;
                case 2: Color = Colors.Brown; break;
                case 3: Color = Colors.DarkCyan; break;
                case 4: Color = Colors.Orange; break;
                case 5: Color = Colors.Violet; break;
                case 6: Color = Colors.DeepSkyBlue; break;
                case 7: Color = Colors.Gray; break;
                default: Color = Colors.DeepSkyBlue; break;
            }

            if (EmailPresenter.ThisPage.NotSeenDictionary.Contains(Id))
            {
                IsNotSeenIndicator = 1;
            }
            else
            {
                IsNotSeenIndicator = 0;
            }
        }

        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
    }
    #endregion

    #region Email附件展示类
    /// <summary>
    /// 提供Email附件信息
    /// </summary>
    public sealed class EmailAttachment
    {
        /// <summary>
        /// 创建EmailAttachment的实例
        /// </summary>
        /// <param name="Entity"></param>
        public EmailAttachment(MimeEntity Entity)
        {
            this.Entity = Entity;
        }

        /// <summary>
        /// 获取附件对象
        /// </summary>
        public MimeEntity Entity { get; private set; }

        /// <summary>
        /// 获取附件文件名称
        /// </summary>
        public string FileName
        {
            get
            {
                if (Entity is MessagePart)
                {
                    return string.IsNullOrEmpty(Entity.ContentDisposition?.FileName) ? Entity.ContentType.Name ?? "attached.eml" : Entity.ContentDisposition.FileName;
                }
                else
                {
                    var part = (MimePart)Entity;
                    return part.FileName;
                }
            }
        }

        /// <summary>
        /// 获取附件类型
        /// </summary>
        public string Type
        {
            get
            {
                return FileName.Split(".").Last().ToUpper();
            }
        }
    }
    #endregion

    #region Email-HTML解析类
    /// <summary>
    /// 提供对Email内嵌HTML的解析
    /// </summary>
    public sealed class HtmlPreviewVisitor : MimeVisitor
    {
        List<MultipartRelated> stack = new List<MultipartRelated>();
        List<MimeEntity> attachments = new List<MimeEntity>();
        readonly string tempDir;
        string body;

        public HtmlPreviewVisitor(string TempDirectory)
        {
            tempDir = TempDirectory;
        }

        public IList<MimeEntity> Attachments
        {
            get
            {
                return attachments;
            }
        }

        public string HtmlBody
        {
            get
            {
                return body ?? string.Empty;
            }
        }

        protected override void VisitMultipartAlternative(MultipartAlternative alternative)
        {
            for (int i = alternative.Count - 1; i >= 0 && body == null; i--)
            {
                alternative[i].Accept(this);
            }
        }

        protected override void VisitMultipartRelated(MultipartRelated related)
        {
            var root = related.Root;

            stack.Add(related);

            root.Accept(this);

            stack.RemoveAt(stack.Count - 1);
        }

        private bool TryGetImage(string url, out MimePart image)
        {
            UriKind kind;
            int index;
            Uri uri;

            if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                kind = UriKind.Absolute;
            }
            else if (Uri.IsWellFormedUriString(url, UriKind.Relative))
            {
                kind = UriKind.Relative;
            }
            else
            {
                kind = UriKind.RelativeOrAbsolute;
            }

            try
            {
                uri = new Uri(url, kind);
            }
            catch
            {
                image = null;
                return false;
            }

            for (int i = stack.Count - 1; i >= 0; i--)
            {
                if ((index = stack[i].IndexOf(uri)) == -1)
                {
                    continue;
                }
                image = stack[i][index] as MimePart;
                return image != null;
            }

            image = null;

            return false;
        }

        private string SaveImage(MimePart image, string url)
        {
            string fileName = url.Replace(':', '_').Replace('\\', '_').Replace('/', '_');

            string path = Path.Combine(tempDir, fileName);

            if (!File.Exists(path))
            {
                using (var output = File.Create(path))
                {
                    image.Content.DecodeTo(output);
                }
            }

            return "data://" + path.Replace('\\', '/');
        }

        private void HtmlTagCallback(HtmlTagContext ctx, HtmlWriter htmlWriter)
        {
            if (ctx.TagId == HtmlTagId.Image && !ctx.IsEndTag && stack.Count > 0)
            {
                ctx.WriteTag(htmlWriter, false);

                foreach (var attribute in ctx.Attributes)
                {
                    if (attribute.Id == HtmlAttributeId.Src)
                    {
                        string url;

                        if (!TryGetImage(attribute.Value, out MimePart image))
                        {
                            htmlWriter.WriteAttribute(attribute);
                            continue;
                        }

                        url = SaveImage(image, attribute.Value);

                        htmlWriter.WriteAttributeName(attribute.Name);
                        htmlWriter.WriteAttributeValue(url);
                    }
                    else
                    {
                        htmlWriter.WriteAttribute(attribute);
                    }
                }
            }
            else if (ctx.TagId == HtmlTagId.Body && !ctx.IsEndTag)
            {
                ctx.WriteTag(htmlWriter, false);

                foreach (var attribute in ctx.Attributes)
                {
                    if (attribute.Name.ToLowerInvariant() == "oncontextmenu")
                    {
                        continue;
                    }
                    htmlWriter.WriteAttribute(attribute);
                }

                htmlWriter.WriteAttribute("oncontextmenu", "return false;");
            }
            else
            {
                ctx.WriteTag(htmlWriter, true);
            }
        }

        protected override void VisitTextPart(TextPart entity)
        {
            TextConverter converter;

            if (body != null)
            {
                attachments.Add(entity);
                return;
            }

            if (entity.IsHtml)
            {
                converter = new HtmlToHtml
                {
                    HtmlTagCallback = HtmlTagCallback
                };
            }
            else if (entity.IsFlowed)
            {
                var flowed = new FlowedToHtml();
                if (entity.ContentType.Parameters.TryGetValue("delsp", out string delsp))
                {
                    flowed.DeleteSpace = delsp.ToLowerInvariant() == "yes";
                }
                converter = flowed;
            }
            else
            {
                converter = new TextToHtml();
            }

            body = converter.Convert(entity.Text);
        }

        protected override void VisitTnefPart(TnefPart entity)
        {
            attachments.AddRange(entity.ExtractAttachments());
        }

        protected override void VisitMessagePart(MessagePart entity)
        {
            attachments.Add(entity);
        }

        protected override void VisitMimePart(MimePart entity)
        {
            attachments.Add(entity);
        }
    }
    #endregion

    #region Email信息包装回传类
    /// <summary>
    /// Email编辑最终完成时提供数据包传递
    /// </summary>
    public sealed class SendEmailData
    {
        /// <summary>
        /// 获取收件人信息
        /// </summary>
        public List<MailboxAddress> To { get; private set; }

        /// <summary>
        /// 获取Email主题
        /// </summary>
        public string Subject { get; set; }

        /// <summary>
        /// 获取Email正文
        /// </summary>
        public string Text { get; private set; }

        /// <summary>
        /// 获取发送类型
        /// </summary>
        public EmailSendType SendType { get; private set; }

        /// <summary>
        /// 获取附件
        /// </summary>
        public List<MimePart> Attachments { get; private set; }

        /// <summary>
        /// 创建SendEmailData的实例
        /// </summary>
        /// <param name="Text">正文内容</param>
        /// <param name="SendType">发送选项</param>
        /// <param name="Attachments">附件</param>
        public SendEmailData(string Text, EmailSendType SendType, List<MimePart> Attachments)
        {
            if (SendType == EmailSendType.NormalSend)
            {
                throw new InvalidEnumArgumentException("if EmailSendType is NormalSend ,Please use another overload");
            }
            else if (SendType == EmailSendType.Forward)
            {
                throw new InvalidEnumArgumentException("if EmailSendType is Forward ,Please use another overload");
            }
            To = null;
            Subject = null;
            this.Text = Text;
            if (Attachments != null)
            {
                this.Attachments = new List<MimePart>(Attachments);
            }
            else
            {
                this.Attachments = null;
            }
            this.SendType = SendType;
        }

        /// <summary>
        /// 创建发送类型为Forward的SendEmailData的实例
        /// </summary>
        /// <param name="To">收件人</param>
        public SendEmailData(string To)
        {
            string[] ToGroup = To.Split(";");
            List<MailboxAddress> Address = new List<MailboxAddress>(ToGroup.Length);
            foreach (var Person in ToGroup)
            {
                Address.Add(new MailboxAddress(Person));
            }
            this.To = Address;
            SendType = EmailSendType.Forward;
        }

        /// <summary>
        /// 创建发送类型为NormalSend的SendEmailData的实例
        /// </summary>
        /// <param name="To"></param>
        /// <param name="Subject"></param>
        /// <param name="Text"></param>
        /// <param name="Attachments"></param>
        public SendEmailData(string To, string Subject, string Text, List<MimePart> Attachments)
        {
            string[] ToGroup = To.Split(";");
            List<MailboxAddress> Address = new List<MailboxAddress>(ToGroup.Length);
            foreach (var Person in ToGroup)
            {
                Address.Add(new MailboxAddress(Person));
            }
            this.To = Address;
            this.Subject = Subject;
            this.Text = Text;
            if (Attachments != null)
            {
                this.Attachments = new List<MimePart>(Attachments);
            }
            else
            {
                this.Attachments = null;
            }
            SendType = EmailSendType.NormalSend;
        }

    }
    #endregion

    #region 初始化Email设置时的数据传递包
    /// <summary>
    /// 提供Email初始化时的数据传递
    /// </summary>
    public sealed class EmailLoginData
    {
        /// <summary>
        /// 获取Email用户名
        /// </summary>
        public string EmailAddress { get; private set; }

        /// <summary>
        /// 获取昵称
        /// </summary>
        public string CallName { get; private set; }

        /// <summary>
        /// 获取密码
        /// </summary>
        public string Password { get; private set; }

        /// <summary>
        /// 获取IMAP服务器地址
        /// </summary>
        public string IMAPAddress { get; private set; }

        /// <summary>
        /// 获取IMAP服务器端口
        /// </summary>
        public int IMAPPort { get; private set; }

        /// <summary>
        /// 获取SMTP服务器地址
        /// </summary>
        public string SMTPAddress { get; private set; }

        /// <summary>
        /// 获取SMTP服务器端口
        /// </summary>
        public int SMTPPort { get; private set; }

        /// <summary>
        /// 获取是否启用SSL安全连接
        /// </summary>
        public bool IsEnableSSL { get; private set; }

        /// <summary>
        /// 创建EmailLoginData的实例
        /// </summary>
        /// <param name="EmailAddress">用户名</param>
        /// <param name="CallName">昵称</param>
        /// <param name="Password">密码</param>
        public EmailLoginData(string EmailAddress, string CallName, string Password)
        {
            this.EmailAddress = EmailAddress;
            this.CallName = CallName;
            this.Password = Password;
        }

        /// <summary>
        /// Email第二步设置完成时对剩余信息进行补充
        /// </summary>
        /// <param name="IMAPAddress">IMAP服务器地址</param>
        /// <param name="IMAPPort">IMAP端口</param>
        /// <param name="SMTPAddress">SMTP服务器地址</param>
        /// <param name="SMTPPort">SMTP端口</param>
        /// <param name="IsEnableSSL">是否启用SSL加密连接</param>
        public void SetExtraData(string IMAPAddress, int IMAPPort, string SMTPAddress, int SMTPPort, bool IsEnableSSL)
        {
            this.IMAPAddress = IMAPAddress;
            this.IMAPPort = IMAPPort;
            this.SMTPAddress = SMTPAddress;
            this.SMTPPort = SMTPPort;
            this.IsEnableSSL = IsEnableSSL;
        }
    }
    #endregion

    #region 自定义媒体播放控件
    public sealed class CustomMediaTransportControls : MediaTransportControls
    {
        public static Button ChangeModeButton;
        public event EventHandler ChangeMode;

        public CustomMediaTransportControls()
        {
            DefaultStyleKey = typeof(CustomMediaTransportControls);
        }

        protected override void OnApplyTemplate()
        {
            ChangeModeButton = GetTemplateChild("ChangeModeButton") as Button;
            ChangeModeButton.Click += ChangeModeButton_Click;
            ChangeModeButton.SetValue(StyleProperty, Application.Current.Resources["PlayInOrderButtonStyle"]);
            base.OnApplyTemplate();
        }

        private void ChangeModeButton_Click(object sender, RoutedEventArgs e)
        {
            ChangeMode?.Invoke(null, null);
        }
    }

    #endregion

    #region 空摄像头设备显示类
    /// <summary>
    /// 提供下拉框的“无”选项
    /// </summary>
    public sealed class EmptyCameraDevice
    {
        public string DisplayName { get; private set; } = "无";
    }
    #endregion

    #region Toast通知
    public sealed class PopToast
    {
        public static ToastContent GenerateToastContent()
        {
            return new ToastContent()
            {
                Launch = "Restart",
                Scenario = ToastScenario.Alarm,

                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                        new AdaptiveText()
                        {
                            Text = "SmartLens需要重新打开App"
                        },

                        new AdaptiveText()
                        {
                            Text = "初始化已完成"
                        },

                        new AdaptiveText()
                        {
                            Text = "请点击以立即重新打开Smartlens"
                        }
                        }
                    }
                },

                Actions = new ToastActionsCustom
                {
                    Buttons =
                    {
                        new ToastButton("立即启动","Restart")
                        {
                            ActivationType =ToastActivationType.Foreground
                        },
                        new ToastButtonDismiss("稍后")
                    }
                }
            };
        }
    }

    #endregion

    #region MD5哈希值计算和检验工具类
    /// <summary>
    /// 计算或验证哈希值
    /// </summary>
    public sealed class MD5Util
    {
        /// <summary>
        /// 异步计算SmartLens所有文件哈希值并保存至数据库中
        /// </summary>
        /// <returns>无</returns>
        public static async Task CalculateAndStorageMD5Async()
        {
            var InstallFolder = Package.Current.InstalledLocation;
            List<KeyValuePair<string, string>> CalculateResult = new List<KeyValuePair<string, string>>();
            await CalculateMD5Async(InstallFolder, CalculateResult);
            await SQLite.GetInstance().SetMD5ValueAsync(CalculateResult);
        }

        /// <summary>
        /// 异步检查SmartLens文件完整性
        /// </summary>
        /// <returns>键值对</returns>
        public static async Task<KeyValuePair<bool, string>> CheckSmartLensIntegrityAsync()
        {
            var InstallFolder = Package.Current.InstalledLocation;
            List<KeyValuePair<string, string>> CalculateResult = new List<KeyValuePair<string, string>>();
            await CalculateMD5Async(InstallFolder, CalculateResult);
            var DataBaseResult = await SQLite.GetInstance().GetMD5ValueAsync();

            foreach (var ErrorPart in from item in DataBaseResult
                                      from item1 in CalculateResult
                                      where item.Key == item1.Key
                                      where item.Value != item1.Value
                                      select item.Key)
            {
                return new KeyValuePair<bool, string>(false, ErrorPart);
            }

            return new KeyValuePair<bool, string>(true, null);
        }

        /// <summary>
        /// 异步递归计算指定文件夹的所有文件的MD5值
        /// </summary>
        /// <param name="Folder">要计算的文件夹</param>
        /// <param name="MD5List">计算结果</param>
        /// <returns>无</returns>
        private static async Task CalculateMD5Async(StorageFolder Folder, List<KeyValuePair<string, string>> MD5List)
        {
            var FileList = await Folder.GetFilesAsync();
            using (MD5 md5 = new MD5CryptoServiceProvider())
            {
                foreach (var file in FileList)
                {
                    if (file.Name == "SmartLens.exe")
                    {
                        continue;
                    }
                    using (Stream stream = await file.OpenStreamForReadAsync())
                    {
                        byte[] Val = md5.ComputeHash(stream);
                        StringBuilder sb = new StringBuilder();
                        for (int i = 0; i < Val.Length; i++)
                        {
                            sb.Append(Val[i].ToString("x2"));
                        }
                        MD5List.Add(new KeyValuePair<string, string>(file.Name, sb.ToString()));
                    }
                }
            }

            var FolderList = await Folder.GetFoldersAsync();
            if (FolderList.Count != 0)
            {
                foreach (var folder in FolderList)
                {
                    await CalculateMD5Async(folder, MD5List);
                }
            }
        }
    }
    #endregion
}
