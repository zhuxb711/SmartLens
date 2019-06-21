using SmartLens.NetEase;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using System.Linq;

namespace SmartLens
{
    public sealed partial class MusicAlbum : Page
    {
        ObservableCollection<SearchSingleMusic> AlbumCollection;
        Song[] AlbumSong;
        bool IsSame = false;

        public MusicAlbum()
        {
            InitializeComponent();
            Loaded += MusicAlbum_Loaded;
        }

        private void MusicAlbum_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsSame)
            {
                IsSame = false;
                return;
            }

            AlbumCollection = new ObservableCollection<SearchSingleMusic>();
            AlbumList.ItemsSource = AlbumCollection;

            foreach (var item in AlbumSong)
            {
                TimeSpan Duration = TimeSpan.FromMilliseconds(item.Dt);
                if (item.Ar.Count == 1)
                {
                    AlbumCollection.Add(new SearchSingleMusic(item.Name, item.Ar[0].Name, item.Al.Name, string.Format("{0:D2}:{1:D2}", Duration.Minutes, Duration.Seconds), item.Id, item.Al.Pic.ToString(), item.Mv));
                }
                else if (item.Ar.Count > 1)
                {
                    string CombineName = string.Empty;
                    foreach (var names in item.Ar)
                    {
                        CombineName = CombineName + names.Name + "/";
                    }
                    CombineName = CombineName.Remove(CombineName.Length - 1);
                    AlbumCollection.Add(new SearchSingleMusic(item.Name, CombineName, item.Al.Name, string.Format("{0:D2}:{1:D2}", Duration.Minutes, Duration.Seconds), item.Id, item.Al.Pic.ToString(), item.Mv));
                }
                else
                {
                    AlbumCollection.Add(new SearchSingleMusic(item.Name, "Unknown", item.Al.Name, string.Format("{0:D2}:{1:D2}", Duration.Minutes, Duration.Seconds), item.Id, item.Al.Pic.ToString(), item.Mv));
                }
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            AlbumImage.ImageOpened += AlbumImage_ImageOpened;

            AlbumImage.Opacity = 0;

            AlbumResult Album = e.Parameter as AlbumResult;
            AlbumName.Text = Album.Album.Name;
            SingerName.Text = Album.Album.Artist.Name;
            AlbumIntroText.Text = Album.Album.Description == null ? "无简介" : Album.Album.Description.ToString();

            var Bitmapimage = new BitmapImage();
            AlbumImage.Source = Bitmapimage;
            Bitmapimage.UriSource = new Uri(Album.Album.PicUrl);

            AlbumSong = Album.Songs;
            PublishTime.Text = GetTime(Album.Album.PublishTime);

        }

        private void AlbumImage_ImageOpened(object sender, RoutedEventArgs e)
        {
            try
            {
                ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("ToAlbumAnimation");

                animation?.TryStart(AlbumImage, new UIElement[] { Animation });
            }
            finally
            {
                AlbumImage.Opacity = 1;
            }
        }

        /// <summary>
        /// 从TimeStamp中获取发行日期的文字描述
        /// </summary>
        /// <param name="TimeStamp">时间戳</param>
        /// <returns>发行日期的文字描述</returns>
        private string GetTime(long TimeStamp)
        {
            DateTime dtStart = TimeZoneInfo.ConvertTime(new DateTime(1970, 1, 1), TimeZoneInfo.Local);
            TimeSpan ToNow = new TimeSpan(TimeStamp * 10000);
            DateTime targetDt = dtStart.Add(ToNow);
            return "发行日期：" + targetDt.Year + " 年 " + targetDt.Month + " 月 " + targetDt.Day + " 日 ";
        }

