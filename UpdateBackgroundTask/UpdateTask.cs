using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using Windows.UI.Notifications;

namespace UpdateBackgroundTask
{
    public sealed class UpdateTask : IBackgroundTask
    {
        IBackgroundTaskInstance BackTaskInstance;
        BackgroundTaskDeferral Deferral;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            BackTaskInstance = taskInstance;
            BackTaskInstance.Canceled += BackTaskInstance_Canceled;
            Deferral = BackTaskInstance.GetDeferral();

            if (await Package.Current.VerifyContentIntegrityAsync())
            {
                await CalculateAndStorageMD5Async();
            }

            ShowCompleteNotification();
            Deferral.Complete();
        }

        private void ShowCompleteNotification()
        {
            var Content = new ToastContent()
            {
                Scenario = ToastScenario.Default,
                Launch = "Update",
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = "初始化完成"
                            },

                            new AdaptiveText()
                            {
                               Text = "SmartLens已成功完成更新后初始化任务"
                            },

                            new AdaptiveText()
                            {
                               Text = "SQLite数据库已更新"
                            }
                        }
                    }
                },
            };
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));

        }

        private async Task CalculateAndStorageMD5Async()
        {
            var InstallFolder = Package.Current.InstalledLocation;
            List<KeyValuePair<string, string>> CalculateResult = new List<KeyValuePair<string, string>>();
            await CalculateMD5Async(InstallFolder, CalculateResult);

            using (SQLite SQL = new SQLite())
            {
                SQL.SetMD5ValueAsync(CalculateResult);
            }
        }

        private async Task CalculateMD5Async(StorageFolder Folder, List<KeyValuePair<string, string>> MD5List)
        {
            var FileList = await Folder.GetFilesAsync();
            using (MD5 md5 = new MD5CryptoServiceProvider())
            {
                foreach (var file in FileList)
                {
                    if (file.Name == "SmartLens.exe")
                    {
                        continue;
                    }
                    using (Stream stream = await file.OpenStreamForReadAsync())
                    {
                        byte[] Val = md5.ComputeHash(stream);
                        StringBuilder sb = new StringBuilder();
                        for (int i = 0; i < Val.Length; i++)
                        {
                            sb.Append(Val[i].ToString("x2"));
                        }
                        MD5List.Add(new KeyValuePair<string, string>(file.Name, sb.ToString()));
                    }
                }
            }

            var FolderList = await Folder.GetFoldersAsync();
            if (FolderList.Count != 0)
            {
                foreach (var folder in FolderList)
                {
                    await CalculateMD5Async(folder, MD5List);
                }
            }
        }

        private void BackTaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            ApplicationData.Current.LocalSettings.Values["CurrentVersion"] = "ReCalculateNextTime";
        }
    }
}
