using SmartLens.NetEase;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;


namespace SmartLens
{

    public sealed partial class MusicSinger : Page
    {
        ObservableCollection<SearchSingleMusic> HotSongCollection;
        ObservableCollection<SingerAlbum> AlbumCollection;
        public ObservableCollection<SingerMV> MVCollection;
        CancellationTokenSource CancelToken = null;
        AutoResetEvent Locker = null;
        ArtistResult Artist;
        bool IsSame = false;

        public MusicSinger()
        {
            InitializeComponent();
            Loaded += MusicSearch_SingerPage_Loaded;
        }

        private async void MusicSearch_SingerPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsSame)
            {
                IsSame = false;
                Locker.Set();
                return;
            }

            if (PivotControl.SelectedIndex != 0)
            {
                PivotControl.SelectedIndex = 0;
            }

            HotSongCollection = new ObservableCollection<SearchSingleMusic>();
            AlbumCollection = new ObservableCollection<SingerAlbum>();
            MVCollection = new ObservableCollection<SingerMV>();

            HotSongList.ItemsSource = HotSongCollection;
            GridViewControl.ItemsSource = AlbumCollection;
            MVGridView.ItemsSource = MVCollection;

            foreach (var item in Artist.HotSongs)
            {
                TimeSpan Duration = TimeSpan.FromMilliseconds(item.Dt);
                if (item.Ar.Count == 1)
                {
                    HotSongCollection.Add(new SearchSingleMusic(item.Name, item.Ar[0].Name, item.Al.Name, string.Format("{0:D2}:{1:D2}", Duration.Minutes, Duration.Seconds), item.Id, item.Al.Pic.ToString(), item.Mv));
                }
                else if (item.Ar.Count > 1)
                {
                    string CombineName = string.Empty;
                    foreach (var names in item.Ar)
                    {
                        CombineName = CombineName + names.Name + "/";
                    }
                    CombineName = CombineName.Remove(CombineName.Length - 1);
                    HotSongCollection.Add(new SearchSingleMusic(item.Name, CombineName, item.Al.Name, string.Format("{0:D2}:{1:D2}", Duration.Minutes, Duration.Seconds), item.Id, item.Al.Pic.ToString(), item.Mv));
                }
                else
                {
                    HotSongCollection.Add(new SearchSingleMusic(item.Name, "Unknown", item.Al.Name, string.Format("{0:D2}:{1:D2}", Duration.Minutes, Duration.Seconds), item.Id, item.Al.Pic.ToString(), item.Mv));
                }
            }

            AlbumSearchResult Result = await NeteaseMusicAPI.GetInstance().Search<AlbumSearchResult>(Artist.Artist.Name, 30, 0, NeteaseMusicAPI.SearchType.Album);
            if (Result.result.albums != null)
            {
                foreach (var item in Result.result.albums)
                {
                    AlbumCollection.Add(new SingerAlbum(item.name, item.id, new Uri(item.picUrl)));
                }
            }

            foreach (var item in HotSongCollection)
            {
                if (CancelToken.IsCancellationRequested)
                {
                    break;
                }
                if (item.MVExists)
                {
                    var MVResult = await NeteaseMusicAPI.GetInstance().MV((int)item.MVid);
                    MVCollection.Add(new SingerMV(MVResult.Data.Name, MVResult.Data.BriefDesc, (int)item.MVid, new Uri(MVResult.Data.Cover)));
                }
            }
            Locker.Set();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (IsSame)
            {
                try
                {
                    ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("AlbumBackAnimation");
                    if (animation != null)
                    {
                        animation.Configuration = new DirectConnectedAnimationConfiguration();
                        Image image = ((GridViewControl.ContainerFromIndex(GridViewControl.SelectedIndex) as GridViewItem).ContentTemplateRoot as FrameworkElement).FindName("AlbumImage") as Image;
                        animation.TryStart(image);
                    }
                }
                catch (Exception) { }
            }

            SingerImage.ImageOpened += SingerImage_ImageOpened;

