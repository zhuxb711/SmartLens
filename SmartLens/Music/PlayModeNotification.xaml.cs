using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;


namespace SmartLens
{
    public sealed partial class PlayModeNotification : UserControl
    {
        private string content;
        private Popup popup;
        public PlayModeNotification()
        {
            InitializeComponent();
            popup = new Popup();
            Width = Window.Current.Bounds.Width;
            Height = Window.Current.Bounds.Height;
            popup.Child = this;
        }

        public void Show(string content)
        {
            this.content = content;
            popup.IsOpen = true;
            NotificationStart();
        }

        private void NotificationStart()
        {
            NotificationContent.Text = content;
            Notification.Begin();
            Notification.Completed += Notification_Completed;
            Window.Current.SizeChanged += Current_SizeChanged;
        }

        private void Current_SizeChanged(object sender, Windows.UI.Core.WindowSizeChangedEventArgs e)
        {
            Width = e.Size.Width;
            Height = e.Size.Height;
        }

        private void Notification_Completed(object sender, object e)
        {
            popup.IsOpen = false;
            Window.Current.SizeChanged -= Current_SizeChanged;
        }
    }
}
