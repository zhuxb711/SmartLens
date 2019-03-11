using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;


namespace SmartLens
{
    public sealed partial class USBFilePresenter : Page
    {
        public ObservableCollection<RemovableDeviceFile> FileCollection;
        public static USBFilePresenter ThisPage { get; private set; }
        Queue<StorageFile> CopyedQueue;
        Queue<StorageFile> CutQueue;
        AutoResetEvent AESControl;
        DispatcherTimer Ticker;
        Frame Nav;
        Queue<StorageFile> AddToZipQueue;
        const int AESCacheSize = 1048576;
        object Locker;
        byte[] EncryptByteBuffer;
        byte[] DecryptByteBuffer;


        public USBFilePresenter()
        {
            InitializeComponent();
            ThisPage = this;
            FileCollection = new ObservableCollection<RemovableDeviceFile>();
            GridViewControl.ItemsSource = FileCollection;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ZipStrings.CodePage = 936;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Nav = e.Parameter as Frame;
            CopyedQueue = new Queue<StorageFile>();
            CutQueue = new Queue<StorageFile>();
            AddToZipQueue = new Queue<StorageFile>();
            AESControl = new AutoResetEvent(false);
            Locker = new object();
            Ticker = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            Ticker.Tick += (s, v) =>
            {
                ProgressInfo.Text = ProgressInfo.Text + "\r文件较大，请耐心等待...";
                Ticker.Stop();
            };
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            CopyedQueue = null;
            CutQueue = null;
            AddToZipQueue = null;
            AESControl?.Dispose();
            Ticker?.Stop();
            Ticker = null;
        }

        public async Task<string> GetSize(StorageFile file)
        {
            BasicProperties Properties = await file.GetBasicPropertiesAsync();
            return Properties.Size / 1024 < 1024 ? Math.Round(Properties.Size / 1024f, 2).ToString() + " KB" :
            (Properties.Size / 1048576 >= 1024 ? Math.Round(Properties.Size / 1073741824f, 2).ToString() + " GB" :
            Math.Round(Properties.Size / 1048576f, 2).ToString() + " MB");
        }

        private void Restore()
        {
            CommandsFlyout.Hide();
            if (GridViewControl.SelectionMode != ListViewSelectionMode.Single)
            {
                GridViewControl.SelectionMode = ListViewSelectionMode.Single;
            }
        }
        private void MulSelection_Click(object sender, RoutedEventArgs e)
        {
            CommandsFlyout.Hide();
            if (GridViewControl.SelectionMode != ListViewSelectionMode.Multiple)
            {
                GridViewControl.SelectionMode = ListViewSelectionMode.Multiple;
            }
            else
            {
                GridViewControl.SelectionMode = ListViewSelectionMode.Single;
            }
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (CopyedQueue.Count != 0)
            {
                CopyedQueue.Clear();
            }
            foreach (RemovableDeviceFile item in GridViewControl.SelectedItems)
            {
                CopyedQueue.Enqueue(item.File);
            }
            Paste.IsEnabled = true;
            if (CutQueue.Count != 0)
            {
                CutQueue.Clear();
            }
            Restore();
        }

