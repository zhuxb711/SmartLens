using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;


namespace SmartLens
{
    public sealed partial class WeatherControl : UserControl
    {
        /// <summary>
        /// 天气图标集合
        /// </summary>
        private Dictionary<string, Uri> WeatherIconCollection;

        public WeatherControl()
        {
            InitializeComponent();
            OnFirstLoad();
        }

        /// <summary>
        /// 仅在第一次初始化时执行
        /// </summary>
        private void OnFirstLoad()
        {
            WeatherIconCollection = new Dictionary<string, Uri>
            {
                { "多云", new Uri("ms-appx:///Weather/WeatherIcon/多云.png") },
                { "晴", new Uri("ms-appx:///Weather/WeatherIcon/晴.png") },
                { "阴", new Uri("ms-appx:///Weather/WeatherIcon/阴.png") },
                { "小雨", new Uri("ms-appx:///Weather/WeatherIcon/小雨.png") },
                { "小到中雨", new Uri("ms-appx:///Weather/WeatherIcon/中雨.png") },
                { "大雨", new Uri("ms-appx:///Weather/WeatherIcon/大雨.png") },
                { "中到大雨", new Uri("ms-appx:///Weather/WeatherIcon/中雨.png") },
                { "暴雨", new Uri("ms-appx:///Weather/WeatherIcon/暴雨.png") },
                { "大暴雨", new Uri("ms-appx:///Weather/WeatherIcon/大暴雨.png") },
                { "特大暴雨", new Uri("ms-appx:///Weather/WeatherIcon/特大暴雨.png") },
                { "大到暴雨", new Uri("ms-appx:///Weather/WeatherIcon/暴雨.png") },
                { "暴雨到大暴雨", new Uri("ms-appx:///Weather/WeatherIcon/大暴雨.png") },
                { "雨夹雪", new Uri("ms-appx:///Weather/WeatherIcon/雨夹雪.png") },
                { "阵雪", new Uri("ms-appx:///Weather/WeatherIcon/阵雪.png") },
                { "雾", new Uri("ms-appx:///Weather/WeatherIcon/雾.png") },
                { "沙尘暴", new Uri("ms-appx:///Weather/WeatherIcon/沙尘暴.png") },
                { "浮尘", new Uri("ms-appx:///Weather/WeatherIcon/浮尘.png") },
                { "扬沙", new Uri("ms-appx:///Weather/WeatherIcon/扬沙.png") },
                { "强沙尘暴", new Uri("ms-appx:///Weather/WeatherIcon/强沙尘暴.png") },
                { "雾霾", new Uri("ms-appx:///Weather/WeatherIcon/霾.png") },
                { "冻雨", new Uri("ms-appx:///Weather/WeatherIcon/冻雨.png") },
                { "中雨", new Uri("ms-appx:///Weather/WeatherIcon/中雨.png") },
                { "雷阵雨伴有冰雹", new Uri("ms-appx:///Weather/WeatherIcon/雷阵雨伴有冰雹.png") },
                { "阵雨", new Uri("ms-appx:///Weather/WeatherIcon/阵雨.png") },
                { "雷阵雨", new Uri("ms-appx:///Weather/WeatherIcon/雷阵雨.png") }
            };

            //订阅天气数据到达事件
            HomePage.WeatherDataGenarated += ThisPage_WeatherDataGenarated;
        }

        /// <summary>
        /// 获取今日天气信息描述
        /// </summary>
        /// <returns>天气概述</returns>
        public string GetTodayWeatherDescribtion()
        {
            if (Describe.Text == "" || Temperature.Text == "" || Humid.Text == "" || PM.Text == "")
            {
                return null;
            }
            return Describe.Text == "多云" || Describe.Text == "晴" || Describe.Text == "阴"
                ? "今天天气" + Describe.Text + "，气温" + Temperature.Text + "℃，" + Humid.Text + "，" + PM.Text.Insert(6, "指数")
                : "今天有" + Describe.Text + "，气温" + Temperature.Text + "℃，" + Humid.Text + "，" + PM.Text.Insert(6, "指数");
        }

