﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.SpeechRecognition;
using Windows.Media.SpeechSynthesis;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace SmartLens
{
    public sealed partial class VoiceRec : Page
    {
        SpeechRecognizer SpeechRec;
        SpeechSynthesizer SpeechSynth;
        bool IsRecognizing = false;
        Task LoadTask;
        CancellationTokenSource Cancellation;
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
                Cancellation = new CancellationTokenSource();
                SpeechRec = new SpeechRecognizer();
                SpeechSynth = new SpeechSynthesizer();

                //获取SRGS.grxml识别语法文件
                var GrammarFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///VoiceRec/SRGS.grxml"));

                //创建该文件的语法约束并添加至语音识别的约束集合
                var SRGSConstraint = new SpeechRecognitionGrammarFileConstraint(GrammarFile, "Control");
                SpeechRec?.Constraints.Add(SRGSConstraint);

                //要播放音乐，则必须动态从数据库取出音乐名称的数据，并添加语法约束
                var SongNames = await SQLite.GetInstance().GetAllMusicNameAsync();

                if (SongNames != null)
                {
                    //若存在音乐数据，则添加语法约束
                    var PlayConstraint = new SpeechRecognitionListConstraint(from item in SongNames select string.Format("{0}{1}", "播放", item), "ChooseMusic");
                    SpeechRec?.Constraints.Add(PlayConstraint);
                }

                //编译所有语法约束
                await SpeechRec.CompileConstraintsAsync();
            });
        }

        private async void Ellipse_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            lock (SyncRootProvider.SyncRoot)
            {
                if (IsRecognizing)
                {
                    return;
                }
                IsRecognizing = true;
            }

            AudioPlay.Stop();

            StatusText.Visibility = Visibility.Collapsed;
            ListeningDisplay.Visibility = Visibility.Visible;

            string RecResult = await WindowsLocalRecognizeAsync();

            if (RecResult == null)
            {
                IsRecognizing = false;
                return;
            }
            if (RecResult == "Failure")
            {
                ListeningDisplay.Visibility = Visibility.Collapsed;
                StatusText.Visibility = Visibility.Visible;
                StatusText.Text = "麦克风未检测到声音输入";
            }
            else
            {
                ListeningDisplay.Visibility = Visibility.Collapsed;
                StatusText.Visibility = Visibility.Visible;
                StatusText.Text = RecResult;
            }

            SpeechSynthesisStream stream = await SpeechSynth.SynthesizeTextToStreamAsync(StatusText.Text);
            AudioPlay.SetSource(stream, stream.ContentType);
            AudioPlay.Play();

            IsRecognizing = false;
        }

        protected async override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (IsRecognizing)
            {
                Cancellation?.Cancel();
                await SpeechRec?.StopRecognitionAsync();
            }
            else
            {
                Cancellation?.Dispose();
                Cancellation = null;
            }

            AudioPlay.Stop();
            AudioPlay.Source = null;

            SpeechRec?.Dispose();
            SpeechRec = null;

            SpeechSynth?.Dispose();
            SpeechSynth = null;
            LoadTask = null;
            StatusText.Visibility = Visibility.Collapsed;
            ListeningDisplay.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 异步启动Windows本地语音识别
        /// </summary>
        /// <returns>识别结果</returns>
        private async Task<string> WindowsLocalRecognizeAsync()
        {
            if (!LoadTask.IsCompleted)
            {
                return "正在初始化，请稍后再试";
            }

            SpeechRecognitionResult Result;
            try
            {
                Result = await SpeechRec.RecognizeAsync();
            }
            catch (Exception)
            {
                return null;
            }
            if (Cancellation.IsCancellationRequested)
            {
                Cancellation.Dispose();
                Cancellation = null;
                return null;
            }

            if (Result.Status == SpeechRecognitionResultStatus.Success && Result.RulePath != null)
            {
                switch (Result.Constraint.Tag)
                {
                    case "Control":
                        {
                            List<string> Path = Result.RulePath.ToList();
                            if (Path.Count >= 2)
                            {
                                switch (Path[1])
                                {
                                    case "Play":
                                        {
                                            PlayCommanded?.Invoke(null, null);
                                            return Result.Text;
                                        }
                                    case "Pause":
                                        {
                                            PauseCommanded?.Invoke(null, null);
                                            return Result.Text;
                                        }
                                    case "NextSong":
                                        {
                                            NextSongCommanded?.Invoke(null, null);
                                            return Result.Text;
                                        }
                                    case "PreviousSong":
                                        {
                                            PreviousSongCommanded?.Invoke(null, null);
                                            return Result.Text;
                                        }
                                    case "Weather":
                                        {
                                            string WeatherInfo = HomePage.ThisPage.WeatherCtr.GetTodayWeatherDescribtion();
                                            return WeatherInfo ?? "天气数据加载中...暂时无法查询";
                                        }

                                    default:
                                        return "Unrecognized未知命令";
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
                        return "Unrecognized未知命令";
                }

            }
            else
            {
                return "Failure";
            }
        }
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

