using SmartLens.NetEase;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.Playback;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace SmartLens
{
    public sealed partial class MusicPage : Page
    {
        public static MusicPage ThisPage { get; private set; }
        private PlayMode CurrentMode = PlayMode.Order;
        private PlayModeNotification ModeNotification;
        public MusicPage()
        {
            InitializeComponent();
            ThisPage = this;
            Loaded += MusicPage_Loaded;
        }

        private async void MusicPage_Loaded(object sender, RoutedEventArgs e)
        {
            MediaControl.MediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
            MediaControl.MediaPlayer.AudioCategory = MediaPlayerAudioCategory.Media;

            //启用实时播放会增加资源占用但同时避免了进度条刷新慢和声音卡顿的问题
            MediaControl.MediaPlayer.RealTimePlayback = true;

            MediaControl.MediaPlayer.PlaybackSession.BufferingStarted += PlaybackSession_BufferingStarted;

            /*
             * 在此处放置异步看起来是不必要的，可能会因为不必要的异步造成开销过大
             * 但不然，原因是异步使得MusicPage_Loaded函数能够增加async关键字
             * async和void配合使用时，调用方将不会等待该函数执行完即返回
             * 同时由于MusicPage_Loaded执行很多初始化操作，若等待MusicPage_Loaded执行完
             * 则会导致进入音乐模块时出现较长的卡顿时间，但其实进入音乐模块不必等待MusicPage_Loaded执行完毕
             */
            await Task.Run(() =>
            {
                MediaPlayList.FavouriteSongList.CurrentItemChanged += MediaList_CurrentItemChanged;
                MediaPlayList.SingerHotSongList.CurrentItemChanged += SingerHotSongList_CurrentItemChanged;
                MediaPlayList.AlbumSongList.CurrentItemChanged += AlbumSongList_CurrentItemChanged;
            });
            MusicNav.Navigate(typeof(MusicList), MusicNav, new DrillInNavigationTransitionInfo());

            ModeNotification = new PlayModeNotification();

            VoiceRec.PlayCommanded += (s, m) =>
            {
                MediaControl.MediaPlayer.Play();
            };
            VoiceRec.PauseCommanded += (s, m) =>
            {
                MediaControl.MediaPlayer.Pause();
            };
            VoiceRec.MusicChoiceCommanded += (s, SongName) =>
            {
                for (int i = 0; i < MusicList.ThisPage.FavouriteMusicCollection.Count; i++)
                {
                    string Music = string.Empty;
                    if (MusicList.ThisPage.FavouriteMusicCollection[i].Music.EndsWith(' '))
                    {
                        Music = MusicList.ThisPage.FavouriteMusicCollection[i].Music.Remove(MusicList.ThisPage.FavouriteMusicCollection[i].Music.Length - 1);
                    }
                    else
                    {
                        Music = MusicList.ThisPage.FavouriteMusicCollection[i].Music;
                    }
                    if (Music == SongName)
                    {
                        if (MediaControl.MediaPlayer.Source != MediaPlayList.FavouriteSongList)
                        {
                            MediaControl.MediaPlayer.Source = MediaPlayList.FavouriteSongList;
                        }
                        MediaPlayList.FavouriteSongList.MoveTo((uint)i);
                        if (MediaControl.MediaPlayer.PlaybackSession.PlaybackState != MediaPlaybackState.Playing)
                        {
                            MediaControl.MediaPlayer.Play();
                        }
                        break;
                    }
                }
            };
            VoiceRec.NextSongCommanded += (s, m) =>
            {
                MediaPlayList.FavouriteSongList.MoveNext();
                if (MediaControl.MediaPlayer.PlaybackSession.PlaybackState != MediaPlaybackState.Playing)
                {
                    MediaControl.MediaPlayer.Play();
                }
            };
            VoiceRec.PreviousSongCommanded += (s, m) =>
            {
                MediaPlayList.FavouriteSongList.MovePrevious();
                if (MediaControl.MediaPlayer.PlaybackSession.PlaybackState != MediaPlaybackState.Playing)
                {
                    MediaControl.MediaPlayer.Play();
                }
            };
            Loaded -= MusicPage_Loaded;
        }

        private async void AlbumSongList_CurrentItemChanged(MediaPlaybackList sender, CurrentMediaPlaybackItemChangedEventArgs args)
        {
            uint CurrentIndex = sender.CurrentItemIndex;

            //由于未知原因，CurrentIndex可能出现4294967295，因此做拦截
            if (CurrentIndex == 4294967295)
            {
                return;
            }
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (MediaControl.MediaPlayer.Source == MediaPlayList.AlbumSongList && MediaPlayList.AlbumSongBackup.Count > 0)
                {
                    SearchSingleMusic music = MediaPlayList.AlbumSongBackup[Convert.ToInt32(CurrentIndex)];
                    var SongSearchResult = await NeteaseMusicAPI.GetInstance().SearchAsync<SingleMusicSearchResult>(music.MusicName, 5, 0, NeteaseMusicAPI.SearchType.Song);

                    foreach (var Song in SongSearchResult.Result.Songs.Where(Song => Song.Name == music.MusicName && Song.Al.Name == music.Album).Select(Song => Song))
                    {
                        var bitmap = new BitmapImage();
                        PicturePlaying.Source = bitmap;
                        bitmap.UriSource = new Uri(Song.Al.PicUrl);
                        break;
                    }

                    MediaItemDisplayProperties Props = args.NewItem.GetDisplayProperties();
                    Props.Type = Windows.Media.MediaPlaybackType.Music;
                    Props.MusicProperties.Title = music.MusicName;
                    Props.MusicProperties.Artist = music.Album;
                    args.NewItem.ApplyDisplayProperties(Props);
                }
            });
        }

        private async void PlaybackSession_PlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            if (MusicMV.ThisPage == null)
            {
                return;
            }
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (MusicMV.ThisPage.MVControl.MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
                {
                    MediaControl.MediaPlayer.Pause();
                    MusicMV.ThisPage.MVControl.MediaPlayer.Pause();
                    MediaControl.MediaPlayer.Play();
                }
            });
        }

        private async void SingerHotSongList_CurrentItemChanged(MediaPlaybackList sender, CurrentMediaPlaybackItemChangedEventArgs args)
        {
            uint CurrentIndex = sender.CurrentItemIndex;

            if (CurrentIndex == 4294967295)
            {
                return;
            }
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (MediaControl.MediaPlayer.Source == MediaPlayList.SingerHotSongList && MediaPlayList.HotSongBackup.Count > 0)
                {
                    SearchSingleMusic music = MediaPlayList.HotSongBackup[Convert.ToInt32(CurrentIndex)];
                    var SongSearchResult = await NeteaseMusicAPI.GetInstance().SearchAsync<SingleMusicSearchResult>(music.MusicName, 5, 0, NeteaseMusicAPI.SearchType.Song);

                    foreach (var Song in SongSearchResult.Result.Songs.Where(Song => Song.Name == music.MusicName && Song.Al.Name == music.Album).Select(Song => Song))
                    {
                        var bitmap = new BitmapImage();
                        PicturePlaying.Source = bitmap;
                        bitmap.UriSource = new Uri(Song.Al.PicUrl);
                        break;
                    }

                    MediaItemDisplayProperties Props = args.NewItem.GetDisplayProperties();
                    Props.Type = Windows.Media.MediaPlaybackType.Music;
                    Props.MusicProperties.Title = music.MusicName;
                    Props.MusicProperties.Artist = music.Album;
                    args.NewItem.ApplyDisplayProperties(Props);
                }
            });
        }

        private async void MediaList_CurrentItemChanged(MediaPlaybackList sender, CurrentMediaPlaybackItemChangedEventArgs args)
        {
            uint Index = sender.CurrentItemIndex;
            if (Index == 4294967295)
            {
                return;
            }
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
             {
                 if (MusicList.ThisPage.FavouriteMusicCollection.Count != 0 && MediaControl.MediaPlayer.Source == MediaPlayList.FavouriteSongList)
                 {
                     var PL = MusicList.ThisPage.FavouriteMusicCollection[Convert.ToInt32(Index)];

                     var bitmap = new BitmapImage();
                     PicturePlaying.Source = bitmap;
                     bitmap.UriSource = new Uri(PL.ImageUrl);
                 }
             });

        }

        private async void PlaybackSession_BufferingStarted(MediaPlaybackSession sender, object args)
        {
            if (args is MediaPlaybackSessionBufferingStartedEventArgs bufferingStartedEventArgs && bufferingStartedEventArgs.IsPlaybackInterruption)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    ContentDialog Dialog = new ContentDialog
                    {
                        Content = "无法缓冲音乐，请检测网络连接",
                        Title = "提示",
                        CloseButtonText = "确定",
                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                    };

                    await Dialog.ShowAsync();
                });
            }

        }

        private void Image_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (MediaControl.MediaPlayer.Source == null)
            {
                return;
            }

            PictureBackup.Visibility = Visibility.Visible;
            ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("ToDetailAnimation", PicturePlaying).Configuration = new BasicConnectedAnimationConfiguration();

            ConnectedAnimationService.GetForCurrentView().DefaultDuration = TimeSpan.FromMilliseconds(400);

            MusicNav.Navigate(typeof(MusicDetail));
        }

        private void CustomMediaTransportControls_ChangeMode(object sender, EventArgs e)
        {
            switch (CurrentMode)
            {
                case PlayMode.Repeat:
                    {
                        CurrentMode = PlayMode.Order;
                        ModeNotification.Show("顺序播放");
                        MediaControl.MediaPlayer.IsLoopingEnabled = false;
                        MediaPlayList.FavouriteSongList.ShuffleEnabled = false;
                        MediaPlayList.FavouriteSongList.AutoRepeatEnabled = false;
                        CustomMediaTransportControls.ChangeModeButton.SetValue(StyleProperty, Application.Current.Resources["PlayInOrderButtonStyle"]);
                        break;
                    }
                case PlayMode.Order:
                    {
                        CurrentMode = PlayMode.Shuffle;
                        ModeNotification.Show("随机播放");
                        MediaPlayList.FavouriteSongList.ShuffleEnabled = true;
                        CustomMediaTransportControls.ChangeModeButton.SetValue(StyleProperty, Application.Current.Resources["RandomPlayButtonStyle"]);
                        break;
                    }
                case PlayMode.Shuffle:
                    {
                        CurrentMode = PlayMode.ListLoop;
                        ModeNotification.Show("列表循环");
                        MediaPlayList.FavouriteSongList.ShuffleEnabled = false;
                        MediaPlayList.FavouriteSongList.AutoRepeatEnabled = true;
                        CustomMediaTransportControls.ChangeModeButton.SetValue(StyleProperty, Application.Current.Resources["ListLoopButtonStyle"]);
                        break;
                    }
                case PlayMode.ListLoop:
                    {
                        CurrentMode = PlayMode.Repeat;
                        ModeNotification.Show("单曲循环");
                        MediaPlayList.FavouriteSongList.AutoRepeatEnabled = false;
                        if (MediaControl.MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Paused)
                        {
                            MediaControl.MediaPlayer.Play();
                        }
                        MediaControl.MediaPlayer.IsLoopingEnabled = true;
                        CustomMediaTransportControls.ChangeModeButton.SetValue(StyleProperty, Application.Current.Resources["RepeatOneButtonStyle"]);
                        break;
                    }
            }
        }

        private void MusicNav_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            //若当前页面与将要导航到的页面相同，则取消导航
            //此操作可有效阻止重复点击同一页面时候可能出现的多重导航
            if (MusicNav.CurrentSourcePageType == e.SourcePageType)
            {
                e.Cancel = true;
            }
        }
    }
}
