using DlibDotNet;
using Microsoft.Toolkit.Uwp.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Graphics.Imaging;
using Windows.Media.Capture.Frames;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System.Display;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using OpenCVBridge;

namespace SmartLens
{
    public sealed partial class Cosmetics : Page
    {
        ObservableCollection<CosmeticsItem> CosmeticsList = null;
        SoftwareBitmap BackImageBuffer = null;
        FrontalFaceDetector FaceDetector = null;
        ShapePredictor FaceModel = null;
        CameraHelper CamHelper = null;
        CancellationTokenSource CancelToken = null;
        AutoResetEvent WaitForTaskComplete = null;
        AutoResetEvent WaitUntilInitialFinshed = null;
        DisplayRequest StayAwake = null;
        SoftwareBitmapSource ImageSource = null;
        OpenCVLibrary OpenCV = null;
        List<Windows.Foundation.Point> PointsCollection = null;
        byte[] DlibImageArray;
        Color color;
        bool IsTaskRunning = false;
        bool AbortSignal = true;

        public Cosmetics()
        {
            InitializeComponent();
            Windows.ApplicationModel.Core.CoreApplication.EnteredBackground += CoreApplication_EnteredBackground;
            Windows.ApplicationModel.Core.CoreApplication.LeavingBackground += CoreApplication_LeavingBackground;
            Windows.ApplicationModel.Core.CoreApplication.Suspending += CoreApplication_Suspending;
            Windows.ApplicationModel.Core.CoreApplication.Resuming += CoreApplication_Resuming;
            Loaded += Cosmetics_Loaded;
        }

        private void CoreApplication_Resuming(object sender, object e)
        {
            FaceDetector = Dlib.GetFrontalFaceDetector();
            FaceModel = ShapePredictor.Deserialize("CosmeticsPage/shape_predictor_68_face_landmarks.dat");
        }

        private void CoreApplication_Suspending(object sender, SuspendingEventArgs e)
        {
            FaceDetector?.Dispose();
            FaceModel?.Dispose();
        }

        private async void CoreApplication_LeavingBackground(object sender, LeavingBackgroundEventArgs e)
        {
            var deferral = e.GetDeferral();
            CancelToken = new CancellationTokenSource();
            IsTaskRunning = false;

            if (ApplicationData.Current.RoamingSettings.Values["LastSelectedCameraSource"] != null)
            {
                string LastSelectedCameraSource = ApplicationData.Current.RoamingSettings.Values["LastSelectedCameraSource"].ToString();
                var MediaFraSourceGroup = await CameraHelper.GetFrameSourceGroupsAsync();
                for (int i = 0; i < MediaFraSourceGroup.Count; i++)
                {
                    if (MediaFraSourceGroup[i].DisplayName == LastSelectedCameraSource)
                    {
                        CameraProvider.SetCameraFrameSource(MediaFraSourceGroup[i]);
                        break;
                    }
                }
            }
            CamHelper = CameraProvider.GetCameraHelperInstance();
            await CamHelper.InitializeAndStartCaptureAsync();
            var temp = CamHelper.PreviewFrameSource.SupportedFormats;
            MediaFrameFormat NV12 = null;
            MediaFrameFormat YUY2 = null;
            foreach (var item in temp)
            {
                if (item.VideoFormat.Width == 640 && item.VideoFormat.Height == 480)
                {
                    if (item.Subtype == "NV12")
                    {
                        NV12 = item.VideoFormat.MediaFrameFormat;
                    }
                    else if (item.Subtype == "YUY2")
                    {
                        YUY2 = item.VideoFormat.MediaFrameFormat;
                    }
                }
            }
            if (NV12 != null)
            {
                await CamHelper.PreviewFrameSource.SetFormatAsync(NV12);
            }
            else if (YUY2 != null)
            {
                await CamHelper.PreviewFrameSource.SetFormatAsync(YUY2);
            }
            else
            {
                throw new Exception("摄像头分辨率不受支持");
            }

            CamHelper.FrameArrived += CamHelper_FrameArrived;
            StayAwake?.RequestActive();

            deferral.Complete();
        }

        private async void CoreApplication_EnteredBackground(object sender, EnteredBackgroundEventArgs e)
        {
            var deferral = e.GetDeferral();
            CancelToken?.Cancel();
            await Task.Run(() =>
            {
                WaitForTaskComplete.WaitOne(1500);
                CamHelper.FrameArrived -= CamHelper_FrameArrived;
            });
            CancelToken?.Dispose();
            CameraProvider.Dispose();
            StayAwake?.RequestRelease();
            deferral.Complete();
        }

