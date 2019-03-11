using SmartLens.NetEase;
using System;
using System.Collections.ObjectModel;
using Windows.Media.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace SmartLens
{
    public sealed partial class MusicMV : Page
    {
        private Uri MovieUri;
        public static MusicMV ThisPage { get; private set; }
        private readonly ObservableCollection<MVSuggestion> MVSuggestionCollection = new ObservableCollection<MVSuggestion>();
        private readonly NeteaseMusicAPI NetEase = NeteaseMusicAPI.GetInstance();
        private long ArtistID;
        private bool IsSame = false;
        public MusicMV()
        {
            InitializeComponent();
            ThisPage = this;
            MVSuggestControl.ItemsSource = MVSuggestionCollection;

            Loaded += async (s, e) =>
            {
                if (IsSame)
                {
                    IsSame = false;
                    return;
                }

                MVControl.MediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
                MVControl.MediaPlayer.MediaFailed += MediaPlayer_MediaFailed;

                MVControl.Source = MediaSource.CreateFromUri(MovieUri);
                var Result = await NetEase.Artist(ArtistID);
                foreach (var item in Result.HotSongs)
                {
                    if (item.Mv != 0)
                    {
                        var MVResult = await NetEase.MV((int)item.Mv);
                        if (MVResult.Data.BriefDesc == "")
                        {
                            MVSuggestionCollection.Add(new MVSuggestion(MVResult.Data.Name, "无简介", (int)item.Mv, new Uri(MVResult.Data.Cover)));
                        }
                        else
                        {
                            MVSuggestionCollection.Add(new MVSuggestion(MVResult.Data.Name, MVResult.Data.BriefDesc, (int)item.Mv, new Uri(MVResult.Data.Cover)));
                        }
                        if (MVName.Text == MVResult.Data.Name)
                        {
                            MVSuggestControl.SelectedItem = MVSuggestionCollection[MVSuggestionCollection.Count - 1];
                        }
                    }
                }
                if (MVSuggestControl.SelectedItem != null)
                {
                    MVSuggestControl.ScrollIntoViewSmoothly(MVSuggestControl.SelectedItem, ScrollIntoViewAlignment.Leading);
                }

                MVSuggestControl.SelectionChanged += MVSuggestControl_SelectionChanged;
            };

        }

        private async void MVSuggestControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (MVSuggestControl.SelectedItem != null)
                {
                    MVSuggestControl.ScrollIntoViewSmoothly(MVSuggestControl.SelectedItem, ScrollIntoViewAlignment.Leading);
                }
            });
        }

        private async void MediaPlayer_MediaFailed(Windows.Media.Playback.MediaPlayer sender, Windows.Media.Playback.MediaPlayerFailedEventArgs args)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "错误",
                    Content = "无法缓冲该MV\r\r原因如下：\r\r" + args.ErrorMessage,
                    Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                };
                await dialog.ShowAsync();
            });
        }

        private async void PlaybackSession_PlaybackStateChanged(Windows.Media.Playback.MediaPlaybackSession sender, object args)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (MusicPage.ThisPage.MediaControl.MediaPlayer.PlaybackSession.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing)
                {
                    MVControl.MediaPlayer.Pause();
                    MusicPage.ThisPage.MediaControl.MediaPlayer.Pause();
                    MVControl.MediaPlayer.Play();
                }
            });
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (IsSame)
            {
                return;
            }

            Data MVData = e.Parameter as Data;
            ArtistID = MVData.ArtistId;
            MVName.Text = MVData.Name;
            if (MVData.Desc == null)
            {
                MVIntro.Text = "无简介";
            }
            else
            {
                MVIntro.Text = MVData.Desc == "" ? "无简介" : MVData.Desc;
            }

            if (MVData.Brs.The720 != null)
            {
                MovieUri = new Uri(MVData.Brs.The720);
            }
            else if (MVData.Brs.The480 != null)
            {
                MovieUri = new Uri(MVData.Brs.The480);
            }
            else if (MVData.Brs.The240 != null)
            {
                MovieUri = new Uri(MVData.Brs.The240);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (e.SourcePageType.Name == "MusicDetail")
            {
                IsSame = true;
            }
            else
            {
                MVControl.MediaPlayer.PlaybackSession.PlaybackStateChanged -= PlaybackSession_PlaybackStateChanged;
                MVControl.MediaPlayer.MediaFailed -= MediaPlayer_MediaFailed;
                MVSuggestControl.SelectionChanged -= MVSuggestControl_SelectionChanged;
                MVControl.MediaPlayer.Pause();
                MVControl.Source = null;
                MVSuggestionCollection.Clear();
            }
        }

        private async void MVSuggestControl_ItemClick(object sender, ItemClickEventArgs e)
        {
            var Result = await NeteaseMusicAPI.GetInstance().MV((e.ClickedItem as MVSuggestion).MovieID);
            MVName.Text = Result.Data.Name;

            if (Result.Data.Desc == null)
            {
                MVIntro.Text = "无简介";
            }
            else
            {
                MVIntro.Text = Result.Data.Desc == "" ? "无简介" : Result.Data.Desc;
            }

            if (Result.Data.Brs.The720 != null)
            {
                MovieUri = new Uri(Result.Data.Brs.The720);
                MVControl.Source = MediaSource.CreateFromUri(MovieUri);
            }
            else if (Result.Data.Brs.The480 != null)
            {
                MovieUri = new Uri(Result.Data.Brs.The480);
                MVControl.Source = MediaSource.CreateFromUri(MovieUri);
            }
            else if (Result.Data.Brs.The240 != null)
            {
                MovieUri = new Uri(Result.Data.Brs.The240);
                MVControl.Source = MediaSource.CreateFromUri(MovieUri);
            }
        }

        private void MVControl_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            MVControl.IsFullWindow = !MVControl.IsFullWindow;
        }
    }

}
