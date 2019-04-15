using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
        public ObservableCollection<WebSiteItem> FavouriteCollection;
        public Dictionary<string, WebSiteItem> FavouriteDictionary;
        public ObservableCollection<KeyValuePair<DateTime, WebSiteItem>> HistoryCollection;
        public HistoryTreeFlag HistoryFlag;
        private WebPage CurrentWebPage;
        public WebTab()
        {
            InitializeComponent();
            ThisPage = this;
            OnFirstLoad();

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
                CurrentWebPage.FavEmptyTips.Visibility = FavouriteCollection.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            };

            HistoryCollection.CollectionChanged += (s, e) =>
            {
                CurrentWebPage.HistoryEmptyTips.Visibility = HistoryCollection.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                lock (SyncRootProvider.SyncRoot)
                {
                    switch (e.Action)
                    {
                        case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:

                            var RemoveNode = from Item in CurrentWebPage.HistoryTree.RootNodes[0].Children
                                             let temp = Item.Content as WebSiteItem
                                             where temp.Subject == "正在加载..." && temp.WebSite == ((KeyValuePair<DateTime, WebSiteItem>)e.OldItems[0]).Value.WebSite
                                             select Item;
                            if (RemoveNode.Count() > 0)
                            {
                                CurrentWebPage.HistoryTree.RootNodes[0].Children.Remove(RemoveNode.First());
                            }

                            break;
                        case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                            var TreeNode = from Item in CurrentWebPage.HistoryTree.RootNodes
                                           where (Item.Content as WebSiteItem).Subject == "今天"
                                           select Item;
                            if (TreeNode.Count() == 0)
                            {
                                CurrentWebPage.HistoryTree.RootNodes.Insert(0, new TreeViewNode
                                {
                                    Content = new WebSiteItem("今天", string.Empty),
                                    HasUnrealizedChildren = true,
                                    IsExpanded = true
                                });
                                HistoryFlag = HistoryTreeFlag.Today;
                                foreach (KeyValuePair<DateTime, WebSiteItem> New in e.NewItems)
                                {
                                    CurrentWebPage.HistoryTree.RootNodes[0].Children.Insert(0, new TreeViewNode
                                    {
                                        Content = New.Value,
                                        HasUnrealizedChildren = false,
                                        IsExpanded = false
                                    });
                                    if (New.Value.Subject != "正在加载...")
                                    {
                                        SQLite.GetInstance().SetWebHistoryList(New);
                                    }
                                }

                            }
                            else
                            {

                                foreach (KeyValuePair<DateTime, WebSiteItem> New in e.NewItems)
                                {
                                    TreeNode.First().Children.Insert(0, new TreeViewNode
                                    {
                                        Content = New.Value,
                                        HasUnrealizedChildren = false,
                                        IsExpanded = false
                                    });
                                    if (New.Value.Subject != "正在加载...")
                                    {
                                        SQLite.GetInstance().SetWebHistoryList(New);
                                    }
                                }

                            }
                            break;
                    }
                }
            };

            try
            {
                switch (ApplicationData.Current.LocalSettings.Values["WebTabOpenMethod"]?.ToString() ?? "空白页")
                {
                    case "空白页":
                        CreateNewTab(new Uri("about:blank"));
                        break;
                    case "主页":
                        CreateNewTab(new Uri(ApplicationData.Current.LocalSettings.Values["WebTabMainPage"].ToString()));
                        break;
                    case "特定页":
                        CreateNewTab(new Uri(ApplicationData.Current.LocalSettings.Values["WebTabSpecifiedPage"].ToString()));
                        break;
                }
            }
            catch (Exception)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Content = "导航失败，请检查网址或网络连接",
                    Title = "提示",
                    CloseButtonText = "确定"
                };
                _ = dialog.ShowAsync();
                CreateNewTab(new Uri("about:blank"));
            }

        }

        private async void OnFirstLoad()
        {
            var FavList = await SQLite.GetInstance().GetWebFavouriteList();

            if (FavList.Count > 0)
            {
                FavouriteCollection = new ObservableCollection<WebSiteItem>(FavList);
                FavouriteDictionary = new Dictionary<string, WebSiteItem>();

                foreach (var Item in FavList)
                {
                    FavouriteDictionary.Add(Item.WebSite, Item);
                }
            }
            else
            {
                FavouriteCollection = new ObservableCollection<WebSiteItem>();
                FavouriteDictionary = new Dictionary<string, WebSiteItem>();
            }

            var HistoryList = await SQLite.GetInstance().GetWebHistoryList();

            if (HistoryList.Count > 0)
            {
                HistoryCollection = new ObservableCollection<KeyValuePair<DateTime, WebSiteItem>>(HistoryList);
                bool ExistToday = false, ExistYesterday = false, ExistEarlier = false;
                foreach (var HistoryItem in HistoryCollection)
                {
                    if (HistoryItem.Key == DateTime.Today.AddDays(-1))
                    {
                        ExistYesterday = true;
                    }
                    else if (HistoryItem.Key == DateTime.Today)
                    {
                        ExistToday = true;
                    }
                    else
                    {
                        ExistEarlier = true;
                    }
                }

                if (ExistYesterday && ExistToday && ExistEarlier)
                {
                    HistoryFlag = HistoryTreeFlag.All;
                }
                else if (!ExistYesterday && ExistToday && ExistEarlier)
                {
                    HistoryFlag = HistoryTreeFlag.TodayEarlier;
                }
                else if (ExistYesterday && !ExistToday && ExistEarlier)
                {
                    HistoryFlag = HistoryTreeFlag.YesterdayEarlier;
                }
                else if (ExistYesterday && ExistToday && !ExistEarlier)
                {
                    HistoryFlag = HistoryTreeFlag.TodayYesterday;
                }
                else if (!ExistYesterday && !ExistToday && ExistEarlier)
                {
                    HistoryFlag = HistoryTreeFlag.Earlier;
                }
                else if (!ExistYesterday && ExistToday && !ExistEarlier)
                {
                    HistoryFlag = HistoryTreeFlag.Today;
                }
                else if (ExistYesterday && !ExistToday && !ExistEarlier)
                {
                    HistoryFlag = HistoryTreeFlag.Yesterday;
                }

            }
            else
            {
                HistoryCollection = new ObservableCollection<KeyValuePair<DateTime, WebSiteItem>>();
                HistoryFlag = HistoryTreeFlag.None;
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
                WebPage Web = new WebPage(uri);
                TabViewItem CurrentItem = new TabViewItem
                {
                    Header = "空白页",
                    Icon = new SymbolIcon(Symbol.Document),
                    Content = Web
                };
                Web.ThisTab = CurrentItem;
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
            try
            {
                switch (ApplicationData.Current.LocalSettings.Values["WebTabOpenMethod"].ToString())
                {
                    case "空白页":
                        CreateNewTab(new Uri("about:blank"));
                        break;
                    case "主页":
                        CreateNewTab(new Uri(ApplicationData.Current.LocalSettings.Values["WebTabMainPage"].ToString()));
                        break;
                    case "特定页":
                        CreateNewTab(new Uri(ApplicationData.Current.LocalSettings.Values["WebTabSpecifiedPage"].ToString()));
                        break;
                }
            }
            catch (Exception)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Content = "导航失败，请检查网址或网络连接",
                    Title = "提示",
                    CloseButtonText = "确定"
                };
                _ = dialog.ShowAsync();
                CreateNewTab(new Uri("about:blank"));
            }

        }

        private void TabControl_TabClosing(object sender, TabClosingEventArgs e)
        {
            (e.Tab.Content as WebPage).Dispose();
            e.Tab.Content = null;
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabControl.SelectedIndex == -1)
            {
                return;
            }

            CurrentWebPage = (TabControl.SelectedItem as TabViewItem).Content as WebPage;

            if (CurrentWebPage.PivotControl.SelectedIndex != 0)
            {
                CurrentWebPage.PivotControl.SelectedIndex = 0;
            }

            if (FavouriteDictionary.ContainsKey(CurrentWebPage.AutoSuggest.Text))
            {
                CurrentWebPage.Favourite.Symbol = Symbol.SolidStar;
                CurrentWebPage.Favourite.Foreground = new SolidColorBrush(Colors.Gold);
                CurrentWebPage.IsPressedFavourite = true;
            }
            else
            {
                CurrentWebPage.Favourite.Symbol = Symbol.OutlineStar;
                CurrentWebPage.Favourite.Foreground = new SolidColorBrush(Colors.White);
                CurrentWebPage.IsPressedFavourite = false;
            }
        }
    }
}
