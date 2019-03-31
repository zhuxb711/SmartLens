using Microsoft.Toolkit.Uwp.Connectivity;
using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;
using Windows.Devices.WiFi;
using Windows.Media.Capture.Frames;
using Windows.Security.Credentials;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace SmartLens
{
    public sealed partial class SettingsPage : Page
    {
        private WiFiAdapter WiFi = null;
        public ObservableCollection<WiFiInfo> WiFiList;
        private DispatcherTimer WiFiScanTimer = null;
        bool IsScanRunning = false;
        public static SettingsPage ThisPage { get; private set; }
        private bool IsResetBluetooth = true;
        private bool IsResetWiFi = false;
        private List<WiFiInDataBase> StoragedWiFiInfoCollection;
        IReadOnlyList<MediaFrameSourceGroup> MediaFraSourceGroup;

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
            /*
             * 监听网络变化事件是因为如果通过外部途径(如Windows)更改了当前连接的网络
             * 则SmartLens也必须能够将当前连接的网络更改为正确的那个
             */
            NetworkHelper.Instance.NetworkChanged += Instance_NetworkChanged;
            ThisPage = this;
            OnFirstLoad();
        }

        private void CoreApplication_LeavingBackground(object sender, Windows.ApplicationModel.LeavingBackgroundEventArgs e)
        {
            NetworkHelper.Instance.NetworkChanged += Instance_NetworkChanged;
            WiFiScanTimer_Tick(null, null);
            if (WiFiScanTimer != null)
            {
                WiFiScanTimer.Tick += WiFiScanTimer_Tick;
                WiFiScanTimer.Start();
            }
            Progressing.Visibility = Visibility.Visible;
        }

        private void CoreApplication_EnteredBackground(object sender, Windows.ApplicationModel.EnteredBackgroundEventArgs e)
        {
            NetworkHelper.Instance.NetworkChanged -= Instance_NetworkChanged;
            if (WiFiScanTimer != null)
            {
                WiFiScanTimer.Tick -= WiFiScanTimer_Tick;
                WiFiScanTimer.Stop();
            }
            Progressing.Visibility = Visibility.Collapsed;
        }

        private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            await Radio.RequestAccessAsync();
            StoragedWiFiInfoCollection = await SQLite.GetInstance().GetAllWiFiDataAsync();
            var Size = await GetFolderSize(ApplicationData.Current.TemporaryFolder.Path);

            ClearCache.Content = "清除缓存(" + (Size / 1024 < 1024 ? Math.Round(Size / 1024f, 2).ToString() + " KB" :
            (Size / 1048576 >= 1024 ? Math.Round(Size / 1073741824f, 2).ToString() + " GB" :
            Math.Round(Size / 1048576f, 2).ToString() + " MB")) + ")";
        }

        /// <summary>
        /// 获取指定路径下的文件夹及其子文件、子文件夹等的总大小
        /// </summary>
        /// <param name="fullPath">文件夹路径</param>
        /// <returns>总大小</returns>
        public async Task<long> GetFolderSize(string fullPath)
        {
            if (Directory.Exists(fullPath))
            {
                long result = 0;
                DirectoryInfo initialDirectory = new DirectoryInfo(fullPath);
                var files = initialDirectory.GetFiles();
                foreach (FileInfo file in files)
                {
                    result += file.Length;
                }
                var directories = initialDirectory.GetDirectories();
                foreach (DirectoryInfo directory in directories)
                {
                    result += await GetFolderSize(directory.FullName);
                }
                return result;
            }
            else
            {
                return -1;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            //处理进入后台和离开后台的事件，在进入后台时停止WiFi搜索
            CoreApplication.EnteredBackground += CoreApplication_EnteredBackground;
            CoreApplication.LeavingBackground += CoreApplication_LeavingBackground;
            StoragedWiFiInfoCollection = new List<WiFiInDataBase>();
            WiFiList = new ObservableCollection<WiFiInfo>();
            WiFiControl.ItemsSource = WiFiList;
            if (WiFiScanTimer == null && WiFi != null)
            {
                WiFiScanTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(15)
                };
                WiFiScanTimer_Tick(null, null);
                WiFiScanTimer.Tick += WiFiScanTimer_Tick;
                WiFiScanTimer.Start();
            }
        }

        private async void OnFirstLoad()
        {
            var RadioDevice = await Radio.GetRadiosAsync();
            foreach (var Device in RadioDevice)
            {
                if (Device.Kind == RadioKind.Bluetooth && Device.State == RadioState.On)
                {
                    BluetoothSwitch.IsOn = true;
                }
                else if (Device.Kind == RadioKind.WiFi && Device.State == RadioState.On)
                {
                    WiFiSwitch.IsOn = true;
                }
            }

            if (ApplicationData.Current.LocalSettings.Values["EnableIntegrityCheck"] is bool IsEnable)
            {
                Integrity.IsOn = IsEnable;
            }

            MediaFraSourceGroup = await CameraHelper.GetFrameSourceGroupsAsync();
            if (MediaFraSourceGroup.Count == 0)
            {
                /*
                 * 使用EmptyCanmeraDevice类的原因是：控件CameraSelection设置为显示(某个类下的DisplayName)属性
                 * 因此简单添加“无”是不行的，因此构建一个具有DispalyName属性的类来实现正确显示
                 */
                CameraSelection.Items.Add(new EmptyCameraDevice());
                CameraSelection.SelectedIndex = 0;
                return;
            }

            CameraSelection.ItemsSource = MediaFraSourceGroup;

            if (ApplicationData.Current.LocalSettings.Values["LastSelectedCameraSource"] is string LastSelectedCameraSource)
            {
                for (int i = 0; i < MediaFraSourceGroup.Count; i++)
                {
                    if (MediaFraSourceGroup[i].DisplayName == LastSelectedCameraSource)
                    {
                        CameraSelection.SelectedIndex = i;
                        break;
                    }
                }
                if (CameraSelection.SelectedIndex == -1)
                {
                    CameraSelection.SelectedIndex = 0;
                    ApplicationData.Current.LocalSettings.Values["LastSelectedCameraSource"] = MediaFraSourceGroup[0].DisplayName;
                }
            }
            else
            {
                CameraSelection.SelectedIndex = 0;
                ApplicationData.Current.LocalSettings.Values["LastSelectedCameraSource"] = MediaFraSourceGroup[0].DisplayName;
            }

            if (MediaFraSourceGroup.Count != 1)
            {
                CameraSelection.SelectionChanged += (s, t) =>
                {
                    if (CameraSelection.SelectedIndex >= 0)
                    {
                        CameraProvider.SetCameraFrameSource(MediaFraSourceGroup[CameraSelection.SelectedIndex]);
                        ApplicationData.Current.LocalSettings.Values["LastSelectedCameraSource"] = MediaFraSourceGroup[CameraSelection.SelectedIndex].DisplayName;
                    }
                };
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            NetworkHelper.Instance.NetworkChanged -= Instance_NetworkChanged;
            CoreApplication.EnteredBackground -= CoreApplication_EnteredBackground;
            CoreApplication.LeavingBackground -= CoreApplication_LeavingBackground;

            if (WiFiScanTimer != null)
            {
                WiFiScanTimer.Tick -= WiFiScanTimer_Tick;
                WiFiScanTimer.Stop();
                WiFiScanTimer = null;
            }
            Progressing.Visibility = Visibility.Collapsed;
            WiFiList.Clear();
            WiFiList = null;
            StoragedWiFiInfoCollection.Clear();
            StoragedWiFiInfoCollection = null;
        }

        private void Instance_NetworkChanged(object sender, EventArgs e)
        {
            if (NetworkHelper.Instance.ConnectionInformation.NetworkNames.Count > 0)
            {
                for (int i = 0; i < WiFiList.Count; i++)
                {
                    if (WiFiList[i].IsConnected == true)
                    {
                        WiFiList[i].ChangeConnectionStateAsync(false);
                    }
                    if (WiFiList[i].IsConnected != true && NetworkHelper.Instance.ConnectionInformation.NetworkNames[0] == WiFiList[i].Name)
                    {
                        WiFiList[i].ChangeConnectionStateAsync(true, true);
                    }
                }
            }
        }

        /// <summary>
        /// 异步触发蓝牙状态转换
        /// </summary>
        /// <param name="OnOrOff">开启或关闭</param>
        /// <returns>成功完成与否</returns>
        private async Task<bool> ToggleBluetoothStatusAsync(bool OnOrOff)
        {
            try
            {
                var RadioDevice = await Radio.GetRadiosAsync();

                foreach (var Device in from Device in RadioDevice
                                       where Device.Kind == RadioKind.Bluetooth
                                       select Device)
                {
                    if (OnOrOff)
                    {
                        await Device.SetStateAsync(RadioState.On);
                    }
                    else
                    {
                        await Device.SetStateAsync(RadioState.Off);
                    }
                }

            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        private async void BluetoothSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (IsResetBluetooth)
            {
                IsResetBluetooth = false;
                return;
            }

            if (BluetoothSwitch.IsOn)
            {
                if (!await ToggleBluetoothStatusAsync(true))
                {
                    IsResetBluetooth = true;
                    BluetoothSwitch.IsOn = false;
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "打开蓝牙时出现问题",
                        CloseButtonText = "确定",
                        Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                    };
                    await dialog.ShowAsync();
                }
            }
            else
            {
                if (!await ToggleBluetoothStatusAsync(false))
                {
                    IsResetBluetooth = true;
                    BluetoothSwitch.IsOn = true;
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "关闭蓝牙时出现问题",
                        CloseButtonText = "确定",
                        Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                    };
                    await dialog.ShowAsync();
                }
            }
        }

        /// <summary>
        /// 异步初始化WiFi适配器
        /// </summary>
        /// <returns>成功与否</returns>
        private async Task<bool> InitializeWiFiAdapterAsync()
        {
            var WiFiAdapterResults = await DeviceInformation.FindAllAsync(WiFiAdapter.GetDeviceSelector());
            if (WiFiAdapterResults.Count >= 1)
            {
                WiFi = await WiFiAdapter.FromIdAsync(WiFiAdapterResults.FirstOrDefault().Id);
                WiFi.AvailableNetworksChanged += WiFi_AvailableNetworksChanged;
            }
            else
            {
                return false;
            }
            return true;
        }

        private async void WiFi_AvailableNetworksChanged(WiFiAdapter sender, object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Progressing.Visibility = Visibility.Collapsed;
            });
        }

        /// <summary>
        /// 异步触发WiFi状态转换
        /// </summary>
        /// <param name="IsOn">开启或关闭</param>
        /// <returns>成功与否</returns>
        private async Task<bool> ToggleWiFiStatusAsync(bool IsOn)
        {
            var RadioDevice = await Radio.GetRadiosAsync();

            foreach (var Device in from Device in RadioDevice
                                   where Device.Kind == RadioKind.WiFi
                                   select Device)
            {
                if (IsOn)
                {
                    await Device.SetStateAsync(RadioState.On);
                    return true;
                }
                else
                {
                    await Device.SetStateAsync(RadioState.Off);
                    return true;
                }
            }

            return false;
        }

        private async void WiFiSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (IsResetWiFi)
            {
                IsResetWiFi = false;
                return;
            }

            if (WiFiSwitch.IsOn)
            {
                if (!(await ToggleWiFiStatusAsync(true) && await InitializeWiFiAdapterAsync()))
                {
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "打开WiFi时出现问题",
                        CloseButtonText = "确定",
                        Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                    };
                    IsResetWiFi = true;
                    WiFiSwitch.IsOn = false;
                    await dialog.ShowAsync();
                }
                else
                {
                    if (WiFiScanTimer == null)
                    {
                        WiFiScanTimer = new DispatcherTimer
                        {
                            Interval = TimeSpan.FromSeconds(15)
                        };
                        WiFiScanTimer_Tick(null, null);

                        WiFiScanTimer.Tick += WiFiScanTimer_Tick;
                        WiFiScanTimer.Start();
                    }
                }
            }
            else
            {
                if (await ToggleWiFiStatusAsync(false))
                {
                    if (WiFiScanTimer != null)
                    {
                        WiFiScanTimer.Tick -= WiFiScanTimer_Tick;
                        WiFiScanTimer.Stop();
                        WiFiScanTimer = null;
                    }
                    if (WiFi != null)
                    {
                        WiFi.AvailableNetworksChanged -= WiFi_AvailableNetworksChanged;
                        WiFi = null;
                    }
                    Progressing.Visibility = Visibility.Collapsed;
                    WiFiList.Clear();
                }
                else
                {
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "关闭WiFi时出现问题",
                        CloseButtonText = "确定",
                        Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                    };
                    IsResetWiFi = true;
                    WiFiSwitch.IsOn = true;
                    await dialog.ShowAsync();
                }
            }
        }

        private async void WiFiScanTimer_Tick(object sender, object e)
        {
            if (IsScanRunning)
            {
                return;
            }
            IsScanRunning = true;

            Progressing.Visibility = Visibility.Visible;
            await WiFi.ScanAsync();
            if (WiFiList != null)
            {
                if (WiFiList.Count != 0)
                {
                    foreach (var WiFiNetwork in WiFi.NetworkReport.AvailableNetworks)
                    {
                        bool IsExist = false;
                        for (int i = 0; i < WiFiList.Count; i++)
                        {
                            if (WiFiNetwork.Bssid == WiFiList[i].MAC)
                            {
                                WiFiList[i].Update(WiFiNetwork);
                                IsExist = true;
                                break;
                            }
                        }
                        if (!IsExist)
                        {
                            WiFiList.Add(new WiFiInfo(WiFiNetwork));
                            WiFiList[WiFiList.Count - 1].IsUpdated = true;
                            IsExist = false;
                        }
                    }
                    for (int i = 0; i < WiFiList.Count; i++)
                    {
                        if (WiFiList[i].IsUpdated)
                        {
                            WiFiList[i].IsUpdated = false;
                        }
                        else
                        {
                            if (!WiFiList[i].IsConnected)
                            {
                                WiFiList.RemoveAt(i);
                            }
                            i--;
                        }
                    }
                }
                else
                {
                    foreach (var WiFiNetwork in WiFi.NetworkReport.AvailableNetworks)
                    {
                        if (NetworkHelper.Instance.ConnectionInformation.NetworkNames.Count > 0 && NetworkHelper.Instance.ConnectionInformation.NetworkNames[0] == WiFiNetwork.Ssid)
                        {
                            WiFiList.Insert(0, new WiFiInfo(WiFiNetwork, true));
                        }
                        else
                        {
                            WiFiList.Add(new WiFiInfo(WiFiNetwork));
                        }
                    }
                }
            }
            IsScanRunning = false;
        }


        private void WiFiControl_ItemClick(object sender, ItemClickEventArgs e)
        {
            var ItemInWiFiControl = WiFiControl.ContainerFromItem(e.ClickedItem) as ListViewItem;
            var ClickedItem = e.ClickedItem as WiFiInfo;
            if (ClickedItem.IsConnected)
            {
                ItemInWiFiControl.ContentTemplate = WiFiConnectedState;
                return;
            }

            if (ItemInWiFiControl.ContentTemplate == WiFiNormalState)
            {
                foreach (var WiFiInfo in from WiFiInfo in StoragedWiFiInfoCollection
                                         where WiFiInfo.SSID == ClickedItem.Name
                                         select WiFiInfo)
                {
                    ClickedItem.AutoConnect = WiFiInfo.AutoConnect;
                    break;
                }
                ItemInWiFiControl.ContentTemplate = WiFiPressState;
            }
        }

        private void WiFiControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (var ItemInWiFiControl in from RemovedItem in e.RemovedItems
                                              let ItemInWiFiControl = WiFiControl.ContainerFromItem(RemovedItem) as ListViewItem
                                              where ItemInWiFiControl.ContentTemplate != WiFiNormalState
                                              select ItemInWiFiControl)
            {
                ItemInWiFiControl.ContentTemplate = WiFiNormalState;
            }

            foreach (var WiFi in WiFiList)
            {
                WiFi.HideMessage();
            }
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            var ItemInWiFiControl = WiFiControl.ContainerFromItem(WiFiControl.SelectedItem) as ListViewItem;
            foreach (var WiFiInfo in from WiFiInfo in StoragedWiFiInfoCollection
                                     where WiFiInfo.SSID == WiFiList[WiFiControl.SelectedIndex].Name
                                     select WiFiInfo)
            {
                WiFiList[WiFiControl.SelectedIndex].Password = WiFiInfo.Password;
                break;
            }

            if (WiFiList[WiFiControl.SelectedIndex].Password != "")
            {
                ItemInWiFiControl.ContentTemplate = WiFiConnectingState;
                ConfirmButton_Click(null, null);
            }
            else
            {
                ItemInWiFiControl.ContentTemplate = WiFiPasswordState;
            }

            //连接WiFi期间需要暂时停止搜索，因为此时搜索将导致WiFi列表位移
            WiFiScanTimer.Tick -= WiFiScanTimer_Tick;
            WiFiScanTimer.Stop();
            Progressing.Visibility = Visibility.Collapsed;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var ItemInWiFiControl = WiFiControl.ContainerFromItem(WiFiControl.SelectedItem) as ListViewItem;
            WiFiList[WiFiControl.SelectedIndex].HideMessage();
            ItemInWiFiControl.ContentTemplate = WiFiPressState;
        }

        private async void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            string Pass = WiFiList[WiFiControl.SelectedIndex].Password;
            if (Pass != "" && Pass.Length >= 8)
            {
                WiFiList[WiFiControl.SelectedIndex].HideMessage();
                (WiFiControl.ContainerFromItem(WiFiControl.SelectedItem) as ListViewItem).ContentTemplate = WiFiConnectingState;

                WiFiInfo Info = WiFiList[WiFiControl.SelectedIndex];
                PasswordCredential Credential = new PasswordCredential
                {
                    Password = Info.Password
                };

                var ConnectResult = await WiFi.ConnectAsync(Info.GetWiFiAvailableNetwork(), Info.AutoConnect ? WiFiReconnectionKind.Automatic : WiFiReconnectionKind.Manual, Credential);
                if (ConnectResult.ConnectionStatus == WiFiConnectionStatus.Success)
                {
                    foreach (var WiFiInfo in from WiFiInfo in WiFiList
                                             where WiFiInfo.IsConnected == true
                                             select WiFiInfo)
                    {
                        WiFiInfo.ChangeConnectionStateAsync(false);
                    }

                    Info.HideMessage();
                    Info.ChangeConnectionStateAsync(true, true);

                    (WiFiControl.ContainerFromItem(WiFiControl.SelectedItem) as ListViewItem).ContentTemplate = WiFiConnectedState;

                    bool IsExist = false;

                    foreach (var WiFiInfo in from WiFiInfo in StoragedWiFiInfoCollection
                                             where WiFiInfo.SSID == Info.Name
                                             select WiFiInfo)
                    {
                        if (WiFiInfo.Password != Info.Password)
                        {
                            await SQLite.GetInstance().UpdateWiFiDataAsync(Info.Name, Info.Password);
                        }
                        else if (WiFiInfo.Password == Info.Password)
                        {
                            IsExist = true;
                        }

                        if (WiFiInfo.AutoConnect != Info.AutoConnect)
                        {
                            await SQLite.GetInstance().UpdateWiFiDataAsync(Info.Name, Info.AutoConnect);
                        }

                        break;
                    }

                    if (!IsExist)
                    {
                        IsExist = false;
                        StoragedWiFiInfoCollection.Add(new WiFiInDataBase(Info.Name, Info.Password, Info.AutoConnect ? "True" : "False"));
                        await SQLite.GetInstance().SetWiFiDataAsync(Info.Name, Info.Password, Info.AutoConnect);
                    }
                }
                else
                {
                    WiFi.Disconnect();
                    (WiFiControl.ContainerFromItem(WiFiControl.SelectedItem) as ListViewItem).ContentTemplate = WiFiErrorState;
                    Info.ShowMessage("连接失败");
                }

                //连接完成后重新开始搜索
                WiFiScanTimer.Tick += WiFiScanTimer_Tick;
                WiFiScanTimer.Start();
            }
            else
            {
                WiFiList[WiFiControl.SelectedIndex].ShowMessage("密码必须非空且大于8位");
            }
        }

        private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            WiFi.Disconnect();
            var item = WiFiList[WiFiControl.SelectedIndex];
            item.AutoConnect = false;
            await SQLite.GetInstance().UpdateWiFiDataAsync(item.Name, false);
            WiFiList[WiFiControl.SelectedIndex].ChangeConnectionStateAsync(false);
            (WiFiControl.ContainerFromItem(WiFiControl.SelectedItem) as ListViewItem).ContentTemplate = WiFiPressState;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            var ItemInWiFiControl = WiFiControl.ContainerFromItem(WiFiControl.SelectedItem) as ListViewItem;
            WiFiList[WiFiControl.SelectedIndex].HideMessage();
            ItemInWiFiControl.ContentTemplate = WiFiPressState;
        }

        private async void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog contentDialog = new ContentDialog
            {
                Title = "警告",
                Content = " 操作将完全初始化SmartLens，包括：\r\r     • 清除全部数据存储\r\r     • SmartLens将自动关闭\r\r 您需要按提示重新启动SmartLens",
                PrimaryButtonText = "继续",
                CloseButtonText = "取消",
                Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
            };
            if (await contentDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                /*
                 * 以下操作主要有：
                 * 关闭SQL数据库
                 * 清除应用程序所有存储数据
                 * 准备Toast弹出通知
                 * 启动弹出通知
                 * 关闭SmartLens本身
                 */
                SQLite.GetInstance().Dispose();
                await ApplicationData.Current.ClearAsync();
                ToastContent content = PopToast.GenerateToastContent();
                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(content.GetXml()));
                CoreApplication.Exit();
            }
        }

        private void Theme_Toggled(object sender, RoutedEventArgs e)
        {
            /*
             * 不能立即更改主题，当且仅当应用程序启动时才能够更改
             * 启动时，将在App.cs中查询并更改完成
             */
            ThemeSwitcher.IsLightEnabled = !Theme.IsOn;
        }

        private async void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            await ApplicationData.Current.ClearAsync(ApplicationDataLocality.Temporary);
            ContentDialog contentDialog = new ContentDialog
            {
                Title = "提示",
                Content = "清除缓存成功",
                CloseButtonText = "确定",
                Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
            };
            await contentDialog.ShowAsync();
            ClearCache.Content = "清除缓存(0KB)";
        }

        private async void ErrorExport_Click(object sender, RoutedEventArgs e)
        {
            if (await ApplicationData.Current.LocalFolder.FileExistsAsync("ErrorLog.txt"))
            {
                FileSavePicker Picker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.Desktop,
                    CommitButtonText = "保存",
                    DefaultFileExtension = ".txt",
                    SuggestedFileName = "SmartLens错误日志"
                };
                Picker.FileTypeChoices.Add("文本文件", new List<string> { ".txt" });

                if ((await Picker.PickSaveFileAsync()) is StorageFile file)
                {
                    var LogFile = await ApplicationData.Current.LocalFolder.GetFileAsync("ErrorLog.txt");
                    await LogFile.CopyAndReplaceAsync(file);
                }
            }
            else
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "提示",
                    Content = "无可用的错误日志导出",
                    CloseButtonText = "确定",
                    Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                };
                await dialog.ShowAsync();
            }
        }

        private void Integrity_Toggled(object sender, RoutedEventArgs e)
        {
            if (Integrity.IsOn)
            {
                ApplicationData.Current.LocalSettings.Values["EnableIntegrityCheck"] = true;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["EnableIntegrityCheck"] = false;
            }
        }
    }
}
