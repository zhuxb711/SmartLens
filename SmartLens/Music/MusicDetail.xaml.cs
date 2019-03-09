using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.Media.Playback;
using SmartLens.NetEase;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace SmartLens
{
    public sealed partial class MusicDetail : Page
    {
        private NeteaseMusicAPI NetEaseMusic = NeteaseMusicAPI.GetInstance();
        public DispatcherTimer timer = new DispatcherTimer();
        private DispatcherTimer timer1 = new DispatcherTimer();
        private long LastSongID;

        public MusicDetail()
        {
            InitializeComponent();
            MusicPage.ThisPage.MediaControl.MediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
            MediaPlayList.FavouriteSongList.CurrentItemChanged += MediaList_CurrentItemChanged;
            MediaPlayList.SingerHotSongList.CurrentItemChanged += SingerHotSongList_CurrentItemChanged;
            MediaPlayList.AlbumSongList.CurrentItemChanged += AlbumSongList_CurrentItemChanged;
            timer1.Interval = TimeSpan.FromMilliseconds(25);
            timer1.Tick += Timer1_Tick;
            timer.Interval = TimeSpan.FromMilliseconds(1000);
            timer.Tick += Timer_Tick;
            Loaded += MusicDetail_Loaded;
        }

        private async void AlbumSongList_CurrentItemChanged(MediaPlaybackList sender, CurrentMediaPlaybackItemChangedEventArgs args)
        {
            if (MediaPlayList.AlbumSongList.CurrentItemIndex == 4294967295)
            {
                return;
            }

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                BackBlurBrush.Amount = 0;
                timer1.Start();
                LrcControl.c_lrc_items.Children.Clear();

                if (MusicPage.ThisPage.MediaControl.MediaPlayer.Source == MediaPlayList.AlbumSongList)
                {
                    try
                    {
                        timer.Start();
                    }
                    catch (Exception) { }
                    SearchSingleMusic PL = MediaPlayList.AlbumSongBackup[Convert.ToInt32(MediaPlayList.AlbumSongList.CurrentItemIndex)];
                    Title.Text = PL.Music;

                    var song = await NetEaseMusic.Search<SingleMusicSearchResult>(PL.Music, 5, 0, NeteaseMusicAPI.SearchType.Song);
                    foreach (var item in song.Result.Songs)
                    {
                        if (item.Name == PL.Music && item.Ar[0].Name == PL.Artist && item.Al.Name == PL.Album)
                        {
                            var bitmap = new BitmapImage();
                            Image1.ImageSource = bitmap;
                            bitmap.UriSource = new Uri(item.Al.PicUrl);
                            break;
                        }
                    }

                    var back = new ImageBrush
                    {
                        ImageSource = Image1.ImageSource,
                        Stretch = Stretch.UniformToFill
                    };
                    GridBack.Background = back;
                    LyricResult LrcTemp = await NetEaseMusic.Lyric(PL.SongID[0]);
                    if (LrcTemp.Lrc == null)
                    {
                        TextBlock TB = new TextBlock()
                        {
                            Text = "纯音乐，无歌词",
                            FontSize = 18
                        };
                        LrcControl.c_lrc_items.Children.Add(TB);
                        timer.Stop();
                    }
                    else
                    {
                        LrcControl.LoadLrc(LrcTemp.Lrc.Lyric, LrcTemp.Tlyric.Lyric);
                    }
                }
                else
                {
                    try
                    {
                        timer.Start();
                    }
                    catch (Exception) { }

                    var bitmap = new BitmapImage();
                    Image1.ImageSource = bitmap;
                    bitmap.UriSource = new Uri(MusicSearch.ForDetail_ImageURL);

                    var back = new ImageBrush
                    {
                        ImageSource = Image1.ImageSource,
                        Stretch = Stretch.UniformToFill
                    };
                    GridBack.Background = back;
                    Title.Text = MusicSearch.ForDetail_Name;
                    LyricResult LrcTemp = await NetEaseMusic.Lyric(MusicSearch.ForDetail_ID);
                    if (LrcTemp.Lrc == null)
                    {
                        TextBlock TB = new TextBlock()
                        {
                            Text = "纯音乐，无歌词",
                            FontSize = 18
                        };
                        LrcControl.c_lrc_items.Children.Add(TB);
                        timer.Stop();
                    }
                    else
                    {
                        LrcControl.LoadLrc(LrcTemp.Lrc.Lyric, LrcTemp.Tlyric.Lyric);
                    }
                }
            });

        }

        private async void MusicDetail_Loaded(object sender, RoutedEventArgs e)
        {
            if (MusicPage.ThisPage.MediaControl.MediaPlayer.Source == MediaPlayList.FavouriteSongList)
            {
                PlayList PL = MusicList.ThisPage.MusicInfo[Convert.ToInt32(MediaPlayList.FavouriteSongList.CurrentItemIndex)];

                if (PL.SongID==LastSongID)
                {
                    return;
                }
                else
                {
                    LastSongID = PL.SongID;
                }

                LrcControl.c_lrc_items.Children.Clear();
                Title.Text = PL.Music;

                var bitmap = new BitmapImage();
                Image1.ImageSource = bitmap;
                bitmap.UriSource = new Uri(PL.ImageUrl);

                var back = new ImageBrush
                {
                    ImageSource = Image1.ImageSource,
                    Stretch = Stretch.UniformToFill
                };
                GridBack.Background = back;
                LyricResult LrcTemp = await NetEaseMusic.Lyric(PL.SongID);
                if (LrcTemp.Lrc == null)
                {
                    TextBlock TB = new TextBlock()
                    {
                        Text = "纯音乐，无歌词",
                        FontSize = 18
                    };
                    LrcControl.c_lrc_items.Children.Add(TB);
                    timer.Stop();
                }
                else
                {
                    LrcControl.LoadLrc(LrcTemp.Lrc.Lyric, LrcTemp.Tlyric?.Lyric);
                }
            }
            else if (MusicPage.ThisPage.MediaControl.MediaPlayer.Source == MediaPlayList.SingerHotSongList)
            {
                SearchSingleMusic PL = MediaPlayList.HotSongBackup[Convert.ToInt32(MediaPlayList.SingerHotSongList.CurrentItemIndex)];

                if (PL.SongID[0] == LastSongID)
                {
                    return;
                }
                else
                {
                    LastSongID = PL.SongID[0];
                }

                LrcControl.c_lrc_items.Children.Clear();

                Title.Text = PL.Music;

                var song = await NetEaseMusic.Search<SingleMusicSearchResult>(PL.Music, 5, 0, NeteaseMusicAPI.SearchType.Song);
                foreach (var item in song.Result.Songs)
                {
                    if (item.Name == PL.Music && item.Ar[0].Name == PL.Artist && item.Al.Name == PL.Album)
                    {
                        var bitmap = new BitmapImage();
                        Image1.ImageSource = bitmap;
                        bitmap.UriSource = new Uri(item.Al.PicUrl);
                        break;
                    }
                }

                var back = new ImageBrush
                {
                    ImageSource = Image1.ImageSource,
                    Stretch = Stretch.UniformToFill
                };
                GridBack.Background = back;
                LyricResult LrcTemp = await NetEaseMusic.Lyric(PL.SongID[0]);
                if (LrcTemp.Lrc == null)
                {
                    TextBlock TB = new TextBlock()
                    {
                        Text = "纯音乐，无歌词",
                        FontSize = 18
                    };
                    LrcControl.c_lrc_items.Children.Add(TB);
                    timer.Stop();
                }
                else
                {
                    LrcControl.LoadLrc(LrcTemp.Lrc.Lyric, LrcTemp.Tlyric?.Lyric);
                }

            }
            else if (MusicPage.ThisPage.MediaControl.MediaPlayer.Source == MediaPlayList.AlbumSongList)
            {
                SearchSingleMusic PL = MediaPlayList.AlbumSongBackup[Convert.ToInt32(MediaPlayList.AlbumSongList.CurrentItemIndex)];

                if (PL.SongID[0] == LastSongID)
                {
                    return;
                }
                else
                {
                    LastSongID = PL.SongID[0];
                }

                LrcControl.c_lrc_items.Children.Clear();

                Title.Text = PL.Music;

                var song = await NetEaseMusic.Search<SingleMusicSearchResult>(PL.Music, 5, 0, NeteaseMusicAPI.SearchType.Song);
                foreach (var item in song.Result.Songs)
                {
                    if (item.Name == PL.Music && item.Ar[0].Name == PL.Artist && item.Al.Name == PL.Album)
                    {
                        var bitmap = new BitmapImage();
                        Image1.ImageSource = bitmap;
                        bitmap.UriSource = new Uri(item.Al.PicUrl);
                        break;
                    }
                }

                var back = new ImageBrush
                {
                    ImageSource = Image1.ImageSource,
                    Stretch = Stretch.UniformToFill
                };
                GridBack.Background = back;
                LyricResult LrcTemp = await NetEaseMusic.Lyric(PL.SongID[0]);
                if (LrcTemp.Lrc == null)
                {
                    TextBlock TB = new TextBlock()
                    {
                        Text = "纯音乐，无歌词",
                        FontSize = 18
                    };
                    LrcControl.c_lrc_items.Children.Add(TB);
                    timer.Stop();
                }
                else
                {
                    LrcControl.LoadLrc(LrcTemp.Lrc.Lyric, LrcTemp.Tlyric?.Lyric);
                }

            }
            else
            {
                var bitmap = new BitmapImage();
                Image1.ImageSource = bitmap;
                bitmap.UriSource = new Uri(MusicSearch.ForDetail_ImageURL);

                var back = new ImageBrush
                {
                    ImageSource = Image1.ImageSource,
                    Stretch = Stretch.UniformToFill
                };
                GridBack.Background = back;

                if (MusicSearch.ForDetail_ID == LastSongID)
                {
                    return;
                }
                else
                {
                    LastSongID = MusicSearch.ForDetail_ID;
                }

                LrcControl.c_lrc_items.Children.Clear();

                Title.Text = MusicSearch.ForDetail_Name;
                LyricResult LrcTemp = await NetEaseMusic.Lyric(MusicSearch.ForDetail_ID);
                if (LrcTemp.Lrc == null)
                {
                    TextBlock TB = new TextBlock()
                    {
                        Text = "纯音乐，无歌词",
                        FontSize = 18
                    };
                    LrcControl.c_lrc_items.Children.Add(TB);
                    timer.Stop();
                }
                else
                {
                    LrcControl.LoadLrc(LrcTemp.Lrc.Lyric, LrcTemp.Tlyric?.Lyric);
                }

            };
        }

        private async void SingerHotSongList_CurrentItemChanged(MediaPlaybackList sender, CurrentMediaPlaybackItemChangedEventArgs args)
        {
            if (MediaPlayList.SingerHotSongList.CurrentItemIndex == 4294967295)
            {
                return;
            }

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                BackBlurBrush.Amount = 0;
                timer1.Start();
                LrcControl.c_lrc_items.Children.Clear();

                if (MusicPage.ThisPage.MediaControl.MediaPlayer.Source == MediaPlayList.SingerHotSongList)
                {
                    try
                    {
                        timer.Start();
                    }
                    catch (Exception) { }
                    LrcControl.c_lrc_items.Children.Clear();
                    SearchSingleMusic PL = MediaPlayList.HotSongBackup[Convert.ToInt32(MediaPlayList.SingerHotSongList.CurrentItemIndex)];
                    Title.Text = PL.Music;

                    var song = await NetEaseMusic.Search<SingleMusicSearchResult>(PL.Music, 5, 0, NeteaseMusicAPI.SearchType.Song);
                    foreach (var item in song.Result.Songs)
                    {
                        if (item.Name == PL.Music && item.Ar[0].Name == PL.Artist && item.Al.Name == PL.Album)
                        {
                            var bitmap = new BitmapImage();
                            Image1.ImageSource = bitmap;
                            bitmap.UriSource = new Uri(item.Al.PicUrl);
                            break;
                        }
                    }

                    var back = new ImageBrush
                    {
                        ImageSource = Image1.ImageSource,
                        Stretch = Stretch.UniformToFill
                    };
                    GridBack.Background = back;
                    LyricResult LrcTemp = await NetEaseMusic.Lyric(PL.SongID[0]);
                    if (LrcTemp.Lrc == null)
                    {
                        TextBlock TB = new TextBlock()
                        {
                            Text = "纯音乐，无歌词",
                            FontSize = 18
                        };
                        LrcControl.c_lrc_items.Children.Add(TB);
                        timer.Stop();
                    }
                    else
                    {
                        LrcControl.LoadLrc(LrcTemp.Lrc.Lyric, LrcTemp.Tlyric.Lyric);
                    }
                }
                else
                {
                    try
                    {
                        timer.Start();
                    }
                    catch (Exception) { }

                    var bitmap = new BitmapImage();
                    Image1.ImageSource = bitmap;
                    bitmap.UriSource = new Uri(MusicSearch.ForDetail_ImageURL);

                    var back = new ImageBrush
                    {
                        ImageSource = Image1.ImageSource,
                        Stretch = Stretch.UniformToFill
                    };
                    GridBack.Background = back;
                    Title.Text = MusicSearch.ForDetail_Name;
                    LyricResult LrcTemp = await NetEaseMusic.Lyric(MusicSearch.ForDetail_ID);
                    if (LrcTemp.Lrc == null)
                    {
                        TextBlock TB = new TextBlock()
                        {
                            Text = "纯音乐，无歌词",
                            FontSize = 18
                        };
                        LrcControl.c_lrc_items.Children.Add(TB);
                        timer.Stop();

                    }
                    else
                    {
                        LrcControl.LoadLrc(LrcTemp.Lrc.Lyric, LrcTemp.Tlyric.Lyric);
                    }
                }
            });
        }

        private void Timer1_Tick(object sender, object e)
        {
            BackBlurBrush.Amount += 0.5;
            if(BackBlurBrush.Amount>=15)
            {
                timer1.Stop();
            }
        }

        private async void MediaList_CurrentItemChanged(MediaPlaybackList sender, CurrentMediaPlaybackItemChangedEventArgs args)
        {
            if (MediaPlayList.FavouriteSongList.CurrentItemIndex == 4294967295)
            {
                return;
            }

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                BackBlurBrush.Amount = 0;
                timer1.Start();
                LrcControl.c_lrc_items.Children.Clear();

                if (MusicPage.ThisPage.MediaControl.MediaPlayer.Source == MediaPlayList.FavouriteSongList)
                {
                    try
                    {
                        timer.Start();
                    }
                    catch (Exception) { }

                    PlayList PL = MusicList.ThisPage.MusicInfo[Convert.ToInt32(MediaPlayList.FavouriteSongList.CurrentItemIndex)];
                    Title.Text = PL.Music;

                    var bitmap = new BitmapImage();
                    Image1.ImageSource = bitmap;
                    bitmap.UriSource = new Uri(PL.ImageUrl);

                    var back = new ImageBrush
                    {
                        ImageSource = Image1.ImageSource,
                        Stretch = Stretch.UniformToFill
                    };
                    GridBack.Background = back;
                    LyricResult LrcTemp = await NetEaseMusic.Lyric(PL.SongID);
                    if (LrcTemp.Lrc == null)
                    {
                        TextBlock TB = new TextBlock()
                        {
                            Text = "纯音乐，无歌词",
                            FontSize = 18
                        };
                        LrcControl.c_lrc_items.Children.Add(TB);
                        timer.Stop();
                    }
                    else
                    {
                        LrcControl.LoadLrc(LrcTemp.Lrc.Lyric, LrcTemp.Tlyric.Lyric);
                    }
                }
                else
                {
                    try
                    {
                        timer.Start();
                    }
                    catch (Exception) { }

                    var bitmap = new BitmapImage();
                    Image1.ImageSource = bitmap;
                    bitmap.UriSource = new Uri(MusicSearch.ForDetail_ImageURL);

                    var back = new ImageBrush
                    {
                        ImageSource = Image1.ImageSource,
                        Stretch = Stretch.UniformToFill
                    };
                    GridBack.Background = back;
                    Title.Text = MusicSearch.ForDetail_Name;
                    LyricResult LrcTemp = await NetEaseMusic.Lyric(MusicSearch.ForDetail_ID);
                    if (LrcTemp.Lrc == null)
                    {
                        TextBlock TB = new TextBlock()
                        {
                            Text = "纯音乐，无歌词",
                            FontSize = 18
                        };
                        LrcControl.c_lrc_items.Children.Add(TB);
                        timer.Stop();

                    }
                    else
                    {
                        LrcControl.LoadLrc(LrcTemp.Lrc.Lyric, LrcTemp.Tlyric.Lyric);
                    }
                }
            });
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            try
            {
                ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("ToDetailAnimation");
                if (animation != null)
                {
                    animation.TryStart(GridBack);
                }
            }
            catch (Exception) { }

            if (MusicPage.ThisPage.MediaControl.MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
            {
                BackBlurBrush.Amount = 0;
                EllStoryboard.Begin();
                timer.Start();
                timer1.Start();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("DetailBackAnimation", GridBack);

            EllStoryboard.Stop();
            timer.Stop();
            timer1.Stop();
            BackBlurBrush.Amount = 0;
        }


        private async void PlaybackSession_PlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (sender.PlaybackState == MediaPlaybackState.Playing)
                {
                    EllStoryboard.Resume();
                    timer.Start();
                }
                else if (sender.PlaybackState == MediaPlaybackState.Paused)
                {
                    EllStoryboard.Pause();
                    timer.Stop();
                }
            });
        }

        private void Timer_Tick(object sender, object e)
        {
            LrcControl.LrcRoll(MusicPage.ThisPage.MediaControl.MediaPlayer.PlaybackSession.Position.TotalMilliseconds);
        }
    }
}
