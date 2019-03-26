using DlibDotNet;
using Microsoft.Toolkit.Uwp.Helpers;
using OpenCVBridge;
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

            //由于SmartLens需要加载人脸识别模型，处于美妆状态的SmartLens具有较高的内存占用
            //因此必须处理进入、退出后台和挂起、恢复事件，以便在必要的时候降低内存占用
            //从而降低因内存占用过高导致被Windows终止的风险
            Windows.ApplicationModel.Core.CoreApplication.EnteredBackground += CoreApplication_EnteredBackground;
            Windows.ApplicationModel.Core.CoreApplication.LeavingBackground += CoreApplication_LeavingBackground;
            Windows.ApplicationModel.Core.CoreApplication.Suspending += CoreApplication_Suspending;
            Windows.ApplicationModel.Core.CoreApplication.Resuming += CoreApplication_Resuming;

            Loaded += Cosmetics_Loaded;
        }

        private void CoreApplication_Resuming(object sender, object e)
        {
            //用户在挂起后回到SmartLens时，SmartLens重新激活，再次加载识别模型和检测器
            //初始化人脸检测器
            FaceDetector = Dlib.GetFrontalFaceDetector();
            //加载人脸识别训练集模型
            FaceModel = ShapePredictor.Deserialize("Cosmetics/shape_predictor_68_face_landmarks.dat");
        }

        private void CoreApplication_Suspending(object sender, SuspendingEventArgs e)
        {
            //EnteredBackground事件激活后一段时间，若用户未返回SmartLens
            //Windows将挂起SmartLens，为了进一步降低挂起时终止风险
            //直接卸载识别模型，释放检测器资源，可释放100M左右的内存
            FaceDetector?.Dispose();
            FaceModel?.Dispose();
        }

        private async void CoreApplication_LeavingBackground(object sender, LeavingBackgroundEventArgs e)
        {
            var deferral = e.GetDeferral();
            CancelToken = new CancellationTokenSource();
            IsTaskRunning = false;

            //读取设置模块中指定的摄像头，并设置为当前使用的摄像头
            if (ApplicationData.Current.LocalSettings.Values["LastSelectedCameraSource"] is string LastSelectedCameraSource)
            {
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

            //初始化摄像头并开始捕获
            CamHelper = CameraProvider.GetCameraHelperInstance();
            await CamHelper.InitializeAndStartCaptureAsync();

            //读取摄像头支持的格式，并选择预定的格式
            var SupportedFormats = CamHelper.PreviewFrameSource.SupportedFormats;
            MediaFrameFormat NV12 = null;
            MediaFrameFormat YUY2 = null;

            foreach (var FrameFormat in from FrameFormat in SupportedFormats
                                        where FrameFormat.VideoFormat.Width == 640 && FrameFormat.VideoFormat.Height == 480
                                        select FrameFormat)
            {
                if (FrameFormat.Subtype == "NV12")
                {
                    NV12 = FrameFormat.VideoFormat.MediaFrameFormat;
                }
                else if (FrameFormat.Subtype == "YUY2")
                {
                    YUY2 = FrameFormat.VideoFormat.MediaFrameFormat;
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

            //激活屏幕保持常亮请求
            StayAwake?.RequestActive();

            deferral.Complete();
        }

        private async void CoreApplication_EnteredBackground(object sender, EnteredBackgroundEventArgs e)
        {
            //向图像处理部分发送终止请求，等待终止或者超时
            //一旦图像处理部分成功终止，立即释放部分资源进入后台停止活动
            var deferral = e.GetDeferral();
            CancelToken?.Cancel();

            await Task.Run(() =>
            {
                CamHelper.FrameArrived -= CamHelper_FrameArrived;
                WaitForTaskComplete.WaitOne(1500);
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

            //1228800刚好满足640 X 480分辨率的图像的需求
            DlibImageArray = new byte[1228800];

            //以下为加载美妆图片和信息的过程
            StorageFolder LipFolder = await (await Package.Current.InstalledLocation.GetFolderAsync("CosmeticsPage")).GetFolderAsync("LipLogo");
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
            CosmeticsControl.SelectedIndex = 0;

            //读取设置模块中指定的摄像头，并设置为当前使用的摄像头
            if (ApplicationData.Current.LocalSettings.Values["LastSelectedCameraSource"] is string LastSelectedCameraSource)
            {
                var MediaFraSourceGroup = await CameraHelper.GetFrameSourceGroupsAsync();
                foreach (var FrameSource in from MediaFrameSourceGroup FrameSource in MediaFraSourceGroup
                                            where FrameSource.DisplayName == LastSelectedCameraSource
                                            select FrameSource)
                {
                    CameraProvider.SetCameraFrameSource(FrameSource);
                    break;
                }
            }

            CancelToken = new CancellationTokenSource();
            OpenCV = new OpenCVLibrary();

            //异步初始化检测器，加载人脸识别训练集模型
            await Task.Run(() =>
            {
                FaceDetector = Dlib.GetFrontalFaceDetector();
                FaceModel = ShapePredictor.Deserialize("Cosmetics/shape_predictor_68_face_landmarks.dat");
            });

            //初始化摄像头并开始捕获
            //读取摄像头支持的格式，并选择预定的格式
            try
            {
                CamHelper = CameraProvider.GetCameraHelperInstance();
                await CamHelper.InitializeAndStartCaptureAsync();
                var SupportedFormats = CamHelper.PreviewFrameSource.SupportedFormats;
                MediaFrameFormat NV12 = null;
                MediaFrameFormat YUY2 = null;

                foreach (var FrameFormat in from FrameFormat in SupportedFormats
                                            where FrameFormat.VideoFormat.Width == 640 && FrameFormat.VideoFormat.Height == 480
                                            select FrameFormat)
                {
                    if (FrameFormat.Subtype == "NV12")
                    {
                        NV12 = FrameFormat.VideoFormat.MediaFrameFormat;
                    }
                    else if (FrameFormat.Subtype == "YUY2")
                    {
                        YUY2 = FrameFormat.VideoFormat.MediaFrameFormat;
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

            //激活屏幕常亮请求
            StayAwake = new DisplayRequest();
            StayAwake.RequestActive();

            WaitUntilInitialFinshed.Set();
            LoadingControl.IsLoading = false;
            AbortSignal = false;
        }

        //美妆图像处理核心函数
        //当有新的一帧从摄像头传来时将执行该函数
        private async void CamHelper_FrameArrived(object sender, FrameEventArgs e)
        {
            SoftwareBitmap IncomeSoftwareBitmap = e.VideoFrame?.SoftwareBitmap;
            if (IncomeSoftwareBitmap != null)
            {
                try
                {
                    /*Interlocked提供原子操作，在锁定的情况下进行安全的Exchange交换
                      确保交换时不会被意外改动，属于线程同步技术
                      以下语句的解释如下：
                      保留BackImageBuffer的原始值
                      将IncomeSoftwareBitmap的值安全的赋值给BackImageBuffer
                      IncomeSoftwareBitmap再获取到BackImageBuffer的原始值
                     */
                    IncomeSoftwareBitmap = Interlocked.Exchange(ref BackImageBuffer, IncomeSoftwareBitmap);
                }
                catch (Exception)
                {
                    return;
                }
                IncomeSoftwareBitmap?.Dispose();

                //避免多次执行以下内容
                if (IsTaskRunning)
                {
                    return;
                }
                IsTaskRunning = true;

                SoftwareBitmap CapturedImage = null;

                /*
                 再次进行原子操作Exchange交换，BackImageBuffer赋值为null，CapturedImage获得BackImageBuffer原始值
                 将其放置在while里的原理是：上面的IsTaskRunning的判断虽然会return，但已执行了第一个Exchange
                 即BackImageBuffer在下面进行图像处理的同时也在不断刷新的，等while一次执行完毕之后
                 再回来的时候，BackImageBuffer已经不是null的值了，因此可以一直执行下面的循环
                 */
                while ((CapturedImage = Interlocked.Exchange(ref BackImageBuffer, null)) != null)
                {
                    //Image控件仅支持格式为Bgra8、Premultiplied格式的图像
                    CapturedImage = SoftwareBitmap.Convert(CapturedImage, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

                    //将图像转换成byte数组
                    Windows.Storage.Streams.Buffer buffer = new Windows.Storage.Streams.Buffer(1228800);
                    CapturedImage.CopyToBuffer(buffer);
                    using (var Reader = DataReader.FromBuffer(buffer))
                    {
                        Reader.ReadBytes(DlibImageArray);
                    }

                    //将byte数组转换为Dlib可识别的数据
                    using (Array2D<RgbPixel> ImageData = Dlib.LoadImageData<RgbPixel>(ImagePixelFormat.Bgra, DlibImageArray, 480, 640, 2560))
                    {

                        //检测人脸并将嘴唇特征点提取并打包成点集
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

                                //调用OpenCVBridge提供的API，进行OpenCV处理
                                OpenCV.ApplyLipstickPrimaryMethod(CapturedImage, CapturedImage, PointsCollection, color);
                                PointsCollection.Clear();
                            }


                            await CaptureControl.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                            {
                                try
                                {
                                    //异步将处理完毕的图像刷新至Image控件
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
                                    //异步将不含有人脸的图像刷新至Image控件
                                    var task = ImageSource.SetBitmapAsync(CapturedImage);
                                }
                                catch (Exception) { }
                            });
                        }
                    }

                    //检测是否请求了任务取消，若请求了取消，则释放WaitForTaskComplete等待
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

        /// <summary>
        /// 检测图像中的所有人脸并返回特征点
        /// </summary>
        /// <param name="img">图像数据</param>
        /// <returns>包含特征点的类</returns>
        private List<FullObjectDetection> DlibFunction(Array2D<RgbPixel> img)
        {
            Rectangle[] dets = FaceDetector.Operator(img);
            List<FullObjectDetection> FaceLandMarkContainer = (from rect in dets let FaceLandMarkRawData = FaceModel.Detect(img, rect) where FaceLandMarkRawData.Parts > 2 select FaceLandMarkRawData).ToList();
            return FaceLandMarkContainer.Any() ? FaceLandMarkContainer : null;
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
