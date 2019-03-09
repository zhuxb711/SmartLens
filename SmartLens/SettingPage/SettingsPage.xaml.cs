using Microsoft.Toolkit.Uwp.Connectivity;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;
using Windows.Devices.WiFi;
using Windows.Security.Credentials;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Core;
using System.Collections.Generic;
using Microsoft.Toolkit.Uwp.Helpers;
using Windows.Media.Capture.Frames;
using Windows.Storage;
using Windows.ApplicationModel.Core;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.UI.Notifications;
using System.IO;

namespace SmartLens
{
    public sealed partial class SettingsPage : Page
    {
        private WiFiAdapter WiFi = null;
        public ObservableCollection<WiFiInfo> WiFiList;
        private DispatcherTimer WiFiScanTimer = null;
        private AutoResetEvent ScanSignal;
        public static SettingsPage ThisPage { get; private set; }
        private bool IsInitializeBT = false;
        private List<WiFiInDataBase> WiFiContainer;
        IReadOnlyList<MediaFrameSourceGroup> MediaFraSourceGroup;

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
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
            WiFiContainer = await SQLite.GetInstance().GetAllWiFiData();
            var Size = await GetFolderSize(ApplicationData.Current.TemporaryFolder.Path);
            ClearCache.Content = "清除缓存(" + (Size / 1024 < 1024 ? Math.Round(Size / 1024f, 2).ToString() + " KB" :
            (Size / 1048576 >= 1024 ? Math.Round(Size / 1073741824f, 2).ToString() + " GB" :
            Math.Round(Size / 1048576f, 2).ToString() + " MB")) + ")";
        }

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
            CoreApplication.EnteredBackground += CoreApplication_EnteredBackground;
            CoreApplication.LeavingBackground += CoreApplication_LeavingBackground;
            WiFiContainer = new List<WiFiInDataBase>();
            ScanSignal = new AutoResetEvent(true);
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
            foreach (var item in RadioDevice)
            {
                if (item.Kind == RadioKind.Bluetooth && item.State == RadioState.On)
                {
                    IsInitializeBT = true;
                    BluetoothSwitch.IsOn = true;
                }
                else if (item.Kind == RadioKind.WiFi && item.State == RadioState.On)
                {
                    WiFiSwitch.IsOn = true;
                }
            }
            MediaFraSourceGroup = await CameraHelper.GetFrameSourceGroupsAsync();

            if(MediaFraSourceGroup.Count==0)
            {
                CameraSelection.Items.Add(new EmptyCameraDevice());
                CameraSelection.SelectedIndex = 0;
                return;
            }

            CameraSelection.ItemsSource = MediaFraSourceGroup;
            if (ApplicationData.Current.LocalSettings.Values["LastSelectedCameraSource"] == null)
            {
                CameraSelection.SelectedIndex = 0;
                ApplicationData.Current.LocalSettings.Values["LastSelectedCameraSource"] = MediaFraSourceGroup[0].DisplayName;
            }
            else
            {
                string LastSelectedCameraSource = ApplicationData.Current.LocalSettings.Values["LastSelectedCameraSource"].ToString();
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
            WiFiContainer.Clear();
            WiFiContainer = null;
            ScanSignal.Dispose();
        }

        private void Instance_NetworkChanged(object sender, EventArgs e)
        {
            if (NetworkHelper.Instance.ConnectionInformation.NetworkNames.Count > 0)
            {
                for (int i = 0; i < WiFiList.Count; i++)
                {
                    if (WiFiList[i].IsConnected == true)
                    {
                        WiFiList[i].ChangeConnectState(false);
                    }
                    if (WiFiList[i].IsConnected != true && NetworkHelper.Instance.ConnectionInformation.NetworkNames[0] == WiFiList[i].Name)
                    {
                        WiFiList[i].ChangeConnectState(true, WiFiList[i]);
                    }
                }
            }
        }

        private async Task<bool> ToggleBluetoothStatusAsync(bool OnOrOff)
        {
            try
            {
                var Access = await Radio.RequestAccessAsync();
                if (Access != RadioAccessStatus.Allowed)
                {
                    return false;
                }
                BluetoothAdapter BTAdapter = await BluetoothAdapter.GetDefaultAsync();
                if (BTAdapter != null)
                {
                    var BTRadio = await BTAdapter.GetRadioAsync();
                    if (OnOrOff)
                    {
                        await BTRadio.SetStateAsync(RadioState.On);
                    }
                    else
                    {
                        await BTRadio.SetStateAsync(RadioState.Off);
                        BTAdapter = null;
                    }
                    return true;
                }
                else
                {
                    return false;
                }

            }
            catch (Exception)
            {
                return false;
            }
        }

