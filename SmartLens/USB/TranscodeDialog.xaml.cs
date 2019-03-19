using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace SmartLens
{
    public sealed partial class TranscodeDialog : ContentDialog
    {
        public StorageFile SourceFile { get; set; }
        MediaProcessingTrigger ProcessingTrigger;
        BackgroundTaskRegistration TaskRegistration;
        public TranscodeDialog()
        {
            InitializeComponent();
            Loaded += TranscodeDialog_Loaded;
        }

        private void TranscodeDialog_Loaded(object sender, RoutedEventArgs e)
        {
            switch (SourceFile.FileType)
            {
                case ".mp4":
                    Format.Items.Add("MKV(.mkv)格式");
                    Format.Items.Add("AVI(.avi)格式");
                    Format.Items.Add("WMV(.wmv)格式");
                    Quality.Items.Add("UHD2160p");
                    Quality.Items.Add("HD1080p");
                    Quality.Items.Add("HD720p");
                    Quality.Items.Add("WVGA");
                    Quality.Items.Add("VGA");
                    Quality.Items.Add("QVGA");
                    break;
                case ".mkv":
                    Format.Items.Add("MP4(.mp4)格式");
                    Format.Items.Add("AVI(.avi)格式");
                    Format.Items.Add("WMV(.wmv)格式");
                    Quality.Items.Add("UHD2160p");
                    Quality.Items.Add("HD1080p");
                    Quality.Items.Add("HD720p");
                    Quality.Items.Add("WVGA");
                    Quality.Items.Add("VGA");
                    Quality.Items.Add("QVGA");
                    break;
                case ".avi":
                    Format.Items.Add("MKV(.mkv)格式");
                    Format.Items.Add("MP4(.mp4)格式");
                    Format.Items.Add("WMV(.wmv)格式");
                    Quality.Items.Add("UHD2160p");
                    Quality.Items.Add("HD1080p");
                    Quality.Items.Add("HD720p");
                    Quality.Items.Add("WVGA");
                    Quality.Items.Add("VGA");
                    Quality.Items.Add("QVGA");
                    break;
                case ".wmv":
                    Format.Items.Add("MKV(.mkv)格式");
                    Format.Items.Add("MP4(.mp4)格式");
                    Format.Items.Add("AVI(.avi)格式");
                    Quality.Items.Add("UHD2160p");
                    Quality.Items.Add("HD1080p");
                    Quality.Items.Add("HD720p");
                    Quality.Items.Add("WVGA");
                    Quality.Items.Add("VGA");
                    Quality.Items.Add("QVGA");
                    break;
                case ".mov":
                    Format.Items.Add("MKV(.mkv)格式");
                    Format.Items.Add("MP4(.mp4)格式");
                    Format.Items.Add("AVI(.avi)格式");
                    Format.Items.Add("WMV(.wmv)格式");
                    Quality.Items.Add("UHD2160p");
                    Quality.Items.Add("HD1080p");
                    Quality.Items.Add("HD720p");
                    Quality.Items.Add("WVGA");
                    Quality.Items.Add("VGA");
                    Quality.Items.Add("QVGA");
                    break;
                case ".flac":
                    Format.Items.Add("ALAC(.alac)格式");
                    Format.Items.Add("AAC(.m4a)格式");
                    Format.Items.Add("MP3(.mp3)格式");
                    Format.Items.Add("WMA(.wma)格式");
                    Quality.Items.Add("High");
                    Quality.Items.Add("Medium");
                    Quality.Items.Add("Low");
                    break;
                case ".alac":
                    Format.Items.Add("AAC(.m4a)格式");
                    Format.Items.Add("MP3(.mp3)格式");
                    Format.Items.Add("WMA(.wma)格式");
                    Quality.Items.Add("High");
                    Quality.Items.Add("Medium");
                    Quality.Items.Add("Low");
                    break;
                case ".m4a":
                    Format.Items.Add("ALAC(.alac)格式");
                    Format.Items.Add("MP3(.mp3)格式");
                    Format.Items.Add("WMA(.wma)格式");
                    Quality.Items.Add("High");
                    Quality.Items.Add("Medium");
                    Quality.Items.Add("Low");
                    break;
                case ".mp3":
                    Format.Items.Add("FLAC(.flac)格式");
                    Format.Items.Add("ALAC(.alac)格式");
                    Format.Items.Add("AAC(.m4a)格式");
                    Format.Items.Add("WMA(.wma)格式");
                    Quality.Items.Add("High");
                    Quality.Items.Add("Medium");
                    Quality.Items.Add("Low");
                    break;
                case ".wma":
                    Format.Items.Add("ALAC(.alac)格式");
                    Format.Items.Add("AAC(.m4a)格式");
                    Format.Items.Add("MP3(.mp3)格式");
                    Quality.Items.Add("High");
                    Quality.Items.Add("Medium");
                    Quality.Items.Add("Low");
                    break;
                default:
                    throw new InvalidDataException("不受支持的格式");
            }
            Format.SelectedIndex = 0;
            Quality.SelectedIndex = 0;
        }

        private void Quality_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

            switch (Quality.SelectedItem as string)
            {
                case "UHD2160p":
                    Info.Text = "分辨率3840 X 2160，30FPS";
                    break;
                case "HD1080p":
                    Info.Text = "分辨率1920 X 1080，30FPS";
                    break;
                case "HD720p":
                    Info.Text = "分辨率1280 X 720，30FPS";
                    break;
                case "WVGA":
                    Info.Text = "分辨率800 X 480，30FPS";
                    break;
                case "VGA":
                    Info.Text = "分辨率640 X 480，30FPS";
                    break;
                case "QVGA":
                    Info.Text = "分辨率320 X 240，30FPS";
                    break;
                case "High":
                    Info.Text = "比特率192kbps，采样率48khz";
                    break;
                case "Medium":
                    Info.Text = "比特率128kbps，采样率44.1khz";
                    break;
                case "Low":
                    Info.Text = "比特率96kbps，采样率44.1khz";
                    break;
            }
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (SourceFile == null)
            {
                throw new InvalidCastException("转码源文件未正确设置");
            }

            await SetMediaTranscodeConfig();
            RegisterMediaTranscodeBackgroundTask();
            await LaunchMediaTranscodeBackgroundTaskAsync();
        }

        private async Task SetMediaTranscodeConfig()
        {
            switch (Format.SelectedItem as string)
            {
                case "MKV(.mkv)格式":
                    ApplicationData.Current.LocalSettings.Values["MediaTranscodeEncodingProfile"] = "MKV";
                    break;
                case "AVI(.avi)格式":
                    ApplicationData.Current.LocalSettings.Values["MediaTranscodeEncodingProfile"] = "AVI";
                    break;
                case "WMV(.wmv)格式":
                    ApplicationData.Current.LocalSettings.Values["MediaTranscodeEncodingProfile"] = "WMV";
                    break;
                case "MP4(.mp4)格式":
                    ApplicationData.Current.LocalSettings.Values["MediaTranscodeEncodingProfile"] = "MP4";
                    break;
                case "ALAC(.alac)格式":
                    ApplicationData.Current.LocalSettings.Values["MediaTranscodeEncodingProfile"] = "ALAC";
                    break;
                case "AAC(.m4a)格式":
                    ApplicationData.Current.LocalSettings.Values["MediaTranscodeEncodingProfile"] = "M4A";
                    break;
                case "WMA(.wma)格式":
                    ApplicationData.Current.LocalSettings.Values["MediaTranscodeEncodingProfile"] = "WMA";
                    break;
                case "MP3(.mp3)格式":
                    ApplicationData.Current.LocalSettings.Values["MediaTranscodeEncodingProfile"] = "MP3";
                    break;
            }

            ApplicationData.Current.LocalSettings.Values["MediaTranscodeQuality"] = Quality.SelectedItem as string;

            var FutureItemAccessList = StorageApplicationPermissions.FutureAccessList;
            ApplicationData.Current.LocalSettings.Values["MediaTranscodeInputFileToken"] = FutureItemAccessList.Add(SourceFile);

            string Type = ApplicationData.Current.LocalSettings.Values["MediaTranscodeEncodingProfile"] as string;
            StorageFile DestinationFile = await USBControl.ThisPage.CurrentFolder.CreateFileAsync(SourceFile.DisplayName + "." + Type.ToLower(), CreationCollisionOption.ReplaceExisting);

            ApplicationData.Current.LocalSettings.Values["MediaTranscodeOutputFileToken"] = FutureItemAccessList.Add(DestinationFile);
            ApplicationData.Current.LocalSettings.Values["MediaTranscodeAlgorithm"] = (bool)SpeedUpEnable.IsChecked ? "Default" : "MrfCrf444";

        }

        private async Task LaunchMediaTranscodeBackgroundTaskAsync()
        {
            bool success = true;

            if (ProcessingTrigger != null)
            {
                MediaProcessingTriggerResult ActivationResult = await ProcessingTrigger.RequestAsync();

                switch (ActivationResult)
                {
                    case MediaProcessingTriggerResult.Allowed:
                        break;
                    case MediaProcessingTriggerResult.CurrentlyRunning:

                    case MediaProcessingTriggerResult.DisabledByPolicy:

                    case MediaProcessingTriggerResult.UnknownError:
                        success = false;
                        break;
                }

                if (!success)
                {
                    TaskRegistration.Unregister(true);
                }
            }

        }

        private void RegisterMediaTranscodeBackgroundTask()
        {
            ProcessingTrigger = new MediaProcessingTrigger();

            BackgroundTaskBuilder TaskBuilder = new BackgroundTaskBuilder
            {
                Name = "TranscodingBackgroundTask",
                TaskEntryPoint = "MediaProcessingBackgroundTask.MediaProcessingTask"
            };
            TaskBuilder.SetTrigger(ProcessingTrigger);

            foreach (var RegistedTask in from RegistedTask in BackgroundTaskRegistration.AllTasks
                                         where RegistedTask.Value.Name == "TranscodingBackgroundTask"
                                         select RegistedTask)
            {
                RegistedTask.Value.Unregister(true);
            }

            TaskRegistration = TaskBuilder.Register();
            TaskRegistration.Completed += TaskRegistration_Completed;
        }

        private async void TaskRegistration_Completed(BackgroundTaskRegistration sender, BackgroundTaskCompletedEventArgs args)
        {
            TaskRegistration.Completed -= TaskRegistration_Completed;
            sender.Unregister(true);
            if (ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] is string ExcuteStatus)
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "提示"
                    };

                    if (ExcuteStatus == "Success")
                    {
                        dialog.Content = "转码成功完成";
                        dialog.CloseButtonText = "知道了";
                        await dialog.ShowAsync();
                    }
                    else
                    {
                        dialog.Content = "转码失败，原因如下\r\r" + ExcuteStatus;
                        dialog.CloseButtonText = "确定";
                        await dialog.ShowAsync();
                    }
                });
            }
        }

        public T FindChildOfType<T>(DependencyObject root) where T : class
        {
            var MyQueue = new Queue<DependencyObject>();
            MyQueue.Enqueue(root);
            while (MyQueue.Count > 0)
            {
                DependencyObject current = MyQueue.Dequeue();
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(current); i++)
                {
                    var child = VisualTreeHelper.GetChild(current, i);
                    var typedChild = child as T;
                    if (typedChild != null)
                    {
                        return typedChild;
                    }
                    MyQueue.Enqueue(child);
                }
            }
            return null;
        }
    }
}
