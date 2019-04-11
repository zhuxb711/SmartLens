using Microsoft.Toolkit.Uwp.UI.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Devices.Bluetooth;
using Windows.Devices.Radios;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace SmartLens
{
    public sealed partial class WebPage : Page
    {
        private bool CanCancelLoading;
        public bool IsPressedFavourite;
        public WebView WebBrowser = null;

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
                WebBrowser.Visibility = Visibility.Visible;
                WebBrowser.Navigate(uri);
            }
        }

        /// <summary>
        /// 初始化WebView
        /// </summary>
        private void InitializeWebView()
        {
            Gr.Children.Add(WebBrowser);

            FavouriteList.ItemsSource = WebTab.ThisPage.FavouriteCollection;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            WebBrowser.SetValue(Grid.RowProperty, 1);
            WebBrowser.SetValue(Canvas.ZIndexProperty, 0);
            WebBrowser.Visibility = Visibility.Collapsed;
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
        }

        private async void WebBrowser_NavigationFailed(object sender, WebViewNavigationFailedEventArgs e)
        {
            ContentDialog dialog = new ContentDialog
            {
                Content = "导航失败，请检查网络连接",
                Title = "提示",
                CloseButtonText = "确定"
            };
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
            WebBrowser.Navigate(new Uri("https://www.baidu.com"));
        }

        private void WebBrowser_ContentLoading(WebView sender, WebViewContentLoadingEventArgs args)
        {
            (WebTab.ThisPage.TabControl.SelectedItem as TabViewItem).Header = WebBrowser.DocumentTitle != "" ? WebBrowser.DocumentTitle : "正在加载...";

            AutoSuggest.Text = args.Uri.ToString();

            Back.IsEnabled = WebBrowser.CanGoBack;
            Forward.IsEnabled = WebBrowser.CanGoForward;

            if (WebTab.ThisPage.FavouriteDictionary.ContainsKey(AutoSuggest.Text))
            {
                Favourite.Symbol = Symbol.SolidStar;
                Favourite.Foreground = new SolidColorBrush(Colors.Gold);
            }
            else
            {
                Favourite.Symbol = Symbol.OutlineStar;
                Favourite.Foreground = new SolidColorBrush(Colors.White);
            }
        }

        private void WebBrowser_NewWindowRequested(WebView sender, WebViewNewWindowRequestedEventArgs args)
        {
            WebTab.ThisPage.TabCollection.Add(new TabViewItem()
            {
                Icon = new SymbolIcon(Symbol.Document),
                Content = new WebPage(args.Uri)
            });
            WebTab.ThisPage.TabControl.SelectedIndex = WebTab.ThisPage.TabCollection.Count - 1;
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
                if (WebBrowser.Visibility == Visibility.Collapsed)
                {
                    WebBrowser.Visibility = Visibility.Visible;
                }
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
        }

        private void Forward_Click(object sender, RoutedEventArgs e)
        {
            WebBrowser.GoForward();
        }

        private void Home_Click(object sender, RoutedEventArgs e)
        {
            if (WebBrowser.Visibility == Visibility.Collapsed)
            {
                WebBrowser.Visibility = Visibility.Visible;
            }
            WebBrowser.Navigate(new Uri("https://www.baidu.com"));
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            if (WebBrowser.Visibility == Visibility.Collapsed)
            {
                WebBrowser.Visibility = Visibility.Visible;
            }
            if (CanCancelLoading)
            {
                WebBrowser.Stop();
                RefreshState.Symbol = Symbol.Refresh;
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
            if ((string)(WebTab.ThisPage.TabControl.SelectedItem as TabViewItem).Header == "正在加载...")
            {
                (WebTab.ThisPage.TabControl.SelectedItem as TabViewItem).Header = WebBrowser.DocumentTitle;
            }
            RefreshState.Symbol = Symbol.Refresh;
            Progress.IsActive = false;
            CanCancelLoading = false;
        }

        private void WebBrowser_NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
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
                WebBrowser.Navigate(new Uri("https://www.baidu.com"));
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
            await WebView.ClearTemporaryWebDataAsync();
            ContentDialog dialog = new ContentDialog
            {
                Content = "所有缓存和数据均已清空",
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
        }

        private void FavoutiteListButton_Click(object sender, RoutedEventArgs e)
        {
            SplitControl.IsPaneOpen = !SplitControl.IsPaneOpen;
        }

        private void FavouriteList_ItemClick(object sender, ItemClickEventArgs e)
        {
            WebBrowser.Navigate(new Uri((e.ClickedItem as FavouriteItem).WebSite));
        }

        private void Favourite_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsPressedFavourite)
            {
                return;
            }
            Favourite.Foreground = new SolidColorBrush(Colors.Gold);
        }

        private void Favourite_PointerExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsPressedFavourite)
            {
                return;
            }
            Favourite.Foreground = new SolidColorBrush(Colors.White);
        }

        private void Favourite_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (WebTab.ThisPage.TabCollection[WebTab.ThisPage.TabControl.SelectedIndex].Header.ToString() == "空白页")
            {
                return;
            }

            if (Favourite.Symbol == Symbol.OutlineStar)
            {
                IsPressedFavourite = true;
                Favourite.Symbol = Symbol.SolidStar;
                Favourite.Foreground = new SolidColorBrush(Colors.Gold);

                if (!WebTab.ThisPage.FavouriteDictionary.ContainsKey(AutoSuggest.Text))
                {
                    var FavItem = new FavouriteItem(WebBrowser.DocumentTitle, AutoSuggest.Text);
                    WebTab.ThisPage.FavouriteCollection.Add(FavItem);
                    WebTab.ThisPage.FavouriteDictionary.Add(AutoSuggest.Text, FavItem);
                }
            }
            else
            {
                IsPressedFavourite = false;
                Favourite.Symbol = Symbol.OutlineStar;
                Favourite.Foreground = new SolidColorBrush(Colors.White);

                if (WebTab.ThisPage.FavouriteDictionary.ContainsKey(AutoSuggest.Text))
                {
                    var FavItem = WebTab.ThisPage.FavouriteDictionary[AutoSuggest.Text];
                    WebTab.ThisPage.FavouriteCollection.Remove(FavItem);
                    WebTab.ThisPage.FavouriteDictionary.Remove(FavItem.WebSite);
                }
            }
        }
    }

    public sealed class FavouriteItem
    {
        public string Subject { get; private set; }
        public string WebSite { get; private set; }
        public FavouriteItem(string Subject, string WebSite)
        {
            this.Subject = Subject;
            this.WebSite = WebSite;
        }
    }
}
