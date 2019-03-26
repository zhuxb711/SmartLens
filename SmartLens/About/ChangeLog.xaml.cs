using System;
using System.IO;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;


namespace SmartLens
{
    public sealed partial class ChangeLog : Page
    {
        public ChangeLog()
        {
            InitializeComponent();
            Loaded += ChangeLog_Loaded;
        }

        private async void ChangeLog_Loaded(object sender, RoutedEventArgs e)
        {
            MarkdownControl.Text = await File.ReadAllTextAsync("About/ChangeLog.txt");
        }

        private void MarkdownControl_LinkClicked(object sender, Microsoft.Toolkit.Uwp.UI.Controls.LinkClickedEventArgs e)
        {
            MainPage.ThisPage.NavFrame.Navigate(typeof(WebTab), new Uri(e.Link), new DrillInNavigationTransitionInfo());
        }
    }
}
