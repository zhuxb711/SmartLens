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
        private Dictionary<string, Uri> WeatherIconCollection;

        public WeatherControl()
        {
            InitializeComponent();
            OnFirstLoad();
        }

        private void OnFirstLoad()
        {
            WeatherIconCollection = new Dictionary<string, Uri>
            {
                { "多云", new Uri("ms-appx:///Weather/WeatherIcon/cloud.png") },
                { "晴", new Uri("ms-appx:///Weather/WeatherIcon/fine.png") },
                { "阴", new Uri("ms-appx:///Weather/WeatherIcon/overcast.png") },
                { "小雨", new Uri("ms-appx:///Weather/WeatherIcon/small_rain.png") },
                { "小到中雨", new Uri("ms-appx:///Weather/WeatherIcon/stom_rain.png") },
                { "大雨", new Uri("ms-appx:///Weather/WeatherIcon/big_rain.png") },
                { "中到大雨", new Uri("ms-appx:///Weather/WeatherIcon/big_rain.png") },
                { "暴雨", new Uri("ms-appx:///Weather/WeatherIcon/mbig_rain.png") },
                { "大暴雨", new Uri("ms-appx:///Weather/WeatherIcon/mbig_rain.png") },
                { "特大暴雨", new Uri("ms-appx:///Weather/WeatherIcon/mbig_rain.png") },
                { "大到暴雨", new Uri("ms-appx:///Weather/WeatherIcon/mbig_rain.png") },
                { "暴雨到大暴雨", new Uri("ms-appx:///Weather/WeatherIcon/mbig_rain.png") },
                { "雨夹雪", new Uri("ms-appx:///Weather/WeatherIcon/rain_snow.png") },
                { "阵雪", new Uri("ms-appx:///Weather/WeatherIcon/quick_snow.png") },
                { "雾", new Uri("ms-appx:///Weather/WeatherIcon/fog.png") },
                { "沙尘暴", new Uri("ms-appx:///Weather/WeatherIcon/sand.png") },
                { "浮尘", new Uri("ms-appx:///Weather/WeatherIcon/sand.png") },
                { "扬沙", new Uri("ms-appx:///Weather/WeatherIcon/sand.png") },
                { "强沙尘暴", new Uri("ms-appx:///Weather/WeatherIcon/sand.png") },
                { "雾霾", new Uri("ms-appx:///Weather/WeatherIcon/sand.png") },
                { "冻雨", new Uri("ms-appx:///Weather/WeatherIcon/ice_rain.png") },
                { "中雨", new Uri("ms-appx:///Weather/WeatherIcon/mid_rain.png") },
                { "雷阵雨伴有冰雹", new Uri("ms-appx:///Weather/WeatherIcon/quick_rain_ice2.png") },
                { "阵雨", new Uri("ms-appx:///Weather/WeatherIcon/quick_rain.png") },
                { "雷阵雨", new Uri("ms-appx:///Weather/WeatherIcon/lquick_rain.png") }
            };

            HomePage.WeatherDataGenarated += ThisPage_WeatherDataGenarated;
            WeatherIcon1.Source = new BitmapImage();
            WeatherIcon2.Source = new BitmapImage();
            WeatherIcon3.Source = new BitmapImage();
            WeatherIcon4.Source = new BitmapImage();
        }

        public string GetTodayWeatherDescribtion()
        {
            if (Describe.Text == "" || Temperature.Text == "" || Humid.Text == "" || PM.Text == "")
            {
                return null;
            }
            if (Describe.Text == "多云" || Describe.Text == "晴" || Describe.Text == "阴")
            {
                return "今天天气" + Describe.Text + "，气温" + Temperature.Text + "℃，" + Humid.Text + "，" + PM.Text.Insert(6, "指数");
            }
            else
            {
                return "今天有" + Describe.Text + "，气温" + Temperature.Text + "℃，" + Humid.Text + "，" + PM.Text.Insert(6, "指数");
            }
        }

        public void Error(ErrorReason reason)
        {
            if (reason == ErrorReason.NetWork)
            {
                Notise.Text = "网络连接失败";
            }
            else
            {
                Notise.Text = "地理位置授权被拒绝";
            }
            Pro.Visibility = Visibility.Collapsed;
            Retry.Visibility = Visibility.Visible;
        }

        private async void ThisPage_WeatherDataGenarated(object sender, WeatherData e)
        {
            List<int> list = new List<int>(4)
                {
                    int.Parse(Regex.Replace(e.Data.forecast[1].high, @"[^0-9]+", ""))/10,
                    int.Parse(Regex.Replace(e.Data.forecast[2].high, @"[^0-9]+", ""))/10,
                    int.Parse(Regex.Replace(e.Data.forecast[3].high, @"[^0-9]+", ""))/10,
                    int.Parse(Regex.Replace(e.Data.forecast[4].high, @"[^0-9]+", ""))/10
                };
            List<int> list1 = new List<int>(4)
                {
                    int.Parse(Regex.Replace(e.Data.forecast[1].low, @"[^0-9]+", ""))/10,
                    int.Parse(Regex.Replace(e.Data.forecast[2].low, @"[^0-9]+", "")) / 10,
                    int.Parse(Regex.Replace(e.Data.forecast[3].low, @"[^0-9]+", "")) / 10,
                    int.Parse(Regex.Replace(e.Data.forecast[4].low, @"[^0-9]+", "")) / 10
                };
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                for (int i = 1; i <= 4; i++)
                {
                    switch (i)
                    {
                        case 1: (WeatherIcon1.Source as BitmapImage).UriSource = WeatherIconCollection[e.Data.forecast[i].type]; break;
                        case 2: (WeatherIcon2.Source as BitmapImage).UriSource = WeatherIconCollection[e.Data.forecast[i].type]; break;
                        case 3: (WeatherIcon3.Source as BitmapImage).UriSource = WeatherIconCollection[e.Data.forecast[i].type]; break;
                        case 4: (WeatherIcon4.Source as BitmapImage).UriSource = WeatherIconCollection[e.Data.forecast[i].type]; break;
                    }
                }
                WeatherIconCollection?.Clear();
                WeatherIconCollection = null;
            });


            int[] temp = new int[4];
            int[] temp1 = new int[4];
            list.CopyTo(temp);
            list.CopyTo(temp1);

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async() =>
            {
                PointCollection PCollection1 = GetPolyLineChartPoints(list);
                PointCollection PCollection2 = GetPolyLineChartPoints(list1);

                await ApplyToScreen(PCollection1, PCollection2, ref temp, ref temp1);

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

                Wind1.Text = e.Data.forecast[1].notice;
                Wind2.Text = e.Data.forecast[2].notice;
                Wind3.Text = e.Data.forecast[3].notice;
                Wind4.Text = e.Data.forecast[4].notice;
                LoadingControl.IsLoading = false;
            });
        }

        private PointCollection GetPolyLineChartPoints(List<int> datas, int TopHeight = 80, int MaxValue = 50)
        {
            PointCollection PCollection = new PointCollection();
            int x = 10;
            int[] temp = new int[4];
            datas.CopyTo(temp);
            datas.Sort();
            int Max = datas[3];
            int Min = datas[0];
            int MaxDecMin = Max - Min;
            int DistanceBetweenXPoint = 80;
            for (int i = 0; i < 4; i++)
            {
                int y;
                if (temp[i] > MaxValue || temp[i] < -MaxValue)
                {
                    y = 0;
                }
                else if (Max == Min)
                {
                    y = TopHeight - temp[i];
                }
                else y = TopHeight - ((60 / MaxDecMin) * (temp[i] - Min) + 10);
                Point point = new Point(x, y);
                PCollection.Add(point);
                x += DistanceBetweenXPoint;
            }

            return PCollection;
        }

        private Task ApplyToScreen(PointCollection UPCollection, PointCollection DPCollection, ref int[] UDataGroup, ref int[] DDataGroup)
        {
            PolyUpContainer.Children.Clear();
            Polyline PLU = new Polyline
            {
                Points = UPCollection,
                Stroke = new SolidColorBrush(Colors.Cyan),
                Height = 80,
                Width=350,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Center,
                StrokeThickness = 2.5
            };
            PolyUpContainer.Children.Add(PLU);
            for (int i = 0; i < UPCollection.Count; i++)
            {
                TextBlock text = new TextBlock
                {
                    Text = UDataGroup[i].ToString(),
                    FontSize = 11,
                    TextAlignment = TextAlignment.Center
                };
                Canvas.SetLeft(text, UPCollection[i].X - 5);
                Canvas.SetTop(text, UPCollection[i].Y - 20);
                PolyUpContainer.Children.Add(text);
            }

            PolyDownContainer.Children.Clear();
            Polyline PLD = new Polyline
            {
                Points = DPCollection,
                Stroke = new SolidColorBrush(Colors.Cyan),
                Height = 80,
                Width=350,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Center,
                StrokeThickness = 2.5
            };
            PolyDownContainer.Children.Add(PLD);
            for (int i = 0; i < DPCollection.Count; i++)
            {
                TextBlock text = new TextBlock
                {
                    Text = DDataGroup[i].ToString(),
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
