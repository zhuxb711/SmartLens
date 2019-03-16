using Bluetooth.Core.Services;
using Bluetooth.Services.Obex;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace SmartLens
{
    public sealed partial class BluetoothUI : ContentDialog
    {
        ObservableCollection<BluetoothList> BluetoothDeviceCollection;
        List<BluetoothDevice> PairedBluetoothDeviceCollection;
        AutoResetEvent PinLock = null;
        DeviceWatcher DeviceWatcher = null;
        private int LastSelectIndex = -1;
        private readonly object Locker = new object();
        private bool ClosePermission = true;
        private bool IsPinConfirm = false;
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
            if (DeviceWatcher != null)
            {
                DeviceWatcher.Stop();
                DeviceWatcher = null;
            }

            PinLock.Dispose();
            PinLock = null;
            BluetoothDeviceCollection.Clear();
            BluetoothDeviceCollection = null;
            PairedBluetoothDeviceCollection.Clear();
            PairedBluetoothDeviceCollection = null;
        }

        private void BluetoothUI_Loaded(object sender, RoutedEventArgs e)
        {
            PairedBluetoothDeviceCollection = new List<BluetoothDevice>();
            BluetoothDeviceCollection = new ObservableCollection<BluetoothList>();
            BluetoothControl.ItemsSource = BluetoothDeviceCollection;
            PinLock = new AutoResetEvent(false);
            CreateBluetoothWatcher();
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (BluetoothControl.SelectedIndex == -1 || !BluetoothDeviceCollection[BluetoothControl.SelectedIndex].DeviceInfo.Pairing.IsPaired)
            {
                Tips.Text = "请先选择一个已配对的设备";
                Tips.Visibility = Visibility.Visible;
                ClosePermission = false;
                return;
            }
            ClosePermission = true;

            try
            {
                //首先连接到RFComm服务，获取到设备的规范名称
                string CanonicalName = await ConnectToRfcommServiceAsync(BluetoothDeviceCollection[BluetoothControl.SelectedIndex]);

                BluetoothService BTService = BluetoothService.GetDefault();
                BTService.SearchForPairedDevicesSucceeded += (s, e) =>
                {
                    PairedBluetoothDeviceCollection = e.PairedDevices;
                };

                //能到这里说明该设备已经配对，启动搜索，完成后PairedBluetoothDeviceCollection被填充
                await BTService.SearchForPairedDevicesAsync();
                foreach (var BTDevice in from BTDevice in PairedBluetoothDeviceCollection
                                             //找到符合刚刚RFComm服务获取到的规范名称的蓝牙设备
                                         where BTDevice.DeviceHost.CanonicalName == CanonicalName
                                         select BTDevice)
                {
                    //从该设备的BluetoothDevice对象获取到Obex服务的实例
                    ObexServiceProvider.SetObexInstance(ObexService.GetDefaultForBluetoothDevice(BTDevice));
                    break;
                }

                WebPage.ThisPage?.ResetEvent?.Set();

                if (ObexServiceProvider.ObexClient == null)
                {
                    throw new Exception("未能找到已配对的设备，请打开该设备的蓝牙开关");
                }
            }
            catch (Exception e)
            {
                Tips.Text = e.Message;
                Tips.Visibility = Visibility.Visible;
                ClosePermission = false;
            }
        }

        /// <summary>
        /// 创建蓝牙的检测器，检测器将定期检测蓝牙设备
        /// </summary>
        public void CreateBluetoothWatcher()
        {
            if (DeviceWatcher != null)
            {
                DeviceWatcher.Stop();
                DeviceWatcher = null;
                Progress.IsActive = true;
                StatusText.Text = "正在搜索";
            }

            //根据指定的筛选条件创建检测器
            DeviceWatcher = DeviceInformation.CreateWatcher("(System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\")", new string[] { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.Bluetooth.Le.IsConnectable" }, DeviceInformationKind.AssociationEndpoint);

            DeviceWatcher.Added += async (s, e) =>
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    lock (Locker)
                    {
                        try
                        {
                            if (BluetoothDeviceCollection != null)
                            {
                                BluetoothDeviceCollection.Add(new BluetoothList(e));
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
                            if (BluetoothDeviceCollection != null)
                            {
                                for (int i = 0; i < BluetoothDeviceCollection.Count; i++)
                                {
                                    if (BluetoothDeviceCollection[i].Id == e.Id)
                                    {
                                        BluetoothDeviceCollection[i].Update(e);
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
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    lock (Locker)
                    {
                        try
                        {
                            if (BluetoothDeviceCollection != null)
                            {
                                for (int i = 0; i < BluetoothDeviceCollection.Count; i++)
                                {
                                    if (BluetoothDeviceCollection[i].Id == e.Id)
                                    {
                                        BluetoothDeviceCollection.RemoveAt(i);
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
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Progress.IsActive = false;
                    StatusText.Text = "搜索完成";
                });
            };

            DeviceWatcher.Start();
        }

        /// <summary>
        /// 连接到指定的蓝牙设备的RFComm服务
        /// </summary>
        /// <param name="BL">要连接到的设备</param>
        /// <returns>主机对象的规范名称</returns>
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
                await PairAsync(BluetoothDeviceCollection[LastSelectIndex].DeviceInfo);
            }
            else
            {
                var list = BluetoothDeviceCollection[BluetoothControl.SelectedIndex];
                var UnPairResult = await list.DeviceInfo.Pairing.UnpairAsync();
                if (UnPairResult.Status == DeviceUnpairingResultStatus.Unpaired || UnPairResult.Status == DeviceUnpairingResultStatus.AlreadyUnpaired)
                {
                    list.OnPropertyChanged("CancelOrPairButton");
                    list.OnPropertyChanged("Name");
                    list.OnPropertyChanged("IsPaired");
                }
            }
        }

        /// <summary>
        /// 异步启动蓝牙的配对过程
        /// </summary>
        /// <param name="DeviceInfo"></param>
        /// <returns></returns>
        private async Task PairAsync(DeviceInformation DeviceInfo)
        {
            DevicePairingKinds PairKinds = DevicePairingKinds.ConfirmOnly | DevicePairingKinds.ConfirmPinMatch;

            DeviceInformationCustomPairing CustomPairing = DeviceInfo.Pairing.Custom;

            CustomPairing.PairingRequested += CustomPairInfo_PairingRequested;

            DevicePairingResult PairResult = await CustomPairing.PairAsync(PairKinds, DevicePairingProtectionLevel.EncryptionAndAuthentication);

            CustomPairing.PairingRequested -= CustomPairInfo_PairingRequested;

            if (PairResult.Status == DevicePairingResultStatus.Paired)
            {
                DeviceWatcher.Stop();
                BluetoothDeviceCollection.Clear();
                DeviceWatcher.Start();
            }
            else
            {
                Tips.Text = "配对失败";
            }
        }

        private async void CustomPairInfo_PairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
        {
            Deferral PairDeferral = args.GetDeferral();

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
                        break;
                    }
                case DevicePairingKinds.ConfirmOnly:
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            Tips.Text = "请确认是否与配对设备配对";
                            Tips.Visibility = Visibility.Visible;
                            PinConfirm.Visibility = Visibility.Visible;
                            PinRefuse.Visibility = Visibility.Visible;
                        });
                        break;
                    }
            }
            await Task.Run(() =>
            {
                PinLock.WaitOne();

                if (IsPinConfirm)
                {
                    args.Accept();
                }
            });

            PairDeferral.Complete();
        }

        private void PinConfirm_Click(object sender, RoutedEventArgs e)
        {
            IsPinConfirm = true;
            PinLock.Set();
            Tips.Text = "";
            Tips.Visibility = Visibility.Collapsed;
            PinConfirm.Visibility = Visibility.Collapsed;
            PinRefuse.Visibility = Visibility.Collapsed;
        }

        private void PinRefuse_Click(object sender, RoutedEventArgs e)
        {
            IsPinConfirm = false;
            PinLock.Set();
            Tips.Text = "";
            Tips.Visibility = Visibility.Collapsed;
            PinConfirm.Visibility = Visibility.Collapsed;
            PinRefuse.Visibility = Visibility.Collapsed;
        }

        private void RefreshContainer_RefreshRequested(RefreshContainer sender, RefreshRequestedEventArgs args)
        {
            Deferral RefreshDeferral = args.GetDeferral();
            BluetoothDeviceCollection.Clear();
            CreateBluetoothWatcher();
            RefreshDeferral.Complete();
        }
    }

}
