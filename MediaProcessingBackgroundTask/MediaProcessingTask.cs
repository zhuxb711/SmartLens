using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace MediaProcessingBackgroundTask
{
    public sealed class MediaProcessingTask : IBackgroundTask
    {
        IBackgroundTaskInstance BackTaskInstance;
        BackgroundTaskDeferral Deferral;
        CancellationTokenSource Cancellation;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            Cancellation = new CancellationTokenSource();
            BackTaskInstance = taskInstance;
            BackTaskInstance.Canceled += BackTaskInstance_Canceled;
            BackTaskInstance.Progress = 0;
            Deferral = BackTaskInstance.GetDeferral();

            await TranscodeMediaAsync();

            Deferral.Complete();
        }

        private async Task TranscodeMediaAsync()
        {
            MediaTranscoder Transcoder = new MediaTranscoder
            {
                HardwareAccelerationEnabled = true
            };

            if (ApplicationData.Current.LocalSettings.Values["MediaTranscodeInputFilePath"] is string InputFilePath
                && ApplicationData.Current.LocalSettings.Values["MediaTranscodeOutputFilePath"] is string OutputFilePath)
            {
                try
                {
                    StorageFile InputFile = await StorageFile.GetFileFromPathAsync(InputFilePath);
                    StorageFile OutputFile = await StorageFile.GetFileFromPathAsync(OutputFilePath);
                    MediaEncodingProfile Profile = MediaEncodingProfile.CreateHevc(VideoEncodingQuality.HD1080p);
                    PrepareTranscodeResult Result = await Transcoder.PrepareFileTranscodeAsync(InputFile, OutputFile, Profile);
                    if (Result.CanTranscode)
                    {
                        Progress<double> TranscodeProgress = new Progress<double>(ProgressHandler);
                        await Result.TranscodeAsync().AsTask(Cancellation.Token, TranscodeProgress);
                    }
                    else
                    {
                        Debug.WriteLine("无法转换原因" + Result.FailureReason.ToString());
                    }
                }
                catch(Exception e)
                {
                    Debug.WriteLine("出现错误：" + e.Message);
                }
            }

        }

        private void ProgressHandler(double CurrentValue)
        {
            BackTaskInstance.Progress = (uint)CurrentValue;
        }

        private void BackTaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            throw new NotImplementedException();
        }
    }
}