        /// <summary>
        /// 通知天气控件发生错误，并告知用户
        /// </summary>
        /// <param name="reason">错误原因</param>
        public void Error(ErrorReason reason)
        {
            switch (reason)
            {
                case ErrorReason.Location:
                    {
                        Notise.Text = "地理位置授权被拒绝";
                        break;
                    }
                case ErrorReason.NetWork:
                    {
                        Notise.Text = "网络连接失败";
                        break;
                    }
                case ErrorReason.APIError:
                    {
                        Notise.Text = "天气API异常";
                        break;
                    }
            }

            Pro.Visibility = Visibility.Collapsed;
            Retry.Visibility = Visibility.Visible;
        }

        private async void ThisPage_WeatherDataGenarated(object sender, WeatherData e)
        {
            //使用正则表达式处理最高/最低温数据，获得纯数字信息
            List<int> UpDataList = new List<int>(4)
                {
                    int.Parse(Regex.Replace(e.Data.forecast[1].high, @"[^0-9]+", ""))/10,
                    int.Parse(Regex.Replace(e.Data.forecast[2].high, @"[^0-9]+", ""))/10,
                    int.Parse(Regex.Replace(e.Data.forecast[3].high, @"[^0-9]+", ""))/10,
                    int.Parse(Regex.Replace(e.Data.forecast[4].high, @"[^0-9]+", ""))/10
                };
            List<int> DownDataList = new List<int>(4)
                {
                    int.Parse(Regex.Replace(e.Data.forecast[1].low, @"[^0-9]+", ""))/10,
                    int.Parse(Regex.Replace(e.Data.forecast[2].low, @"[^0-9]+", "")) / 10,
                    int.Parse(Regex.Replace(e.Data.forecast[3].low, @"[^0-9]+", "")) / 10,
                    int.Parse(Regex.Replace(e.Data.forecast[4].low, @"[^0-9]+", "")) / 10
                };

            //设置各天气图标区域的图像，完成后清空WeatherIconCollection
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                BitmapImage Image = new BitmapImage();
                TodayWeatherIcon.Source = Image;
                Image.UriSource = WeatherIconCollection[e.Data.forecast[0].type];

                for (int i = 1; i <= 4; i++)
                {
                    switch (i)
                    {
                        case 1:
                            {
                                BitmapImage image = new BitmapImage();
                                WeatherIcon1.Source = image;
                                image.UriSource = WeatherIconCollection[e.Data.forecast[i].type];
                                break;
                            }
                        case 2:
                            {
                                BitmapImage image = new BitmapImage();
                                WeatherIcon2.Source = image;
                                image.UriSource = WeatherIconCollection[e.Data.forecast[i].type];
                                break;
                            }
                        case 3:
                            {
                                BitmapImage image = new BitmapImage();
                                WeatherIcon3.Source = image;
                                image.UriSource = WeatherIconCollection[e.Data.forecast[i].type];
                                break;
                            }
                        case 4:
                            {
                                BitmapImage image = new BitmapImage();
                                WeatherIcon4.Source = image;
                                image.UriSource = WeatherIconCollection[e.Data.forecast[i].type];
                                break;
                            }
                    }
                }
                WeatherIconCollection?.Clear();
                WeatherIconCollection = null;
            });


            int[] OriginUpData = new int[4];
            int[] OriginDownData = new int[4];
            UpDataList.CopyTo(OriginUpData);
            DownDataList.CopyTo(OriginDownData);

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                PointCollection PCollection1 = GetPolyLineChartPointsCollection(UpDataList);
                PointCollection PCollection2 = GetPolyLineChartPointsCollection(DownDataList);

                await ApplyToScreenAsync(PCollection1, PCollection2, ref OriginUpData, ref OriginDownData);

                Location.Text = e.Location;

                Describe.Text = e.Data.forecast[0].type;

                Temperature.Text = e.Data.wendu;
                PM.Text = "PM2.5  " + e.Data.forecast[0].aqi.ToString();

                Wind.Text = e.Data.forecast[0].fx + " " + e.Data.forecast[0].fl;
                Humid.Text = "相对湿度" + e.Data.shidu;