        private async void Paste_Click(object sender, RoutedEventArgs e)
        {
            Restore();
            if (CutQueue.Count != 0)
            {
                LoadingActivation(true, "正在剪切");
                Queue<string> ErrorCollection = new Queue<string>();

                while (CutQueue.Count != 0)
                {
                    var CutFile = CutQueue.Dequeue();
                    try
                    {
                        await CutFile.MoveAsync(USBControl.ThisPage.CurrentFolder, CutFile.Name, NameCollisionOption.GenerateUniqueName);
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        ErrorCollection.Enqueue(CutFile.Name);
                    }
                }

                if (ErrorCollection.Count != 0)
                {
                    string temp = "";
                    while (ErrorCollection.Count != 0)
                    {
                        temp = temp + ErrorCollection.Dequeue() + "\r";
                    }
                    ContentDialog contentDialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "因设备剩余空间大小不足\r以下文件无法剪切：\r" + temp,
                        CloseButtonText = "确定",
                        Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                    };
                    LoadingActivation(false);
                    await contentDialog.ShowAsync();
                }

                RefreshFileDisplay();
                await Task.Delay(500);
                LoadingActivation(false);
                Paste.IsEnabled = false;
            }
            else if (CopyedQueue.Count != 0)
            {

                LoadingActivation(true, "正在复制");
                Queue<string> ErrorCollection = new Queue<string>();
                while (CopyedQueue.Count != 0)
                {
                    var CopyedFile = CopyedQueue.Dequeue();
                    try
                    {
                        await CopyedFile.CopyAsync(USBControl.ThisPage.CurrentFolder, CopyedFile.Name, NameCollisionOption.GenerateUniqueName);
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        ErrorCollection.Enqueue(CopyedFile.Name);
                    }
                }
                if (ErrorCollection.Count != 0)
                {
                    string temp = "";
                    while (ErrorCollection.Count != 0)
                    {
                        temp = temp + ErrorCollection.Dequeue() + "\r";
                    }
                    ContentDialog contentDialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "因设备剩余空间大小不足\r以下文件无法复制：\r\r" + temp,
                        CloseButtonText = "确定",
                        Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                    };
                    LoadingActivation(false);
                    await contentDialog.ShowAsync();
                }
                RefreshFileDisplay();
                await Task.Delay(500);
                LoadingActivation(false);
            }
            Paste.IsEnabled = false;
        }

        private async void RefreshFileDisplay()
        {
            var FileList = await USBControl.ThisPage.CurrentFolder.GetFilesAsync();
            foreach (var file in FileList)
            {
                int i = 0;
                for (; i < FileCollection.Count; i++)
                {
                    if (FileCollection[i].Name == file.Name)
                    {
                        break;
                    }
                }
                if (i == FileCollection.Count)
                {
                    var Thumbnail = await GetThumbnail(file);
                    if (Thumbnail != null)
                    {
                        FileCollection.Add(new RemovableDeviceFile(await GetSize(file), file, Thumbnail));
                        if (file.FileType == ".zip")
                        {
                            await Task.Delay(200);
                            GridViewItem item = (GridViewControl.ContainerFromIndex(FileCollection.Count - 1) as GridViewItem);
                            item.AllowDrop = true;
                            item.Drop += USBControl.ThisPage.USBControl_Drop;
                            item.DragOver += USBControl.ThisPage.Item_DragOver;
                        }
                    }
                    else
                    {
                        FileCollection.Add(new RemovableDeviceFile(await GetSize(file), file, new BitmapImage(new Uri("ms-appx:///Assets/DocIcon.png")) { DecodePixelHeight = 60, DecodePixelWidth = 60 }));
                    }
                }
            }
        }

        private void Cut_Click(object sender, RoutedEventArgs e)
        {
            if (CutQueue.Count != 0)
            {
                CutQueue.Clear();
            }
            foreach (RemovableDeviceFile item in GridViewControl.SelectedItems)
            {
                CutQueue.Enqueue(item.File);
            }
            Paste.IsEnabled = true;
            if (CopyedQueue.Count != 0)
            {
                CopyedQueue.Clear();
            }
            Restore();
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var FileList = new List<object>(GridViewControl.SelectedItems);
            Restore();
            ContentDialog contentDialog = new ContentDialog
            {
                Title = "警告",
                PrimaryButtonText = "是",
                CloseButtonText = "否",
                Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
            };

            if (FileList.Count == 1)
            {
                contentDialog.Content = "此操作将永久删除 \"" + (FileList[0] as RemovableDeviceFile).Name + " \"\r\r是否继续?";
            }
            else
            {
                contentDialog.Content = "此操作将永久删除 \"" + (FileList[0] as RemovableDeviceFile).Name + "\" 等" + FileList.Count + "个文件\r\r是否继续?";
            }
            if (await contentDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                LoadingActivation(true, "正在删除");
                foreach (var item in FileList)
                {
                    var file = (item as RemovableDeviceFile).File;
                    await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    for (int i = 0; i < FileCollection.Count; i++)
                    {
                        if (FileCollection[i].Name == file.Name)
                        {
                            if (file.FileType == ".zip")
                            {
                                GridViewItem GridItem = GridViewControl.ContainerFromItem(item) as GridViewItem;
                                GridItem.Drop -= USBControl.ThisPage.USBControl_Drop;
                                GridItem.DragOver -= USBControl.ThisPage.Item_DragOver;
                            }

                            FileCollection.RemoveAt(i);
                            i--;
                        }
                    }
                }
                await Task.Delay(500);
                LoadingActivation(false);
            }
        }

        private void LoadingActivation(bool IsLoading, string Info = null, bool EnableProgressDisplay = false)
        {
            if (IsLoading)
            {
                if (EnableProgressDisplay)
                {
                    ProRing.Visibility = Visibility.Collapsed;
                    ProBar.Visibility = Visibility.Visible;
                    ProgressInfo.Text = Info + "...0%";
                }
                else
                {
                    ProRing.Visibility = Visibility.Visible;
                    ProBar.Visibility = Visibility.Collapsed;
                    ProgressInfo.Text = Info + "...";
                    Ticker.Start();
                }
            }
            else
            {
                if (!EnableProgressDisplay)
                {
                    Ticker.Stop();
                }
            }
            LoadingControl.IsLoading = IsLoading;
        }

        private async void Rename_Click(object sender, RoutedEventArgs e)
        {
            if (GridViewControl.SelectedItems.Count > 1)
            {
                Restore();
                ContentDialog content = new ContentDialog
                {
                    Title = "错误",
                    Content = "无法同时重命名多个文件",
                    CloseButtonText = "确定",
                    Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                };
                await content.ShowAsync();
                return;
            }

            var file = (GridViewControl.SelectedItem as RemovableDeviceFile).File;
            RenameDialog dialog = new RenameDialog(file.DisplayName, file.FileType);
            if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
            {
                if (dialog.DesireName == file.FileType)
                {
                    ContentDialog content = new ContentDialog
                    {
                        Title = "错误",
                        Content = "文件名不能为空，重命名失败",
                        CloseButtonText = "确定",
                        Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                    };
                    await content.ShowAsync();
                    return;
                }
                await file.RenameAsync(dialog.DesireName, NameCollisionOption.GenerateUniqueName);
            }
            else
            {
                return;
            }
            for (int i = 0; i < FileCollection.Count; i++)
            {
                if (FileCollection[i].Name == dialog.DesireName)
                {
                    FileCollection[i].NameUpdateRequested();
                }
            }
        }

        private async void AES_Click(object sender, RoutedEventArgs e)
        {
            var FileList = new List<object>(GridViewControl.SelectedItems);
            Restore();
            string CheckSame = ".sle";
            int CheckCount = 0;
            for (int i = 0; i < FileList.Count; i++)
            {
                if (((RemovableDeviceFile)FileList[i]).File.FileType == CheckSame)
                {
                    CheckCount++;
                }
            }
            if (CheckCount != FileList.Count && CheckCount != 0)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "错误",
                    Content = "  同时加密或解密多个文件时，.sle文件不能与其他文件混杂\r\r  允许的组合如下：\r\r      • 全部为.sle文件\r\r      • 全部为非.sln文件",
                    CloseButtonText = "确定",
                    Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                };
                await dialog.ShowAsync();
                return;
            }
            foreach (RemovableDeviceFile AESFile in FileList)
            {
                StorageFile SelectedFile = AESFile.File;
                int KeySizeRequest;
                string KeyRequest;
                bool IsDeleteRequest;

                if (SelectedFile.FileType != ".sle")
                {
                    AESDialog Dialog = new AESDialog(true, SelectedFile.Name);
                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        KeyRequest = Dialog.Key;
                        IsDeleteRequest = Dialog.IsDeleteChecked;
                        KeySizeRequest = Dialog.KeySize;
                    }
                    else
                    {
                        LoadingActivation(false);
                        DecryptByteBuffer = null;
                        EncryptByteBuffer = null;
                        return;
                    }
                    LoadingActivation(true, "正在加密");
                    await Task.Run(async () =>
                    {
                        using (var FileStream = await SelectedFile.OpenStreamForReadAsync())
                        {
                            StorageFile file = await USBControl.ThisPage.CurrentFolder.CreateFileAsync(SelectedFile.DisplayName + ".sle", CreationCollisionOption.GenerateUniqueName);
                            using (var TargetFileStream = await file.OpenStreamForWriteAsync())
                            {
                                byte[] Tail = Encoding.UTF8.GetBytes("$" + KeySizeRequest + "|" + SelectedFile.FileType + "$");
                                byte[] PasswordFlag = Encoding.UTF8.GetBytes("PASSWORD_CORRECT");

                                if (FileStream.Length < AESCacheSize)
                                {
                                    EncryptByteBuffer = new byte[FileStream.Length];
                                    FileStream.Read(EncryptByteBuffer, 0, EncryptByteBuffer.Length);
                                    await TargetFileStream.WriteAsync(Tail, 0, Tail.Length);
                                    await TargetFileStream.WriteAsync(AESProvider.EncryptForUSB(PasswordFlag, KeyRequest, KeySizeRequest), 0, PasswordFlag.Length);
                                    var EncryptedBytes = AESProvider.EncryptForUSB(EncryptByteBuffer, KeyRequest, KeySizeRequest);
                                    await TargetFileStream.WriteAsync(EncryptedBytes, 0, EncryptedBytes.Length);
                                }
                                else
                                {
                                    EncryptByteBuffer = new byte[Tail.Length];
                                    await TargetFileStream.WriteAsync(Tail, 0, Tail.Length);
                                    await TargetFileStream.WriteAsync(AESProvider.EncryptForUSB(PasswordFlag, KeyRequest, KeySizeRequest), 0, PasswordFlag.Length);

                                    long BytesWrite = 0;
                                    EncryptByteBuffer = new byte[AESCacheSize];
                                    while (BytesWrite < FileStream.Length)
                                    {
                                        if (FileStream.Length - BytesWrite < AESCacheSize)
                                        {
                                            if (FileStream.Length - BytesWrite == 0)
                                            {
                                                break;
                                            }
                                            EncryptByteBuffer = new byte[FileStream.Length - BytesWrite];
                                        }

                                        BytesWrite += FileStream.Read(EncryptByteBuffer, 0, EncryptByteBuffer.Length);
                                        var EncryptedBytes = AESProvider.EncryptForUSB(EncryptByteBuffer, KeyRequest, KeySizeRequest);
                                        await TargetFileStream.WriteAsync(EncryptedBytes, 0, EncryptedBytes.Length);
                                    }

                                }
                            }
                        }
                    });
                }
                else
                {
                    AESDialog Dialog = new AESDialog(false, SelectedFile.Name);
                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        KeyRequest = Dialog.Key;
                        IsDeleteRequest = Dialog.IsDeleteChecked;
                    }
                    else
                    {
                        LoadingActivation(false);
                        DecryptByteBuffer = null;
                        EncryptByteBuffer = null;
                        return;
                    }

                    LoadingActivation(true, "正在解密");
                    await Task.Run(async () =>
                    {
                        using (var FileStream = await SelectedFile.OpenStreamForReadAsync())
                        {
                            string FileType;
                            byte[] DecryptedBytes;
                            int SignalLength = 0;
                            int EncryptKeySize = 0;

                            DecryptByteBuffer = new byte[20];
                            FileStream.Read(DecryptByteBuffer, 0, DecryptByteBuffer.Length);
                            try
                            {
                                if (Encoding.UTF8.GetString(DecryptByteBuffer, 0, 1) != "$")
                                {
                                    throw new Exception("文件格式错误");
                                }
                            }
                            catch (Exception)
                            {
                                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                                {
                                    ContentDialog dialog = new ContentDialog
                                    {
                                        Title = "错误",
                                        Content = "  文件格式检验错误，文件可能已损坏",
                                        CloseButtonText = "确定",
                                        Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                                    };
                                    LoadingActivation(false);
                                    DecryptByteBuffer = null;
                                    EncryptByteBuffer = null;
                                    await dialog.ShowAsync();
                                });

                                return;
                            }
                            StringBuilder builder = new StringBuilder();
                            for (int i = 1; ; i++)
                            {
                                string temp = Encoding.UTF8.GetString(DecryptByteBuffer, i, 1);
                                if (temp == "|")
                                {
                                    EncryptKeySize = int.Parse(builder.ToString());
                                    KeyRequest = KeyRequest.PadRight(EncryptKeySize / 8, '0');
                                    builder.Clear();
                                    continue;
                                }
                                if (temp != "$")
                                {
                                    builder.Append(temp);
                                }
                                else
                                {
                                    SignalLength = i + 1;
                                    break;
                                }
                            }
                            FileType = builder.ToString();
                            FileStream.Seek(SignalLength, SeekOrigin.Begin);

                            byte[] PasswordConfirm = new byte[16];
                            await FileStream.ReadAsync(PasswordConfirm, 0, PasswordConfirm.Length);
                            if (Encoding.UTF8.GetString(AESProvider.DecryptForUSB(PasswordConfirm, KeyRequest, EncryptKeySize)) != "PASSWORD_CORRECT")
                            {
                                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                                {
                                    ContentDialog dialog = new ContentDialog
                                    {
                                        Title = "错误",
                                        Content = "  密码错误，无法解密\r\r  请重试...",
                                        CloseButtonText = "确定",
                                        Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                                    };
                                    LoadingActivation(false);
                                    DecryptByteBuffer = null;
                                    EncryptByteBuffer = null;
                                    await dialog.ShowAsync();
                                    AESControl.Set();
                                });
                                AESControl.WaitOne();
                                return;
                            }
                            else
                            {
                                SignalLength += 16;
                            }

                            if (FileStream.Length - SignalLength < AESCacheSize)
                            {
                                DecryptByteBuffer = new byte[FileStream.Length - SignalLength];
                            }
                            else
                            {
                                DecryptByteBuffer = new byte[AESCacheSize];
                            }
                            FileStream.Read(DecryptByteBuffer, 0, DecryptByteBuffer.Length);
                            DecryptedBytes = AESProvider.DecryptForUSB(DecryptByteBuffer, KeyRequest, EncryptKeySize);

                            StorageFile file = await USBControl.ThisPage.CurrentFolder.CreateFileAsync(SelectedFile.DisplayName + FileType, CreationCollisionOption.GenerateUniqueName);
                            using (var TargetFileStream = await file.OpenStreamForWriteAsync())
                            {
                                await TargetFileStream.WriteAsync(DecryptedBytes, 0, DecryptedBytes.Length);

                                if (FileStream.Length - SignalLength >= AESCacheSize)
                                {
                                    long BytesRead = DecryptByteBuffer.Length + SignalLength;
                                    while (BytesRead < FileStream.Length)
                                    {
                                        if (FileStream.Length - BytesRead < AESCacheSize)
                                        {
                                            if (FileStream.Length - BytesRead == 0)
                                            {
                                                break;
                                            }
                                            DecryptByteBuffer = new byte[FileStream.Length - BytesRead];
                                        }
                                        BytesRead += FileStream.Read(DecryptByteBuffer, 0, DecryptByteBuffer.Length);
                                        DecryptedBytes = AESProvider.DecryptForUSB(DecryptByteBuffer, KeyRequest, EncryptKeySize);
                                        await TargetFileStream.WriteAsync(DecryptedBytes, 0, DecryptedBytes.Length);
                                    }

                                }
                            }
                        }
                    });
                }

                if (IsDeleteRequest)
                {
                    await SelectedFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    for (int i = 0; i < FileCollection.Count; i++)
                    {
                        if (FileCollection[i].DisplayName == SelectedFile.DisplayName)
                        {
                            FileCollection.Remove(FileCollection[i]);
                            break;
                        }
                    }
                }
                DecryptByteBuffer = null;
                EncryptByteBuffer = null;
            }
            await Task.Delay(500);
            LoadingActivation(false);
            RefreshFileDisplay();
        }

        private async void BluetoothShare_Click(object sender, RoutedEventArgs e)
        {
            List<object> FileList = new List<object>(GridViewControl.SelectedItems);
            Restore();
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                foreach (RemovableDeviceFile file in FileList)
                {
                    BluetoothUI Bluetooth = new BluetoothUI();
                    var result = await Bluetooth.ShowAsync();
                    if (result == ContentDialogResult.Secondary)
                    {
                        return;
                    }
                    else if (result == ContentDialogResult.Primary)
                    {
                        BluetoothFileTransfer FileTransfer = new BluetoothFileTransfer
                        {
                            Filestream = (await file.File.OpenAsync(FileAccessMode.Read)).AsStream(),
                            FileName = file.File.Name
                        };
                        await FileTransfer.ShowAsync();
                    }
                }
            });
        }

        private void GridViewControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            lock (Locker)
            {
                if (GridViewControl.SelectedIndex == -1)
                {
                    Copy.IsEnabled = false;
                    Cut.IsEnabled = false;
                    Rename.IsEnabled = false;
                    AES.IsEnabled = false;
                    Delete.IsEnabled = false;
                }
                else
                {
                    Copy.IsEnabled = true;
                    Cut.IsEnabled = true;
                    Rename.IsEnabled = true;
                    AES.IsEnabled = true;
                    AES.Label = "AES加密";
                    foreach (RemovableDeviceFile item in e.AddedItems)
                    {
                        if (item.File.FileType == ".sle")
                        {
                            AES.Label = "AES解密";
                            break;
                        }
                    }
                    Delete.IsEnabled = true;
                }
            }
        }

        private void GridViewControl_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (GridViewControl.SelectedItems.Count <= 1)
            {
                var Context = (e.OriginalSource as FrameworkElement)?.DataContext as RemovableDeviceFile;
                GridViewControl.SelectedIndex = FileCollection.IndexOf(Context);
                e.Handled = true;
            }
        }


        public async Task<BitmapImage> GetThumbnail(StorageFile file)
        {
            var Thumbnail = await file.GetThumbnailAsync(ThumbnailMode.ListView);
            if (Thumbnail == null)
            {
                return null;
            }
            BitmapImage bitmapImage = new BitmapImage
            {
                DecodePixelHeight = 70,
                DecodePixelWidth = 70
            };
            bitmapImage.SetSource(Thumbnail);
            return bitmapImage;
        }

        private void GridViewControl_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if (GridViewControl.SelectedItems.Count <= 1)
            {
                var Context = (e.OriginalSource as FrameworkElement)?.DataContext as RemovableDeviceFile;
                GridViewControl.SelectedIndex = FileCollection.IndexOf(Context);
                e.Handled = true;
            }
        }

        private async void Attribute_Click(object sender, RoutedEventArgs e)
        {
            var SelectedGroup = GridViewControl.SelectedItems;
            if (SelectedGroup.Count != 1)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "错误",
                    Content = "仅允许查看单个文件属性，请重试",
                    CloseButtonText = "确定",
                    Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                };
                await dialog.ShowAsync();
            }
            else
            {
                AttributeDialog Dialog = new AttributeDialog((SelectedGroup[0] as RemovableDeviceFile).File);
                await Dialog.ShowAsync();
            }
        }

        private async void Zip_Click(object sender, RoutedEventArgs e)
        {
            List<object> FileList = new List<object>(GridViewControl.SelectedItems);
            Restore();

            int CheckCount = 0;
            for (int i = 0; i < FileList.Count; i++)
            {
                if (((RemovableDeviceFile)FileList[i]).File.FileType == ".zip")
                {
                    CheckCount++;
                }
            }
            if (CheckCount == FileList.Count)
            {
                await UnZip(FileList);
            }
            else
            {
                ZipDialog dialog;
                if (FileList.Count == 1)
                {
                    dialog = new ZipDialog(true, (FileList[0] as RemovableDeviceFile).DisplayName);
                }
                else
                {
                    dialog = new ZipDialog(true);
                }
                if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    LoadingActivation(true, "正在压缩", true);
                    if (dialog.IsCryptionEnable)
                    {
                        await CreateZip(FileList, dialog.FileName, (int)dialog.Level, true, dialog.Key, dialog.Password);
                    }
                    else
                    {
                        await CreateZip(FileList, dialog.FileName, (int)dialog.Level);
                    }
                    RefreshFileDisplay();
                }
                else
                {
                    return;
                }
            }
            LoadingActivation(false);
        }

        private async Task UnZip(List<object> ZFileList)
        {
            foreach (RemovableDeviceFile ZFile in ZFileList)
            {
                StorageFolder NewFolder = null;
                using (var ZipFileStream = await ZFile.File.OpenStreamForReadAsync())
                {
                    ZipFile zipFile = new ZipFile(ZipFileStream);
                    try
                    {
                        if (zipFile[0].IsCrypted)
                        {
                            ZipDialog dialog = new ZipDialog(false);
                            if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                            {
                                LoadingActivation(true, "正在解压", true);
                                zipFile.Password = dialog.Password;
                            }
                            else
                            {
                                break;
                            }
                        }
                        else
                        {
                            LoadingActivation(true, "正在解压", true);
                        }
                        await Task.Run(async () =>
                        {
                            object Lock = new object();
                            int HCounter = 0, TCounter = 0, RepeatFilter = -1;
                            foreach (ZipEntry Entry in zipFile)
                            {
                                if (!Entry.IsFile)
                                {
                                    continue;
                                }
                                using (Stream ZipTempStream = zipFile.GetInputStream(Entry))
                                {
                                    NewFolder = await USBControl.ThisPage.CurrentFolder.CreateFolderAsync(ZFile.File.DisplayName, CreationCollisionOption.OpenIfExists);
                                    StorageFile NewFile = await NewFolder.CreateFileAsync(Entry.Name, CreationCollisionOption.ReplaceExisting);
                                    using (Stream stream = await NewFile.OpenStreamForWriteAsync())
                                    {
                                        double FileSize = Entry.Size;
                                        StreamUtils.Copy(ZipTempStream, stream, new byte[4096], async (s, e) =>
                                        {
                                            await LoadingControl.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                                            {
                                                lock (Lock)
                                                {
                                                    string temp = ProgressInfo.Text.Remove(ProgressInfo.Text.LastIndexOf('.') + 1);
                                                    TCounter = Convert.ToInt32((e.Processed / FileSize) * 100);
                                                    if (RepeatFilter == TCounter)
                                                    {
                                                        return;
                                                    }
                                                    else
                                                    {
                                                        RepeatFilter = TCounter;
                                                    }

                                                    int CurrentProgress = Convert.ToInt32((HCounter + TCounter) / ((double)zipFile.Count));
                                                    ProgressInfo.Text = temp + CurrentProgress + "%";
                                                    ProBar.Value = CurrentProgress;

                                                    if (TCounter == 100)
                                                    {
                                                        HCounter += 100;
                                                    }
                                                }
                                            });

                                        }, TimeSpan.FromMilliseconds(100), null, string.Empty);
                                    }
                                }
                            }
                        });
                    }
                    catch (Exception e)
                    {
                        ContentDialog dialog = new ContentDialog
                        {
                            Title = "错误",
                            Content = "解压文件时发生异常\r\r错误信息：\r\r" + e.Message,
                            Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush,
                            CloseButtonText = "确定"
                        };
                        await dialog.ShowAsync();
                        break;
                    }
                    finally
                    {
                        zipFile.IsStreamOwner = false;
                        zipFile.Close();
                    }
                }
                string RelativeId = (USBControl.ThisPage.CurrentNode.Content as StorageFolder).FolderRelativeId;

                foreach (var item in USBControl.ThisPage.CurrentNode.Children)
                {
                    if ((item.Content as StorageFolder).FolderRelativeId == NewFolder.FolderRelativeId)
                    {
                        goto JUMP;
                    }
                }

                USBControl.ThisPage.CurrentNode.Children.Add(new TreeViewNode
                {
                    Content = await USBControl.ThisPage.CurrentFolder.GetFolderAsync(NewFolder.Name),
                    HasUnrealizedChildren = false
                });
                USBControl.ThisPage.FolderDictionary[RelativeId].Add(NewFolder);
            JUMP: continue;
            }
        }

        private async Task CreateZip(List<object> FileList, string NewZipName, int ZipLevel, bool EnableCryption = false, KeySize Size = KeySize.None, string Password = null)
        {
            var Newfile = await USBControl.ThisPage.CurrentFolder.CreateFileAsync(NewZipName, CreationCollisionOption.GenerateUniqueName);
            using (var NewFileStream = await Newfile.OpenStreamForWriteAsync())
            {
                ZipOutputStream ZipStream = new ZipOutputStream(NewFileStream);
                try
                {
                    ZipStream.SetLevel(ZipLevel);
                    ZipStream.UseZip64 = UseZip64.Off;
                    object Lock = new object();
                    int HCounter = 0, TCounter = 0, RepeatFilter = -1;
                    if (EnableCryption)
                    {
                        ZipStream.Password = Password;
                        await Task.Run(async () =>
                        {
                            foreach (RemovableDeviceFile ZipFile in FileList)
                            {
                                ZipEntry NewEntry = new ZipEntry(ZipFile.File.Name)
                                {
                                    DateTime = DateTime.Now,
                                    AESKeySize = (int)Size,
                                    IsCrypted = true,
                                    CompressionMethod = CompressionMethod.Deflated
                                };

                                ZipStream.PutNextEntry(NewEntry);
                                using (Stream stream = await ZipFile.File.OpenStreamForReadAsync())
                                {
                                    StreamUtils.Copy(stream, ZipStream, new byte[4096], async (s, e) =>
                                    {
                                        await LoadingControl.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                                        {
                                            lock (Lock)
                                            {
                                                string temp = ProgressInfo.Text.Remove(ProgressInfo.Text.LastIndexOf('.') + 1);
                                                TCounter = (int)e.PercentComplete;
                                                if (RepeatFilter == TCounter)
                                                {
                                                    return;
                                                }
                                                else
                                                {
                                                    RepeatFilter = TCounter;
                                                }
                                                int CurrentProgress = Convert.ToInt32((HCounter + TCounter) / (float)FileList.Count);
                                                ProgressInfo.Text = temp + CurrentProgress + "%";
                                                ProBar.Value = CurrentProgress;

                                                if (TCounter == 100)
                                                {
                                                    HCounter += 100;
                                                }
                                            }
                                        });
                                    }, TimeSpan.FromMilliseconds(100), null, string.Empty);
                                    ZipStream.CloseEntry();
                                }
                            }
                        });
                    }
                    else
                    {
                        await Task.Run(async () =>
                        {
                            foreach (RemovableDeviceFile ZipFile in FileList)
                            {
                                ZipEntry NewEntry = new ZipEntry(ZipFile.File.Name)
                                {
                                    DateTime = DateTime.Now
                                };

                                ZipStream.PutNextEntry(NewEntry);
                                using (Stream stream = await ZipFile.File.OpenStreamForReadAsync())
                                {
                                    StreamUtils.Copy(stream, ZipStream, new byte[4096], async (s, e) =>
                                    {
                                        await LoadingControl.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                                        {
                                            lock (Lock)
                                            {
                                                string temp = ProgressInfo.Text.Remove(ProgressInfo.Text.LastIndexOf('.') + 1);
                                                TCounter = (int)e.PercentComplete;
                                                if (RepeatFilter == TCounter)
                                                {
                                                    return;
                                                }
                                                else
                                                {
                                                    RepeatFilter = TCounter;
                                                }

                                                int CurrentProgress = Convert.ToInt32((HCounter + TCounter) / (float)FileList.Count);
                                                ProgressInfo.Text = temp + CurrentProgress + "%";
                                                ProBar.Value = CurrentProgress;

                                                if (TCounter == 100)
                                                {
                                                    HCounter += 100;
                                                }
                                            }
                                        });
                                    }, TimeSpan.FromMilliseconds(100), null, string.Empty);
                                    ZipStream.CloseEntry();
                                }
                            }
                        });
                    }
                }
                catch (Exception e)
                {
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "压缩文件时发生异常\r\r错误信息：\r\r" + e.Message,
                        Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush,
                        CloseButtonText = "确定"
                    };
                    await dialog.ShowAsync();
                }
                finally
                {
                    ZipStream.IsStreamOwner = false;
                    ZipStream.Close();
                }

            }
        }

        private void GridViewControl_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (!((e.OriginalSource as FrameworkElement)?.DataContext is RemovableDeviceFile ReFile))
            {
                return;
            }
            if (ReFile.File.FileType == ".zip")
            {
                Nav.Navigate(typeof(ZipExplorer), ReFile, new DrillInNavigationTransitionInfo());
            }
            else if (ReFile.File.FileType == ".jpg" || ReFile.File.FileType == ".png" || ReFile.File.FileType == ".bmp")
            {
                Nav.Navigate(typeof(USBPhotoViewer), ReFile.File.FolderRelativeId, new DrillInNavigationTransitionInfo());
            }
            else if (ReFile.File.FileType == ".mkv" || ReFile.File.FileType == ".mp4" || ReFile.File.FileType == ".mp3" || ReFile.File.FileType == ".flac")
            {
                Nav.Navigate(typeof(USBMediaPlayer), ReFile.File, new DrillInNavigationTransitionInfo());
            }
            else if (ReFile.File.FileType == ".txt")
            {
                Nav.Navigate(typeof(USBTextViewer), ReFile, new DrillInNavigationTransitionInfo());
            }
        }

        private void GridViewControl_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            AddToZipQueue.Clear();
            foreach (RemovableDeviceFile item in e.Items)
            {
                AddToZipQueue.Enqueue(item.File);
            }
        }

        public async Task AddFileToZip(RemovableDeviceFile file)
        {
            LoadingActivation(true, "正在执行添加操作");
            using (var ZipFileStream = (await file.File.OpenAsync(FileAccessMode.ReadWrite)).AsStream())
            {
                ZipFile zipFile = new ZipFile(ZipFileStream);
                try
                {
                    await Task.Run(async () =>
                    {
                        while (AddToZipQueue.Count > 0)
                        {
                            zipFile.BeginUpdate();

                            StorageFile ToAddFile = AddToZipQueue.Dequeue();
                            using (var filestream = await ToAddFile.OpenStreamForReadAsync())
                            {
                                CustomStaticDataSource CSD = new CustomStaticDataSource();
                                CSD.SetStream(filestream);
                                zipFile.Add(CSD, ToAddFile.Name);
                                zipFile.CommitUpdate();
                            }
                        }
                    });

                }
                finally
                {
                    zipFile.IsStreamOwner = false;
                    zipFile.Close();
                }
            }

            file.SizeUpdateRequested(await GetSize(file.File));

            await Task.Delay(500);
            LoadingActivation(false);

        }
    }
}
