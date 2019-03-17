﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
            double HorizonLocation = SplashImageRect.X + (SplashImageRect.Width * 0.5);
            double VerticalLocation = SplashImageRect.Y + (SplashImageRect.Height * 0.75);

            SplashProgressRing.SetValue(Canvas.LeftProperty, HorizonLocation - (SplashProgressRing.Width * 0.5));
            SplashProgressRing.SetValue(Canvas.TopProperty, VerticalLocation);

            Display.SetValue(Canvas.LeftProperty, HorizonLocation - (Display.Width * 0.5));
            Display.SetValue(Canvas.TopProperty, VerticalLocation + SplashProgressRing.Height + 20);

            Continue.SetValue(Canvas.LeftProperty, HorizonLocation - 75);
            Continue.SetValue(Canvas.TopProperty, VerticalLocation + SplashProgressRing.Height + Display.Height + 20);

            Cancel.SetValue(Canvas.LeftProperty, HorizonLocation + 5);
            Cancel.SetValue(Canvas.TopProperty, VerticalLocation + SplashProgressRing.Height + Display.Height + 20);

            extendedSplashImage.SetValue(Canvas.LeftProperty, SplashImageRect.X);
            extendedSplashImage.SetValue(Canvas.TopProperty, SplashImageRect.Y);
            extendedSplashImage.Height = SplashImageRect.Height;
            extendedSplashImage.Width = SplashImageRect.Width;
        }

        private async void DismissExtendedSplash()
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var rootFrame = new Frame();
                Window.Current.Content = rootFrame;
                rootFrame.Navigate(typeof(MainPage));
            });
        }

        private async Task SaveErrorToLog(string ErrorModuleName)
        {
            var ErrorFile = await ApplicationData.Current.RoamingFolder.CreateFileAsync("ErrorLog.txt", CreationCollisionOption.OpenIfExists);
            string CurrentTime = DateTime.Now.ToShortDateString() + "，" + DateTime.Now.ToShortTimeString();
            await FileIO.AppendTextAsync(ErrorFile, CurrentTime + "\r出现校验错误，错误模块：" + ErrorModuleName + "\r\r");
        }

        private async void Screen_Dismissed(SplashScreen sender, object args)
        {
            if (ApplicationData.Current.LocalSettings.Values["FirstStartUp"] == null)
            {
                ApplicationData.Current.LocalSettings.Values["FirstStartUp"] = "FALSE";
                if (await Package.Current.VerifyContentIntegrityAsync())
                {
                    await MD5Util.CalculateAndStorageMD5Async();
                    DismissExtendedSplash();
                }
                else
                {
                    //出现问题
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async() =>
                    {
                        SplashProgressRing.Visibility = Visibility.Collapsed;
                        Display.Text = "完整性校验失败\rSmartLens存在异常";
                        Continue.Visibility = Visibility.Visible;
                        await SaveErrorToLog("初始化检验失败");
                    });
                }
            }
            else
            {
                KeyValuePair<bool, string> Result = await MD5Util.CheckSmartLensIntegrity();
                if (Result.Key)
                {
                    DismissExtendedSplash();
                }
                else
                {
                    //出现问题
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async() =>
                    {
                        SplashProgressRing.Visibility = Visibility.Collapsed;
                        Display.Text = "完整性校验失败\rSmartLens存在异常" + "\r异常组件:" + Result.Value;
                        Continue.Visibility = Visibility.Visible;
                        await SaveErrorToLog(Result.Value);
                    });
                }
            }
        }

        private void Current_SizeChanged(object sender, WindowSizeChangedEventArgs e)
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
