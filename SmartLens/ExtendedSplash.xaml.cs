using System;
using System.Collections.Generic;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


namespace SmartLens
{
    public sealed partial class ExtendedSplash : Page
    {
        internal Rect SplashImageRect;
        private SplashScreen Splash;
        public ExtendedSplash(SplashScreen Screen)
        {
            InitializeComponent();

            Window.Current.SizeChanged += Current_SizeChanged;
            Splash = Screen;

            if (Screen != null)
            {
                Screen.Dismissed += Screen_Dismissed;

                SplashImageRect = Screen.ImageLocation;

                SetControlPosition();
            }
        }

        private void SetControlPosition()
        {
            SplashProgressRing.SetValue(Canvas.LeftProperty, SplashImageRect.X + (SplashImageRect.Width * 0.5) - (SplashProgressRing.Width * 0.5));
            SplashProgressRing.SetValue(Canvas.TopProperty, SplashImageRect.Y + SplashImageRect.Height + (SplashImageRect.Height * 0.1));

            Display.SetValue(Canvas.LeftProperty, SplashImageRect.X + (SplashImageRect.Width * 0.5) - (Display.Width * 0.5));
            Display.SetValue(Canvas.TopProperty, SplashImageRect.Y + SplashImageRect.Height + (SplashImageRect.Height * 0.1) - SplashProgressRing.Height - 20);

            ButtonCollection.SetValue(Canvas.LeftProperty, SplashImageRect.X + (SplashImageRect.Width * 0.5) - (ButtonCollection.Width * 0.5));
            ButtonCollection.SetValue(Canvas.TopProperty, SplashImageRect.Y + SplashImageRect.Height + (SplashImageRect.Height * 0.1) + SplashProgressRing.Height + 20);

            extendedSplashImage.SetValue(Canvas.LeftProperty, SplashImageRect.X);
            extendedSplashImage.SetValue(Canvas.TopProperty, SplashImageRect.Y);
            extendedSplashImage.Height = SplashImageRect.Height;
            extendedSplashImage.Width = SplashImageRect.Width;
        }

        private async void DismissExtendedSplash()
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var rootFrame = new Frame();
                Window.Current.Content = rootFrame;
                rootFrame.Navigate(typeof(MainPage));
            });
        }

        private async void Screen_Dismissed(SplashScreen sender, object args)
        {
            if (ApplicationData.Current.RoamingSettings.Values["FirstStartUp"] == null)
            {
                ApplicationData.Current.RoamingSettings.Values["FirstStartUp"] = "FALSE";
                if (await Package.Current.VerifyContentIntegrityAsync())
                {
                    await MD5Util.CalculateAndStorageMD5Async();
                    DismissExtendedSplash();
                }
                else
                {
                    //出现问题
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        SplashProgressRing.Visibility = Visibility.Collapsed;
                        Display.Text = "完整性校验失败\rSmartLens存在异常";
                        ButtonCollection.Visibility = Visibility.Visible;
                    });
                }
            }
            else
            {
                if (ApplicationData.Current.RoamingSettings.Values["EnableIntegrityCheck"] is bool IsEnable)
                {
                    if (IsEnable)
                    {
                        KeyValuePair<bool, string> Result = await MD5Util.CheckSmartLensIntegrity();
                        if (Result.Key)
                        {
                            DismissExtendedSplash();
                        }
                        else
                        {
                            //出现问题
                            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                SplashProgressRing.Visibility = Visibility.Collapsed;
                                Display.Text = "完整性校验失败\rSmartLens存在异常" + "\r异常组件:" + Result.Value;
                                ButtonCollection.Visibility = Visibility.Visible;
                            });
                        }
                    }
                }
                else
                {
                    ApplicationData.Current.RoamingSettings.Values["EnableIntegrityCheck"] = true;

                    KeyValuePair<bool, string> Result = await MD5Util.CheckSmartLensIntegrity();
                    if (Result.Key)
                    {
                        DismissExtendedSplash();
                    }
                    else
                    {
                        //出现问题
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            SplashProgressRing.Visibility = Visibility.Collapsed;
                            Display.Text = "完整性校验失败\rSmartLens存在异常" + "\r异常组件:" + Result.Value;
                            ButtonCollection.Visibility = Visibility.Visible;
                        });
                    }
                }
            }
        }

        private void Current_SizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
        {
            if (Splash != null)
            {
                SplashImageRect = Splash.ImageLocation;
                SetControlPosition();
            }
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            DismissExtendedSplash();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            CoreApplication.Exit();
        }
    }
}