        private async void Cosmetics_Loaded(object sender, RoutedEventArgs e)
        {
            WaitForTaskComplete = new AutoResetEvent(false);
            WaitUntilInitialFinshed = new AutoResetEvent(false);
            ImageSource = new SoftwareBitmapSource();
            CaptureControl.Source = ImageSource;
            PointsCollection = new List<Windows.Foundation.Point>();

            CosmeticsList = new ObservableCollection<CosmeticsItem>();
            CosmeticsControl.ItemsSource = CosmeticsList;

            DlibImageArray = new byte[1228800];

            StorageFolder LipFolder = await (await Package.Current.InstalledLocation.GetFolderAsync("CosmeticsPage")).GetFolderAsync("LipLogo");
            var LipLogo = await LipFolder.GetFilesAsync();
            for (int i = 0; i < LipLogo.Count; i++)
            {
                Color temp = Colors.Red;
                string describe = string.Empty;
                switch (LipLogo[i].DisplayName)
                {
                    case "Dior": temp = Color.FromArgb(1, 100, 0, 30); describe = "法国著名时尚消费品牌"; break;
                    case "CHANEL": temp = Color.FromArgb(1, 70, 0, 0); describe = "法国著名奢侈品品牌"; break;
                    case "GIVENCHY": temp = Color.FromArgb(1, 100, 30, 30); describe = "法国著名时尚消费品牌"; break;
                    case "M.A.C": temp = Color.FromArgb(1, 50, 0, 50); describe = "美国著名化妆品品牌"; break;
                    case "LANCOME": temp = Color.FromArgb(1, 100, 0, 0); describe = "法国著名化妆品品牌"; break;
                }
                CosmeticsList.Add(new CosmeticsItem(new Uri("ms-appx:///CosmeticsPage/LipLogo/" + LipLogo[i].Name), LipLogo[i].DisplayName, describe, temp));
            }
            CosmeticsControl.SelectedIndex = 0;

            if (ApplicationData.Current.RoamingSettings.Values["LastSelectedCameraSource"] != null)
            {
                string LastSelectedCameraSource = ApplicationData.Current.RoamingSettings.Values["LastSelectedCameraSource"].ToString();
                var MediaFraSourceGroup = await CameraHelper.GetFrameSourceGroupsAsync();
                for (int i = 0; i < MediaFraSourceGroup.Count; i++)
                {
                    if (MediaFraSourceGroup[i].DisplayName == LastSelectedCameraSource)
                    {
                        CameraProvider.SetCameraFrameSource(MediaFraSourceGroup[i]);
                        break;
                    }
                }
            }

            CancelToken = new CancellationTokenSource();
            OpenCV = new OpenCVLibrary();

            await Task.Run(() =>
            {
                FaceDetector = Dlib.GetFrontalFaceDetector();
                FaceModel = ShapePredictor.Deserialize("CosmeticsPage/shape_predictor_68_face_landmarks.dat");
            });
            try
            {
                CamHelper = CameraProvider.GetCameraHelperInstance();
                await CamHelper.InitializeAndStartCaptureAsync();
                var temp = CamHelper.PreviewFrameSource.SupportedFormats;
                MediaFrameFormat NV12 = null;
                MediaFrameFormat YUY2 = null;
                foreach (var item in temp)
                {
                    if (item.VideoFormat.Width == 640 && item.VideoFormat.Height == 480)
                    {
                        if (item.Subtype == "NV12")
                        {
                            NV12 = item.VideoFormat.MediaFrameFormat;
                        }
                        else if (item.Subtype == "YUY2")
                        {
                            YUY2 = item.VideoFormat.MediaFrameFormat;
                        }
                    }
                }
                if (NV12 != null)
                {
                    await CamHelper.PreviewFrameSource.SetFormatAsync(NV12);
                }
                else if (YUY2 != null)
                {
                    await CamHelper.PreviewFrameSource.SetFormatAsync(YUY2);
                }
                else
                {
                    throw new Exception("摄像头分辨率不受支持");
                }

                CamHelper.FrameArrived += CamHelper_FrameArrived;

            }
            catch (Exception ex)
            {
                ContentDialog Message = new ContentDialog
                {
                    Title = "错误",
                    Content = "无法启动相机预览,错误消息如下：\r\r" + ex.Message,
                    CloseButtonText = "确定"
                };
                await Message.ShowAsync();
                AbortSignal = true;
                WaitUntilInitialFinshed.Set();

                return;
            }

            StayAwake = new DisplayRequest();
            StayAwake.RequestActive();

            WaitUntilInitialFinshed.Set();
            LoadingControl.IsLoading = false;
            AbortSignal = false;
        }

