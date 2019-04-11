using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace SmartLens
{
    public sealed partial class WebTab : Page
    {
        public static WebTab ThisPage { get; set; }
        public ObservableCollection<TabViewItem> TabCollection = new ObservableCollection<TabViewItem>();
        private readonly object SyncRoot = new object();
        public ObservableCollection<FavouriteItem> FavouriteCollection = new ObservableCollection<FavouriteItem>();
        public Dictionary<string, FavouriteItem> FavouriteDictionary = new Dictionary<string, FavouriteItem>();

        public WebTab()
        {
            InitializeComponent();
            ThisPage = this;
            TabControl.ItemsSource = TabCollection;
            TabCollection.CollectionChanged += (s, e) =>
            {
                if (TabCollection.Count > 0)
                {
                    TabViewItem Item = TabCollection[0] as TabViewItem;
                    if (TabCollection.Count == 1)
                    {
                        Item.IsClosable = false;
                    }
                    else if (Item.IsClosable == false)
                    {
                        Item.IsClosable = true;
                    }
                }
            };
            FavouriteCollection.CollectionChanged += (s, e) =>
            {
                (TabCollection[TabControl.SelectedIndex].Content as WebPage).FavEmptyTips.Visibility = FavouriteCollection.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            };

            switch (ApplicationData.Current.LocalSettings.Values["WebTabOpenMethod"]?.ToString() ?? "空白页")
            {
                case "空白页":
                    CreateNewTab();
                    break;
                case "主页":
                    CreateNewTab(new Uri(ApplicationData.Current.LocalSettings.Values["WebTabMainPage"].ToString()));
                    break;
                case "特定页":
                    CreateNewTab(new Uri(ApplicationData.Current.LocalSettings.Values["WebTabSpecifiedPage"].ToString()));
                    break;
            }
        }

        /// <summary>
        /// 创建新的WebTab标签页
        /// </summary>
        /// <param name="uri">导航网址</param>
        private void CreateNewTab(Uri uri = null)
        {
            lock (SyncRoot)
            {
                TabViewItem CurrentItem = new TabViewItem
                {
                    Header = "空白页",
                    Icon = new SymbolIcon(Symbol.Document),
                    Content = new WebPage(uri)
                };
                TabCollection.Add(CurrentItem);
                TabControl.SelectedItem = CurrentItem;
            }
        }


        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is Uri uri)
            {
                CreateNewTab(uri);
            }
        }

        private void AddTabButtonUpper_Click(object sender, RoutedEventArgs e)
        {
            switch (ApplicationData.Current.LocalSettings.Values["WebTabOpenMethod"].ToString())
            {
                case "空白页":
                    CreateNewTab();
                    break;
                case "主页":
                    CreateNewTab(new Uri(ApplicationData.Current.LocalSettings.Values["WebTabMainPage"].ToString()));
                    break;
                case "特定页":
                    CreateNewTab(new Uri(ApplicationData.Current.LocalSettings.Values["WebTabSpecifiedPage"].ToString()));
                    break;
            }
        }

        private void TabControl_TabClosing(object sender, TabClosingEventArgs e)
        {
            (e.Tab.Content as WebPage).Dispose();
            e.Tab.Content = null;
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(TabControl.SelectedIndex==-1)
            {
                return;
            }

            var Instance = TabCollection[TabControl.SelectedIndex].Content as WebPage;
            if (FavouriteDictionary.ContainsKey(Instance.AutoSuggest.Text))
            {
                Instance.Favourite.Symbol = Symbol.SolidStar;
                Instance.Favourite.Foreground = new SolidColorBrush(Colors.Gold);
                Instance.IsPressedFavourite = true;
            }
            else
            {
                Instance.Favourite.Symbol = Symbol.OutlineStar;
                Instance.Favourite.Foreground = new SolidColorBrush(Colors.White);
                Instance.IsPressedFavourite = false;
            }
        }
    }
}
