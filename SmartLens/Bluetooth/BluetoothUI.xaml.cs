using System;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using System.Collections.ObjectModel;
using Windows.UI.Core;
using Bluetooth.Core.Services;
using Bluetooth.Services.Obex;


namespace SmartLens
{
    public sealed partial class BluetoothUI : ContentDialog
    {
        ObservableCollection<BluetoothList> BluetoothInfo;
        List<BluetoothDevice> BTD;
        Deferral PairDeferral = null;
        Deferral RefreshDeferral = null;
        DevicePairingRequestedEventArgs PairRequestArgs = null;
        DeviceWatcher DeviceWatcher = null;
        int LastSelectIndex = -1;
        object Locker = new object();
        private bool ClosePermission { get; set; } = true;

        public BluetoothUI()
        {
            InitializeComponent();
            Loaded += BluetoothUI_Loaded;
            Closing += BluetoothUI_Closing;
        }

        private void BluetoothUI_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            if (!ClosePermission)
            {
                args.Cancel = true;
            }
            if (DeviceWatcher.Status == DeviceWatcherStatus.Started || DeviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted)
            {
                DeviceWatcher.Stop();
            }
        }

        private void BluetoothUI_Loaded(object sender, RoutedEventArgs e)
        {
            BTD = new List<BluetoothDevice>();
            BluetoothInfo = new ObservableCollection<BluetoothList>();
            BluetoothControl.ItemsSource = BluetoothInfo;
            CreateBluetoothWatcher();
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (BluetoothControl.SelectedIndex == -1 || !BluetoothInfo[BluetoothControl.SelectedIndex].DeviceInfo.Pairing.IsPaired)
            {
                Tips.Text = "请先选择一个已配对的设备";
                Tips.Visibility = Visibility.Visible;
                ClosePermission = false;
                return;
            }
            BluetoothService BTService = BluetoothService.GetDefault();
            BTService.SearchForPairedDevicesSucceeded += (s, e) =>
            {
                BTD = e.PairedDevices;
            };
            ClosePermission = true;
            try
            {
                var CanonicalName = await ConnectToRfcommServiceAsync(BluetoothInfo[BluetoothControl.SelectedIndex] as BluetoothList);
                await BTService.SearchForPairedDevicesAsync();
                foreach (var item in BTD)
                {
                    if (item.DeviceHost.CanonicalName == CanonicalName)
                    {
                        Obex.ObexClient = ObexService.GetDefaultForBluetoothDevice(item);
                        break;
                    }
                }
                WebPage.ThisPage.ResetEvent.Set();
                if (Obex.ObexClient == null)
                {
                    throw new Exception("未能找到已配对的设备，请打开该设备的蓝牙开关");
                }
            }
            catch (Exception e)
            {
                Tips.Text = e.Message;
                Tips.Visibility = Visibility.Visible;
                ClosePermission = false;
                BTService = null;
            }
        }