                Date1.Text = e.Data.forecast[1].week;
                Date2.Text = e.Data.forecast[2].week;
                Date3.Text = e.Data.forecast[3].week;
                Date4.Text = e.Data.forecast[4].week;

                LoadingControl.IsLoading = false;
            });
        }

        /// <summary>
        /// 由气温数据获取用于折线图的PointCollection，此函数将自动放大各点之间的差距，使最大值或最小值始终处于上界或下界
        /// </summary>
        /// <param name="TemperatureData">气温数据</param>
        /// <param name="ControlHeight">折线图所在区域的高度</param>
        /// <param name="MaxValue">气温可能的上下界</param>
        /// <returns>表示折线图中的点的PointCollection</returns>
        private PointCollection GetPolyLineChartPointsCollection(List<int> TemperatureData, int ControlHeight = 80, int MaxValue = 50)
        {
            PointCollection PCollection = new PointCollection();

            int[] OriginData = new int[4];
            TemperatureData.CopyTo(OriginData);
            TemperatureData.Sort();

            int x = 0;
            int Max = TemperatureData[3];
            int Min = TemperatureData[0];
            int MaxDecMin = Max - Min;
            int DistanceBetweenXPoint = 90;

            PCollection.Add(new Point(x, ControlHeight));
            for (int i = 0; i < 4; i++)
            {
                int y;
                if (OriginData[i] > MaxValue || OriginData[i] < -MaxValue)
                {
                    y = 0;
                }
                else if (Max == Min)
                {
                    y = ControlHeight - OriginData[i];
                }
                else
                {
                    y = ControlHeight - ((60 / MaxDecMin) * (OriginData[i] - Min) + 10);
                }
                Point point = new Point(x, y);
                PCollection.Add(point);
                x += DistanceBetweenXPoint;
            }
            PCollection.Add(new Point(x - DistanceBetweenXPoint, ControlHeight));
            return PCollection;
        }

        /// <summary>
        /// 将PointCollection所描绘的图像绘制到控件上并为每一个数据点贴上值标签
        /// </summary>
        /// <param name="UPCollection">最高气温数据</param>
        /// <param name="DPCollection">最低气温数据</param>
        /// <param name="UDataGroup">用作标签的原始最高气温数据</param>
        /// <param name="DDataGroup">用作标签的原始最低气温数据</param>
        /// <returns>无</returns>
        private Task ApplyToScreenAsync(PointCollection UPCollection, PointCollection DPCollection, ref int[] UDataGroup, ref int[] DDataGroup)
        {
            PolyUpContainer.Children.Clear();
            Polygon PLU = new Polygon
            {
                Points = UPCollection,
                Fill = new SolidColorBrush(Colors.SkyBlue),
                Height = 80,
                Width = 350,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            PolyUpContainer.Children.Add(PLU);
            for (int i = 1; i < 5; i++)
            {
                TextBlock text = new TextBlock
                {
                    Text = UDataGroup[i - 1].ToString(),
                    FontSize = 11,
                    TextAlignment = TextAlignment.Center
                };
                Canvas.SetLeft(text, UPCollection[i].X - 5);
                Canvas.SetTop(text, UPCollection[i].Y - 20);
                PolyUpContainer.Children.Add(text);
            }

            PolyDownContainer.Children.Clear();
            Polygon PLD = new Polygon
            {
                Points = DPCollection,
                Fill = new SolidColorBrush(Colors.SkyBlue),
                Height = 80,
                Width = 350,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            PolyDownContainer.Children.Add(PLD);
            for (int i = 1; i < 5; i++)
            {
                TextBlock text = new TextBlock
                {
                    Text = DDataGroup[i - 1].ToString(),
                    FontSize = 11,
                    TextAlignment = TextAlignment.Center
                };
                Canvas.SetLeft(text, DPCollection[i].X - 5);
                Canvas.SetTop(text, DPCollection[i].Y - 20);
                PolyDownContainer.Children.Add(text);
            }
            return Task.CompletedTask;
        }

        private void Retry_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            HomePage.ThisPage.OnFirstLoad();
            Pro.Visibility = Visibility.Visible;
            Notise.Text = "正在加载...";
            Retry.Visibility = Visibility.Collapsed;
        }
    }
}
