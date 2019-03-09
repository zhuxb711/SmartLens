using System.Text.RegularExpressions;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace SmartLens
{
    public sealed partial class EmailStartupOne : Page
    {
        string EmailAddress { get; set; }
        string CallName { get; set; }
        string Password { get; set; }
        Frame Nav;
        public EmailStartupOne()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Nav = e.Parameter as Frame;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            Regex EmailCheck = new Regex("^\\s*([A-Za-z0-9_-]+(\\.\\w+)*@(\\w+\\.)+\\w{2,5})\\s*$");
            if (string.IsNullOrWhiteSpace(EmailAddress) || !EmailCheck.IsMatch(EmailAddress))
            {
                Address.BorderBrush = new SolidColorBrush(Colors.Red);
                return;
            }
            else
            {
                Address.BorderBrush = new SolidColorBrush(Colors.Gray);
            }
            if (string.IsNullOrWhiteSpace(CallName))
            {
                Call.BorderBrush = new SolidColorBrush(Colors.Red);
                return;
            }
            else
            {
                Call.BorderBrush = new SolidColorBrush(Colors.Gray);
            }
            if (string.IsNullOrEmpty(Password))
            {
                Pass.BorderBrush = new SolidColorBrush(Colors.Red);
                return;
            }
            else
            {
                Pass.BorderBrush = new SolidColorBrush(Colors.Gray);
            }

            Nav.Navigate(typeof(EmailStartupTwo), new EmailLoginData(EmailAddress, CallName, Password), new DrillInNavigationTransitionInfo());
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            MainPage.ThisPage.NavFrame.GoBack();
        }
    }

    public sealed class EmailLoginData
    {
        public string EmailAddress { get; private set; }
        public string CallName { get; private set; }
        public string Password { get; private set; }
        public string IMAPAddress { get; private set; }
        public int IMAPPort { get; private set; }
        public string SMTPAddress { get; private set; }
        public int SMTPPort { get; private set; }
        public bool IsEnableSSL { get; private set; }

        public EmailLoginData(string EmailAddress, string CallName, string Password)
        {
            this.EmailAddress = EmailAddress;
            this.CallName = CallName;
            this.Password = Password;
        }

        public void SetExtraData(string IMAPAddress, int IMAPPort, string SMTPAddress, int SMTPPort, bool IsEnableSSL)
        {
            this.IMAPAddress = IMAPAddress;
            this.IMAPPort = IMAPPort;
            this.SMTPAddress = SMTPAddress;
            this.SMTPPort = SMTPPort;
            this.IsEnableSSL = IsEnableSSL;
        }
    }
}
