using System;
using System.Collections.ObjectModel;
using Windows.Media.Core;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.Media.Playback;
using SmartLens.NetEase;
using Windows.UI.Xaml.Media.Animation;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace SmartLens
{
    public sealed partial class MusicSearch : Page
    {
        public static long ForDetail_ID { get; set; }
        public static string ForDetail_ImageURL { get; set; }
        public static string ForDetail_Name { get; set; }
        private NeteaseMusicAPI NetEaseMusic = NeteaseMusicAPI.GetInstance();
        public ObservableCollection<SearchSingleMusic> SingleMusicList = new ObservableCollection<SearchSingleMusic>();
        private ObservableCollection<SearchSinger> SingerList = new ObservableCollection<SearchSinger>();
        private ObservableCollection<SearchAlbum> AlbumList = new ObservableCollection<SearchAlbum>();
        Frame MusicNav = null;

        public MusicSearch()
        {
            InitializeComponent();
            SingleMusicControl.ItemsSource = SingleMusicList;
            SingerControl.ItemsSource = SingerList;
            AlbumControl.ItemsSource = AlbumList;
            SearchOrder.SelectedIndex = 0;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            MusicNav = (Frame)e.Parameter;
            switch (PivotControl.SelectedIndex)
            {
                case 1:
                    {
                        try
                        {
                            ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("BackAnimation");
                            if (animation != null)
                            {
                                animation.Configuration = new DirectConnectedAnimationConfiguration();
                                Image image = ((SingerControl.ContainerFromIndex(SingerControl.SelectedIndex) as ListViewItem).ContentTemplateRoot as FrameworkElement).FindName("SingerImage") as Image;
                                animation.TryStart(image);
                            }
                        }
                        catch (Exception) { }
                        break;
                    }
                case 2:
                    {
                        try
                        {
                            ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("AlbumBackAnimation");
                            if (animation != null)
                            {
                                animation.Configuration = new DirectConnectedAnimationConfiguration();
                                Image image = ((AlbumControl.ContainerFromIndex(AlbumControl.SelectedIndex) as ListViewItem).ContentTemplateRoot as FrameworkElement).FindName("AlbumImage") as Image;
                                animation.TryStart(image);
                            }
                        }
                        catch (Exception) { }
                        break;
                    }
            }

        }


        private async void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && sender.Text != "")
            {
                try
                {
                    switch (SearchOrder.SelectedIndex)
                    {
                        case 0: sender.ItemsSource = (await NetEaseMusic.Search<SingleMusicSearchResult>(sender.Text, 10)).Result.Songs; break;
                        case 1: sender.ItemsSource = (await NetEaseMusic.Search<ArtistSearchResult>(sender.Text, 10, 0, NeteaseMusicAPI.SearchType.Artist)).result.artists; break;
                        case 2: sender.ItemsSource = (await NetEaseMusic.Search<AlbumSearchResult>(sender.Text, 10, 0, NeteaseMusicAPI.SearchType.Album)).result.albums; break;
                    }
                }
                catch (Exception)
                {
                }
            }
        }


        private async void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            LoadingControl.IsLoading = true;
            if (args.ChosenSuggestion is Song song)
            {
                await GetSingleMusic(song.Name);
                await GetArtists(song.Name);
                await GetAlbum(song.Name);
            }
            else if (args.ChosenSuggestion is ArtistSearchResult.Artists art)
            {
                await GetSingleMusic(art.name);
                await GetArtists(art.name);
                await GetAlbum(art.name);
            }
            else if (args.ChosenSuggestion is AlbumSearchResult.AlbumsItem bum)
            {
                await GetSingleMusic(bum.name);
                await GetArtists(bum.name);
                await GetAlbum(bum.name);
            }
            else if (args.ChosenSuggestion == null && args.QueryText != "")
            {
                await GetSingleMusic(args.QueryText);
                await GetArtists(args.QueryText);
                await GetAlbum(args.QueryText);
            }
            LoadingControl.IsLoading = false;
        }

        private async Task GetSingleMusic(string song)
        {
            SingleMusicSearchResult Result = await NetEaseMusic.Search<SingleMusicSearchResult>(song);
            if (Result.Result.Songs == null)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "错误",
                    Content = "无法在乐库中搜索到该歌曲，请重试",
                    CloseButtonText = "确定",
                    Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                };
                await dialog.ShowAsync();
                return;
            }
            SingleMusicList.Clear();
            foreach (var item in Result.Result.Songs)
            {
                TimeSpan Duration = TimeSpan.FromMilliseconds(item.Dt);
                if (item.Ar.Count == 1)
                {
                    SingleMusicList.Add(new SearchSingleMusic(item.Name, item.Ar[0].Name, item.Al.Name, string.Format("{0:D2}:{1:D2}", Duration.Minutes, Duration.Seconds), item.Id, item.Al.PicUrl, item.Mv));
                }
                else if (item.Ar.Count > 1)
                {
                    string CombineName = string.Empty;
                    foreach (var names in item.Ar)
                    {
                        CombineName = CombineName + names.Name + "/";
                    }
                    CombineName = CombineName.Remove(CombineName.Length - 1);
                    SingleMusicList.Add(new SearchSingleMusic(item.Name, CombineName, item.Al.Name, string.Format("{0:D2}:{1:D2}", Duration.Minutes, Duration.Seconds), item.Id, item.Al.PicUrl, item.Mv));
                }
                else
                {
                    SingleMusicList.Add(new SearchSingleMusic(item.Name, "Unknown", item.Al.Name, string.Format("{0:D2}:{1:D2}", Duration.Minutes, Duration.Seconds), item.Id, item.Al.PicUrl, item.Mv));
                }
            }


        }

        private async Task GetArtists(string art)
        {
            ArtistSearchResult Result = await NetEaseMusic.Search<ArtistSearchResult>(art, 30, 0, NeteaseMusicAPI.SearchType.Artist);
            if (Result.result.artists == null)
            {
                return;
            }
            SingerList.Clear();
            foreach (var item in Result.result.artists)
            {
                SingerList.Add(new SearchSinger(item.name, new Uri(item.img1v1Url), item.id));
            }
        }

        private async Task GetAlbum(string bum)
        {
            AlbumSearchResult Result = await NetEaseMusic.Search<AlbumSearchResult>(bum, 30, 0, NeteaseMusicAPI.SearchType.Album);
            AlbumList.Clear();
            if (Result.result.albums == null)
            {
                return;
            }
            foreach (var item in Result.result.albums)
            {
                AlbumList.Add(new SearchAlbum(new Uri(item.picUrl), item.name, item.artist.name, item.id));
            }
        }

        private void AutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is Song song)
            {
                sender.Text = song.Name;
            }
            else if (args.SelectedItem is ArtistSearchResult.Artists art)
            {
                sender.Text = art.name;
            }
        }

        private async void FontIcon_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            FontIcon FI = sender as FontIcon;
            SingleMusicControl.SelectedItem = ((FontIcon)sender).DataContext;
            SearchSingleMusic SSM = SingleMusicList[SingleMusicControl.SelectedIndex];
            if (((SolidColorBrush)FI.Foreground).Color == Colors.White)
            {
                string MusicURL = (await NetEaseMusic.GetSongsUrl(SingleMusicList[SingleMusicControl.SelectedIndex].SongID)).Data[0].Url;
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
                MusicList.ThisPage.MusicInfo.Add(new PlayList(SSM.Music, SSM.Artist, SSM.Album, SSM.Duration, SSM.ImageUrl, SSM.SongID[0], SSM.MVid));
                MediaPlaybackItem Item = new MediaPlaybackItem(MediaSource.CreateFromUri(new Uri(MusicURL)));
                MediaItemDisplayProperties Props = Item.GetDisplayProperties();
                Props.Type = Windows.Media.MediaPlaybackType.Music;
                Props.MusicProperties.Title = SSM.Music;
                Props.MusicProperties.Artist = SSM.Artist;
                Item.ApplyDisplayProperties(Props);
                MediaPlayList.FavouriteSongList.Items.Add(Item);
                await SQLite.GetInstance().SetMusicData(SSM.Music, SSM.Artist, SSM.Album, SSM.Duration, SSM.ImageUrl, SSM.SongID[0], SSM.MVid);
            }
            else
            {
                FI.Glyph = "\uEB51";
                FI.Foreground = new SolidColorBrush(Colors.White);
                for (int i = 0; i < MusicList.ThisPage.MusicInfo.Count; i++)
                {
                    if (MusicList.ThisPage.MusicInfo[i].SongID == SSM.SongID[0])
                    {
                        await SQLite.GetInstance().DelMusic(MusicList.ThisPage.MusicInfo[i]);
                        MusicList.ThisPage.MusicInfo.RemoveAt(i);
                        MediaPlayList.FavouriteSongList.Items.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private async void SymbolIcon_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            SingleMusicControl.SelectedItem = ((SymbolIcon)sender).DataContext;
            var SongURL = (await NetEaseMusic.GetSongsUrl(SingleMusicList[SingleMusicControl.SelectedIndex].SongID)).Data[0].Url;
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

            var bitmap = new BitmapImage();
            MusicPage.ThisPage.PicturePlaying.Source = bitmap;
            bitmap.UriSource = new Uri(SingleMusicList[SingleMusicControl.SelectedIndex].ImageUrl);

            ForDetail_ID = SingleMusicList[SingleMusicControl.SelectedIndex].SongID[0];
            ForDetail_ImageURL = SingleMusicList[SingleMusicControl.SelectedIndex].ImageUrl;
            ForDetail_Name = SingleMusicList[SingleMusicControl.SelectedIndex].Music;

            MusicPage.ThisPage.MediaControl.MediaPlayer.Play();
        }

        private async void SingerControl_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is SearchSinger item)
            {
                LoadingControl.IsLoading = true;
                var Result = await NetEaseMusic.Artist(long.Parse(item.ID));
                LoadingControl.IsLoading = false;
                await Task.Delay(500);

                Image image=((SingerControl.ContainerFromItem(e.ClickedItem) as ListViewItem).ContentTemplateRoot as FrameworkElement).FindName("SingerImage") as Image;
                ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("ToSingerAnimation", image).Configuration = new BasicConnectedAnimationConfiguration();
               
                ConnectedAnimationService.GetForCurrentView().DefaultDuration = TimeSpan.FromMilliseconds(400);

                MusicNav.Navigate(typeof(MusicSinger), Result,new DrillInNavigationTransitionInfo());
            }
        }

        private async void MV_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            LoadingControl.IsLoading = true;
            SingleMusicControl.SelectedItem = ((TextBlock)sender).DataContext;
            var Result = await NetEaseMusic.MV((int)SingleMusicList[SingleMusicControl.SelectedIndex].MVid);
            LoadingControl.IsLoading = false;
            await Task.Delay(500);
            MusicPage.ThisPage.MusicNav.Navigate(typeof(MusicMV), Result.Data, new DrillInNavigationTransitionInfo());
        }

        private async void SingleMusicControl_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            var SongURL = (await NetEaseMusic.GetSongsUrl(SingleMusicList[SingleMusicControl.SelectedIndex].SongID)).Data[0].Url;
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

            var bitmap = new BitmapImage();
            MusicPage.ThisPage.PicturePlaying.Source = bitmap;
            bitmap.UriSource = new Uri(SingleMusicList[SingleMusicControl.SelectedIndex].ImageUrl);

            ForDetail_ID = SingleMusicList[SingleMusicControl.SelectedIndex].SongID[0];
            ForDetail_ImageURL = SingleMusicList[SingleMusicControl.SelectedIndex].ImageUrl;
            ForDetail_Name = SingleMusicList[SingleMusicControl.SelectedIndex].Music;

            MusicPage.ThisPage.MediaControl.MediaPlayer.Play();
        }

        private async void AlbumControl_ItemClick(object sender, ItemClickEventArgs e)
        {
            LoadingControl.IsLoading = true;
            var Result = await NetEaseMusic.Album((e.ClickedItem as SearchAlbum).ID);
            LoadingControl.IsLoading = false;
            await Task.Delay(500);

            Image image = ((AlbumControl.ContainerFromItem(e.ClickedItem) as ListViewItem).ContentTemplateRoot as FrameworkElement).FindName("AlbumImage") as Image;
            ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("ToAlbumAnimation", image).Configuration = new BasicConnectedAnimationConfiguration();

            ConnectedAnimationService.GetForCurrentView().DefaultDuration = TimeSpan.FromMilliseconds(500);

            MusicPage.ThisPage.MusicNav.Navigate(typeof(MusicAlbum), Result, new SuppressNavigationTransitionInfo());
        }
    }
}
