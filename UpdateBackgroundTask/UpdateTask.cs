using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                await ComputeAndStorageHeshAsync();
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

        private async Task ComputeAndStorageHeshAsync()
        {
            var InstallFolder = Package.Current.InstalledLocation;
            List<StorageFile> FileList = new List<StorageFile>();

            await EnumAllFileAsync(InstallFolder, FileList);
            List<KeyValuePair<string, string>> CalculateResult = await ComputeHashAsync(FileList.Where((x, i) => FileList.FindIndex(y => y.Name == x.Name) == i).ToList());

            using (SQLite SQL = new SQLite())
            {
                SQL.SetHeshValueAsync(CalculateResult);
            }
        }

        private IReadOnlyList<StorageFile[]> SplitToArray(List<StorageFile> list, int GroupNum)
        {
            if (GroupNum == 0)
            {
                return null;
            }

            if (list.Count < GroupNum || GroupNum == 1)
            {
                return new List<StorageFile[]>(GroupNum)
                {
                    list.ToArray()
                };
            }

            int BlockLength = list.Count / GroupNum;
            List<StorageFile[]> Result = new List<StorageFile[]>(GroupNum);

            for (int i = 0; i < GroupNum; i++)
            {
                if (i == GroupNum - 1)
                {
                    int RestLength = list.Count - BlockLength * i;
                    StorageFile[] array = new StorageFile[RestLength];
                    list.CopyTo(BlockLength * i, array, 0, RestLength);
                    Result.Add(array);
                }
                else
                {
                    StorageFile[] array = new StorageFile[BlockLength];
                    list.CopyTo(BlockLength * i, array, 0, BlockLength);
                    Result.Add(array);
                }
            }
            return Result;
        }


        private async Task<List<KeyValuePair<string, string>>> ComputeHashAsync(List<StorageFile> FileList)
        {
            IReadOnlyList<StorageFile[]> FileGroup = SplitToArray(FileList, Environment.ProcessorCount);
            Task<List<KeyValuePair<string, string>>>[] TaskGroup = new Task<List<KeyValuePair<string, string>>>[Environment.ProcessorCount];

            for (int i = 0; i < Environment.ProcessorCount; i++)
            {
                TaskGroup[i] = Task.Factory.StartNew(new Func<object, List<KeyValuePair<string, string>>>((e) =>
                {
                    StorageFile[] FileCollection = e as StorageFile[];
                    List<KeyValuePair<string, string>> Result = new List<KeyValuePair<string, string>>(FileCollection.Length);
                    using (SHA256 SHA = SHA256.Create())
                    {
                        foreach (var file in FileCollection)
                        {
                            using (Stream stream = file.OpenStreamForReadAsync().Result)
                            {
                                byte[] Val = SHA.ComputeHash(stream);
                                StringBuilder sb = new StringBuilder();
                                for (int n = 0; n < Val.Length; n++)
                                {
                                    _ = sb.Append(Val[n].ToString("x2"));
                                }
                                Result.Add(new KeyValuePair<string, string>(file.Name, sb.ToString()));
                            }
                        }
                    }
                    return Result;

                }), FileGroup[i]);
            }

            List<KeyValuePair<string, string>> CalculateResult = new List<KeyValuePair<string, string>>(FileList.Count);

            foreach (var Result in await Task.WhenAll(TaskGroup))
            {
                CalculateResult.AddRange(Result);
            }

            return CalculateResult;
        }

        private async Task EnumAllFileAsync(StorageFolder Folder, List<StorageFile> FileList)
        {
            foreach (var file in await Folder.GetFilesAsync())
            {
                if (file.Name == "SmartLens.exe")
                {
                    continue;
                }
                FileList.Add(file);
            }

            IReadOnlyList<StorageFolder> FolderList = await Folder.GetFoldersAsync();
            if (FolderList.Count != 0)
            {
                foreach (var folder in FolderList)
                {
                    await EnumAllFileAsync(folder, FileList);
                }
            }
        }


        private void BackTaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            ApplicationData.Current.LocalSettings.Values["CurrentVersion"] = "ReCalculateNextTime";
        }

    }
}
