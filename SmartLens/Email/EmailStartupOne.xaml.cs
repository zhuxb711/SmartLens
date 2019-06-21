using System.Text.RegularExpressions;
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
            if (string.IsNullOrEmpty(Password))
            {
                Pass.BorderBrush = new SolidColorBrush(Colors.Red);
                return;
            }
            else
            {
                Pass.BorderBrush = new SolidColorBrush(Colors.Gray);
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

            Nav.Navigate(typeof(EmailStartupTwo), new EmailLoginData(EmailAddress, CallName, Password), new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            MainPage.ThisPage.NavFrame.GoBack();
        }
    }
}