            if (e.Parameter is ArtistResult Artist)
            {
                SingerImage.Opacity = 0;

                var bitmapimage = new BitmapImage();
                SingerImage.Source = bitmapimage;
                bitmapimage.UriSource = new Uri(Artist.Artist.Img1V1Url);

                CancelToken = new CancellationTokenSource();
                Locker = new AutoResetEvent(false);
                if (IsSame)
                {
                    SingerImage.Opacity = 1;
                    return;
                }
                this.Artist = Artist;
                SingerName.Text = Artist.Artist.Name;
                SingerIntroName.Text = Artist.Artist.Name + "简介:";
                SingerIntroText.Text = "        " + (Artist.Artist.BriefDesc ?? "无简介");
                if (Artist.Artist.Alias.Count != 0)
                {
                    GroupName.Text = Artist.Artist.Alias[0];
                }
                else
                {
                    GroupName.Text = "";
                }
                SongCount.Text = "单曲数: " + Artist.Artist.MusicSize.ToString();
                MVCount.Text = "MV数: " + Artist.Artist.MvSize.ToString();
                AlbumCount.Text = "专辑数: " + Artist.Artist.AlbumSize.ToString();
            }
        }

        private void SingerImage_ImageOpened(object sender, RoutedEventArgs e)
        {
            try
            {
                ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("ToSingerAnimation");

                animation?.TryStart(SingerImage, new UIElement[] { SingerInfo });
            }
            finally
            {
                SingerImage.Opacity = 1;
            }
        }

        protected async override void OnNavigatedFrom(NavigationEventArgs e)
        {
            SingerImage.ImageOpened -= SingerImage_ImageOpened;

            CancelToken.Cancel();

            if (e.NavigationMode == NavigationMode.Back)
            {
                ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("BackAnimation", SingerImage);
            }

            await Task.Run(() =>
            {
                Locker.WaitOne();
            });

            CancelToken.Dispose();
            Locker.Dispose();
            if (e.SourcePageType.Name != "MusicDetail" && e.SourcePageType.Name != "MusicMV" && e.SourcePageType.Name != "MusicAlbum")
            {
                HotSongCollection.Clear();
                AlbumCollection.Clear();
                MVCollection.Clear();
                HotSongCollection = null;
                AlbumCollection = null;
                MVCollection = null;
                IsSame = false;
            }
            else
            {
                IsSame = true;
            }
        }

        private async void PlayAll_Click(object sender, RoutedEventArgs e)
        {
            bool ExistCannotPlay = false;
            if (MediaPlayList.SingerHotSongList.Items.Count != 0)
            {
                MediaPlayList.HotSongBackup.Clear();
                MediaPlayList.SingerHotSongList.Items.Clear();
            }

            MusicPage.ThisPage.MediaControl.AutoPlay = true;
            MusicPage.ThisPage.MediaControl.MediaPlayer.Source = MediaPlayList.SingerHotSongList;

            foreach (SearchSingleMusic item in HotSongCollection)
            {
                MediaPlayList.HotSongBackup.Add(new SearchSingleMusic(item.Music, item.Artist, item.Album, item.Duration, item.SongID[0], item.ImageUrl, item.MVid));

                string uri = (await NeteaseMusicAPI.GetInstance().GetSongsUrl(item.SongID)).Data[0].Url;
                if (uri == null)
                {
                    ExistCannotPlay = true;
                    continue;
                }
                MediaBinder binder = new MediaBinder
                {
                    Token = uri
                };
                binder.Binding += Binder_Binding;
                MediaPlaybackItem Item = new MediaPlaybackItem(MediaSource.CreateFromMediaBinder(binder));

                MediaItemDisplayProperties Props = Item.GetDisplayProperties();
                Props.Type = Windows.Media.MediaPlaybackType.Music;
                Props.MusicProperties.Title = item.Music;
                Props.MusicProperties.Artist = item.Album;
                Item.ApplyDisplayProperties(Props);

                MediaPlayList.SingerHotSongList.Items.Add(Item);

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

        private async void FontIcon_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            FontIcon FI = sender as FontIcon;
            HotSongList.SelectedItem = ((FontIcon)sender).DataContext;
            SearchSingleMusic SSM = HotSongCollection[HotSongList.SelectedIndex];
            if (((SolidColorBrush)FI.Foreground).Color == Colors.White)
            {
                string MusicURL = (await NeteaseMusicAPI.GetInstance().GetSongsUrl(HotSongCollection[HotSongList.SelectedIndex].SongID)).Data[0].Url;
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

                var song = await NeteaseMusicAPI.GetInstance().Search<SingleMusicSearchResult>(SSM.Music, 5, 0, NeteaseMusicAPI.SearchType.Song);
                string ImgURL = "";
                foreach (var item in song.Result.Songs)
                {
                    if (item.Name == SSM.Music && item.Al.Name == SSM.Album)
                    {
                        ImgURL = item.Al.PicUrl;
                        break;
                    }
                }
                MusicList.ThisPage.FavouriteMusicCollection.Add(new PlayList(SSM.Music, SSM.Artist, SSM.Album, SSM.Duration, ImgURL, SSM.SongID[0], SSM.MVid));
                MediaPlaybackItem Item = new MediaPlaybackItem(MediaSource.CreateFromUri(new Uri(MusicURL)));
                MediaItemDisplayProperties Props = Item.GetDisplayProperties();
                Props.Type = Windows.Media.MediaPlaybackType.Music;
                Props.MusicProperties.Title = SSM.Music;
                Props.MusicProperties.Artist = SSM.Artist;
                Item.ApplyDisplayProperties(Props);
                MediaPlayList.FavouriteSongList.Items.Add(Item);
                await SQLite.GetInstance().SetMusicData(SSM.Music, SSM.Artist, SSM.Album, SSM.Duration, ImgURL, SSM.SongID[0], SSM.MVid);
            }
            else
            {
                FI.Glyph = "\uEB51";
                FI.Foreground = new SolidColorBrush(Colors.White);
                for (int i = 0; i < MusicList.ThisPage.FavouriteMusicCollection.Count; i++)
                {
                    if (MusicList.ThisPage.FavouriteMusicCollection[i].SongID == SSM.SongID[0])
                    {
                        await SQLite.GetInstance().DelMusic(MusicList.ThisPage.FavouriteMusicCollection[i]);
                        MusicList.ThisPage.FavouriteMusicCollection.RemoveAt(i);
                        MediaPlayList.FavouriteSongList.Items.RemoveAt(i);
                        break;
                    }
                }
            }

        }

        private async void SymbolIcon_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            HotSongList.SelectedItem = ((SymbolIcon)sender).DataContext;
            var SongURL = (await NeteaseMusicAPI.GetInstance().GetSongsUrl(HotSongCollection[HotSongList.SelectedIndex].SongID)).Data[0].Url;
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

            var music = HotSongCollection[HotSongList.SelectedIndex];
            var song = await NeteaseMusicAPI.GetInstance().Search<SingleMusicSearchResult>(music.Music, 5, 0, NeteaseMusicAPI.SearchType.Song);
            foreach (var item in song.Result.Songs)
            {
                if (item.Name == music.Music && item.Al.Name == music.Album)
                {
                    var bitmap = new BitmapImage();
                    MusicPage.ThisPage.PicturePlaying.Source = bitmap;
                    bitmap.UriSource = new Uri(item.Al.PicUrl);

                    MusicSearch.ForDetail_ImageURL = item.Al.PicUrl;
                    break;
                }
            }
            MusicSearch.ForDetail_ID = HotSongCollection[HotSongList.SelectedIndex].SongID[0];
            MusicSearch.ForDetail_Name = HotSongCollection[HotSongList.SelectedIndex].Music;
            MusicPage.ThisPage.MediaControl.MediaPlayer.Play();
        }

        private async void MV_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            LoadingControl.IsLoading = true;
            HotSongList.SelectedItem = ((TextBlock)sender).DataContext;
            var Result = await NeteaseMusicAPI.GetInstance().MV((int)HotSongCollection[HotSongList.SelectedIndex].MVid);
            await Task.Delay(500);
            LoadingControl.IsLoading = false;
            MusicPage.ThisPage.MusicNav.Navigate(typeof(MusicMV), Result.Data, new DrillInNavigationTransitionInfo());
        }

        private async void MVGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            LoadingControl.IsLoading = true;
            var Result = await NeteaseMusicAPI.GetInstance().MV((e.ClickedItem as SingerMV).MovieID);
            await Task.Delay(500);
            LoadingControl.IsLoading = false;
            MusicPage.ThisPage.MusicNav.Navigate(typeof(MusicMV), Result.Data, new DrillInNavigationTransitionInfo());
        }

        private async void HotSongList_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            var SongURL = (await NeteaseMusicAPI.GetInstance().GetSongsUrl(HotSongCollection[HotSongList.SelectedIndex].SongID)).Data[0].Url;
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

            var music = HotSongCollection[HotSongList.SelectedIndex];
            var song = await NeteaseMusicAPI.GetInstance().Search<SingleMusicSearchResult>(music.Music, 5, 0, NeteaseMusicAPI.SearchType.Song);
            foreach (var item in song.Result.Songs)
            {
                if (item.Name == music.Music && item.Al.Name == music.Album)
                {
                    var bitmap = new BitmapImage();
                    MusicPage.ThisPage.PicturePlaying.Source = bitmap;
                    bitmap.UriSource = new Uri(item.Al.PicUrl);

                    MusicSearch.ForDetail_ImageURL = item.Al.PicUrl;
                    break;
                }
            }
            MusicSearch.ForDetail_ID = HotSongCollection[HotSongList.SelectedIndex].SongID[0];
            MusicSearch.ForDetail_Name = HotSongCollection[HotSongList.SelectedIndex].Music;
            MusicPage.ThisPage.MediaControl.MediaPlayer.Play();
        }

        private async void GridViewControl_ItemClick(object sender, ItemClickEventArgs e)
        {
            LoadingControl.IsLoading = true;
            var Result = await NeteaseMusicAPI.GetInstance().Album((e.ClickedItem as SingerAlbum).ID);
            LoadingControl.IsLoading = false;
            await Task.Delay(500);


            Image image = ((GridViewControl.ContainerFromItem(e.ClickedItem) as GridViewItem).ContentTemplateRoot as FrameworkElement).FindName("AlbumImage") as Image;
            ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("ToAlbumAnimation", image).Configuration = new BasicConnectedAnimationConfiguration();

            MusicPage.ThisPage.MusicNav.Navigate(typeof(MusicAlbum), Result, new SuppressNavigationTransitionInfo());
        }
    }
}