        private async void PlayAll_Click(object sender, RoutedEventArgs e)
        {
            bool ExistCannotPlay = false;
            if (MediaPlayList.AlbumSongList.Items.Count != 0)
            {
                MediaPlayList.AlbumSongBackup.Clear();
                MediaPlayList.AlbumSongList.Items.Clear();
            }

            MusicPage.ThisPage.MediaControl.AutoPlay = true;
            MusicPage.ThisPage.MediaControl.MediaPlayer.Source = MediaPlayList.AlbumSongList;

            foreach (SearchSingleMusic item in AlbumCollection)
            {
                MediaPlayList.AlbumSongBackup.Add(new SearchSingleMusic(item.MusicName, item.Artist, item.Album, item.Duration, item.SongID[0], item.ImageUrl, item.MVid));

                string uri = (await NeteaseMusicAPI.GetInstance().GetSongsUrlAsync(item.SongID)).Data[0].Url;
                if (uri == null)
                {
                    ExistCannotPlay = true;
                    continue;
                }

                //采用延迟加载技术，仅当该歌曲处于即将播放状态时才调用Binder_Binding加载资源
                MediaBinder binder = new MediaBinder
                {
                    Token = uri
                };
                binder.Binding += Binder_Binding;
                MediaPlaybackItem Item = new MediaPlaybackItem(MediaSource.CreateFromMediaBinder(binder));

                //与系统的SMTC集成
                MediaItemDisplayProperties Props = Item.GetDisplayProperties();
                Props.Type = Windows.Media.MediaPlaybackType.Music;
                Props.MusicProperties.Title = item.MusicName;
                Props.MusicProperties.Artist = item.Album;
                Item.ApplyDisplayProperties(Props);

                MediaPlayList.AlbumSongList.Items.Add(Item);

            }
            if (ExistCannotPlay)
            {
                ExistCannotPlay = false;
                ContentDialog dialog = new ContentDialog
                {
                    Title = "抱歉",
                    Content = "部分歌曲暂时无法播放，已自动忽略",
                    CloseButtonText = "确定",
                    Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                };
                await dialog.ShowAsync();
            }

        }

        private void Binder_Binding(MediaBinder sender, MediaBindingEventArgs args)
        {
            args.SetUri(new Uri(sender.Token));
            sender.Binding -= Binder_Binding;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            AlbumImage.ImageOpened -= AlbumImage_ImageOpened;

            if (e.NavigationMode == NavigationMode.Back)
            {
                ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("AlbumBackAnimation", AlbumImage);
            }

            if (e.SourcePageType.Name != "MusicDetail" && e.SourcePageType.Name != "MusicMV")
            {
                AlbumCollection.Clear();
                AlbumCollection = null;
                AlbumSong = null;
                IsSame = false;
            }
            else
            {
                IsSame = true;
            }
        }

        private async void FontIcon_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            FontIcon FI = sender as FontIcon;
            AlbumList.SelectedItem = ((FontIcon)sender).DataContext;
            SearchSingleMusic SSM = AlbumCollection[AlbumList.SelectedIndex];
            if (((SolidColorBrush)FI.Foreground).Color == Colors.White)
            {
                string MusicURL = (await NeteaseMusicAPI.GetInstance().GetSongsUrlAsync(AlbumCollection[AlbumList.SelectedIndex].SongID)).Data[0].Url;
                if (MusicURL == null)
                {
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "抱歉",
                        Content = "当前歌曲暂时无法播放",
                        CloseButtonText = "确定",
                        Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                    };
                    await dialog.ShowAsync();
                    return;
                }
                FI.Glyph = "\uEB52";
                FI.Foreground = new SolidColorBrush(Colors.Red);

                var song = await NeteaseMusicAPI.GetInstance().SearchAsync<SingleMusicSearchResult>(SSM.MusicName, 5, 0, NeteaseMusicAPI.SearchType.Song);
                string ImgURL = "";

                foreach (var Song in song.Result.Songs.Where(Song => Song.Name == SSM.MusicName && Song.Al.Name == SSM.Album).Select(Song => Song))
                {
                    ImgURL = Song.Al.PicUrl;
                    break;
                }

