using System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml;
using SmartLens.NetEase;
using System.Threading.Tasks;

namespace SmartLens
{
    public sealed partial class MusicPage : Page
    {
        public static MusicPage ThisPage { get; set; }
        private PlayMode CurrentMode = PlayMode.Order;
        private PlayModeNotification ModeNotification;
        private enum PlayMode
        {
            Order = 0,
            Shuffle = 1,
            ListLoop = 2,
            RepeatOnce = 3
        }

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
            MediaControl.MediaPlayer.RealTimePlayback = true;
            MediaControl.MediaPlayer.PlaybackSession.BufferingStarted += PlaybackSession_BufferingStarted;
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
                    string temp = string.Empty;
                    if (MusicList.ThisPage.FavouriteMusicCollection[i].Music.EndsWith(' '))
                    {
                        temp = MusicList.ThisPage.FavouriteMusicCollection[i].Music.Remove(MusicList.ThisPage.FavouriteMusicCollection[i].Music.Length - 1);
                    }
                    else
                    {
                        temp = MusicList.ThisPage.FavouriteMusicCollection[i].Music;
                    }
                    if (temp == SongName)
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
            uint temp = sender.CurrentItemIndex;
            if (temp == 4294967295)
            {
                return;
            }
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (MediaControl.MediaPlayer.Source == MediaPlayList.AlbumSongList && MediaPlayList.AlbumSongBackup.Count > 0)
                {
                    SearchSingleMusic music = MediaPlayList.AlbumSongBackup[Convert.ToInt32(temp)];
                    var song = await NeteaseMusicAPI.GetInstance().Search<SingleMusicSearchResult>(music.Music, 5, 0, NeteaseMusicAPI.SearchType.Song);
                    foreach (var item in song.Result.Songs)
                    {
                        if (item.Name == music.Music && item.Al.Name == music.Album)
                        {
                            var bitmap = new BitmapImage();
                            PicturePlaying.Source = bitmap;
                            bitmap.UriSource = new Uri(item.Al.PicUrl);
                            break;
                        }
                    }
                    MediaItemDisplayProperties Props = args.NewItem.GetDisplayProperties();
                    Props.Type = Windows.Media.MediaPlaybackType.Music;
                    Props.MusicProperties.Title = music.Music;
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
            uint temp = sender.CurrentItemIndex;
            if (temp == 4294967295)
            {
                return;
            }
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (MediaControl.MediaPlayer.Source == MediaPlayList.SingerHotSongList && MediaPlayList.HotSongBackup.Count > 0)
                {
                    SearchSingleMusic music = MediaPlayList.HotSongBackup[Convert.ToInt32(temp)];
                    var song = await NeteaseMusicAPI.GetInstance().Search<SingleMusicSearchResult>(music.Music, 5, 0, NeteaseMusicAPI.SearchType.Song);
                    foreach (var item in song.Result.Songs)
                    {
                        if (item.Name == music.Music && item.Al.Name == music.Album)
                        {
                            var bitmap = new BitmapImage();
                            PicturePlaying.Source = bitmap;
                            bitmap.UriSource = new Uri(item.Al.PicUrl);
                            break;
                        }
                    }
                    MediaItemDisplayProperties Props = args.NewItem.GetDisplayProperties();
                    Props.Type = Windows.Media.MediaPlaybackType.Music;
                    Props.MusicProperties.Title = music.Music;
                    Props.MusicProperties.Artist = music.Album;
                    args.NewItem.ApplyDisplayProperties(Props);
                }
            });
        }

        private async void MediaList_CurrentItemChanged(MediaPlaybackList sender, CurrentMediaPlaybackItemChangedEventArgs args)
        {
            uint temp = sender.CurrentItemIndex;
            if (temp == 4294967295)
            {
                return;
            }
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
             {
                 if (MusicList.ThisPage.FavouriteMusicCollection.Count != 0 && MediaControl.MediaPlayer.Source == MediaPlayList.FavouriteSongList)
                 {
                     var PL = MusicList.ThisPage.FavouriteMusicCollection[Convert.ToInt32(temp)];

                     var bitmap = new BitmapImage();
                     PicturePlaying.Source = bitmap;
                     bitmap.UriSource = new Uri(PL.ImageUrl);
                 }
             });

        }

        private async void PlaybackSession_BufferingStarted(MediaPlaybackSession sender, object args)
        {

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                ContentDialog Dialog = new ContentDialog
                {
                    Content = "无法缓冲音乐，请检测网络连接",
                    Title = "提示",
                    CloseButtonText = "确定"
                };
                if (args is MediaPlaybackSessionBufferingStartedEventArgs bufferingStartedEventArgs && bufferingStartedEventArgs.IsPlaybackInterruption)
                {
                    await Dialog.ShowAsync();
                }
            });

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
                case PlayMode.RepeatOnce:
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
                        CurrentMode = PlayMode.RepeatOnce;
                        ModeNotification.Show("单曲循环");
                        MediaPlayList.FavouriteSongList.AutoRepeatEnabled = false;
                        MediaControl.MediaPlayer.IsLoopingEnabled = true;
                        CustomMediaTransportControls.ChangeModeButton.SetValue(StyleProperty, Application.Current.Resources["RepeatOneButtonStyle"]);
                        break;
                    }
            }
        }

        private void MusicNav_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            if (MusicNav.CurrentSourcePageType == e.SourcePageType)
            {
                e.Cancel = true;
            }
        }
    }
}
