using System;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace SmartLens
{
    public sealed partial class About : Page
    {
        public static bool IsEnterChangeLog { get; set; } = false;

        public About()
        {
            InitializeComponent();
            Version.Text = string.Format("Version: {0}.{1}.{2}.{3}", Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision);
        }

        private void HyperlinkButton_Click(object sender, RoutedEventArgs e)
        {
            IsEnterChangeLog = true;
            MainPage.ThisPage.NavFrame.Navigate(typeof(ChangeLog), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
        }

        private async void HyperlinkButton_Click_1(object sender, RoutedEventArgs e)
        {
            if (ApplicationData.Current.LocalSettings.Values["UseInsideWebBrowser"] is true)
            {
                MainPage.ThisPage.NavFrame.Navigate(typeof(WebTab), new Uri("https://github.com/zhuxb711/SmartLens"), new DrillInNavigationTransitionInfo());
            }
            else
            {
                await Launcher.LaunchUriAsync(new Uri("https://github.com/zhuxb711/SmartLens"));
            }
        }
    }
}
