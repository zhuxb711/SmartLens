using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.PointOfService;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Security.Cryptography;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace SmartLens
{
    public sealed partial class CodeScanner : Page
    {
        MediaCapture Capture;
        ClaimedBarcodeScanner ClaimedScanner;
        AutoResetEvent ExitLocker;
        ObservableCollection<BarcodeItem> BarcodeHistory;
        bool IsRunning;
        object SyncRoot;
        public CodeScanner()
        {
            InitializeComponent();
            Loaded += CodeScanner_Loaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            SyncRoot = new object();
            IsRunning = false;
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            using (ExitLocker)
            {
                await Task.Run(() =>
                {
                    ExitLocker.WaitOne();
                });
            }

            if (Capture != null)
            {
                using (Capture)
                {
                    await Capture.StopPreviewAsync();
                    PreviewControl.Source = null;
                }
            }

            if (ClaimedScanner != null)
            {
                using (ClaimedScanner)
                {
                    await ClaimedScanner.StopSoftwareTriggerAsync();
                    await ClaimedScanner.DisableAsync();
                    ClaimedScanner.DataReceived -= ClaimedScanner_DataReceived;
                }
            }
            BarcodeHistory.Clear();
            BarcodeHistory = null;
            SyncRoot = null;
            ExitLocker = null;
            Capture = null;
            ClaimedScanner = null;
        }

        private async void CodeScanner_Loaded(object sender, RoutedEventArgs e)
        {
            BarcodeList.ItemsSource = BarcodeHistory = new ObservableCollection<BarcodeItem>();
            ExitLocker = new AutoResetEvent(false);

            string Selector = BarcodeScanner.GetDeviceSelector(PosConnectionTypes.Local);
            DeviceInformationCollection DeviceCollection = await DeviceInformation.FindAllAsync(Selector);

            foreach (var DeviceID in from Device in DeviceCollection
                                     where Device.Name.Contains(ApplicationData.Current.RoamingSettings.Values["LastSelectedCameraSource"].ToString())
                                     select Device.Id)
            {
                using (BarcodeScanner Scanner = await BarcodeScanner.FromIdAsync(DeviceID))
                {
                    Capture = new MediaCapture();
                    var InitializeSettings = new MediaCaptureInitializationSettings
                    {
                        VideoDeviceId = Scanner.VideoDeviceId,
                        StreamingCaptureMode = StreamingCaptureMode.Video,
                        PhotoCaptureSource = PhotoCaptureSource.VideoPreview
                    };
                    await Capture.InitializeAsync(InitializeSettings);

                    var CameraFocusControl = Capture.VideoDeviceController.FocusControl;
                    if (CameraFocusControl.Supported)
                    {
                        await CameraFocusControl.UnlockAsync();
                        CameraFocusControl.Configure(new FocusSettings { Mode = FocusMode.Continuous, AutoFocusRange = AutoFocusRange.FullRange });
                        await CameraFocusControl.FocusAsync();
                    }
                    PreviewControl.Source = Capture;

                    ClaimedScanner = await Scanner.ClaimScannerAsync();
                    ClaimedScanner.IsDisabledOnDataReceived = false;
                    ClaimedScanner.IsDecodeDataEnabled = true;
                    ClaimedScanner.DataReceived += ClaimedScanner_DataReceived;
                    await ClaimedScanner.EnableAsync();
                    await ClaimedScanner.StartSoftwareTriggerAsync();

                    await Capture.StartPreviewAsync();
                    LoadingControl.IsLoading = false;
                }
            }
            ExitLocker.Set();
        }

        private async void ClaimedScanner_DataReceived(ClaimedBarcodeScanner sender, BarcodeScannerDataReceivedEventArgs args)
        {
            lock (SyncRoot)
            {
                if (IsRunning)
                {
                    return;
                }
                IsRunning = true;
            }

            string BarcodeType = BarcodeSymbologies.GetName(args.Report.ScanDataType).ToUpper();
            string BarcodeLabel = GetDataLabel(args);

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                ContentDialog dialog = new ContentDialog
                {
                    Content = BarcodeLabel,
                    Title = BarcodeType + "标签内容",
                    CloseButtonText = "确定"
                };
                await dialog.ShowAsync();

                BarcodeHistory.Add(new BarcodeItem(BarcodeType, BarcodeLabel));
            });

            IsRunning = false;
        }

        private string GetDataLabel(BarcodeScannerDataReceivedEventArgs args)
        {
            return args.Report.ScanDataLabel == null
                ? "数据不存在"
                : CryptographicBuffer.ConvertBinaryToString(BinaryStringEncoding.Utf8, args.Report.ScanDataLabel);
        }

        private void Hyperlink_Click(Windows.UI.Xaml.Documents.Hyperlink sender, Windows.UI.Xaml.Documents.HyperlinkClickEventArgs args)
        {
            string Url = ((args.OriginalSource as FrameworkElement)?.DataContext as BarcodeItem).DataLabel;
        }
    }

    public sealed class BarcodeItem
    {
        public string DataType { get; private set; }
        public string DataLabel { get; private set; }
        public Visibility TextVisibility { get; private set; }
        public Visibility HyperLinkVisibility { get; private set; }
        public BarcodeItem(string DataType, string DataLabel)
        {
            this.DataType = DataType;
            this.DataLabel = DataLabel;

            Regex RegexUrl = new Regex("(https?|ftp|file)://[-A-Za-z0-9+&@#/%?=~_|!:,.;]+[-A-Za-z0-9+&@#/%=~_|]");
            if (RegexUrl.IsMatch(DataLabel))
            {
                TextVisibility = Visibility.Collapsed;
                HyperLinkVisibility = Visibility.Visible;
            }
            else
            {
                TextVisibility = Visibility.Visible;
                HyperLinkVisibility = Visibility.Collapsed;
            }
        }
    }
}
