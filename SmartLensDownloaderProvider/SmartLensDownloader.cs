using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;

namespace SmartLensDownloaderProvider
{
    public enum DownloadResult
    {
        Success = 0,
        TaskCancel = 1,
        Error = 2,
        Unknown = 3
    }

    public enum DownloadState
    {
        Downloading = 0,
        Stopped = 1,
        Paused = 2,
        Error = 3,
        None = 4,
        AlreadyFinished = 5
    }

    public sealed class DownloadOperator : INotifyPropertyChanged, IDisposable
    {
        internal Progress<(long, long)> Progress;

        public DownloadState State { get; private set; } = DownloadState.None;

        public DownloadResult DownloadResult { get; private set; } = DownloadResult.Unknown;

        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler<DownloadOperator> DownloadErrorDetected;

        public event EventHandler<DownloadOperator> DownloadSucceed;

        public event EventHandler<DownloadOperator> DownloadTaskCancel;

        private double percentage;
        public double Percentage
        {
            get => percentage;
            private set
            {
                percentage = value;
                OnPropertyChanged("Percentage");
            }
        }

        private string bytereceived;
        public string ByteReceived
        {
            get => bytereceived;
            private set
            {
                bytereceived = value;
                OnPropertyChanged("ByteReceived");
            }
        }

        private string totalbytecount;
        public string TotalFileSize
        {
            get => totalbytecount;
            private set
            {
                totalbytecount = string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
                OnPropertyChanged("TotalFileSize");
            }
        }

        public Uri Address { get; private set; }

        public StorageFile TempFile { get; private set; }

        internal CancellationTokenSource CancellationToken = new CancellationTokenSource();

        internal ManualResetEvent PauseSignal = new ManualResetEvent(true);

        public string ActualFileName { get; private set; }

        private SmartLensDownloader Downloader = SmartLensDownloader.CurrentInstance;

        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }

        private string GetSizeDescription(long PropertiesSize)
        {
            return PropertiesSize / 1024f < 1024 ? (PropertiesSize / 1024f).ToString("0.00") + " KB" : (PropertiesSize / 1048576 >= 1024 ? (PropertiesSize / 1073741824f).ToString("0.00") + " GB" : (PropertiesSize / 1048576f).ToString("0.00") + " MB");
        }

        public async void StartDownload()
        {
            if (Downloader == null)
            {
                throw new NullReferenceException("不存在SmartLensDownloader实例，无法执行此操作");
            }

            switch (State)
            {
                case DownloadState.Downloading:
                    throw new InvalidOperationException("下载任务已开始");
                case DownloadState.AlreadyFinished:
                    throw new InvalidOperationException("下载任务已完成，此任务已不可用");
                case DownloadState.Error:
                    throw new InvalidOperationException("下载任务出现错误，此任务已不可用");
                case DownloadState.Stopped:
                    throw new InvalidOperationException("下载任务已取消，此任务已不可用");
                case DownloadState.Paused:
                    throw new InvalidOperationException("请使用ResumeDownload恢复下载任务");
            }

            State = DownloadState.Downloading;

            DownloadResult = await Downloader.DownloadFileAsync(this);
            switch (DownloadResult)
            {
                case DownloadResult.Error:
                    DownloadErrorDetected?.Invoke(null, this);
                    State = DownloadState.Error;
                    await TempFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    break;
                case DownloadResult.Success:
                    DownloadSucceed?.Invoke(null, this);
                    State = DownloadState.AlreadyFinished;
                    await TempFile.RenameAsync(ActualFileName, NameCollisionOption.GenerateUniqueName);
                    break;
                case DownloadResult.TaskCancel:
                    DownloadTaskCancel?.Invoke(null, this);
                    State = DownloadState.Stopped;
                    await TempFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    break;
            }

            Dispose();
        }

        public void StopDownload()
        {
            if (Downloader == null)
            {
                throw new NullReferenceException("不存在SmartLensDownloader实例，无法执行此操作");
            }

            switch (State)
            {
                case DownloadState.Error:
                    throw new InvalidOperationException("下载任务出现错误，此任务已不可用");
                case DownloadState.Stopped:
                    throw new InvalidOperationException("下载任务已取消，此任务已不可用");
                case DownloadState.AlreadyFinished:
                    throw new InvalidOperationException("下载任务已完成，此任务已不可用");
            }

            State = DownloadState.Stopped;

            CancellationToken.Cancel();
        }

        public void PauseDownload()
        {
            if (Downloader == null)
            {
                throw new NullReferenceException("不存在SmartLensDownloader实例，无法执行此操作");
            }

            switch (State)
            {
                case DownloadState.Stopped:
                    throw new InvalidOperationException("下载任务已取消，此任务已不可用");
                case DownloadState.Paused:
                    throw new InvalidOperationException("下载任务已暂停");
                case DownloadState.Error:
                    throw new InvalidOperationException("下载任务出现错，此任务已不可用");
                case DownloadState.AlreadyFinished:
                    throw new InvalidOperationException("下载任务已完成，此任务已不可用");
            }

            State = DownloadState.Paused;

            PauseSignal.Reset();
        }

