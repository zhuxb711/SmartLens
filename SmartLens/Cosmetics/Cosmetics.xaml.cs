using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Effects;
using Windows.Storage;
using Windows.System.Display;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace SmartLens
{
    public sealed partial class Cosmetics : Page
    {
        ObservableCollection<CosmeticsItem> CosmeticsList = null;
        DisplayRequest StayAwake = null;
        MediaCapture Capture;
        IMediaExtension VideoEffect;
        AutoResetEvent ExitLocker;
        CancellationTokenSource Cancellation;

        public Cosmetics()
        {
            InitializeComponent();

            //由于SmartLens需要加载人脸识别模型，处于美妆状态的SmartLens具有较高的内存占用
            //因此必须处理进入、退出后台事件，以便在必要的时候降低内存占用
            //从而降低因内存占用过高导致被Windows终止的风险
            Windows.ApplicationModel.Core.CoreApplication.LeavingBackground += CoreApplication_LeavingBackground;
            Windows.ApplicationModel.Core.CoreApplication.EnteredBackground += CoreApplication_EnteredBackground;
            Loaded += Cosmetics_Loaded;
        }

        private async void CoreApplication_EnteredBackground(object sender, EnteredBackgroundEventArgs e)
        {
            var Deferral = e.GetDeferral();

            await Capture.ClearEffectsAsync(MediaStreamType.VideoPreview);
            await Capture.StopPreviewAsync();
            Capture = null;
            MediaCaptureProvider.Dispose();

            CaptureControl.Source = null;

            StayAwake.RequestRelease();

            Deferral.Complete();
        }

        private async void CoreApplication_LeavingBackground(object sender, LeavingBackgroundEventArgs e)
        {
            var Deferral = e.GetDeferral();

            if (ApplicationData.Current.LocalSettings.Values["LastSelectedCameraSource"] is string LastSelectedCameraSource)
            {
                var MediaFraSourceGroup = await MediaFrameSourceGroup.FindAllAsync();
                foreach (var FrameSource in from MediaFrameSourceGroup FrameSource in MediaFraSourceGroup
                                            where FrameSource.DisplayName == LastSelectedCameraSource
                                            select FrameSource)
                {
                    CaptureControl.Source = Capture = await MediaCaptureProvider.SetFrameSourceAndInitializeCaptureAsync(FrameSource);
                    break;
                }
            }
            else
            {
                CaptureControl.Source = Capture = await MediaCaptureProvider.SetFrameSourceAndInitializeCaptureAsync();
            }

            ApplicationData.Current.LocalSettings.Values["ReturnCosmeticsEffectExcution"] = true;
            VideoEffectDefinition EffectDefinition = new VideoEffectDefinition("CosmeticsEffect.CosmeticsVideoEffect");

            ApplicationData.Current.LocalSettings.Values["ReturnCosmeticsEffectExcution"] = false;
            VideoEffect = await Capture.AddVideoEffectAsync(EffectDefinition, MediaStreamType.VideoPreview);

            CaptureControl.Source = Capture;
            await Capture.StartPreviewAsync();
            VideoEffect.SetProperties(new PropertySet() { { "LipColor", (CosmeticsControl.SelectedItem as CosmeticsItem).LipColor } });
            StayAwake.RequestActive();

            Deferral.Complete();
        }


        private async void Cosmetics_Loaded(object sender, RoutedEventArgs e)
        {
            ExitLocker = new AutoResetEvent(false);
            CosmeticsList = new ObservableCollection<CosmeticsItem>();
            Cancellation = new CancellationTokenSource();
            CosmeticsControl.ItemsSource = CosmeticsList;

            //以下为加载美妆图片和信息的过程
            StorageFolder LipFolder = await (await Package.Current.InstalledLocation.GetFolderAsync("Cosmetics")).GetFolderAsync("LipLogo");
            var LipLogo = await LipFolder.GetFilesAsync();
            for (int i = 0; i < LipLogo.Count; i++)
            {
                Color LipColor = Colors.Red;
                string Describe = string.Empty;
                switch (LipLogo[i].DisplayName)
                {
                    case "Dior": LipColor = Color.FromArgb(1, 100, 0, 30); Describe = "法国著名时尚消费品牌"; break;
                    case "CHANEL": LipColor = Color.FromArgb(1, 70, 0, 0); Describe = "法国著名奢侈品品牌"; break;
                    case "GIVENCHY": LipColor = Color.FromArgb(1, 100, 30, 30); Describe = "法国著名时尚消费品牌"; break;
                    case "M.A.C": LipColor = Color.FromArgb(1, 50, 0, 50); Describe = "美国著名化妆品品牌"; break;
                    case "LANCOME": LipColor = Color.FromArgb(1, 100, 0, 0); Describe = "法国著名化妆品品牌"; break;
                }
                CosmeticsList.Add(new CosmeticsItem(new Uri("ms-appx:///Cosmetics/LipLogo/" + LipLogo[i].Name), LipLogo[i].DisplayName, Describe, LipColor));
            }

            //读取设置模块中指定的摄像头，并设置为当前使用的摄像头
            if (ApplicationData.Current.LocalSettings.Values["LastSelectedCameraSource"] is string LastSelectedCameraSource)
            {
                var MediaFraSourceGroup = await MediaFrameSourceGroup.FindAllAsync();
                foreach (var FrameSource in from MediaFrameSourceGroup FrameSource in MediaFraSourceGroup
                                            where FrameSource.DisplayName == LastSelectedCameraSource
                                            select FrameSource)
                {
                    CaptureControl.Source = Capture = await MediaCaptureProvider.SetFrameSourceAndInitializeCaptureAsync(FrameSource);
                    break;
                }
            }
            else
            {
                CaptureControl.Source = Capture = await MediaCaptureProvider.SetFrameSourceAndInitializeCaptureAsync();
            }

            if (Capture != null)
            {
                await Task.Run(async () =>
                {
                    ApplicationData.Current.LocalSettings.Values["ReturnCosmeticsEffectExcution"] = true;
                    VideoEffectDefinition EffectDefinition = new VideoEffectDefinition("CosmeticsEffect.CosmeticsVideoEffect");

                    ApplicationData.Current.LocalSettings.Values["ReturnCosmeticsEffectExcution"] = false;
                    VideoEffect = await Capture.AddVideoEffectAsync(EffectDefinition, MediaStreamType.VideoPreview);
                });

                if (!Cancellation.IsCancellationRequested)
                {
                    await Capture.StartPreviewAsync();

                    CosmeticsControl.SelectedIndex = 0;

                    LoadingControl.IsLoading = false;
                }
            }
            else
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "错误",
                    Content = "无可用的摄像头设备或设备异常，请检查摄像头连接",
                    CloseButtonText = "确定"
                };
                _ = await dialog.ShowAsync();
                if (MainPage.ThisPage.NavFrame.CanGoBack)
                {
                    MainPage.ThisPage.NavFrame.GoBack();
                }
            }

            StayAwake = new DisplayRequest();
            StayAwake.RequestActive();

            ExitLocker.Set();
        }

        protected async override void OnNavigatedFrom(NavigationEventArgs e)
        {
            await Task.Run(() =>
            {
                Cancellation.Cancel();
                ExitLocker.WaitOne();
                ExitLocker.Dispose();
                ExitLocker = null;
                Cancellation.Dispose();
                Cancellation = null;
            });

            Windows.ApplicationModel.Core.CoreApplication.LeavingBackground -= CoreApplication_LeavingBackground;
            Windows.ApplicationModel.Core.CoreApplication.EnteredBackground -= CoreApplication_EnteredBackground;

            if (Capture != null)
            {
                await Capture.ClearEffectsAsync(MediaStreamType.VideoPreview);

                if (Capture.CameraStreamState == Windows.Media.Devices.CameraStreamState.Streaming)
                {
                    await Capture.StopPreviewAsync();
                }

                Capture = null;
            }
            MediaCaptureProvider.Dispose();

            CaptureControl.Source = null;
            CosmeticsList.Clear();
            CosmeticsList = null;
            StayAwake.RequestRelease();
            StayAwake = null;
        }

        private void CosmeticsControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count != 0)
            {
                VideoEffect?.SetProperties(new PropertySet() { { "LipColor", (e.AddedItems[0] as CosmeticsItem).LipColor } });
            }
        }
    }
}
