using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Security.Credentials;
using Windows.Services.Store;
using Windows.Storage;
using Windows.System;
using Windows.UI.Notifications;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace SmartLens
{
    public sealed partial class MainPage : Page
    {
        public static MainPage ThisPage { get; set; }
        private ApplicationTrigger ProcessingTrigger;
        private BackgroundTaskRegistration TaskRegistration;
        private Dictionary<Type, string> PageDictionary;
        private StoreContext Context;
        private IReadOnlyList<StorePackageUpdate> Updates;


        public MainPage()
        {
            InitializeComponent();
            Window.Current.SetTitleBar(TitleBar);
            ThisPage = this;
            Loaded += MainPage_Loaded;
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is string Para)
            {
                if (Para == "UpdateIntegrityDataRequest")
                {
                    RegisterUpdateBackgroundTask();
                    await LaunchUpdateBackgroundTaskAsync();
                }
            }
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _ = await StorageFolder.GetFolderFromPathAsync("C:\\");
            }
            catch (UnauthorizedAccessException)
            {
                ContentDialog Dialog = new ContentDialog
                {
                    Title = "提示",
                    Content = "请开启此应用的文件系统访问权限以正常工作\r\r然后重新启动该应用",
                    PrimaryButtonText = "导航至权限页",
                    CloseButtonText = "关闭应用"
                };
                switch (await Dialog.ShowAsync())
                {
                    case ContentDialogResult.Primary:
                        await Launcher.LaunchUriAsync(new Uri("ms-settings:appsfeatures-app"));
                        ToastContent Content = new ToastContent()
                        {
                            Scenario = ToastScenario.Reminder,

                            Visual = new ToastVisual()
                            {
                                BindingGeneric = new ToastBindingGeneric()
                                {
                                    Children =
                                    {
                                        new AdaptiveText()
                                        {
                                            Text = "正在等待用户完成操作..."
                                        },

                                        new AdaptiveText()
                                        {
                                            Text = "请开启文件系统权限"
                                        },

                                        new AdaptiveText()
                                        {
                                            Text = "随后点击下方的立即启动"
                                        }
                                    }
                                }
                            },

                            Actions = new ToastActionsCustom
                            {
                                Buttons =
                                {
                                    new ToastButton("立即启动","Restart")
                                    {
                                        ActivationType =ToastActivationType.Foreground
                                    },
                                    new ToastButtonDismiss("稍后")
                                }
                            }
                        };
                        ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
                        Application.Current.Exit();
                        break;
                    default:
                        Application.Current.Exit();
                        break;
                }
                return;
            }


            var View = ApplicationView.GetForCurrentView();
            if (ApplicationData.Current.LocalSettings.Values["EnableScreenCapture"] is bool Enable)
            {
                View.IsScreenCaptureEnabled = Enable;
            }
            else
            {
                View.IsScreenCaptureEnabled = false;
                ApplicationData.Current.LocalSettings.Values["EnableScreenCapture"] = false;
            }

            if (ApplicationData.Current.LocalSettings.Values["EmailProtectionMode"] == null && await KeyCredentialManager.IsSupportedAsync())
            {
                ApplicationData.Current.LocalSettings.Values["EmailProtectionMode"] = true;
            }

            if (ApplicationData.Current.LocalSettings.Values["UseInsideWebBrowser"] == null)
            {
                ApplicationData.Current.LocalSettings.Values["UseInsideWebBrowser"] = true;
            }

            PageDictionary = new Dictionary<Type, string>()
            {
                {typeof(HomePage), "首页"},
                {typeof(MusicPage), "音乐"},
                {typeof(VoiceRec), "语音识别"},
                {typeof(WebTab), "网页浏览"},
                {typeof(About),"关于" },
                {typeof(ChangeLog),"关于" },
                {typeof(USBControl),"USB管理" },
                {typeof(EmailPage),"邮件" },
                {typeof(CodeScanner),"QR识别" }
            };

            if (ApplicationData.Current.LocalSettings.Values["USBActivateRequest"] is bool IsUSB && IsUSB)
            {
                ApplicationData.Current.LocalSettings.Values["USBActivateRequest"] = null;
                NavigationView.SelectedItem = NavigationView.MenuItems[5] as NavigationViewItemBase;
                NavFrame.Navigate(typeof(USBControl), NavFrame);
            }
            else
            {
                NavigationView.SelectedItem = NavigationView.MenuItems.FirstOrDefault() as NavigationViewItemBase;
                NavFrame.Navigate(typeof(HomePage), NavFrame);
            }

            await CheckAndInstallUpdate();
        }

        private async Task CheckAndInstallUpdate()
        {
            Context = StoreContext.GetDefault();
            Updates = await Context.GetAppAndOptionalStorePackageUpdatesAsync();

            if (Updates.Count > 0)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "更新可用",
                    Content = "SmartLens有新的更新啦😊😁（￣︶￣）↗　\rSmartLens的最新更新将修补诸多的小问题，并提供有意思的小功能\rSmartLens具备自动更新的功能，稍后将自动更新\r⇱或⇲\r您也可以访问Microsoft Store手动更新哦~~~~",
                    CloseButtonText = "稍后提示",
                    PrimaryButtonText = "立即下载"
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    SendUpdatableToastWithProgress();

                    Progress<StorePackageUpdateStatus> UpdateProgress = new Progress<StorePackageUpdateStatus>((Status) =>
                    {
                        string Tag = "SmartLens-Updating";

                        var data = new NotificationData
                        {
                            SequenceNumber = 0
                        };
                        data.Values["ProgressValue"] = (Status.PackageDownloadProgress * 1.25).ToString("0.##");
                        data.Values["ProgressString"] = Math.Ceiling(Status.PackageDownloadProgress * 125).ToString() + "%";

                        ToastNotificationManager.CreateToastNotifier().Update(data, Tag);
                    });

                    if (Context.CanSilentlyDownloadStorePackageUpdates)
                    {
                        StorePackageUpdateResult DownloadResult = await Context.TrySilentDownloadAndInstallStorePackageUpdatesAsync(Updates).AsTask(UpdateProgress);

                        if (DownloadResult.OverallState != StorePackageUpdateState.Completed)
                        {
                            ShowErrorNotification();
                        }
                    }
                    else
                    {
                        StorePackageUpdateResult DownloadResult = await Context.RequestDownloadAndInstallStorePackageUpdatesAsync(Updates).AsTask(UpdateProgress);

                        if (DownloadResult.OverallState != StorePackageUpdateState.Completed)
                        {
                            ShowErrorNotification();
                        }
                    }
                }
            }
        }

        private void ShowErrorNotification()
        {
            var Content = new ToastContent()
            {
                Scenario = ToastScenario.Default,
                Launch = "UpdateError",
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = "更新失败"
                            },

                            new AdaptiveText()
                            {
                                Text = "SmartLens无法更新至最新版"
                            }
                        }
                    }
                },
            };
            ToastNotificationManager.History.Clear();
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
        }

        private void SendUpdatableToastWithProgress()
        {
            string Tag = "SmartLens-Updating";

            var content = new ToastContent()
            {
                Launch = "Updating",
                Scenario = ToastScenario.Reminder,
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = "SmartLens更新下载中..."
                            },

                            new AdaptiveProgressBar()
                            {
                                Title = "正在更新",
                                Value = new BindableProgressBarValue("ProgressValue"),
                                Status = new BindableString("ProgressStatus"),
                                ValueStringOverride = new BindableString("ProgressString")
                            }
                        }
                    }
                }
            };

            var Toast = new ToastNotification(content.GetXml())
            {
                Tag = Tag,
                Data = new NotificationData()
            };
            Toast.Data.Values["ProgressValue"] = "0";
            Toast.Data.Values["ProgressStatus"] = "正在下载...";
            Toast.Data.Values["ProgressString"] = "0%";
            Toast.Data.SequenceNumber = 0;

            ToastNotificationManager.History.Clear();
            ToastNotificationManager.CreateToastNotifier().Show(Toast);
        }


        private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                NavFrame.Navigate(typeof(SettingsPage));
            }
            else
            {
                switch (args.InvokedItem.ToString())
                {
                    case "首页": NavFrame.Navigate(typeof(HomePage), NavFrame); break;
                    case "音乐": NavFrame.Navigate(typeof(MusicPage)); break;
                    case "语音识别": NavFrame.Navigate(typeof(VoiceRec)); break;
                    case "网页浏览": NavFrame.Navigate(typeof(WebTab)); break;
                    case "关于": NavFrame.Navigate(typeof(About)); break;
                    case "USB管理": NavFrame.Navigate(typeof(USBControl)); break;
                    case "邮件": NavFrame.Navigate(typeof(EmailPage)); break;
                    case "QR识别": NavFrame.Navigate(typeof(CodeScanner)); break;
                }
            }
        }

        private void NavigationView_Loaded(object sender, RoutedEventArgs e)
        {
            KeyboardAccelerator GoBack = new KeyboardAccelerator
            {
                Key = VirtualKey.GoBack
            };
            GoBack.Invoked += BackInvoked;
            KeyboardAccelerator AltLeft = new KeyboardAccelerator
            {
                Key = VirtualKey.Left
            };
            AltLeft.Invoked += BackInvoked;
            KeyboardAccelerators.Add(GoBack);
            KeyboardAccelerators.Add(AltLeft);
            AltLeft.Modifiers = VirtualKeyModifiers.Menu;

        }

        private void BackInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            BackRequested();
            args.Handled = true;
        }

        /// <summary>
        /// 请求后退
        /// </summary>
        private async void BackRequested()
        {
            switch (NavFrame.CurrentSourcePageType.Name)
            {
                case "MusicPage":
                    {
                        if (MusicPage.ThisPage.MusicNav.CanGoBack)
                        {
                            string LastPageName = MusicPage.ThisPage.MusicNav.CurrentSourcePageType.Name;
                            MusicPage.ThisPage.MusicNav.GoBack();
                            if (LastPageName == "MusicDetail")
                            {
                                try
                                {
                                    ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("DetailBackAnimation");
                                    if (animation != null)
                                    {
                                        animation.Configuration = new BasicConnectedAnimationConfiguration();
                                        animation.TryStart(MusicPage.ThisPage.PicturePlaying);
                                        await Task.Delay(500);
                                    }
                                }
                                finally
                                {
                                    MusicPage.ThisPage.PictureBackup.Visibility = Visibility.Collapsed;
                                }
                            }
                        }
                        else if (NavFrame.CanGoBack)
                        {
                            NavFrame.GoBack();
                        }
                        break;
                    }
                case "USBControl":
                    {
                        if (USBControl.ThisPage.Nav.CanGoBack)
                        {
                            USBControl.ThisPage.Nav.GoBack();
                        }
                        else if (NavFrame.CanGoBack)
                        {
                            NavFrame.GoBack();
                        }
                        break;
                    }
                default:
                    {
                        if (NavFrame.CanGoBack)
                        {
                            NavFrame.GoBack();
                        }
                        break;
                    }
            }
        }

        private void NavigationView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            BackRequested();
        }

        private void NavFrame_Navigated(object sender, NavigationEventArgs e)
        {
            if (About.IsEnterChangeLog)
            {
                About.IsEnterChangeLog = false;
                return;
            }
            NavigationView.IsBackEnabled = NavFrame.CanGoBack;

            if (NavFrame.SourcePageType == typeof(SettingsPage))
            {
                NavigationView.SelectedItem = NavigationView.SettingsItem as NavigationViewItem;
            }
            else
            {
                string stringTag = PageDictionary[NavFrame.SourcePageType];

                foreach (var MenuItem in from NavigationViewItemBase MenuItem in NavigationView.MenuItems
                                         where MenuItem is NavigationViewItem && MenuItem.Content.ToString() == stringTag
                                         select MenuItem)
                {
                    MenuItem.IsSelected = true;
                    break;
                }
            }
        }

        private void NavFrame_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            if (NavFrame.CurrentSourcePageType == e.SourcePageType)
            {
                e.Cancel = true;
            }
        }


        private async Task LaunchUpdateBackgroundTaskAsync()
        {
            bool success = true;

            if (ProcessingTrigger != null)
            {
                ApplicationTriggerResult ActivationResult = await ProcessingTrigger.RequestAsync();

                switch (ActivationResult)
                {
                    case ApplicationTriggerResult.Allowed:
                        break;
                    case ApplicationTriggerResult.CurrentlyRunning:

                    case ApplicationTriggerResult.DisabledByPolicy:

                    case ApplicationTriggerResult.UnknownError:
                        success = false;
                        break;
                }

                if (!success)
                {
                    TaskRegistration.Unregister(false);
                    ApplicationData.Current.LocalSettings.Values["CurrentVersion"] = "ReCalculateNextTime";
                }
            }

        }

        private void RegisterUpdateBackgroundTask()
        {
            ProcessingTrigger = new ApplicationTrigger();

            BackgroundTaskBuilder TaskBuilder = new BackgroundTaskBuilder
            {
                Name = "UpdateBackgroundTask",
                TaskEntryPoint = "UpdateBackgroundTask.UpdateTask"
            };
            TaskBuilder.SetTrigger(ProcessingTrigger);

            foreach (var RegistedTask in from RegistedTask in BackgroundTaskRegistration.AllTasks
                                         where RegistedTask.Value.Name == "UpdateBackgroundTask"
                                         select RegistedTask)
            {
                RegistedTask.Value.Unregister(true);
            }

            TaskRegistration = TaskBuilder.Register();
            TaskRegistration.Completed += TaskRegistration_Completed;
        }

        private void TaskRegistration_Completed(BackgroundTaskRegistration sender, BackgroundTaskCompletedEventArgs args)
        {
            TaskRegistration.Completed -= TaskRegistration_Completed;
            sender.Unregister(true);
        }
    }
}
