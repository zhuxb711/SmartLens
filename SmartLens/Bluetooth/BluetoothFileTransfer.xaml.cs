using Bluetooth.Services.Obex;
using System;
using System.IO;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


namespace SmartLens
{
    public sealed partial class BluetoothFileTransfer : ContentDialog
    {
        public Stream Filestream { private get; set; }
        public string FileName { private get; set; }
        private StorageFile DeleteQueue;
        private bool IsUserAbort = false;
        private ObexService ObexClient = ObexServiceProvider.ObexClient;
        public BluetoothFileTransfer()
        {
            InitializeComponent();
            Loaded += BluetoothFileTransfer_Loaded;
            Closing += BluetoothFileTransfer_Closing;
        }

        private async void BluetoothFileTransfer_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            if (SecondaryButtonText == "中止" || SecondaryButtonText == "重试")
            {
                args.Cancel = true;
            }
            else
            {
                await DeleteQueue.DeleteAsync(StorageDeleteOption.PermanentDelete);
                Filestream.Dispose();
                Filestream = null;
                DeleteQueue = null;
                ObexServiceProvider.Dispose();
            }
        }

        private async void BluetoothFileTransfer_Loaded(object sender, RoutedEventArgs e)
        {
            ObexClient.DataTransferFailed += async (s, arg) =>
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    ProgressText.Text += " \r传输意外中止";
                    SecondaryButtonText = "完成";
                });
            };
            ObexClient.DataTransferProgressed += async (s, arg) =>
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    ProgressControl.Value = arg.TransferInPercentage * 100;
                    ProgressText.Text = ((int)(arg.TransferInPercentage * 100)).ToString() + "%";
                });
            };
            ObexClient.DataTransferSucceeded += async (s, arg) =>
            {
                IsUserAbort = true;
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    ProgressControl.Value = 100;
                    ProgressText.Text = "100%" + " \r文件传输完成";
                    SecondaryButtonText = "完成";
                });
            };
            ObexClient.ConnectionFailed += async (s, arg) =>
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    ProgressText.Text = "0%" + "连接失败" + arg.ExceptionObject.Message;
                    ProgressControl.Value = 0;
                    SecondaryButtonText = "重试";
                });
            };
            ObexClient.Aborted += async (s, arg) =>
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    ProgressControl.Value = 0;
                    ProgressText.Text = "0%" + " \r文件传输中止";
                    SecondaryButtonText = "退出";
                });
            };
            ObexClient.Disconnected += async (s, arg) =>
            {
                if (IsUserAbort)
                {
                    IsUserAbort = false;
                    return;
                }
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    ProgressText.Text += " \r目标设备中止了文件传输";
                });
            };
            ObexClient.DeviceConnected += async (s, arg) =>
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Title = "正在传输中";
                });
                StorageFolder folder = ApplicationData.Current.TemporaryFolder;
                StorageFile file = await folder.CreateFileAsync(FileName, CreationCollisionOption.ReplaceExisting);
                byte[] bytes = new byte[Filestream.Length];
                Filestream.Read(bytes, 0, bytes.Length);
                Filestream.Seek(0, SeekOrigin.Begin);

                await FileIO.WriteBytesAsync(file, bytes);

                StorageFile fileOpen = await folder.GetFileAsync(FileName);
                await ObexClient.SendFileAsync(fileOpen);
                DeleteQueue = fileOpen;
            };

            await ObexClient.ConnectAsync();
        }

        private async void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (SecondaryButtonText == "中止")
            {
                IsUserAbort = true;
                await ObexClient.AbortAsync();
            }
            else if (SecondaryButtonText == "重试")
            {
                ProgressText.Text = 0 + "%";
                try
                {
                    await ObexClient.ConnectAsync();
                }
                catch (Exception) { }
            }
        }
    }
}
