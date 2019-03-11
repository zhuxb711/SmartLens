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
        public string ID
        {
            get
            {
                return AvailableWiFi.Bssid;
            }
        }
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
        public bool IsConnected { get; private set; }
        public string Password { get; set; } = "";
        public Visibility MessageVisibility { get; set; } = Visibility.Collapsed;
        public bool AutoConnect { get; set; } = true;
        public string Message { get; set; }
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
        public byte SignalBar
        {
            get
            {
                return AvailableWiFi.SignalBars;
            }
        }
        public bool IsUpdated { get; set; } = false;
        public WiFiInfo(WiFiAvailableNetwork e, bool IsConnected = false)
        {
            AvailableWiFi = e;
            this.IsConnected = IsConnected;
        }
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
        public void ShowMessage(string msg)
        {
            Message = msg;
            MessageVisibility = Visibility.Visible;
            OnPropertyChanged("Message");
            OnPropertyChanged("MessageVisibility");
        }
        public void HideMessage()
        {
            Message = "";
            MessageVisibility = Visibility.Collapsed;
            OnPropertyChanged("Message");
            OnPropertyChanged("MessageVisibility");
        }
        public async void ChangeConnectState(bool TureOrFalse, WiFiInfo info = null)
        {
            IsConnected = TureOrFalse;
            await SettingsPage.ThisPage.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (info != null)
                {
                    SettingsPage.ThisPage.WiFiList.Move(SettingsPage.ThisPage.WiFiList.IndexOf(info), 0);
                    SettingsPage.ThisPage.WiFiControl.ScrollIntoView(info);
                }
                OnPropertyChanged("Encryption");
            });
        }
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
    public sealed class WiFiInDataBase
    {
        public string SSID { get; private set; }
        public string Password { get; set; }
        private readonly string autoconnect;
        public bool AutoConnect
        {
            get
            {
                if (autoconnect == "True")
                    return true;
                else return false;
            }
        }
        public WiFiInDataBase(string SSID, string Password, string AutoConnect)
        {
            this.SSID = SSID;
            this.Password = Password;
            autoconnect = AutoConnect;
        }
    }
    #endregion

    #region 联网音乐搜索提供类
    public sealed class NeteaseMusicAPI
    {
        private string _MODULUS = "00e0b509f6259df8642dbc35662901477df22677ec152b5ff68ace615bb7b725152b3ab17a876aea8a5aa76d2e417629ec4ee341f56135fccf695280104e0312ecbda92557c93870114af6c9d05c4f7f0c3685b7a46bee255932575cce10b424d813cfe4875d3e82047b97ddef52741d546b8e289dc6935b3ece0462db0a22b8e7";
        private string _NONCE = "0CoJUm6Qyw8W8jud";
        private string _PUBKEY = "010001";
        private string _VI = "0102030405060708";
        private string _USERAGENT = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/63.0.3239.132 Safari/537.36";
        private string _COOKIE = "os=pc;osver=Microsoft-Windows-10-Professional-build-16299.125-64bit;appver=2.0.3.131777;channel=netease;__remember_me=true";
        private string _REFERER = "http://music.163.com/";
        private string _secretKey;
        private string _encSecKey;
        private static NeteaseMusicAPI Netease = null;

        private NeteaseMusicAPI()
        {
            _secretKey = CreateSecretKey(16);
            _encSecKey = RSAEncode(_secretKey);
        }

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

        public Task<T> Search<T>(string keyword, int limit = 30, int offset = 0, SearchType type = SearchType.Song) where T:class
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


        public Task<ArtistResult> Artist(long artist_id)
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

        public Task<AlbumResult> Album(long album_id)
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

        public Task<DetailResult> Detail(long song_id)
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

        public Task<SongUrls> GetSongsUrl(long[] song_id, long bitrate = 999000)
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

        public Task<LyricResult> Lyric(long song_id)
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

        public Task<MVResult> MV(int mv_id)
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

    #region 单曲搜索类
    public sealed class SearchSingleMusic
    {
        public string Music { get; private set; }
        public string Artist { get; private set; }
        public string Album { get; private set; }
        public string Duration { get; private set; }
        public SolidColorBrush Col { get; private set; } = new SolidColorBrush(Colors.White);
        public string Glyph { get; private set; } = "\uEB51";
        public string ImageUrl { get; private set; }
        public long[] SongID { get; private set; }
        public long MVid { get; private set; }
        public bool MVExists { get; private set; } = false;
        public SearchSingleMusic(string Music, string Artist, string Album, string Duration, long SongID, string Url, long MVid)
        {
            this.Music = Music;
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
            if (MusicList.ThisPage.MusicSongIdDictionary.Contains(SongID))
            {
                Glyph = "\uEB52";
                Col = new SolidColorBrush(Colors.Red);
            }
        }
    }
    #endregion

    #region 歌手搜索类
    public sealed class SearchSinger
    {
        public string Singer { get; private set; }
        public Uri ImageUri { get; private set; }
        public string ID { get; private set; }
        public SearchSinger(string Singer, Uri ImageUri, string ID)
        {
            this.Singer = Singer;
            this.ImageUri = ImageUri;
            this.ID = ID;
        }
    }
    #endregion

    #region 专辑搜索类
    public sealed class SearchAlbum
    {
        public Uri ImageUri { get;private set; }

        public string Name { get;private set; }

        public string Artists { get;private set; }

        public long ID { get; private set; }

        public SearchAlbum(Uri ImageUri, string Name, string Artists,long ID)
        {
            this.ImageUri = ImageUri;
            this.Name = Name;
            this.Artists = Artists;
            this.ID = ID;
        }
    }


    public sealed class SingerAlbum
    {
        public Uri AlbumCover { get; private set; }

        public string Name { get; private set; }

        public long ID { get; private set; }

        public SingerAlbum(string Name,long ID, Uri CoverUri)
        {
            this.Name = Name;
            this.ID = ID;
            AlbumCover = CoverUri;
        }
    }

    #endregion

    #region MV提供类
    public sealed class SingerMV
    {
        public Uri MVCover { get; private set; }

        public string Name { get; private set; }

        public string Introduction { get; private set; }

        public int MovieID { get; private set; }

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
    public sealed class MediaPlayList
    {
        public static MediaPlaybackList FavouriteSongList = new MediaPlaybackList()
        {
            MaxPlayedItemsToKeepOpen = 3
        };

        public static MediaPlaybackList SingerHotSongList = new MediaPlaybackList()
        {
            MaxPlayedItemsToKeepOpen = 3
        };

        public static MediaPlaybackList AlbumSongList = new MediaPlaybackList()
        {
            MaxPlayedItemsToKeepOpen = 3
        };

        public static List<SearchSingleMusic> HotSongBackup = new List<SearchSingleMusic>();

        public static List<SearchSingleMusic> AlbumSongBackup = new List<SearchSingleMusic>();

    }
    #endregion

    #region 音乐列表信息类
    public sealed class PlayList : INotifyPropertyChanged
    {
        public string Music { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public string Duration { get; set; }
        public string ImageUrl { get; set; }
        public long SongID { get; set; }
        public long MVid { get; private set; }
        public bool MVExists { get; private set; } = false;
        private SolidColorBrush fontcolor;
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

        public PlayList(string Music, string Artist, string Album, string Duration, string ImageUrl, long SongID,long MVid, bool LoadAsGray = false)
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

    #region SQL数据库类
    public sealed class SQLite : IDisposable
    {
        private SqliteConnection OLEDB = new SqliteConnection("Filename=SmartLens_SQLite.db");
        private bool IsDisposed = false;
        private static SQLite SQL = null;
        private static object Lock;
        private SQLite()
        {
            OLEDB.Open();
            string Command = "Create Table If Not Exists MusicList (MusicName Text Not Null, Artist Text Not Null, Album Text Not Null, Duration Text Not Null, ImageURL Text Not Null, SongID Int Not Null ,MVid Int Not Null, Primary Key (MusicName,Artist,Album,Duration));Create Table If Not Exists WiFiRecord (SSID Text Not Null, Password Text Not Null, AutoConnect Text Not Null, Primary Key (SSID,Password,AutoConnect))";
            SqliteCommand CreateTable = new SqliteCommand(Command, OLEDB);
            CreateTable.ExecuteNonQuery();
        }

        public static SQLite GetInstance()
        {
            if(Lock==null)
            {
                Lock = new object();
            }
            lock (Lock)
            {
                if (SQL == null)
                {
                    return SQL = new SQLite();
                }
                else
                {
                    return SQL;
                }
            }
        }

        public async void Drop()
        {
            SqliteCommand Command = new SqliteCommand("Drop Table MusicList;Drop Table WiFiRecord", OLEDB);
            await Command.ExecuteNonQueryAsync();
        }

        #region WiFi信息的SQL处理部分
        public async Task<List<WiFiInDataBase>> GetAllWiFiData()
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

        public async Task UpdateWiFiData(string SSID, bool AutoConnect)
        {
            SqliteCommand Command = new SqliteCommand("Update WiFiRecord Set AutoConnect = @AutoConnect Where SSID = @SSID", OLEDB);
            Command.Parameters.AddWithValue("@SSID", SSID);
            Command.Parameters.AddWithValue("@AutoConnect", AutoConnect ? "True" : "False");

            await Command.ExecuteNonQueryAsync();
        }

        public async Task UpdateWiFiData(string SSID, string Password)
        {
            SqliteCommand Command = new SqliteCommand("Update WiFiRecord Set Password = @Password Where SSID = @SSID", OLEDB);
            Command.Parameters.AddWithValue("@SSID", SSID);
            Command.Parameters.AddWithValue("@Password", Password);

            await Command.ExecuteNonQueryAsync();
        }

        public async Task SetWiFiData(string SSID, string Password, bool AutoConnect)
        {
            SqliteCommand Command = new SqliteCommand("Insert Into WiFiRecord Values (@SSID , @Password , @AutoConnect)", OLEDB);
            Command.Parameters.AddWithValue("@SSID", SSID);
            Command.Parameters.AddWithValue("@Password", Password);
            Command.Parameters.AddWithValue("@AutoConnect", AutoConnect ? "True" : "False");

            await Command.ExecuteNonQueryAsync();
        }
        #endregion

        #region Dispose资源释放部分
        public void Dispose()
        {
            if (!IsDisposed)
            {
                OLEDB.Dispose();
            }
            SQL = null;
            Lock = null;
            IsDisposed = true;
        }

        ~SQLite()
        {
            Dispose();
        }
        #endregion


        #region Music的SQL处理部分
        public async Task<string[]> GetAllMusicName()
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

        public async Task GetMusicData()
        {
            SqliteCommand Command = new SqliteCommand("Select * From MusicList", OLEDB);
            SqliteDataReader query = await Command.ExecuteReaderAsync();
            while (query.Read())
            {
                MusicList.ThisPage.MusicInfo.Add(new PlayList(query[0].ToString(), query[1].ToString(), query[2].ToString(), query[3].ToString(), query[4].ToString(), (long)query[5], (long)query[6], true));
            }
            query.Close();

            SqliteDataReader query1 = await Command.ExecuteReaderAsync();
            int Index = 0;
            while (query1.Read())
            {
                long[] temp = new long[1];
                temp[0] = (long)query1[5];
                MediaPlaybackItem Item = new MediaPlaybackItem(MediaSource.CreateFromUri(new Uri((await NeteaseMusicAPI.GetInstance().GetSongsUrl(temp)).Data[0].Url)));
                MediaPlayList.FavouriteSongList.Items.Add(Item);

                MediaItemDisplayProperties Props = Item.GetDisplayProperties();
                Props.Type = Windows.Media.MediaPlaybackType.Music;
                Props.MusicProperties.Title = query1[0].ToString();
                Props.MusicProperties.Artist = query1[2].ToString();
                Item.ApplyDisplayProperties(Props);
                MusicList.ThisPage.MusicInfo[Index++].FontColor = new SolidColorBrush(Colors.White);
            }
            if (MusicList.ThisPage.MusicInfo.Count != 0)
            {
                var bitmap= new BitmapImage();
                MusicList.ThisPage.Image1.Source = bitmap;
                bitmap.UriSource = new Uri(MusicList.ThisPage.MusicInfo[0].ImageUrl);
            }
        }

        public async Task SetMusicData(string MusicName, string Artist, string Album, string Duration, string ImageURL, long SongID, long MVid)
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

        public async Task DelMusic(PlayList list)
        {
            SqliteCommand Command = new SqliteCommand("Delete From MusicList Where MusicName=@MusicName And Artist=@Artist And Album=@Album And Duration=@Duration", OLEDB);
            Command.Parameters.AddWithValue("@MusicName", list.Music);
            Command.Parameters.AddWithValue("@Artist", list.Artist);
            Command.Parameters.AddWithValue("@Album", list.Album);
            Command.Parameters.AddWithValue("@Duration", list.Duration);
            await Command.ExecuteNonQueryAsync();
        }
        #endregion
    }
    #endregion

    #region 美妆品牌展示类
    public sealed class CosmeticsItem
    {
        public Uri ImageUri { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public Color LipColor { get; private set; }
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
    public sealed class CameraProvider
    {
        private static CameraHelper CamHelper = null;
        private static readonly object DisposeLocker = new object();
        private static readonly object CreateLocker = new object();
        private CameraProvider() { }

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

        public static CameraHelper GetCameraHelperInstance()
        {
            lock (CreateLocker)
            {
                return CamHelper = CamHelper ?? new CameraHelper();
            }
        }

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
    public sealed class BluetoothList : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public DeviceInformation DeviceInfo { get; set; }
        public void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
        public string Name
        {
            get
            {
                return DeviceInfo.Name;
            }
        }
        public string Id
        {
            get
            {
                return DeviceInfo.Id;
            }
        }
        public string IsPaired
        {
            get
            {
                if (DeviceInfo.Pairing.IsPaired)
                    return "已配对";
                else return "准备配对";
            }
        }
        public string CancelOrPairButton
        {
            get
            {
                if (DeviceInfo.Pairing.IsPaired)
                    return "取消配对";
                else return "配对";
            }
        }

        public void Update(DeviceInformationUpdate DeviceInfoUpdate)
        {
            DeviceInfo.Update(DeviceInfoUpdate);
            OnPropertyChanged("IsPaired");
            OnPropertyChanged("Name");
        }

        public BluetoothList(DeviceInformation DeviceInfo)
        {
            this.DeviceInfo = DeviceInfo;
        }
    }
    #endregion

    #region 蓝牙Obex协议对象类
    public sealed class Obex
    {
        public static ObexService ObexClient = null;
    }
    #endregion

    #region 可移动设备StorageFile类
    public sealed class RemovableDeviceFile : INotifyPropertyChanged
    {
        public string Size { get; private set; }
        public event PropertyChangedEventHandler PropertyChanged;
        public StorageFile File { get; private set; }
        public BitmapImage Thumbnail { get; private set; }

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

        public void FileUpdateRequested(StorageFile File,string FileSize)
        {
            this.File = File;
            OnPropertyChanged("DisplayName");
            Size = FileSize;
            OnPropertyChanged("Size");
        }

        public void NameUpdateRequested()
        {
            OnPropertyChanged("DisplayName");
        }

        public void SizeUpdateRequested(string Size)
        {
            this.Size = Size;
            OnPropertyChanged("Size");
        }

        public string DisplayName
        {
            get
            {
                return File.DisplayName;
            }
        }

        public string Name
        {
            get
            {
                return File.Name;
            }
        }

        public string Type
        {
            get
            {
                return File.DisplayType;
            }
        }

    }
    #endregion

    #region Zip文件查看器显示类
    public sealed class ZipFileDisplay
    {
        public string Name { get; private set; }
        public string CompresionSize { get; private set; }
        public string ActualSize { get; private set; }
        public string Time { get; private set; }
        public string Type { get; private set; }
        public string IsCrypted { get; private set; }

        public ZipFileDisplay(string Name,string Type, string CompresionSize, string ActualSize,string Time,bool IsCrypted)
        {
            this.CompresionSize = CompresionSize;
            this.Name = Name;
            this.Time = Time;
            this.Type = Type;
            this.ActualSize = ActualSize;
            if(IsCrypted)
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
    public enum KeySize
    {
        None=0,
        AES128=128,
        AES256=256
    }

    public enum CompressionLevel
    {
        Max=9,
        AboveStandard=7,
        Standard=5,
        BelowStandard=3,
        PackOnly=1
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
            if(IsEnable)
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
    public sealed class AESProvider
    {
        public const string Admin256Key = "12345678876543211234567887654321";
        public const string Admin128Key = "1234567887654321";
        private static readonly byte[] AdminIV = Encoding.UTF8.GetBytes("r7BXXKkLb8qrSNn0");
        public static byte[] Encrypt(byte[] ToEncrypt, string key, int KeySize)
        {

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

        public static byte[] Decrypt(byte[] ToDecrypt, string key, int KeySize)
        {
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

        public static byte[] EncryptForUSB(byte[] ToEncrypt, string key, int KeySize)
        {

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

        public static byte[] DecryptForUSB(byte[] ToDecrypt, string key, int KeySize)
        {
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

            EventHandler<object> layoutUpdatedHandler = null;
            layoutUpdatedHandler = delegate
            {
                listViewBase.LayoutUpdated -= layoutUpdatedHandler;

                // 获取目标位置。
                double targetHorizontalOffset = scrollViewer.HorizontalOffset;
                double targetVerticalOffset = scrollViewer.VerticalOffset;

                EventHandler<ScrollViewerViewChangedEventArgs> scrollHandler = null;
                scrollHandler = delegate
                {
                    scrollViewer.ViewChanged -= scrollHandler;

                    // 最终目的，带平滑滚动效果滚动到 item。
                    scrollViewer.ChangeView(targetHorizontalOffset, targetVerticalOffset, null);
                };
                scrollViewer.ViewChanged += scrollHandler;

                // 复原位置，且不需要使用动画效果。
                scrollViewer.ChangeView(originHorizontalOffset, originVerticalOffset, null, true);

            };
            listViewBase.LayoutUpdated += layoutUpdatedHandler;

            listViewBase.ScrollIntoView(item, alignment);
        }
    }
    #endregion

    #region 主题切换器
    public sealed class ThemeSwitcher
    { 
        public static bool IsLightEnabled
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["ThemeValue"] is string Theme)
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
                    ApplicationData.Current.LocalSettings.Values["ThemeValue"] = "Dark";
                    return false;
                }
            }
            set
            {
                if (value)
                {
                    ApplicationData.Current.LocalSettings.Values["ThemeValue"] = "Dark";
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["ThemeValue"] = "Light";
                }
            }
        }
    }
    #endregion

    #region 天气数据到达事件信息传递类
    public sealed class WeatherData
    {
        public Weather.Data Data { get; private set; }
        public string Location { get; private set; }
        public WeatherData(Weather.Data Data, string Location)
        {
            this.Data = Data;
            this.Location = Location;
        }
    }
    #endregion

    #region 天气数据获取错误枚举
    public enum ErrorReason
    {
        Location = 0,
        NetWork = 1
    }
    #endregion

    #region USB图片展示类
    public sealed class PhotoDisplaySupport
    {
        public BitmapImage Bitmap { get; private set; }
        public string FileName
        {
            get
            {
                return PhotoFile.Name;
            }
        }
        public StorageFile PhotoFile { get; private set; }

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
        public bool IsIMAPConnected
        {
            get
            {
                return IMAPClient.IsConnected;
            }
        }
        public string UserName
        {
            get
            {
                return Credential.UserName;
            }
        }
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
        public static EmailProtocolServiceProvider GetInstance()
        {
            lock (SyncRoot)
            {
                return Instance ?? (Instance = new EmailProtocolServiceProvider());
            }
        }

        public void GetStorageData()
        {
            if (ApplicationData.Current.LocalSettings.Values["EmailCredentialName"] is byte[] Name && ApplicationData.Current.LocalSettings.Values["EmailCredentialPassword"] is byte[] Password)
            {
                string DecryptName = Encoding.UTF8.GetString(AESProvider.Decrypt(Name, AESProvider.Admin256Key, 256));
                string DecryptPassword = Encoding.UTF8.GetString(AESProvider.Decrypt(Password, AESProvider.Admin256Key, 256));
                Credential = new NetworkCredential(DecryptName, DecryptPassword);
            }
            if(ApplicationData.Current.LocalSettings.Values["EmailIMAPAddress"] is string IMAPAddress && ApplicationData.Current.LocalSettings.Values["EmailIMAPPort"] is int IMAPPort)
            {
                IMAPServerAddress = new KeyValuePair<string, int>(IMAPAddress, IMAPPort);
            }
            if(ApplicationData.Current.LocalSettings.Values["EmailSMTPAddress"] is string SMTPAddress && ApplicationData.Current.LocalSettings.Values["EmailSMTPPort"] is int SMTPPort)
            {
                SMTPServerAddress = new KeyValuePair<string, int>(SMTPAddress, SMTPPort);
            }
            if(ApplicationData.Current.LocalSettings.Values["EmailEnableSSL"] is bool EnableSSL)
            {
                IsEnableSSL = EnableSSL;
            }
            if (ApplicationData.Current.LocalSettings.Values["EmailCallName"] is string CallName)
            {
                this.CallName = CallName;
            }
        }

        public IMailFolder GetMailFolder()
        {
            return IMAPClient.Inbox;
        }

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
            if(!IMAPClient.IsAuthenticated)
            {
                await IMAPClient.AuthenticateAsync(Credential.UserName, Credential.Password, ConnectionCancellation.Token);
            }

            await task;
        }

        private async Task ConnectSendServiceAsync(CancellationTokenSource ConnectionCancellation)
        {
            if (!SMTPClient.IsConnected)
            {
                await SMTPClient.ConnectAsync(SMTPServerAddress.Key, SMTPServerAddress.Value, IsEnableSSL, ConnectionCancellation.Token);
            }
            if(!SMTPClient.IsAuthenticated)
            { 
                await SMTPClient.AuthenticateAsync(Credential.UserName, Credential.Password, ConnectionCancellation.Token);
            }
            SMTPOprationLock.Set();
        }

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

        public void SetEmailServerAddress(List<KeyValuePair<EmailProtocol, KeyValuePair<string, int>>> EmailAddress, bool IsEnableSSL)
        {
            this.IsEnableSSL = IsEnableSSL;
            ApplicationData.Current.LocalSettings.Values["EmailEnableSSL"] = IsEnableSSL;

            foreach (var Protocol in EmailAddress)
            {
                switch (Protocol.Key)
                {
                    case EmailProtocol.IMAP:
                        {
                            IMAPServerAddress = Protocol.Value;
                            ApplicationData.Current.LocalSettings.Values["EmailIMAPAddress"] = IMAPServerAddress.Key;
                            ApplicationData.Current.LocalSettings.Values["EmailIMAPPort"] = IMAPServerAddress.Value;
                            break;
                        }
                    case EmailProtocol.SMTP:
                        {
                            SMTPServerAddress = Protocol.Value;
                            ApplicationData.Current.LocalSettings.Values["EmailSMTPAddress"] = SMTPServerAddress.Key;
                            ApplicationData.Current.LocalSettings.Values["EmailSMTPPort"] = SMTPServerAddress.Value;
                            break;
                        }
                }
            }

        }

        public Task SetCredential(NetworkCredential Credential,string CallName)
        {
            return Task.Run(() =>
            {
                this.Credential = Credential;
                this.CallName = CallName;
                ApplicationData.Current.LocalSettings.Values["EmailCallName"] = CallName;
                ApplicationData.Current.LocalSettings.Values["EmailCredentialName"] = AESProvider.Encrypt(Encoding.UTF8.GetBytes(Credential.UserName), AESProvider.Admin256Key, 256);
                ApplicationData.Current.LocalSettings.Values["EmailCredentialPassword"] = AESProvider.Encrypt(Encoding.UTF8.GetBytes(Credential.Password), AESProvider.Admin256Key, 256);
            });
        }

        public static bool CheckWhetherInstanceExist()
        {
            return Instance == null ? false : true;
        }

        public void Dispose()
        {
            IMAPClient?.Dispose();
            SMTPClient?.Dispose();

            SMTPOprationLock?.Dispose();
            Instance = null;
        }
    }
    #endregion

    #region Email信息内部传递包
    public sealed class InfomationDeliver
    {
        public string From { get; private set; }
        public string To { get; private set; }
        public EmailSendType SendType { get; private set; }
        public string Title { get; private set; }

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

        public InfomationDeliver(string From,string Title)
        {
            To = null;
            this.From = From;
            this.Title = Title;
            SendType = EmailSendType.Forward;
        }

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
    public enum EmailProtocol
    {
        IMAP = 0,
        SMTP = 1
    }

    public enum EmailSendType
    {
        ReplyToAll = 0,
        Reply = 1,
        NormalSend = 2,
        Forward=3
    }
    #endregion

    #region Email列表展示类
    public sealed class EmailItem : INotifyPropertyChanged
    {
        public MimeMessage Message { get; private set; }
        public IEnumerable<MimeEntity> FileEntitys
        {
            get
            {
                return Message.Attachments;
            }
        }
        public string From
        {
            get
            {
                return Message.From[0].Name == "" ? Message.From[0].ToString() : Message.From[0].Name;
            }
        }
        public string Title
        {
            get
            {
                return Message.Subject;
            }
        }

        public UniqueId Id { get; private set; }

        public string FirstWord
        {
            get
            {
                return From[0].ToString().ToUpper();
            }
        }

        private double Indicator;
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

        public string Date
        {
            get
            {
                return Message.Date.LocalDateTime.Year + "年" + (Message.Date.LocalDateTime.Month < 10 ? "0" + Message.Date.LocalDateTime.Month : Message.Date.LocalDateTime.Month.ToString()) + "月" + (Message.Date.LocalDateTime.Day < 10 ? "0" + Message.Date.LocalDateTime.Day : Message.Date.LocalDateTime.Day.ToString()) + "日";
            }
        }

        public Color Color { get; private set; }

        public void SetSeenIndicator(Visibility visibility)
        {
            if (visibility == Visibility.Visible)
            {
                IsNotSeenIndicator = 1;
                EmailProtocolServiceProvider.GetInstance().GetMailFolder().RemoveFlagsAsync(Id, MessageFlags.Seen, true);
            }
            else
            {
                IsNotSeenIndicator = 0;
                EmailProtocolServiceProvider.GetInstance().GetMailFolder().SetFlagsAsync(Id, MessageFlags.Seen, true);
            }
        }

        public EmailItem(MimeMessage Message, UniqueId Id)
        {
            this.Message = Message;
            this.Id = Id;

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

        public event PropertyChangedEventHandler PropertyChanged;

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
    public sealed class EmailAttachment
    {
        public EmailAttachment(MimeEntity Entity)
        {
            this.Entity = Entity;
        }
        public MimeEntity Entity { get; private set; }
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
    public sealed class SendEmailData
    {
        public List<MailboxAddress> To { get; private set; }
        public string Subject { get; set; }
        public string Text { get; private set; }
        public EmailSendType SendType { get; private set; }
        public List<MimePart> Attachments { get; private set; }

        public SendEmailData(string Text, EmailSendType SendType, List<MimePart> Attachments)
        {
            if (SendType == EmailSendType.NormalSend)
            {
                throw new InvalidEnumArgumentException("if EmailSendType is NormalSend ,Please use another overload");
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

        public SendEmailData(string To)
        {
            string[] temp = To.Split(";");
            List<MailboxAddress> Address = new List<MailboxAddress>(temp.Length);
            foreach (var item in temp)
            {
                Address.Add(new MailboxAddress(item));
            }
            this.To = Address;
            SendType = EmailSendType.Forward;
        }

        public SendEmailData(string To, string Subject, string Text, List<MimePart> Attachments)
        {
            string[] temp = To.Split(";");
            List<MailboxAddress> Address = new List<MailboxAddress>(temp.Length);
            foreach (var item in temp)
            {
                Address.Add(new MailboxAddress(item));
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
                Scenario = ToastScenario.Reminder,

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
                        new ToastButtonDismiss("取消")
                    }
                }
            };
        }
    }

    #endregion
}
