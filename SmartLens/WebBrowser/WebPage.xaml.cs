using Microsoft.Toolkit.Uwp.UI.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Devices.Bluetooth;
using Windows.Devices.Radios;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace SmartLens
{
    public sealed partial class WebPage : Page
    {
        private bool CanCancelLoading;
        public bool IsPressedFavourite;
        public WebView WebBrowser = null;
        private KeyValuePair<DateTime, WebSiteItem> CurrentHistory;
        public TabViewItem ThisTab;

        public WebPage(Uri uri = null)
        {
            InitializeComponent();
        FL:
            try
            {
                if (WebBrowser == null)
                {
                    WebBrowser = new WebView(WebViewExecutionMode.SeparateProcess);
                }
            }
            catch (Exception)
            {
                goto FL;
            }
            InitializeWebView();
            if (uri != null)
            {
                WebBrowser.Navigate(uri);
            }
            Loaded += WebPage_Loaded;

            FavouriteList.ItemsSource = WebTab.ThisPage.FavouriteCollection;
            InitHistoryList();
        }

        private void InitHistoryList()
        {
            switch (WebTab.ThisPage.HistoryFlag)
            {
                case HistoryTreeFlag.All:
                    HistoryTree.RootNodes.Add(new TreeViewNode
                    {
                        Content = new WebSiteItem("今天", string.Empty),
                        HasUnrealizedChildren = true,
                        IsExpanded = true
                    });
                    HistoryTree.RootNodes.Add(new TreeViewNode
                    {
                        Content = new WebSiteItem("昨天", string.Empty),
                        HasUnrealizedChildren = true
                    });
                    HistoryTree.RootNodes.Add(new TreeViewNode
                    {
                        Content = new WebSiteItem("更早", string.Empty),
                        HasUnrealizedChildren = true
                    });
                    break;
                case HistoryTreeFlag.TodayEarlier:
                    HistoryTree.RootNodes.Add(new TreeViewNode
                    {
                        Content = new WebSiteItem("今天", string.Empty),
                        HasUnrealizedChildren = true,
                        IsExpanded = true
                    });
                    HistoryTree.RootNodes.Add(new TreeViewNode
                    {
                        Content = new WebSiteItem("更早", string.Empty),
                        HasUnrealizedChildren = true
                    });
                    break;
                case HistoryTreeFlag.TodayYesterday:
                    HistoryTree.RootNodes.Add(new TreeViewNode
                    {
                        Content = new WebSiteItem("今天", string.Empty),
                        HasUnrealizedChildren = true,
                        IsExpanded = true
                    });
                    HistoryTree.RootNodes.Add(new TreeViewNode
                    {
                        Content = new WebSiteItem("昨天", string.Empty),
                        HasUnrealizedChildren = true
                    });
                    break;
                case HistoryTreeFlag.YesterdayEarlier:
                    HistoryTree.RootNodes.Add(new TreeViewNode
                    {
                        Content = new WebSiteItem("昨天", string.Empty),
                        HasUnrealizedChildren = true,
                        IsExpanded = true
                    });
                    HistoryTree.RootNodes.Add(new TreeViewNode
                    {
                        Content = new WebSiteItem("更早", string.Empty),
                        HasUnrealizedChildren = true
                    });
                    break;
                case HistoryTreeFlag.Today:
                    HistoryTree.RootNodes.Add(new TreeViewNode
                    {
                        Content = new WebSiteItem("今天", string.Empty),
                        HasUnrealizedChildren = true,
                        IsExpanded = true
                    });
                    break;
                case HistoryTreeFlag.Yesterday:
                    HistoryTree.RootNodes.Add(new TreeViewNode
                    {
                        Content = new WebSiteItem("昨天", string.Empty),
                        HasUnrealizedChildren = true,
                        IsExpanded = true
                    });
                    break;
                case HistoryTreeFlag.Earlier:
                    HistoryTree.RootNodes.Add(new TreeViewNode
                    {
                        Content = new WebSiteItem("更早", string.Empty),
                        HasUnrealizedChildren = true,
                        IsExpanded = true
                    });
                    break;
                default:
                    break;
            }

            foreach (var HistoryItem in WebTab.ThisPage.HistoryCollection)
            {
                if (HistoryItem.Key == DateTime.Today.AddDays(-1))
                {
                    var TreeNode = from Item in HistoryTree.RootNodes where (Item.Content as WebSiteItem).Subject == "昨天" select Item;
                    TreeNode.First().Children.Add(new TreeViewNode
                    {
                        Content = HistoryItem.Value,
                        HasUnrealizedChildren = false,
                        IsExpanded = false
                    });

                }
                else if (HistoryItem.Key == DateTime.Today)
                {
                    var TreeNode = from Item in HistoryTree.RootNodes where (Item.Content as WebSiteItem).Subject == "今天" select Item;
                    TreeNode.First().Children.Add(new TreeViewNode
                    {
                        Content = HistoryItem.Value,
                        HasUnrealizedChildren = false,
                        IsExpanded = false
                    });
                }
                else
                {
                    var TreeNode = from Item in HistoryTree.RootNodes where (Item.Content as WebSiteItem).Subject == "更早" select Item;
                    TreeNode.First().Children.Add(new TreeViewNode
                    {
                        Content = HistoryItem.Value,
                        HasUnrealizedChildren = false,
                        IsExpanded = false
                    });
                }
            }
        }
        private void WebPage_Loaded(object sender, RoutedEventArgs e)
        {
            FavEmptyTips.Visibility = WebTab.ThisPage.FavouriteCollection.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            HistoryEmptyTips.Visibility = WebTab.ThisPage.HistoryCollection.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            if (WebTab.ThisPage.HistoryCollection.Count == 0)
            {
                HistoryTree.RootNodes.Clear();
            }
            else
            {
                var TreeNodes = from Item in HistoryTree.RootNodes where (Item.Content as WebSiteItem).Subject == "今天" select Item;
                if (TreeNodes.Count() > 0)
                {
                    var Node = TreeNodes.First();
                    if (Node.Children.Count != WebTab.ThisPage.HistoryCollection.Count)
                    {
                        Node.Children.Clear();

                        foreach (var HistoryItem in WebTab.ThisPage.HistoryCollection)
                        {
                            Node.Children.Add(new TreeViewNode
                            {
                                Content = HistoryItem.Value,
                                HasUnrealizedChildren = false,
                                IsExpanded = false
                            });
                        }
                    }
                }
            }

            if (ApplicationData.Current.LocalSettings.Values["WebTabOpenMethod"] is string Method)
            {
                foreach (var Item in from string Item in TabOpenMethod.Items
                                     where Method == Item
                                     select Item)
                {
                    TabOpenMethod.SelectedItem = Item;
                }
            }
            else
            {
                TabOpenMethod.SelectedIndex = 0;
            }

            if (ApplicationData.Current.LocalSettings.Values["WebTabMainPage"] is string MainPage)
            {
                MainUrl.Text = MainPage;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["WebTabMainPage"] = "https://www.baidu.com";
                MainUrl.Text = "https://www.baidu.com";
            }

            if (ApplicationData.Current.LocalSettings.Values["WebTabSpecifiedPage"] is string Specified)
            {
                SpecificUrl.Text = Specified;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["WebTabSpecifiedPage"] = "about:blank";
                SpecificUrl.Text = "about:blank";
            }

            if (ApplicationData.Current.LocalSettings.Values["WebShowMainButton"] is bool IsShow)
            {
                ShowMainButton.IsOn = IsShow;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["WebShowMainButton"] = true;
                ShowMainButton.IsOn = true;
            }

            if (ApplicationData.Current.LocalSettings.Values["WebEnableJS"] is bool IsEnableJS)
            {
                AllowJS.IsOn = IsEnableJS;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["WebEnableJS"] = true;
                AllowJS.IsOn = true;
            }

            if (ApplicationData.Current.LocalSettings.Values["WebEnableDB"] is bool IsEnableDB)
            {
                AllowIndexedDB.IsOn = IsEnableDB;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["WebEnableDB"] = true;
                AllowIndexedDB.IsOn = true;
            }

            InPrivate.Toggled -= InPrivate_Toggled;
            if (ApplicationData.Current.LocalSettings.Values["WebActivateInPrivate"] is bool EnableInPrivate)
            {
                InPrivate.IsOn = EnableInPrivate;
                if (EnableInPrivate)
                {
                    Favourite.Visibility = Visibility.Collapsed;
                }
                else
                {
                    Favourite.Visibility = Visibility.Visible;
                }
                InPrivate.Toggled += InPrivate_Toggled;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["WebActivateInPrivate"] = false;
                InPrivate.IsOn = false;
                InPrivate.Toggled += InPrivate_Toggled;
            }
        }

        private async void InPrivate_Toggled(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["WebActivateInPrivate"] = InPrivate.IsOn;
            if (InPrivate.IsOn)
            {
                Favourite.Visibility = Visibility.Collapsed;

                if (Resources.TryGetValue("InAppNotificationWithButtonsTemplate", out object NotificationTemplate) && NotificationTemplate is DataTemplate template)
                {
                    InPrivateNotification.Show(template, 10000);
                }
            }
            else
            {
                InPrivateNotification.Dismiss();
                Favourite.Visibility = Visibility.Visible;
                await WebView.ClearTemporaryWebDataAsync();
            }
        }

        /// <summary>
        /// 初始化WebView
        /// </summary>
        private void InitializeWebView()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Gr.Children.Add(WebBrowser);
            WebBrowser.SetValue(Grid.RowProperty, 1);
            WebBrowser.SetValue(Canvas.ZIndexProperty, 0);
            WebBrowser.HorizontalAlignment = HorizontalAlignment.Stretch;
            WebBrowser.VerticalAlignment = VerticalAlignment.Stretch;
            WebBrowser.NewWindowRequested += WebBrowser_NewWindowRequested;
            WebBrowser.ContentLoading += WebBrowser_ContentLoading;
            WebBrowser.NavigationCompleted += WebBrowser_NavigationCompleted;
            WebBrowser.NavigationStarting += WebBrowser_NavigationStarting;
            WebBrowser.LongRunningScriptDetected += WebBrowser_LongRunningScriptDetected;
            WebBrowser.UnsafeContentWarningDisplaying += WebBrowser_UnsafeContentWarningDisplaying;
            WebBrowser.ContainsFullScreenElementChanged += WebBrowser_ContainsFullScreenElementChanged;
            WebBrowser.PermissionRequested += WebBrowser_PermissionRequested;
            WebBrowser.SeparateProcessLost += WebBrowser_SeparateProcessLost;
            WebBrowser.NavigationFailed += WebBrowser_NavigationFailed;
            WebBrowser.FrameNavigationCompleted += WebBrowser_FrameNavigationCompleted;
            WebBrowser.LoadCompleted += WebBrowser_LoadCompleted;
        }

        private void WebBrowser_LoadCompleted(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            
        }

        private void WebBrowser_FrameNavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            
        }

        private async void WebBrowser_NavigationFailed(object sender, WebViewNavigationFailedEventArgs e)
        {
            ContentDialog dialog = new ContentDialog
            {
                Content = "导航失败，请检查网址或网络连接",
                Title = "提示",
                CloseButtonText = "确定"
            };
            WebBrowser.Navigate(new Uri("about:blank"));
            await dialog.ShowAsync();
        }

        private async void WebBrowser_SeparateProcessLost(WebView sender, WebViewSeparateProcessLostEventArgs args)
        {
            ContentDialog dialog = new ContentDialog
            {
                Content = "浏览器进程意外终止\r将自动重启并返回主页",
                Title = "提示",
                CloseButtonText = "确定"
            };
            await dialog.ShowAsync();
            WebBrowser = new WebView(WebViewExecutionMode.SeparateProcess);
            InitializeWebView();
            WebBrowser.Navigate(new Uri(ApplicationData.Current.LocalSettings.Values["WebTabMainPage"].ToString()));
        }

        private void WebBrowser_ContentLoading(WebView sender, WebViewContentLoadingEventArgs args)
        {
            ThisTab.Header = WebBrowser.DocumentTitle != "" ? WebBrowser.DocumentTitle : "正在加载...";

            AutoSuggest.Text = args.Uri.ToString();

            Back.IsEnabled = WebBrowser.CanGoBack;
            Forward.IsEnabled = WebBrowser.CanGoForward;

            if (WebTab.ThisPage.FavouriteDictionary.ContainsKey(AutoSuggest.Text))
            {
                Favourite.Symbol = Symbol.SolidStar;
                Favourite.Foreground = new SolidColorBrush(Colors.Gold);
                IsPressedFavourite = true;
            }
            else
            {
                Favourite.Symbol = Symbol.OutlineStar;
                Favourite.Foreground = new SolidColorBrush(Colors.White);
                IsPressedFavourite = false;
            }

            if (InPrivate.IsOn)
            {
                return;
            }

            lock (SyncRootProvider.SyncRoot)
            {
                var HistoryItems = from Item in WebTab.ThisPage.HistoryCollection
                                   where Item.Value.WebSite == AutoSuggest.Text
                                   select Item;
                var HistoryItem = HistoryItems.FirstOrDefault();
                var RecordTime = HistoryItem.Key;
                if (RecordTime == DateTime.Today)
                {
                    foreach (var (RootNode, InnerNode) in from RootNode in HistoryTree.RootNodes
                                                          where (RootNode.Content as WebSiteItem).Subject == "今天"
                                                          from InnerNode in RootNode.Children
                                                          where (InnerNode.Content as WebSiteItem) == HistoryItem.Value
                                                          select (RootNode, InnerNode))
                    {
                        RootNode.Children.Remove(InnerNode);
                        SQLite.GetInstance().DeleteWebHistory(HistoryItem);
                        break;
                    }
                    WebTab.ThisPage.HistoryCollection.Remove(HistoryItem);
                }

                if (AutoSuggest.Text != "about:blank")
                {
                    CurrentHistory = new KeyValuePair<DateTime, WebSiteItem>(DateTime.Today, new WebSiteItem(WebBrowser.DocumentTitle != "" ? WebBrowser.DocumentTitle : "正在加载...", AutoSuggest.Text));
                    WebTab.ThisPage.HistoryCollection.Insert(0, CurrentHistory);
                }
            }
        }

        private void WebBrowser_NewWindowRequested(WebView sender, WebViewNewWindowRequestedEventArgs args)
        {
            WebPage Web = new WebPage(args.Uri);
            TabViewItem NewItem = new TabViewItem
            {
                Header = "空白页",
                Icon = new SymbolIcon(Symbol.Document),
                Content = Web
            };
            Web.ThisTab = NewItem;

            WebTab.ThisPage.TabCollection.Add(NewItem);
            WebTab.ThisPage.TabControl.SelectedItem = NewItem;
            args.Handled = true;
        }

        public class WebSearchResult
        {
            public string q { get; set; }
            public bool p { get; set; }
            public List<string> s { get; set; }
        }

        private string GetJsonFromWeb(string Context)
        {
            string url = "http://suggestion.baidu.com/su?wd=" + Context + "&cb=window.baidu.sug";
            string str;
            try
            {
                Uri uri = new Uri(url);
                HttpWebRequest wr = (HttpWebRequest)WebRequest.Create(uri);
                Stream s = wr.GetResponse().GetResponseStream();
                StreamReader sr = new StreamReader(s, Encoding.GetEncoding("GBK"));
                str = sr.ReadToEnd();
                str = str.Remove(0, 17);
                str = str.Remove(str.Length - 2, 2);
            }
            catch
            {
                return "";
            }
            return str;
        }

        private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && sender.Text != "")
            {
                if (JsonConvert.DeserializeObject<WebSearchResult>(GetJsonFromWeb(sender.Text)) is WebSearchResult SearchResult)
                {
                    sender.ItemsSource = SearchResult.s;
                }
            }
        }

        private void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion != null)
            {
                WebBrowser.Navigate(new Uri("https://www.baidu.com/s?wd=" + args.ChosenSuggestion.ToString()));
            }
            else
            {
                try
                {
                    WebBrowser.Navigate(new Uri(args.QueryText));
                }
                catch (Exception)
                {
                    WebBrowser.Navigate(new Uri("https://www.baidu.com/s?wd=" + args.QueryText));
                }
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            WebBrowser.GoBack();
            if (WebTab.ThisPage.FavouriteDictionary.ContainsKey(AutoSuggest.Text))
            {
                Favourite.Symbol = Symbol.SolidStar;
                Favourite.Foreground = new SolidColorBrush(Colors.Gold);
                IsPressedFavourite = true;
            }
            else
            {
                Favourite.Symbol = Symbol.OutlineStar;
                Favourite.Foreground = new SolidColorBrush(Colors.White);
                IsPressedFavourite = false;
            }
        }

        private void Forward_Click(object sender, RoutedEventArgs e)
        {
            WebBrowser.GoForward();
            if (WebTab.ThisPage.FavouriteDictionary.ContainsKey(AutoSuggest.Text))
            {
                Favourite.Symbol = Symbol.SolidStar;
                Favourite.Foreground = new SolidColorBrush(Colors.Gold);
                IsPressedFavourite = true;
            }
            else
            {
                Favourite.Symbol = Symbol.OutlineStar;
                Favourite.Foreground = new SolidColorBrush(Colors.White);
                IsPressedFavourite = false;
            }
        }

        private void Home_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                WebBrowser.Navigate(new Uri(ApplicationData.Current.LocalSettings.Values["WebTabMainPage"].ToString()));
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
                WebBrowser.Navigate(new Uri("about:blank"));
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (CanCancelLoading)
            {
                WebBrowser.Stop();
                RefreshState.Symbol = Symbol.Refresh;
                ProGrid.Width = new GridLength(8);
                Progress.IsActive = false;
                CanCancelLoading = false;
            }
            else
            {
                WebBrowser.Refresh();
            }
        }

        private void WebBrowser_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            if (ThisTab.Header.ToString() == "正在加载...")
            {
                ThisTab.Header = WebBrowser.DocumentTitle == "" ? "空白页" : WebBrowser.DocumentTitle;
            }
            if (CurrentHistory.Equals(default(KeyValuePair<DateTime, WebSiteItem>)))
            {
                goto FLAG;
            }
            lock (SyncRootProvider.SyncRoot)
            {
                if (CurrentHistory.Value.Subject == "正在加载...")
                {
                    WebTab.ThisPage.HistoryCollection.Remove(CurrentHistory);
                    if (WebBrowser.DocumentTitle != "")
                    {
                        WebTab.ThisPage.HistoryCollection.Insert(0, new KeyValuePair<DateTime, WebSiteItem>(DateTime.Today, new WebSiteItem(WebBrowser.DocumentTitle, AutoSuggest.Text)));
                    }
                }
            }
        FLAG:
            RefreshState.Symbol = Symbol.Refresh;
            ProGrid.Width = new GridLength(8);
            Progress.IsActive = false;
            CanCancelLoading = false;
        }

        private void WebBrowser_NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            ProGrid.Width = new GridLength(40);
            Progress.IsActive = true;
            CanCancelLoading = true;
            RefreshState.Symbol = Symbol.Cancel;
        }

        private async void WebBrowser_LongRunningScriptDetected(WebView sender, WebViewLongRunningScriptDetectedEventArgs args)
        {
            if (args.ExecutionTime.TotalMilliseconds >= 5000)
            {
                args.StopPageScriptExecution = true;
                ContentDialog dialog = new ContentDialog
                {
                    Content = "检测到长时间运行的JavaScript脚本，可能会导致应用无响应，已自动终止",
                    Title = "警告",
                    CloseButtonText = "确定"
                };
                await dialog.ShowAsync();
            }
        }

        private async void WebBrowser_UnsafeContentWarningDisplaying(WebView sender, object args)
        {
            ContentDialog dialog = new ContentDialog
            {
                Content = "SmartScreen筛选器将该页面标记为不安全",
                Title = "警告",
                CloseButtonText = "继续访问",
                PrimaryButtonText = "返回主页"
            };
            dialog.PrimaryButtonClick += (s, e) =>
            {
                WebBrowser.Navigate(new Uri(ApplicationData.Current.LocalSettings.Values["WebTabMainPage"].ToString()));
            };
            await dialog.ShowAsync();
        }

        private void WebBrowser_ContainsFullScreenElementChanged(WebView sender, object args)
        {
            var applicationView = ApplicationView.GetForCurrentView();

            if (sender.ContainsFullScreenElement)
            {
                applicationView.TryEnterFullScreenMode();
            }
            else if (applicationView.IsFullScreenMode)
            {
                applicationView.ExitFullScreenMode();
            }
        }

        private async void WebBrowser_PermissionRequested(WebView sender, WebViewPermissionRequestedEventArgs args)
        {
            if (args.PermissionRequest.PermissionType == WebViewPermissionType.Geolocation)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Content = "网站请求获取您的精确GPS定位",
                    Title = "权限",
                    CloseButtonText = "拒绝",
                    PrimaryButtonText = "允许"
                };
                dialog.PrimaryButtonClick += (s, e) =>
                {
                    args.PermissionRequest.Allow();
                };
                dialog.CloseButtonClick += (s, e) =>
                {
                    args.PermissionRequest.Deny();
                };
                await dialog.ShowAsync();
            }
            else if (args.PermissionRequest.PermissionType == WebViewPermissionType.WebNotifications)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Content = "网站请求Web通知权限",
                    Title = "权限",
                    CloseButtonText = "拒绝",
                    PrimaryButtonText = "允许"
                };
                dialog.PrimaryButtonClick += (s, e) =>
                {
                    args.PermissionRequest.Allow();
                };
                dialog.CloseButtonClick += (s, e) =>
                {
                    args.PermissionRequest.Deny();
                };
                await dialog.ShowAsync();
            }
        }

        private async void ScreenShot_Click(object sender, RoutedEventArgs e)
        {
            if ((await (await BluetoothAdapter.GetDefaultAsync()).GetRadioAsync()).State != RadioState.On)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Content = "蓝牙功能尚未开启，是否前往设置开启？",
                    Title = "提示",
                    PrimaryButtonText = "确定",
                    CloseButtonText = "取消"
                };
                if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    MainPage.ThisPage.NavFrame.Navigate(typeof(SettingsPage));
                }
                return;
            }
            IRandomAccessStream stream = new InMemoryRandomAccessStream();
            await WebBrowser.CapturePreviewToStreamAsync(stream);
            BluetoothUI Bluetooth = new BluetoothUI();

            var result = await Bluetooth.ShowAsync();
            if (result == ContentDialogResult.Secondary)
            {
                Bluetooth = null;
                stream.Dispose();
                return;
            }
            else if (result == ContentDialogResult.Primary)
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    BluetoothFileTransfer FileTransfer = new BluetoothFileTransfer
                    {
                        Filestream = stream.AsStream(),
                        FileName = "屏幕截图.jpg"
                    };
                    await FileTransfer.ShowAsync();
                });
            }
        }

        private async void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            TipsFly.Hide();

            await WebView.ClearTemporaryWebDataAsync();
            await SQLite.GetInstance().ClearTable("WebHistory");
            WebTab.ThisPage.HistoryCollection.Clear();
            HistoryTree.RootNodes.Clear();

            ContentDialog dialog = new ContentDialog
            {
                Content = "所有缓存和历史记录数据均已清空",
                Title = "提示",
                CloseButtonText = "确定"
            };
            await dialog.ShowAsync();
        }

        private async void About_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new ContentDialog
            {
                Content = "SmartLens自带浏览器\r\r具备SmartScreen保护和完整权限控制\r\r基于Microsoft Edge内核的轻型浏览器",
                Title = "关于",
                CloseButtonText = "确定"
            };
            await dialog.ShowAsync();
        }

        public async void Dispose()
        {
            if (WebBrowser != null)
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    WebBrowser.NewWindowRequested -= WebBrowser_NewWindowRequested;
                    WebBrowser.ContentLoading -= WebBrowser_ContentLoading;
                    WebBrowser.NavigationCompleted -= WebBrowser_NavigationCompleted;
                    WebBrowser.NavigationStarting -= WebBrowser_NavigationStarting;
                    WebBrowser.LongRunningScriptDetected -= WebBrowser_LongRunningScriptDetected;
                    WebBrowser.UnsafeContentWarningDisplaying -= WebBrowser_UnsafeContentWarningDisplaying;
                    WebBrowser.ContainsFullScreenElementChanged -= WebBrowser_ContainsFullScreenElementChanged;
                    WebBrowser.PermissionRequested -= WebBrowser_PermissionRequested;
                    WebBrowser.SeparateProcessLost -= WebBrowser_SeparateProcessLost;
                    WebBrowser.NavigationFailed -= WebBrowser_NavigationFailed;
                    WebBrowser = null;
                });
            }
            CurrentHistory = default;
            ThisTab = null;
            InPrivate.Toggled -= InPrivate_Toggled;
        }

        private void FavoutiteListButton_Click(object sender, RoutedEventArgs e)
        {
            SplitControl.IsPaneOpen = !SplitControl.IsPaneOpen;
        }

        private void Favourite_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (IsPressedFavourite)
            {
                return;
            }
            Favourite.Foreground = new SolidColorBrush(Colors.Gold);
        }

        private void Favourite_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (IsPressedFavourite)
            {
                return;
            }
            Favourite.Foreground = new SolidColorBrush(Colors.White);
        }

        private async void Favourite_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (ThisTab.Header.ToString() == "空白页")
            {
                return;
            }

            if (Favourite.Symbol == Symbol.SolidStar)
            {
                IsPressedFavourite = false;
                Favourite.Symbol = Symbol.OutlineStar;
                Favourite.Foreground = new SolidColorBrush(Colors.White);

                if (WebTab.ThisPage.FavouriteDictionary.ContainsKey(AutoSuggest.Text))
                {
                    var FavItem = WebTab.ThisPage.FavouriteDictionary[AutoSuggest.Text];
                    WebTab.ThisPage.FavouriteCollection.Remove(FavItem);
                    WebTab.ThisPage.FavouriteDictionary.Remove(FavItem.WebSite);

                    await SQLite.GetInstance().DeleteWebFavouriteList(FavItem);
                }
            }
            else
            {
                FavName.Text = WebBrowser.DocumentTitle;
                FavName.SelectAll();
                FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
            }
        }

        private void Setting_Click(object sender, RoutedEventArgs e)
        {
            SettingControl.IsPaneOpen = !SettingControl.IsPaneOpen;
        }

        private void TabOpenMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["WebTabOpenMethod"] = TabOpenMethod.SelectedItem.ToString();
            SpecificUrl.Visibility = TabOpenMethod.SelectedItem.ToString() == "特定页" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowMainButton_Toggled(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["WebShowMainButton"] = ShowMainButton.IsOn;
            if (ShowMainButton.IsOn)
            {
                Home.Visibility = Visibility.Visible;
                HomeGrid.Width = new GridLength(50);
            }
            else
            {
                Home.Visibility = Visibility.Collapsed;
                HomeGrid.Width = new GridLength(0);
            }
        }

        private void AllowJS_Toggled(object sender, RoutedEventArgs e)
        {
            if (WebBrowser == null)
            {
                return;
            }
            ApplicationData.Current.LocalSettings.Values["WebEnableJS"] = AllowJS.IsOn;
            WebBrowser.Settings.IsJavaScriptEnabled = AllowJS.IsOn;
        }

        private void AllowIndexedDB_Toggled(object sender, RoutedEventArgs e)
        {
            if (WebBrowser == null)
            {
                return;
            }
            ApplicationData.Current.LocalSettings.Values["WebEnableDB"] = AllowIndexedDB.IsOn;
            WebBrowser.Settings.IsIndexedDBEnabled = AllowIndexedDB.IsOn;
        }

        private void SettingControl_PaneClosed(SplitView sender, object args)
        {
            if (string.IsNullOrWhiteSpace(MainUrl.Text))
            {
                ApplicationData.Current.LocalSettings.Values["WebTabMainPage"] = "about:blank";
            }
            else
            {
                if (MainUrl.Text.StartsWith("http"))
                {
                    ApplicationData.Current.LocalSettings.Values["WebTabMainPage"] = MainUrl.Text;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["WebTabMainPage"] = "http://" + MainUrl.Text;
                }
            }

            if (string.IsNullOrWhiteSpace(SpecificUrl.Text))
            {
                ApplicationData.Current.LocalSettings.Values["WebTabSpecifiedPage"] = "about:blank";
            }
            else
            {
                if (SpecificUrl.Text == "about:blank")
                {
                    ApplicationData.Current.LocalSettings.Values["WebTabSpecifiedPage"] = SpecificUrl.Text;
                    return;
                }
                if (SpecificUrl.Text.StartsWith("http"))
                {
                    ApplicationData.Current.LocalSettings.Values["WebTabSpecifiedPage"] = SpecificUrl.Text;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["WebTabSpecifiedPage"] = "http://" + SpecificUrl.Text;
                }
            }

        }

        private async void SaveConfirm_Click(object sender, RoutedEventArgs e)
        {
            Fly.Hide();

            IsPressedFavourite = true;
            Favourite.Symbol = Symbol.SolidStar;
            Favourite.Foreground = new SolidColorBrush(Colors.Gold);

            if (!WebTab.ThisPage.FavouriteDictionary.ContainsKey(AutoSuggest.Text))
            {
                var FavItem = new WebSiteItem(FavName.Text, AutoSuggest.Text);
                WebTab.ThisPage.FavouriteCollection.Add(FavItem);
                WebTab.ThisPage.FavouriteDictionary.Add(AutoSuggest.Text, FavItem);

                await SQLite.GetInstance().SetWebFavouriteList(FavItem);
            }

        }

        private void FavName_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveConfirm.IsEnabled = !string.IsNullOrWhiteSpace(FavName.Text);
        }

        private void SaveCancel_Click(object sender, RoutedEventArgs e)
        {
            Fly.Hide();
        }

        private void FavouriteList_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var Context = (e.OriginalSource as FrameworkElement)?.DataContext as WebSiteItem;
            FavouriteList.SelectedItem = Context;
        }

        private void FavouriteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Delete.IsEnabled = FavouriteList.SelectedIndex != -1;
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var FavItem = FavouriteList.SelectedItem as WebSiteItem;
            if (AutoSuggest.Text == FavItem.WebSite)
            {
                Favourite.Symbol = Symbol.OutlineStar;
                Favourite.Foreground = new SolidColorBrush(Colors.White);
                IsPressedFavourite = false;
            }

            WebTab.ThisPage.FavouriteCollection.Remove(FavItem);
            WebTab.ThisPage.FavouriteDictionary.Remove(FavItem.WebSite);

            await SQLite.GetInstance().DeleteWebFavouriteList(FavItem);
        }

        private void FavouriteList_ItemClick(object sender, ItemClickEventArgs e)
        {
            WebBrowser.Navigate(new Uri((e.ClickedItem as WebSiteItem).WebSite));
        }

        private void HistoryTree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            var WebItem = (args.InvokedItem as TreeViewNode).Content as WebSiteItem;
            WebBrowser.Navigate(new Uri(WebItem.WebSite));
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            InPrivateNotification.Dismiss();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            InPrivate.IsOn = false;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            TipsFly.Hide();
        }

        private void SettingControl_PaneOpening(SplitView sender, object args)
        {
            Scroll.ChangeView(null, 0, null);
        }

        private void TextBlock_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
        }

        private async void ClearData_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button).Name == "ClearFav")
            {
                ClearFavFly.Hide();
                await SQLite.GetInstance().ClearTable("WebFavourite");
                WebTab.ThisPage.FavouriteCollection.Clear();
                WebTab.ThisPage.FavouriteDictionary.Clear();
            }
            else
            {
                ClearHistoryFly.Hide();
                await SQLite.GetInstance().ClearTable("WebHistory");
                WebTab.ThisPage.HistoryCollection.Clear();
                HistoryTree.RootNodes.Clear();
            }
        }

        private void CancelClear_Click(object sender, RoutedEventArgs e)
        {
            ClearFavFly.Hide();
            ClearHistoryFly.Hide();
        }
    }
}