                MusicList.ThisPage.FavouriteMusicCollection.Add(new PlayList(SSM.MusicName, SSM.Artist, SSM.Album, SSM.Duration, ImgURL, SSM.SongID[0], SSM.MVid));
                MediaPlaybackItem Item = new MediaPlaybackItem(MediaSource.CreateFromUri(new Uri(MusicURL)));

                MediaItemDisplayProperties Props = Item.GetDisplayProperties();
                Props.Type = Windows.Media.MediaPlaybackType.Music;
                Props.MusicProperties.Title = SSM.MusicName;
                Props.MusicProperties.Artist = SSM.Artist;
                Item.ApplyDisplayProperties(Props);

                MediaPlayList.FavouriteSongList.Items.Add(Item);
                await SQLite.GetInstance().SetMusicDataAsync(SSM.MusicName, SSM.Artist, SSM.Album, SSM.Duration, ImgURL, SSM.SongID[0], SSM.MVid);
            }
            else
            {
                FI.Glyph = "\uEB51";
                FI.Foreground = new SolidColorBrush(Colors.White);
                for (int i = 0; i < MusicList.ThisPage.FavouriteMusicCollection.Count; i++)
                {
                    if (MusicList.ThisPage.FavouriteMusicCollection[i].SongID == SSM.SongID[0])
                    {
                        await SQLite.GetInstance().DeleteMusicAsync(MusicList.ThisPage.FavouriteMusicCollection[i]);
                        MusicList.ThisPage.FavouriteMusicCollection.RemoveAt(i);
                        MediaPlayList.FavouriteSongList.Items.RemoveAt(i);
                        break;
                    }
                }
            }

        }

        private async void SymbolIcon_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            AlbumList.SelectedItem = ((SymbolIcon)sender).DataContext;
            var SongURL = (await NeteaseMusicAPI.GetInstance().GetSongsUrlAsync(AlbumCollection[AlbumList.SelectedIndex].SongID)).Data[0].Url;
            if (SongURL == null)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "抱歉",
                    Content = "当前歌曲暂时无法播放",
                    CloseButtonText = "确定",
                    Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                };
                await dialog.ShowAsync();
                return;
            }
            MusicPage.ThisPage.MediaControl.MediaPlayer.Source = MediaSource.CreateFromUri(new Uri(SongURL));

            var music = AlbumCollection[AlbumList.SelectedIndex];
            var song = await NeteaseMusicAPI.GetInstance().SearchAsync<SingleMusicSearchResult>(music.MusicName, 5, 0, NeteaseMusicAPI.SearchType.Song);

            foreach (var item in song.Result.Songs.Where(item => item.Name == music.MusicName && item.Al.Name == music.Album).Select(item => item))
            {
                var bitmap = new BitmapImage();
                MusicPage.ThisPage.PicturePlaying.Source = bitmap;
                bitmap.UriSource = new Uri(item.Al.PicUrl);
                MusicSearch.ForDetail_ImageURL = item.Al.PicUrl;
                break;
            }

            MusicSearch.ForDetail_ID = AlbumCollection[AlbumList.SelectedIndex].SongID[0];
            MusicSearch.ForDetail_Name = AlbumCollection[AlbumList.SelectedIndex].MusicName;
            MusicPage.ThisPage.MediaControl.MediaPlayer.Play();
        }

        private async void MV_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            LoadingControl.IsLoading = true;
            AlbumList.SelectedItem = ((TextBlock)sender).DataContext;
            var Result = await NeteaseMusicAPI.GetInstance().GetMVAsync((int)AlbumCollection[AlbumList.SelectedIndex].MVid);
            LoadingControl.IsLoading = false;
            await Task.Delay(500);
            MusicPage.ThisPage.MusicNav.Navigate(typeof(MusicMV), Result.Data, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
        }
    }
}
