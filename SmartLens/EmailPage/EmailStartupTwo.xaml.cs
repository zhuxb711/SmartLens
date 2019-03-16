using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;


namespace SmartLens
{
    public sealed partial class EmailStartupTwo : Page
    {
        string IMAPAddress { get; set; }
        string IMAPPort { get; set; }
        string SMTPAddress { get; set; }
        string SMTPPort { get; set; }
        bool IsEnableSSL { get; set; } = true;
        EmailLoginData Data;
        public EmailStartupTwo()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is EmailLoginData data)
            {
                Data = data;
            }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(IMAPAddress))
            {
                IMAPAdd.BorderBrush = new SolidColorBrush(Colors.Red);
                return;
            }
            else
            {
                IMAPAdd.BorderBrush = new SolidColorBrush(Colors.Gray);
            }
            if (string.IsNullOrWhiteSpace(IMAPPort))
            {
                IMAPPo.BorderBrush = new SolidColorBrush(Colors.Red);
                return;
            }
            else
            {
                if (!int.TryParse(IMAPPort, out _))
                {
                    IMAPPo.BorderBrush = new SolidColorBrush(Colors.Red);
                    return;
                }
                IMAPPo.BorderBrush = new SolidColorBrush(Colors.Gray);
            }
            if (string.IsNullOrEmpty(SMTPAddress))
            {
                SMTPAdd.BorderBrush = new SolidColorBrush(Colors.Red);
                return;
            }
            else
            {
                SMTPAdd.BorderBrush = new SolidColorBrush(Colors.Gray);
            }
            if (string.IsNullOrEmpty(SMTPPort))
            {
                SMTPPo.BorderBrush = new SolidColorBrush(Colors.Red);
                return;
            }
            else
            {
                if (!int.TryParse(SMTPPort, out _))
                {
                    SMTPPo.BorderBrush = new SolidColorBrush(Colors.Red);
                    return;
                }
                SMTPPo.BorderBrush = new SolidColorBrush(Colors.Gray);
            }

            Data.SetExtraData(IMAPAddress, int.Parse(IMAPPort), SMTPAddress, int.Parse(SMTPPort), IsEnableSSL);
            EmailPage.ThisPage.Nav.Navigate(typeof(EmailPresenter), Data, new DrillInNavigationTransitionInfo());

            //在设置中设置初始化完成标志，初始化完成
            ApplicationData.Current.RoamingSettings.Values["EmailStartup"] = "True";
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            MainPage.ThisPage.NavFrame.GoBack();
        }
    }
}
