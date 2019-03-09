using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;

namespace SmartLens
{
    public sealed partial class MainPage : Page
    {
        public static MainPage ThisPage { get; set; }
        Dictionary<Type, string> lookup = new Dictionary<Type, string>()
        {
            {typeof(HomePage), "主页"},
            {typeof(MusicPage), "音乐"},
            {typeof(VoiceRec), "语音识别"},
            {typeof(WebTab), "网页浏览"},
            {typeof(Cosmetics),"智能美妆" },
            {typeof(About),"关于" },
            {typeof(ChangeLog),"关于" },
            {typeof(USBControl),"USB管理" },
            {typeof(EmailPage),"邮件" }
        };

        public MainPage()
        {
            InitializeComponent();
            Window.Current.SetTitleBar(Title);
            ThisPage = this;
            Loaded += MainPage_Loaded;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            NavigationView.IsPaneOpen = false;
            foreach (NavigationViewItemBase item in NavigationView.MenuItems)
            {
                if (item is NavigationViewItem && item.Content.ToString() == "主页")
                {
                    NavigationView.SelectedItem = item;
                    NavFrame.Navigate(typeof(HomePage), NavFrame);
                    break;
                }
            }
        }


        private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                NavFrame.Navigate(typeof(SettingsPage));
            }
            else
            {
                switch (args.InvokedItem.ToString())
                {
                    case "主页": NavFrame.Navigate(typeof(HomePage), NavFrame); break;
                    case "音乐": NavFrame.Navigate(typeof(MusicPage)); break;
                    case "语音识别": NavFrame.Navigate(typeof(VoiceRec)); break;
                    case "网页浏览": NavFrame.Navigate(typeof(WebTab)); break;
                    case "智能美妆": NavFrame.Navigate(typeof(Cosmetics)); break;
                    case "关于": NavFrame.Navigate(typeof(About)); break;
                    case "USB管理": NavFrame.Navigate(typeof(USBControl)); break;
                    case "邮件": NavFrame.Navigate(typeof(EmailPage)); break;
                }
            }
        }

        private void NavigationView_Loaded(object sender, RoutedEventArgs e)
        {
            KeyboardAccelerator GoBack = new KeyboardAccelerator
            {
                Key = VirtualKey.GoBack
            };
            GoBack.Invoked += BackInvoked;
            KeyboardAccelerator AltLeft = new KeyboardAccelerator
            {
                Key = VirtualKey.Left
            };
            AltLeft.Invoked += BackInvoked;
            KeyboardAccelerators.Add(GoBack);
            KeyboardAccelerators.Add(AltLeft);
            AltLeft.Modifiers = VirtualKeyModifiers.Menu;

        }

        private void BackInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            BackRequested();
            args.Handled = true;
        }


        private async void BackRequested()
        {
            switch (NavFrame.CurrentSourcePageType.Name)
            {
                case "MusicPage":
                    {
                        if (MusicPage.ThisPage.MusicNav.CanGoBack)
                        {
                            string LastPageName = MusicPage.ThisPage.MusicNav.CurrentSourcePageType.Name;
                            MusicPage.ThisPage.MusicNav.GoBack();
                            if (LastPageName == "MusicDetail")
                            {
                                try
                                {
                                    ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("DetailBackAnimation");
                                    if (animation != null)
                                    {
                                        animation.Configuration = new BasicConnectedAnimationConfiguration();
                                        animation.TryStart(MusicPage.ThisPage.PicturePlaying);
                                        await Task.Delay(500);
                                    }
                                }
                                finally
                                {
                                    MusicPage.ThisPage.PictureBackup.Visibility = Visibility.Collapsed;
                                }
                            }
                        }
                        else if (NavFrame.CanGoBack)
                        {
                            NavFrame.GoBack();
                        }
                        break;
                    }
                case "USBControl":
                    {
                        if (USBControl.ThisPage.Nav.CanGoBack)
                        {
                            USBControl.ThisPage.Nav.GoBack();
                        }
                        else if (NavFrame.CanGoBack)
                        {
                            NavFrame.GoBack();
                        }
                        break;
                    }
                default:
                    {
                        if (NavFrame.CanGoBack)
                        {
                            NavFrame.GoBack();
                        }
                        break;
                    }
            }
        }

        private void NavigationView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            BackRequested();
        }

        private void NavFrame_Navigated(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            if (About.IsEnterChangeLog)
            {
                About.IsEnterChangeLog = false;
                return;
            }
            NavigationView.IsBackEnabled = NavFrame.CanGoBack;

            if (NavFrame.SourcePageType == typeof(SettingsPage))
            {
                NavigationView.SelectedItem = NavigationView.SettingsItem as NavigationViewItem;
            }
            else
            {
                string stringTag = lookup[NavFrame.SourcePageType];
                foreach (NavigationViewItemBase item in NavigationView.MenuItems)
                {
                    if (item is NavigationViewItem && item.Content.ToString() == stringTag)
                    {
                        item.IsSelected = true;
                        break;
                    }
                }
            }
        }

        private void NavFrame_Navigating(object sender, Windows.UI.Xaml.Navigation.NavigatingCancelEventArgs e)
        {
            if (NavFrame.CurrentSourcePageType == e.SourcePageType)
            {
                e.Cancel = true;
            }
        }
    }
}
