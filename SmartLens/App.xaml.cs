using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Notifications;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace SmartLens
{
    /// <summary>
    /// 提供特定于应用程序的行为，以补充默认的应用程序类。
    /// </summary>
    sealed partial class App : Application
    {
        /// <summary>
        /// 初始化单一实例应用程序对象。这是执行的创作代码的第一行，
        /// 已执行，逻辑上等同于 main() 或 WinMain()。
        /// </summary>
        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
            Resuming += App_Resuming;
            UnhandledException += App_UnhandledException;
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.Auto;
            RequestedTheme = ThemeSwitcher.IsLightEnabled ? ApplicationTheme.Light : ApplicationTheme.Dark;
        }

        private void App_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (!(Window.Current.Content is Frame rootFrame))
            {
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                Window.Current.Content = rootFrame;
            }

            string Message =
                "\r以下是错误信息：\r\rException Code错误代码：" + e.Exception.HResult +
                "\r\rMessage错误消息：" + e.Exception.Message +
                "\r\rSource来源：" + (string.IsNullOrEmpty(e.Exception.Source) ? "Unknown" : e.Exception.Source) +
                "\r\rStackTrace堆栈追踪：\r" + (string.IsNullOrEmpty(e.Exception.StackTrace) ? "Unknown" : e.Exception.StackTrace);

            rootFrame.Navigate(typeof(BlueScreen), Message);

            e.Handled = true;
        }

        private void App_Resuming(object sender, object e)
        {
            SQLite.GetInstance();
        }

        /// <summary>
        /// 在应用程序由最终用户正常启动时进行调用。
        /// 将在启动应用程序以打开特定文件等情况下使用。
        /// </summary>
        /// <param name="e">有关启动请求和过程的详细信息。</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            OnLaunchOrOnActivate(e, true);
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            if (args is ToastNotificationActivatedEventArgs e)
            {
                switch (e.Argument)
                {
                    case "Transcode":
                    case "Update":
                    case "Email":
                    case "DownloadNotification":
                    case "Updating":
                    case "UpdateFinished":
                    case "UpdateError":
                        return;
                }
            }
            OnLaunchOrOnActivate(args);
        }

        protected override void OnFileActivated(FileActivatedEventArgs args)
        {
            if (args.Verb == "USBArrival")
            {
                CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;
                var viewTitleBar = ApplicationView.GetForCurrentView().TitleBar;
                viewTitleBar.ButtonBackgroundColor = Colors.Transparent;
                viewTitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                viewTitleBar.ButtonForegroundColor = (Color)Resources["SystemBaseHighColor"];

                if (Window.Current.Content is Frame)
                {
                    MainPage.ThisPage.NavFrame.Navigate(typeof(USBControl), null, new DrillInNavigationTransitionInfo());
                    MainPage.ThisPage.NavigationView.SelectedItem = MainPage.ThisPage.NavigationView.MenuItems[5] as NavigationViewItemBase;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["USBActivateRequest"] = true;
                    var rootFrame = new Frame();
                    Window.Current.Content = rootFrame;
                    rootFrame.Navigate(typeof(MainPage));
                }
                Window.Current.Activate();
            }
        }

        private void OnLaunchOrOnActivate(IActivatedEventArgs m, bool IsLaunch = false)
        {
            if (ApplicationData.Current.LocalSettings.Values["CurrentVersion"] is string Version)
            {
                if (Version != Package.Current.Id.Version.Major.ToString() + Package.Current.Id.Version.Minor.ToString() + Package.Current.Id.Version.Build.ToString())
                {
                    ApplicationData.Current.LocalSettings.Values["CurrentVersion"] = Package.Current.Id.Version.Major.ToString() + Package.Current.Id.Version.Minor.ToString() + Package.Current.Id.Version.Build.ToString();
                    var rootFrame = new Frame();
                    Window.Current.Content = rootFrame;
                    rootFrame.Navigate(typeof(MainPage), "UpdateIntegrityDataRequest");
                }
                else
                {
                    if (ApplicationData.Current.LocalSettings.Values["EnableIntegrityCheck"] is bool IsEnable)
                    {
                        if (IsEnable)
                        {
                            if (IsLaunch)
                            {
                                var args = m as LaunchActivatedEventArgs;
                                ExtendedSplash extendedSplash = new ExtendedSplash(args.SplashScreen);
                                Window.Current.Content = extendedSplash;
                            }
                            else
                            {
                                ToastNotificationManager.History.Clear();
                                ExtendedSplash extendedSplash = new ExtendedSplash(m.SplashScreen);
                                Window.Current.Content = extendedSplash;
                            }
                        }
                        else
                        {
                            var rootFrame = new Frame();
                            Window.Current.Content = rootFrame;
                            rootFrame.Navigate(typeof(MainPage));
                        }
                    }
                    else
                    {
                        ApplicationData.Current.LocalSettings.Values["EnableIntegrityCheck"] = true;
                        if (IsLaunch)
                        {
                            var args = m as LaunchActivatedEventArgs;
                            ExtendedSplash extendedSplash = new ExtendedSplash(args.SplashScreen);
                            Window.Current.Content = extendedSplash;
                        }
                        else
                        {
                            ToastNotificationManager.History.Clear();
                            ExtendedSplash extendedSplash = new ExtendedSplash(m.SplashScreen);
                            Window.Current.Content = extendedSplash;
                        }
                    }

                }
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["CurrentVersion"] = Package.Current.Id.Version.Major.ToString() + Package.Current.Id.Version.Minor.ToString() + Package.Current.Id.Version.Build.ToString();
                if (ApplicationData.Current.LocalSettings.Values["EnableIntegrityCheck"] is bool IsEnable)
                {
                    if (IsEnable)
                    {
                        if (IsLaunch)
                        {
                            var args = m as LaunchActivatedEventArgs;
                            ExtendedSplash extendedSplash = new ExtendedSplash(args.SplashScreen);
                            Window.Current.Content = extendedSplash;
                        }
                        else
                        {
                            ToastNotificationManager.History.Clear();
                            ExtendedSplash extendedSplash = new ExtendedSplash(m.SplashScreen);
                            Window.Current.Content = extendedSplash;
                        }
                    }
                    else
                    {
                        var rootFrame = new Frame();
                        Window.Current.Content = rootFrame;
                        rootFrame.Navigate(typeof(MainPage));
                    }
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["EnableIntegrityCheck"] = true;
                    if (IsLaunch)
                    {
                        var args = m as LaunchActivatedEventArgs;
                        ExtendedSplash extendedSplash = new ExtendedSplash(args.SplashScreen);
                        Window.Current.Content = extendedSplash;
                    }
                    else
                    {
                        ToastNotificationManager.History.Clear();
                        ExtendedSplash extendedSplash = new ExtendedSplash(m.SplashScreen);
                        Window.Current.Content = extendedSplash;
                    }
                }

            }

            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;
            var viewTitleBar = ApplicationView.GetForCurrentView().TitleBar;
            viewTitleBar.ButtonBackgroundColor = Colors.Transparent;
            viewTitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            viewTitleBar.ButtonForegroundColor = (Color)Resources["SystemBaseHighColor"];

            Window.Current.Activate();
        }


        /// <summary>
        /// 导航到特定页失败时调用
        /// </summary>
        ///<param name="sender">导航失败的框架</param>
        ///<param name="e">有关导航失败的详细信息</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            if (!(Window.Current.Content is Frame rootFrame))
            {
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                Window.Current.Content = rootFrame;
            }
            string Message =
                "\r以下是错误信息：\r\rException Code错误代码：" + e.Exception.HResult +
                "\r\rMessage错误消息：" + e.Exception.Message +
                "\r\rSource来源：" + (string.IsNullOrEmpty(e.Exception.Source) ? "Unknown" : e.Exception.Source) +
                "\r\rStackTrace堆栈追踪：\r" + (string.IsNullOrEmpty(e.Exception.StackTrace) ? "Unknown" : e.Exception.StackTrace);

            rootFrame.Navigate(typeof(BlueScreen), Message);
        }

        /// <summary>
        /// 在将要挂起应用程序执行时调用。  在不知道应用程序
        /// 无需知道应用程序会被终止还是会恢复，
        /// 并让内存内容保持不变。
        /// </summary>
        /// <param name="sender">挂起的请求的源。</param>
        /// <param name="e">有关挂起请求的详细信息。</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            //TODO: 保存应用程序状态并停止任何后台活动
            SQLite.GetInstance().Dispose();
            if (EmailProtocolServiceProvider.CheckWhetherInstanceExist())
            {
                EmailProtocolServiceProvider.GetInstance().Dispose();
            }
        }
    }
}