        private async void CamHelper_FrameArrived(object sender, FrameEventArgs e)
        {
            var softwarebitmap = e.VideoFrame?.SoftwareBitmap;
            if (softwarebitmap != null)
            {
                try
                {
                    softwarebitmap = Interlocked.Exchange(ref BackImageBuffer, softwarebitmap);
                }
                catch (Exception)
                {
                    return;
                }
                softwarebitmap?.Dispose();

                if (IsTaskRunning)
                {
                    return;
                }
                IsTaskRunning = true;

                SoftwareBitmap CapturedImage = null;
                while ((CapturedImage = Interlocked.Exchange(ref BackImageBuffer, null)) != null)
                {
                    CapturedImage = SoftwareBitmap.Convert(CapturedImage, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

                    Windows.Storage.Streams.Buffer buffer = new Windows.Storage.Streams.Buffer(1228800);
                    CapturedImage.CopyToBuffer(buffer);
                    using (var Reader = DataReader.FromBuffer(buffer))
                    {
                        Reader.ReadBytes(DlibImageArray);
                    }
                    using (Array2D<RgbPixel> ImageData = Dlib.LoadImageData<RgbPixel>(ImagePixelFormat.Bgra, DlibImageArray, 480, 640, 2560))
                    {
                        List<FullObjectDetection> Faces = DlibFunction(ImageData);
                        if (Faces != null)
                        {
                            for (int j = 0; j < Faces.Count; j++)
                            {
                                using (FullObjectDetection FaceObject = Faces[j])
                                {
                                    for (uint i = 48; i < FaceObject.Parts; i++)
                                    {
                                        var Points = FaceObject.GetPart(i);
                                        PointsCollection.Add(new Windows.Foundation.Point(Points.X, Points.Y));
                                    }
                                }
                                OpenCV.ApplyLipstickPrimaryMethod(CapturedImage, CapturedImage, PointsCollection, color);
                                PointsCollection.Clear();
                            }


                            await CaptureControl.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                            {
                                try
                                {
                                    var task = ImageSource.SetBitmapAsync(CapturedImage);
                                }
                                catch (Exception) { }
                            });
                        }
                        else
                        {
                            await CaptureControl.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                            {
                                try
                                {
                                    var task = ImageSource.SetBitmapAsync(CapturedImage);
                                }
                                catch (Exception) { }
                            });
                        }
                    }
                    if (CancelToken.IsCancellationRequested)
                    {
                        WaitForTaskComplete.Set();
                        return;
                    }
                }
            }
            IsTaskRunning = false;
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            await Task.Run(() =>
            {
                if (AbortSignal)
                {
                    WaitForTaskComplete.Set();
                }
                WaitUntilInitialFinshed.WaitOne();
            });

            CancelToken?.Cancel();

            await Task.Run(() =>
            {
                CamHelper.FrameArrived -= CamHelper_FrameArrived;
                WaitForTaskComplete.WaitOne();
            });

            if (PointsCollection != null)
            {
                PointsCollection = null;
            }

            CameraProvider.Dispose();

            StayAwake?.RequestRelease();
            StayAwake = null;

            FaceDetector?.Dispose();
            FaceModel?.Dispose();

            if (OpenCV != null)
            {
                OpenCV = null;
            }

            BackImageBuffer?.Dispose();
            BackImageBuffer = null;

            CancelToken?.Dispose();

            CosmeticsList.Clear();
            CosmeticsList = null;

            CaptureControl.Source = null;
            ImageSource?.Dispose();

            DlibImageArray = null;

            WaitForTaskComplete.Dispose();
            WaitUntilInitialFinshed.Dispose();

            Windows.ApplicationModel.Core.CoreApplication.EnteredBackground -= CoreApplication_EnteredBackground;
            Windows.ApplicationModel.Core.CoreApplication.LeavingBackground -= CoreApplication_LeavingBackground;
            Windows.ApplicationModel.Core.CoreApplication.Suspending -= CoreApplication_Suspending;
            Windows.ApplicationModel.Core.CoreApplication.Resuming -= CoreApplication_Resuming;
        }

        private List<FullObjectDetection> DlibFunction(Array2D<RgbPixel> img)
        {
            Rectangle[] dets = FaceDetector.Operator(img);
            List<FullObjectDetection> FaceLandMarkContainer = new List<FullObjectDetection>();
            foreach (var rect in dets)
            {
                FullObjectDetection FaceLandMarkRawData = FaceModel.Detect(img, rect);
                if (FaceLandMarkRawData.Parts > 2)
                {
                    FaceLandMarkContainer.Add(FaceLandMarkRawData);
                }
            }
            if (FaceLandMarkContainer.Any())
            {
                return FaceLandMarkContainer;
            }
            else return null;
        }

        private void CosmeticsControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count != 0)
            {
                color = (e.AddedItems[0] as CosmeticsItem).LipColor;
            }
        }
    }
}
