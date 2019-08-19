using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;


namespace SmartLens
{
    public sealed partial class MusicList : Page
    {
        public static MusicList ThisPage { get; set; }
        /// <summary>
        /// 指示离开当前页面前收藏列表中有无音乐
        /// </summary>
        bool LeaveWithoutObejectInList = false;
        Frame MusicNav = null;
        public ObservableCollection<PlayList> FavouriteMusicCollection = new ObservableCollection<PlayList>();
        public HashSet<long> MusicIdDictionary = new HashSet<long>();

        public MusicList()
        {
            InitializeComponent();
            ThisPage = this;
            MusicListControl.ItemsSource = FavouriteMusicCollection;
            FavouriteMusicCollection.CollectionChanged += MusicInfo_CollectionChanged;
            OnFirstLoad();
        }

        private void MusicInfo_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Remove:
                    {
                        foreach (PlayList item in e.OldItems)
                        {
                            MusicIdDictionary.Remove(item.SongID);
                        }

                        break;
                    }

                case NotifyCollectionChangedAction.Add:
                    {
                        foreach (PlayList item in e.NewItems)
                        {
                            MusicIdDictionary.Add(item.SongID);
                        }

                        break;
                    }
            }
        }

        private async void OnFirstLoad()
        {
            await SQLite.GetInstance().GetMusicDataAsync();
            if (FavouriteMusicCollection.Count > 0)
            {
                var bitmap = new BitmapImage();
                MusicPage.ThisPage.PicturePlaying.Source = bitmap;
                bitmap.UriSource = new Uri(FavouriteMusicCollection[0].ImageUrl);
            }
        }

        private async void PlayAll_Click(object sender, RoutedEventArgs e)
        {
            if (MediaPlayList.FavouriteSongList.Items.Count == 0)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "抱歉",
                    Content = "请等待歌曲加载完成",
                    CloseButtonText = "确定",
                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                };
                await dialog.ShowAsync();
                return;
            }
            if (MusicPage.ThisPage.MediaControl.MediaPlayer.Source == MediaPlayList.FavouriteSongList)
            {
                MediaPlayList.FavouriteSongList.MoveTo(0);
            }
            else
            {
                MusicPage.ThisPage.MediaControl.MediaPlayer.Source = MediaPlayList.FavouriteSongList;
            }
            MusicPage.ThisPage.MediaControl.MediaPlayer.Play();
        }

        private void SearchOnline_Click(object sender, RoutedEventArgs e)
        {
            MusicNav.Navigate(typeof(MusicSearch), MusicNav, new DrillInNavigationTransitionInfo());
        }

        private async void Select_Click(object sender, RoutedEventArgs e)
        {
            if (MusicListControl.SelectionMode == ListViewSelectionMode.Single)
            {
                MusicListControl.SelectionMode = ListViewSelectionMode.Multiple;
                Del.Text = "删除";
            }
            else
            {
                Del.Text = "选择音乐";
                while (MusicListControl.SelectedItems.Count != 0)
                {
                    MediaPlayList.FavouriteSongList.Items.RemoveAt(MusicListControl.SelectedIndex);
                    PlayList item = MusicListControl.SelectedItems[0] as PlayList;
                    await SQLite.GetInstance().DeleteMusicAsync(item);
                    FavouriteMusicCollection.Remove(item);
                }
                MusicListControl.SelectionMode = ListViewSelectionMode.Single;
                if (FavouriteMusicCollection.Count != 0)
                {
                    var bitmap = new BitmapImage();
                    Image1.Source = bitmap;
                    bitmap.UriSource = new Uri(FavouriteMusicCollection[0].ImageUrl);
                }
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (FavouriteMusicCollection.Count != 0 && LeaveWithoutObejectInList)
            {
                var bitmap = new BitmapImage();
                Image1.Source = bitmap;
                bitmap.UriSource = new Uri(FavouriteMusicCollection[0].ImageUrl);

                LeaveWithoutObejectInList = false;
            }
            MusicNav = (Frame)e.Parameter;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (FavouriteMusicCollection.Count == 0)
            {
                LeaveWithoutObejectInList = true;
            }
            else
            {
                LeaveWithoutObejectInList = false;
            }
        }

        private async void MusicListControl_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (MusicListControl.SelectedItems.Count == 0)
            {
                return;
            }
            if (MediaPlayList.FavouriteSongList.Items.Count == 0)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "抱歉",
                    Content = "请等待歌曲加载完成",
                    CloseButtonText = "确定",
                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                };
                await dialog.ShowAsync();
                return;
            }
            if (MusicPage.ThisPage.MediaControl.MediaPlayer.Source == MediaPlayList.FavouriteSongList)
            {
                MusicPage.ThisPage.MediaControl.AutoPlay = true;
                MediaPlayList.FavouriteSongList.MoveTo((uint)MusicListControl.SelectedIndex);
            }
            else
            {
                MusicPage.ThisPage.MediaControl.AutoPlay = false;
                MusicPage.ThisPage.MediaControl.MediaPlayer.Source = MediaPlayList.FavouriteSongList;
                MediaPlayList.FavouriteSongList.MoveTo((uint)MusicListControl.SelectedIndex);
                MusicPage.ThisPage.MediaControl.AutoPlay = true;
            }
        }

        private async void MV_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            MusicListControl.SelectedItem = ((TextBlock)sender).DataContext;
            var Result = await NeteaseMusicAPI.GetInstance().GetMVAsync((int)FavouriteMusicCollection[MusicListControl.SelectedIndex].MVid);
            MusicPage.ThisPage.MusicNav.Navigate(typeof(MusicMV), Result.Data, new DrillInNavigationTransitionInfo());
        }
    }
}
