using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Geolocation;
using Windows.UI.Xaml.Controls;

namespace SmartLens
{
    public sealed partial class HomePage : Page
    {
        /// <summary>
        /// 天气数据获取成功时引发
        /// </summary>
        public static event EventHandler<WeatherData> WeatherDataGenarated;

        public static HomePage ThisPage { get; private set; }
        Position.RootObject jp;

        public HomePage()
        {
            InitializeComponent();
            ThisPage = this;
            OnFirstLoad();
        }

        /// <summary>
        /// 仅在第一次初始化时执行
        /// </summary>
        public async void OnFirstLoad()
        {
            Geoposition Position;
            try
            {
                Position = await GetPositionAsync();
            }
            catch (InvalidOperationException)
            {
                WeatherCtr.Error(ErrorReason.Location);
                return;
            }
            catch (Exception)
            {
                WeatherCtr.Error(ErrorReason.NetWork);
                return;
            }

            //将获取到的GPS位置发送至百度地图逆地址解析服务
            float lat = (float)Position.Coordinate.Point.Position.Latitude;
            float lon = (float)Position.Coordinate.Point.Position.Longitude;
            string URL = "http://api.map.baidu.com/geocoder/v2/?location=" + lat + "," + lon + "&output=json&ak=qrTMQKoNdBj3H6N7ZTdIbRnbBOQjcDGQ";
            string Result = await GetWebResponseAsync(URL);

            if (Result != "")
            {
                //异步运行以在完成诸多解析任务的同时保持UI响应能力
                await Task.Run(async () =>
                {
                    var WeatherInfo = await GetWeatherInfoAsync(GetDistrictByAnalysisJSON(Result));
                    if (WeatherInfo == null)
                    {
                        await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            WeatherCtr.Error(ErrorReason.APIError);
                        });
                        return;
                    }

                    //status=200时表示请求成功
                    if (WeatherInfo.status == 200)
                    {
                        WeatherDataGenarated?.Invoke(null, new WeatherData(WeatherInfo.data, jp.result.addressComponent.city + jp.result.addressComponent.district));
                    }
                    else
                    {
                        await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            WeatherCtr.Error(ErrorReason.APIError);
                        });
                    }
                });
            }
        }

        /// <summary>
        /// 向URL指定的网络服务器发出HTTP请求，获取返回服务器返回信息
        /// </summary>
        /// <param name="url">网络服务器地址</param>
        /// <returns>网络服务器返回的信息</returns>
        private async Task<string> GetWebResponseAsync(string url)
        {
            string strBuff = "";
            Uri HttpURL = new Uri(url);
            HttpWebRequest httpReq = (HttpWebRequest)WebRequest.Create(HttpURL);
            try
            {
                HttpWebResponse httpResp = (HttpWebResponse)await httpReq.GetResponseAsync();
                Stream respStream = httpResp.GetResponseStream();
                StreamReader respStreamReader = new StreamReader(respStream, Encoding.UTF8);
                strBuff = respStreamReader.ReadToEnd();
            }
            catch (Exception)
            {
                WeatherCtr.Error(ErrorReason.NetWork);
            }
            return strBuff;
        }


        /// <summary>
        /// 从JSON字符串获取城市和街道信息
        /// </summary>
        /// <param name="JSON">百度地图API返回的JSON字符串</param>
        /// <returns>XXX市XXX区</returns>
        private string GetDistrictByAnalysisJSON(string JSON)
        {
            jp = JsonConvert.DeserializeObject<Position.RootObject>(JSON);
            return jp.result.addressComponent.district;
        }

        /// <summary>
        /// 请求GPS位置定位权限并获取当前位置，精度为100m
        /// </summary>
        /// <returns>设备位置信息</returns>
        private async Task<Geoposition> GetPositionAsync()
        {
            var accessStatus = await Geolocator.RequestAccessAsync();
            if (accessStatus != GeolocationAccessStatus.Allowed)
            {
                throw new InvalidOperationException();
            }
            var geolocator = new Geolocator
            {
                DesiredAccuracyInMeters = 100
            };
            var position = await geolocator.GetGeopositionAsync();
            return position;
        }

        /// <summary>
        /// 根据城市名称向云端API获取天气情况
        /// </summary>
        /// <param name="City">城市名称</param>
        /// <returns>天气信息</returns>
        private async Task<Weather.Root> GetWeatherInfoAsync(string City)
        {
            string URL = null;
            var Jarray = JArray.Parse(File.ReadAllText("Weather/CityCode.json"));

            try
            {
                for (int i = 0; i < Jarray.Count; i++)
                {
                    if (Jarray[i].Last.First.ToString() == City)
                    {
                        URL = "http://t.weather.sojson.com/api/weather/city/" + Jarray[i].Last.Previous.First;
                    }
                }

                string JSON = await GetWebResponseAsync(URL);
                Weather.Root WeatherResult = new Weather.Root();
                if (JSON != "")
                {
                    WeatherResult = JsonConvert.DeserializeObject<Weather.Root>(JSON);
                }
                return WeatherResult;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
