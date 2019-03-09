using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using Windows.Media.SpeechRecognition;
using Windows.Storage;
using System.Threading;


namespace SmartLens
{
    public sealed partial class VoiceRec : Page
    {
        SpeechRecognizer SpeechRec;
        bool IsRecognizing = false;
        Task LoadTask;
        public static event EventHandler PlayCommanded;
        public static event EventHandler PauseCommanded;
        public static event EventHandler<string> MusicChoiceCommanded;
        public static event EventHandler NextSongCommanded;
        public static event EventHandler PreviousSongCommanded;

        public static VoiceRec ThisPage { get; set; }

        public VoiceRec()
        {
            InitializeComponent();
            ThisPage = this;
            Loaded += VoiceRec_Loaded;
        }

        private void VoiceRec_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTask = Task.Run(async () =>
            {
                SpeechRec = new SpeechRecognizer();

                var GrammarFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///VoiceRec/SRGS.grxml"));
                var SRGSConstraint = new SpeechRecognitionGrammarFileConstraint(GrammarFile, "MusicControl");
                SpeechRec.Constraints.Add(SRGSConstraint);
                var SongNames = await SQLite.GetInstance().GetAllMusicName();
                if (SongNames == null)
                {
                    await SpeechRec.CompileConstraintsAsync();
                    return;
                }

                var SongsCommand = SongNames.Select((item) =>
                {
                    return string.Format("{0}{1}", "播放", item);
                });
                var PlayConstraint = new SpeechRecognitionListConstraint(SongsCommand, "ChooseMusic");
                SpeechRec.Constraints.Add(PlayConstraint);
                await SpeechRec.CompileConstraintsAsync();
            });
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            SpeechRec.Dispose();
            LoadTask = null;
        }

        private async Task<string> WindowsLocalRecognizeAsync()
        {
            if(!LoadTask.IsCompleted)
            {
                return "正在初始化，请稍后再试";
            }
            var Result = await SpeechRec.RecognizeAsync();
            if (Result.Status == SpeechRecognitionResultStatus.Success && Result.RulePath != null)
            {
                switch (Result.Constraint.Tag)
                {
                    case "MusicControl":
                        {
                            List<string> Path = Result.RulePath.ToList();
                            if (Path.Count >= 2)
                            {
                                switch (Path[1])
                                {
                                    case "Play":
                                        PlayCommanded?.Invoke(null, null);
                                        return Result.Text;
                                    case "Pause":
                                        PauseCommanded?.Invoke(null, null);
                                        return Result.Text;
                                    case "NextSong":
                                        NextSongCommanded?.Invoke(null, null);
                                        return Result.Text;
                                    case "PreviousSong":
                                        PreviousSongCommanded?.Invoke(null, null);
                                        return Result.Text;
                                    default:
                                        return "Unrecognized";
                                }
                            }
                            else return "None";
                        }
                    case "ChooseMusic":
                        {
                            MusicChoiceCommanded?.Invoke(null, Result.Text.Substring(Result.Text.IndexOf("播放") + 2));
                            return Result.Text;
                        }
                    default:
                        return "Unrecognized";
                }

            }
            else
            {
                return "Failure";
            }
        }

        private async void Ellipse_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (IsRecognizing)
            {
                return;
            }
            IsRecognizing = true;

            ProRing.Visibility = Visibility.Visible;
            ProRing.IsActive = true;
            StatusText.Text = "正在聆听……";
            string temp = await WindowsLocalRecognizeAsync();
            if (temp == "Failure")
            {
                StatusText.Text = "麦克风未检测到声音输入";
            }
            else
            {
                StatusText.Text = temp;
            }
            ProRing.IsActive = false;
            ProRing.Visibility = Visibility.Collapsed;

            IsRecognizing = false;
        }


        #region 百度云识别(弃用)
        //private AudioRecorder Recorder = new AudioRecorder();

        // RecordButton.AddHandler(PointerReleasedEvent, new PointerEventHandler(Button_OnPointerReleased), true);
        /*private async void Button_OnPointerReleased(object sender, PointerRoutedEventArgs e)
        {
            StatusText.Text = "     正在识别……";
            Recorder.StopRecording();
            string temp = await Recorder.StartRecognizeAsync();

            if (temp == null)
            {
                StatusText.Text = "按下说话";
                ContentDialog Dialog = new ContentDialog
                {
                    Content = "网络异常或录音中无有效输入",
                    Title = "提示",
                    CloseButtonText = "确定"
                };
                await Dialog.ShowAsync();
            }
            else
            {
                StatusText.Text = "     " + temp;
            }
            ProRing.IsActive = false;
        }*/
        #endregion

    }
    #region 百度云识别功能实现与录制实现(弃用)
    //public class AudioRecorder
    //{
    //    private MediaCapture _mediaCapture;

    //    private InMemoryRandomAccessStream _memoryBuffer = new InMemoryRandomAccessStream();

    //    public bool IsRecording { get; set; }

    //    private readonly Asr BaiduClient = new Asr("11700828", "yO67mQyuOMuo5gSR6Sot6WHN", "3vU5M1KpF9GC3j3W6DjuqB40BtUGXUlU")
    //    {
    //        Timeout = 60000
    //    };
    //    private readonly MediaEncodingProfile EncodingConfig = MediaEncodingProfile.CreateWav(AudioEncodingQuality.Low);

    //    public async void StartRecording()
    //    {
    //        if (IsRecording)
    //        {
    //            return;
    //        }
    //        MediaCaptureInitializationSettings settings = new MediaCaptureInitializationSettings
    //        {
    //            StreamingCaptureMode = StreamingCaptureMode.Audio
    //        };
    //        _mediaCapture = new MediaCapture();
    //        await _mediaCapture.InitializeAsync(settings);
    //        EncodingConfig.Audio = AudioEncodingProperties.CreatePcm(16000, 1, 16);
    //        await _mediaCapture.StartRecordToStreamAsync(EncodingConfig, _memoryBuffer);
    //        IsRecording = true;
    //    }

    //    public async void StopRecording()
    //    {
    //        try
    //        {
    //            await _mediaCapture.StopRecordAsync();
    //        }
    //        catch (System.Runtime.InteropServices.COMException) { }
    //        IsRecording = false;
    //    }

    //    public Task<string> StartRecognizeAsync()
    //    {
    //        IRandomAccessStream audioStream = _memoryBuffer.CloneStream();
    //        Stream stream = WindowsRuntimeStreamExtensions.AsStreamForRead(audioStream.GetInputStreamAt(0));
    //        try
    //        {
    //            return Task.Run(() =>
    //            {
    //                var Result = BaiduClient.Recognize(stream, "IOT", "wav", 16000, 1537);
    //                return Result["result"]["word"].First.ToString();
    //            });
    //        }
    //        catch (Exception)
    //        {
    //            return null;
    //        }
    //    }

    //}
    #endregion
}

