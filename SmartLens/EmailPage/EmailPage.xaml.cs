using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;


namespace SmartLens
{
    public sealed partial class EmailPage : Page
    {
        public static EmailPage ThisPage { get; private set; }
        public EmailPage()
        {
            InitializeComponent();
            ThisPage = this;
            Loaded += EmailPage_Loaded;
        }

        private void EmailPage_Loaded(object sender, RoutedEventArgs e)
        {
            string Status = ApplicationData.Current.LocalSettings.Values["EmailStartup"]?.ToString();
            if (Status==null)
            {
                Nav.Navigate(typeof(EmailStartupOne), Nav, new DrillInNavigationTransitionInfo());
            }
            else if(Status=="True")
            {
                Nav.Navigate(typeof(EmailPresenter), null, new DrillInNavigationTransitionInfo());
            }
        }

        protected async override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if(EmailPresenter.ThisPage==null)
            {
                return;
            }
            EmailPresenter.ThisPage.ConnectionCancellation?.Cancel();
            await Task.Run(() =>
            {
                EmailPresenter.ThisPage.ExitLocker?.WaitOne();
            });
            EmailPresenter.ThisPage.ExitLocker?.Dispose();
            EmailPresenter.ThisPage.ExitLocker = null;
            EmailPresenter.ThisPage.ConnectionCancellation?.Dispose();
            EmailPresenter.ThisPage.ConnectionCancellation = null;

            EmailPresenter.ThisPage.DisplayMode.SelectionChanged -= EmailPresenter.ThisPage.DisplayMode_SelectionChanged;
            EmailPresenter.ThisPage.LastSelectedItem = null;

            if (EmailProtocolServiceProvider.CheckWhetherInstanceExist())
            {
                EmailProtocolServiceProvider.GetInstance().Dispose();
            }
        }
    }
}