        public void ResumeDownload()
        {
            if (Downloader == null)
            {
                throw new NullReferenceException("不存在SmartLensDownloader实例，无法执行此操作");
            }

            switch (State)
            {
                case DownloadState.Stopped:
                    throw new InvalidOperationException("下载任务已取消，此任务已不可用");
                case DownloadState.Error:
                    throw new InvalidOperationException("下载任务出现错误，此任务已不可用");
                case DownloadState.Downloading:
                    throw new InvalidOperationException("下载任务正在进行中，无法继续任务");
                case DownloadState.AlreadyFinished:
                    throw new InvalidOperationException("下载任务已完成，此任务已不可用");
            }

            State = DownloadState.Downloading;

            PauseSignal.Set();
        }

        internal DownloadOperator(Uri Address, StorageFile TempFile, string ActualFileName)
        {
            this.Address = Address;
            this.TempFile = TempFile;
            this.ActualFileName = ActualFileName;

            Progress = new Progress<(long, long)>();
            Progress.ProgressChanged += Progress_ProgressChanged;
        }

        private void Progress_ProgressChanged(object sender, (long, long) e)
        {
            ByteReceived = GetSizeDescription(e.Item1);
            TotalFileSize = GetSizeDescription(e.Item2);
            Percentage = Math.Ceiling(Convert.ToDouble(e.Item1 * 100 / e.Item2));
        }

        public void Dispose()
        {
            if (State == DownloadState.Downloading || State == DownloadState.Paused)
            {
                throw new InvalidOperationException("暂停和下载状态下不允许注销资源");
            }

            CancellationToken?.Dispose();
            PauseSignal?.Dispose();
            CancellationToken = null;
            PauseSignal = null;
            Downloader = null;
        }
    }

    public sealed class SmartLensDownloader
    {
        private SmartLensDownloader() { }

        public ObservableCollection<DownloadOperator> DownloadList { get; private set; } = new ObservableCollection<DownloadOperator>();

        internal static SmartLensDownloader CurrentInstance;

        public static SmartLensDownloader GetInstance()
        {
            return CurrentInstance ?? (CurrentInstance = new SmartLensDownloader());
        }

        public async Task<DownloadOperator> CreateNewDownloadTask(Uri Address, string SaveFileName)
        {
            if (Address == null || string.IsNullOrWhiteSpace(SaveFileName))
            {
                throw new ArgumentNullException();
            }

            StorageFolder SaveFolder = await StorageApplicationPermissions.FutureAccessList.GetItemAsync("DownloadPath") as StorageFolder;

            if (SaveFolder != null)
            {
                StorageFile TempFile = await SaveFolder.CreateFileAsync("SmartLens_DownloadFile_" + Guid.NewGuid().ToString("N"), CreationCollisionOption.GenerateUniqueName);
                return new DownloadOperator(Address, TempFile, SaveFileName);
            }
            else
            {
                throw new InvalidDataException("StorageApplicationPermissions.FutureAccessList不存在指定的保存文件夹");
            }
        }

        internal Task<DownloadResult> DownloadFileAsync(DownloadOperator Operation)
        {
            DownloadList.Add(Operation);

            return Task.Factory.StartNew((e) =>
            {
                DownloadOperator Para = e as DownloadOperator;

                IProgress<(long, long)> Pro = Para.Progress;

                Stream FileStream = null;
                WebResponse Response = null;
                Stream RemoteStream = null;
                try
                {
                    FileStream = Para.TempFile.OpenStreamForWriteAsync().Result;

                    HttpWebRequest HttpRequest = WebRequest.CreateHttp(Para.Address);
                    HttpRequest.Timeout = 20000;

                    Response = HttpRequest.GetResponse();
                    RemoteStream = Response.GetResponseStream();

                    byte[] BufferArray = new byte[4096];
                    long ByteReceived = 0;
                    int ReadCount = RemoteStream.Read(BufferArray, 0, BufferArray.Length);

                    ByteReceived += ReadCount;
                    Pro.Report((ByteReceived, Response.ContentLength));

                    while (ReadCount > 0 && !Para.CancellationToken.IsCancellationRequested)
                    {
                        FileStream.Write(BufferArray, 0, ReadCount);

                        Para.PauseSignal.WaitOne();

                        ReadCount = RemoteStream.Read(BufferArray, 0, BufferArray.Length);

                        ByteReceived += ReadCount;
                        Pro.Report((ByteReceived, Response.ContentLength));
                    }

                    if (Para.CancellationToken.IsCancellationRequested)
                    {
                        return DownloadResult.TaskCancel;
                    }

                    return DownloadResult.Success;
                }
                catch (Exception)
                {
                    return DownloadResult.Error;
                }
                finally
                {
                    FileStream.Dispose();
                    Response.Dispose();
                    RemoteStream.Dispose();
                }

            }, Operation);
        }
    }

}
