using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;


namespace SmartLens
{
    public partial class Lrc : UserControl
    {

        #region 歌词模型
        public class LrcModel
        {
            /// <summary>
            /// 歌词所在控件
            /// </summary>
            public TextBlock LrcTb { get; set; }

            /// <summary>
            /// 歌词字符串
            /// </summary>
            public string LrcText { get; set; }

            /// <summary>
            /// 时间
            /// </summary>
            public double Time { get; set; }
        }
        #endregion
        #region 变量
        //歌词集合
        public Dictionary<double, LrcModel> Lrcs = new Dictionary<double, LrcModel>();

        //添加当前焦点歌词变量
        public LrcModel FoucsLrc { get; set; }

        Dictionary<TimeSpan, string> TLrcs = new Dictionary<TimeSpan, string>();
        //非焦点歌词颜色
        public SolidColorBrush NoramlLrcColor = new SolidColorBrush(Colors.Black);
        //焦点歌词颜色
        public SolidColorBrush FoucsLrcColor = new SolidColorBrush(Colors.Orange);

        public List<KeyValuePair<double, LrcModel>> SortLrcs;

        int LastIndex = -1;

        #endregion
        public Lrc()
        {
            InitializeComponent();
        }

        #region 加载歌词
        public void LoadLrc(string lrcstr,string Tlrcstr)
        {
            Lrcs.Clear();
            TLrcs.Clear();
            string[] TlrcCollection = null;
            if (Tlrcstr != null)
            {
                TlrcCollection = Tlrcstr.Split('\n');

                foreach (var item in TlrcCollection)
                {
                    if (item.Length > 0 && item.IndexOf(":") != -1)
                    {
                        TimeSpan time = GetTime(item);
                        if (time == TimeSpan.MaxValue)
                        {
                            continue;
                        }
                        TLrcs.Add(time, item.Split(']')[1]);
                    }
                }
            }
            string[] StrCollection = lrcstr.Split('\n');
            for (int i=0;i<StrCollection.Length;i++)
            {
                string str = StrCollection[i];
                if (str.Length > 0 && str.IndexOf(":") != -1)
                {
                    TimeSpan time = GetTime(str);
                    if (time == TimeSpan.MaxValue)
                    {
                        continue;
                    }
                    string lrc=string.Empty;
                    if(TlrcCollection==null)
                    {
                        lrc = str.Split(']')[1];
                    }
                    else
                    {
                        if (TLrcs.ContainsKey(time))
                        {

                            lrc = str.Split(']')[1] + "\r" + TLrcs[time];
                        }
                        else
                        {
                            lrc = str.Split(']')[1];
                        }
                    }

                    TextBlock c_lrcbk = new TextBlock
                    {
                        FontSize = 15,
                        Text = lrc,
                        Foreground = NoramlLrcColor,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    };
                    if (c_lrc_items.Children.Count > 0)
                    {
                        c_lrcbk.Margin = new Thickness(0, 25, 0, 0);
                    }

                    try
                    {
                        Lrcs.Add(time.TotalMilliseconds, new LrcModel()
                        {
                            LrcTb = c_lrcbk,
                            LrcText = lrc ,
                            Time = time.TotalMilliseconds
                        });
                    }
                    catch (ArgumentException)
                    {
                        Lrcs[time.TotalMilliseconds] = new LrcModel()
                        {
                            LrcTb = c_lrcbk,
                            LrcText = lrc ,
                            Time = time.TotalMilliseconds
                        };
                    }

                    c_lrc_items.Children.Add(c_lrcbk);

                }
            }

            SortLrcs = new List<KeyValuePair<double, LrcModel>>(Lrcs.AsEnumerable());
            SortLrcs.Sort((x, y) => x.Key.CompareTo(y.Key));

        }

        //正则表达式提取时间

        public TimeSpan GetTime(string str)
        {
            Regex reg = new Regex(@"\[(?<time>.*)\]", RegexOptions.IgnoreCase);
            string timestr = reg.Match(str).Groups["time"].Value;
            int m = 0;
            //获得分
            try
            {
                m = Convert.ToInt32(timestr.Split(':')[0]);
            }
            catch (Exception)
            {
                return TimeSpan.MaxValue;
            }
            //判断是否有小数点
            int s = 0, f = 0;
            if (timestr.Split(':')[1].IndexOf(".") != -1)
            {
                //有
                s = Convert.ToInt32(timestr.Split(':')[1].Split('.')[0]);
                try
                {
                    //获得毫秒位
                    f = Convert.ToInt32(timestr.Split(':')[1].Split('.')[1]);
                }
                catch (Exception)
                {
                    f = 0;
                }

            }
            else
            {
                //没有
                s = Convert.ToInt32(timestr.Split(':')[1]);

            }
            return new TimeSpan(0, 0, m, s, f);
        }

        #endregion

        #region 歌词滚动
        /// <summary>
        /// 歌词滚动、定位焦点
        /// </summary>
        /// <param name="nowtime"></param>
        public void LrcRoll(double nowtime)
        {
            if(Lrcs.Count==0)
            {
                return;
            }
            if (FoucsLrc == null)
            {
                FoucsLrc = Lrcs.Values.First();
            }
            else
            {
                int index = SortLrcs.FindIndex(m => m.Key>=nowtime);
                if(index<=0||index==LastIndex)
                {
                    return;
                }

                FoucsLrc.LrcTb.FontSize = 15;

                LastIndex = index;
                LrcModel lm = SortLrcs[index-1].Value;
                
                FoucsLrc.LrcTb.Foreground = NoramlLrcColor;


                FoucsLrc = lm;
                FoucsLrc.LrcTb.Foreground = FoucsLrcColor;
                FoucsLrc.LrcTb.FontSize = 20;
                ResetLrcviewScroll();

            }

        }



        #endregion

        #region 调整歌词控件滚动条位置
        public void ResetLrcviewScroll()
        {
            GeneralTransform gf = FoucsLrc.LrcTb.TransformToVisual(c_lrc_items);
            Point p = gf.TransformPoint(new Point(0, 0));
            double os = p.Y - (c_scrollviewer.ActualHeight / 2) + 10;
            c_scrollviewer.ChangeView(c_scrollviewer.HorizontalOffset, os, 1);
        }
        #endregion
    }
}