        private async void BluetoothSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (IsInitializeBT)
            {
                IsInitializeBT = false;
                return;
            }
            ToggleSwitch button = sender as ToggleSwitch;
            var Result = await ToggleBluetoothStatusAsync(button.IsOn);
            if (!Result)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "错误",
                    Content = "打开蓝牙时出现问题",
                    CloseButtonText = "确定"
                };
                await dialog.ShowAsync();
            }
        }

        private async Task<bool> InitializeWiFiAdapterAsync()
        {
            var WifiAdapterResults = await DeviceInformation.FindAllAsync(WiFiAdapter.GetDeviceSelector());
            if (WifiAdapterResults.Count >= 1)
            {
                WiFi = await WiFiAdapter.FromIdAsync(WifiAdapterResults[0].Id);
                WiFi.AvailableNetworksChanged += async (s, e) =>
                {
                    ScanSignal.Set();
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                     {
                         Progressing.Visibility = Visibility.Collapsed;
                     });

                };
            }
            else
            {
                return false;
            }
            return true;
        }

        private async Task<bool> ToggleWiFiStatusAsync(bool IsOn)
        {
            var access = await WiFiAdapter.RequestAccessAsync();
            if (access != WiFiAccessStatus.Allowed)
            {
                return false;
            }
            var RadioDevice = await Radio.GetRadiosAsync();
            foreach (var item in RadioDevice)
            {
                if (item.Kind == RadioKind.WiFi)
                {
                    if (IsOn)
                    {
                        await item.SetStateAsync(RadioState.On);
                        return true;
                    }
                    else
                    {
                        await item.SetStateAsync(RadioState.Off);
                        return true;
                    }
                }
            }
            return false;
        }

        private async void WiFiSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (WiFiSwitch.IsOn)
            {
                if (!(await ToggleWiFiStatusAsync(true) && await InitializeWiFiAdapterAsync()))
                {
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "打开WiFi时出现问题",
                        CloseButtonText = "确定"
                    };
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
                    Progressing.Visibility = Visibility.Collapsed;
                    WiFiList.Clear();
                }
                else
                {
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "关闭WiFi时出现问题",
                        CloseButtonText = "确定"
                    };
                    await dialog.ShowAsync();
                }
            }
        }

        private async void WiFiScanTimer_Tick(object sender, object e)
        {
            await Task.Run(() =>
            {
                ScanSignal.WaitOne();
            });

            Progressing.Visibility = Visibility.Visible;
            await WiFi.ScanAsync();
            if (WiFiList.Count != 0)
            {
                foreach (var item in WiFi.NetworkReport.AvailableNetworks)
                {
                    bool IsExist = false;
                    for (int i = 0; i < WiFiList.Count; i++)
                    {
                        if (item.Bssid == WiFiList[i].ID)
                        {
                            WiFiList[i].Update(item);
                            IsExist = true;
                            break;
                        }
                    }
                    if (!IsExist)
                    {
                        WiFiList.Add(new WiFiInfo(item));
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
                foreach (var item in WiFi.NetworkReport.AvailableNetworks)
                {
                    if (NetworkHelper.Instance.ConnectionInformation.NetworkNames.Count > 0 && NetworkHelper.Instance.ConnectionInformation.NetworkNames[0] == item.Ssid)
                    {
                        WiFiList.Add(new WiFiInfo(item, true));
                        WiFiList.Move(WiFiList.Count - 1, 0);
                    }
                    else
                    {
                        WiFiList.Add(new WiFiInfo(item));
                    }
                }
            }
            ScanSignal.Set();

        }


        private void WiFiControl_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = WiFiControl.ContainerFromItem(e.ClickedItem) as ListViewItem;
            var it = e.ClickedItem as WiFiInfo;
            if (it.IsConnected)
            {
                item.ContentTemplate = WiFiConnectedState;
                return;
            }

            if (item.ContentTemplate == WiFiNormalState)
            {
                foreach (var ite in WiFiContainer)
                {
                    if (ite.SSID == it.Name)
                    {
                        it.AutoConnect = ite.AutoConnect;
                        break;
                    }
                }
                item.ContentTemplate = WiFiPressState;
            }
        }

        private void WiFiControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (var item in e.RemovedItems)
            {
                var it = WiFiControl.ContainerFromItem(item) as ListViewItem;
                if (it.ContentTemplate != WiFiNormalState)
                {
                    it.ContentTemplate = WiFiNormalState;
                }
            }
            foreach (var item in WiFiList)
            {
                item.HideMessage();
            }
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            var Item = WiFiControl.ContainerFromItem(WiFiControl.SelectedItem) as ListViewItem;
            foreach (var item in WiFiContainer)
            {
                if (item.SSID == WiFiList[WiFiControl.SelectedIndex].Name)
                {
                    WiFiList[WiFiControl.SelectedIndex].Password = item.Password;
                    break;
                }
            }
            if (WiFiList[WiFiControl.SelectedIndex].Password != "")
            {
                Item.ContentTemplate = WiFiConnectingState;
                ConfirmButton_Click(null, null);
            }
            else
            {
                Item.ContentTemplate = WiFiPasswordState;
            }
            WiFiScanTimer.Tick -= WiFiScanTimer_Tick;
            WiFiScanTimer.Stop();
            Progressing.Visibility = Visibility.Collapsed;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var Item = WiFiControl.ContainerFromItem(WiFiControl.SelectedItem) as ListViewItem;
            WiFiList[WiFiControl.SelectedIndex].HideMessage();
            Item.ContentTemplate = WiFiPressState;
        }

        private async void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            string Pass = WiFiList[WiFiControl.SelectedIndex].Password;
            if (Pass != "" && Pass.ToCharArray().Length >= 8)
            {
                WiFiList[WiFiControl.SelectedIndex].HideMessage();
                (WiFiControl.ContainerFromItem(WiFiControl.SelectedItem) as ListViewItem).ContentTemplate = WiFiConnectingState;

                WiFiInfo info = WiFiList[WiFiControl.SelectedIndex];
                PasswordCredential Credential = new PasswordCredential
                {
                    Password = info.Password
                };

                var ConnectResult = await WiFi.ConnectAsync(info.GetWiFiAvailableNetwork(), info.AutoConnect ? WiFiReconnectionKind.Automatic : WiFiReconnectionKind.Manual, Credential);
                if (ConnectResult.ConnectionStatus == WiFiConnectionStatus.Success)
                {
                    foreach (var item in WiFiList)
                    {
                        if (item.IsConnected == true)
                        {
                            item.ChangeConnectState(false);
                        }
                    }
                    info.HideMessage();
                    info.ChangeConnectState(true, info);
                    (WiFiControl.ContainerFromItem(WiFiControl.SelectedItem) as ListViewItem).ContentTemplate = WiFiConnectedState;
                    bool IsExist = false;
                    foreach (var item in WiFiContainer)
                    {
                        if (item.SSID == info.Name)
                        {
                            if (item.Password != info.Password)
                            {
                                await SQLite.GetInstance().UpdateWiFiData(info.Name, info.Password);
                            }
                            else if (item.Password == info.Password)
                            {
                                IsExist = true;
                            }

                            if (item.AutoConnect != info.AutoConnect)
                            {
                                await SQLite.GetInstance().UpdateWiFiData(info.Name, info.AutoConnect);
                            }
                            break;
                        }
                    }
                    if (!IsExist)
                    {
                        IsExist = false;
                        WiFiContainer.Add(new WiFiInDataBase(info.Name, info.Password, info.AutoConnect ? "True" : "False"));
                        await SQLite.GetInstance().SetWiFiData(info.Name, info.Password, info.AutoConnect);
                    }
                }
                else
                {
                    WiFi.Disconnect();
                    (WiFiControl.ContainerFromItem(WiFiControl.SelectedItem) as ListViewItem).ContentTemplate = WiFiErrorState;
                    info.ShowMessage("连接失败");
                }
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
            await SQLite.GetInstance().UpdateWiFiData(item.Name, false);
            WiFiList[WiFiControl.SelectedIndex].ChangeConnectState(false);
            (WiFiControl.ContainerFromItem(WiFiControl.SelectedItem) as ListViewItem).ContentTemplate = WiFiPressState;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            var Item = WiFiControl.ContainerFromItem(WiFiControl.SelectedItem) as ListViewItem;
            WiFiList[WiFiControl.SelectedIndex].HideMessage();
            Item.ContentTemplate = WiFiPressState;
        }

        private async void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog contentDialog = new ContentDialog
            {
                Title = "警告",
                Content = " 操作将完全初始化SmartLens，包括：\r\r     • 清除全部数据存储\r\r     • SmartLens将自动关闭\r\r 您需要按提示重新启动SmartLens",
                PrimaryButtonText = "继续",
                CloseButtonText = "取消",
            };
            if (await contentDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await WebView.ClearTemporaryWebDataAsync();
                SQLite.GetInstance().Dispose();
                await ApplicationData.Current.ClearAsync();
                ToastContent content = PopToast.GenerateToastContent();
                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(content.GetXml()));
                CoreApplication.Exit();
            }
        }

        private void Theme_Toggled(object sender, RoutedEventArgs e)
        {
            if(Theme.IsOn)
            {
                ThemeSwitcher.IsLightEnabled = false;
            }
            else
            {
                ThemeSwitcher.IsLightEnabled = true;
            }
        }

        private async void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            await ApplicationData.Current.ClearAsync(ApplicationDataLocality.Temporary);
            ContentDialog contentDialog = new ContentDialog
            {
                Title = "提示",
                Content = "清除缓存成功",
                CloseButtonText = "确定"
            };
            await contentDialog.ShowAsync();
            ClearCache.Content = "清除缓存(0KB)";
        }
    }
}
