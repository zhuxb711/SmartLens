using Windows.ApplicationModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

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
            MainPage.ThisPage.NavFrame.Navigate(typeof(ChangeLog), new DrillInNavigationTransitionInfo());
        }
    }
}