        public void CreateBluetoothWatcher()
        {
            if (DeviceWatcher != null)
            {
                DeviceWatcher.Stop();
                DeviceWatcher = null;
                Progress.IsActive = true;
                StatusText.Text = "正在搜索";
            }
            DeviceWatcher = DeviceInformation.CreateWatcher("(System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\")", new string[] { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.Bluetooth.Le.IsConnectable" }, DeviceInformationKind.AssociationEndpoint);
            DeviceWatcher.Added += async (s, e) =>
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    lock (Locker)
                    {
                        try
                        {
                            if (BluetoothInfo != null)
                            {
                                BluetoothInfo.Add(new BluetoothList(e));
                            }
                        }
                        catch (Exception) { }
                    }
                });

            };
            DeviceWatcher.Updated += async (s, e) =>
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    lock (Locker)
                    {
                        try
                        {
                            if (BluetoothInfo != null)
                            {
                                for (int i = 0; i < BluetoothInfo.Count; i++)
                                {
                                    if (BluetoothInfo[i].Id == e.Id)
                                    {
                                        BluetoothInfo[i].Update(e);
                                        break;
                                    }
                                }
                            }
                        }
                        catch (Exception) { }
                    }
                });
            };
            DeviceWatcher.Removed += async (s, e) =>
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    lock (Locker)
                    {
                        try
                        {
                            if (BluetoothInfo != null)
                            {
                                for (int i = 0; i < BluetoothInfo.Count; i++)
                                {
                                    if (BluetoothInfo[i].Id == e.Id)
                                    {
                                        BluetoothInfo.RemoveAt(i);
                                        i--;
                                    }
                                }
                            }
                        }
                        catch (Exception) { }

                    }
                });

            };
            DeviceWatcher.EnumerationCompleted += async (s, e) =>
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    Progress.IsActive = false;
                    StatusText.Text = "搜索完成";
                });
            };
            DeviceWatcher.Start();
        }

        public async Task<string> ConnectToRfcommServiceAsync(BluetoothList BL)
        {
            var Device = await Windows.Devices.Bluetooth.BluetoothDevice.FromIdAsync(BL.Id);
            var Services = await Device.GetRfcommServicesForIdAsync(RfcommServiceId.ObexObjectPush);
            if (Services.Services.Count == 0)
            {
                throw new Exception("无法发现蓝牙设备的ObexObjectPush服务，该设备不受支持");
            }
            RfcommDeviceService RfcService = Services.Services[0];
            return RfcService.ConnectionHostName.CanonicalName;
        }

        private async void PairOrCancelButton_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            BluetoothControl.SelectedItem = btn.DataContext;
            LastSelectIndex = BluetoothControl.SelectedIndex;
            if (btn.Content.ToString() == "配对")
            {
                await Pair(BluetoothInfo[LastSelectIndex].DeviceInfo);
            }
            else
            {
                var list = BluetoothInfo[BluetoothControl.SelectedIndex] as BluetoothList;
                var UnPairResult = await list.DeviceInfo.Pairing.UnpairAsync();
                if (UnPairResult.Status == DeviceUnpairingResultStatus.Unpaired || UnPairResult.Status == DeviceUnpairingResultStatus.AlreadyUnpaired)
                {
                    list.OnPropertyChanged("CancelOrPairButton");
                    list.OnPropertyChanged("Name");
                    list.OnPropertyChanged("IsPaired");
                }
            }
        }

        private async Task Pair(DeviceInformation DeviceInfo)
        {
            DevicePairingKinds PairKinds = DevicePairingKinds.ConfirmOnly | DevicePairingKinds.ConfirmPinMatch;
            var CustomPairInfo = DeviceInfo.Pairing.Custom;
            CustomPairInfo.PairingRequested += CustomPairInfo_PairingRequested;
            DevicePairingResult PairResult = await CustomPairInfo.PairAsync(PairKinds, DevicePairingProtectionLevel.EncryptionAndAuthentication);
            CustomPairInfo.PairingRequested -= CustomPairInfo_PairingRequested;
            if (PairResult.Status == DevicePairingResultStatus.Paired)
            {
                DeviceWatcher.Stop();
                BluetoothInfo.Clear();
                DeviceWatcher.Start();
            }
            else Tips.Text = "配对失败";
        }

        private async void CustomPairInfo_PairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
        {
            PairDeferral = args.GetDeferral();
            PairRequestArgs = args;
            switch (args.PairingKind)
            {
                case DevicePairingKinds.ConfirmPinMatch:
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            Tips.Text = "请确认PIN码与配对设备一致\r" + args.Pin;
                            Tips.Visibility = Visibility.Visible;
                            PinConfirm.Visibility = Visibility.Visible;
                            PinRefuse.Visibility = Visibility.Visible;
                        });
                    }
                    break;
                case DevicePairingKinds.ConfirmOnly:
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            Tips.Text = "请确认是否与配对设备配对";
                            Tips.Visibility = Visibility.Visible;
                            PinConfirm.Visibility = Visibility.Visible;
                            PinRefuse.Visibility = Visibility.Visible;
                        });
                    }
                    break;
            }

        }

        private void PinConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (PairRequestArgs != null)
            {
                PairRequestArgs.Accept();
                PairRequestArgs = null;
            }
            if (PairDeferral != null)
            {
                PairDeferral.Complete();
                PairDeferral = null;
            }
            Tips.Text = "";
            Tips.Visibility = Visibility.Collapsed;
            PinConfirm.Visibility = Visibility.Collapsed;
            PinRefuse.Visibility = Visibility.Collapsed;
        }

        private void PinRefuse_Click(object sender, RoutedEventArgs e)
        {
            if (PairDeferral != null)
            {
                PairDeferral.Complete();
                PairDeferral = null;
            }
            Tips.Text = "";
            Tips.Visibility = Visibility.Collapsed;
            PinConfirm.Visibility = Visibility.Collapsed;
            PinRefuse.Visibility = Visibility.Collapsed;
        }

        private void RefreshContainer_RefreshRequested(RefreshContainer sender, RefreshRequestedEventArgs args)
        {
            RefreshDeferral = args.GetDeferral();
            BluetoothInfo.Clear();
            CreateBluetoothWatcher();
            RefreshDeferral.Complete();
            RefreshDeferral.Dispose();
            RefreshDeferral = null;
        }
    }

}
