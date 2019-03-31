using System;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace SmartLens
{
    public sealed partial class BlueScreen : Page
    {
        public BlueScreen()
        {
            InitializeComponent();
            Loaded += BlueScreen_Loaded;
        }

        private async void BlueScreen_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var ErrorFile = await ApplicationData.Current.LocalFolder.CreateFileAsync("ErrorLog.txt", CreationCollisionOption.OpenIfExists);
            string CurrentTime = DateTime.Now.ToShortDateString() + "，" + DateTime.Now.ToShortTimeString();
            await FileIO.AppendTextAsync(ErrorFile, CurrentTime + Message.Text + "\r\r");
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is string ExceptionMessage)
            {
                Message.Text = ExceptionMessage;
            }
        }
    }
}
