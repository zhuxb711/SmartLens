using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;


namespace SmartLens
{
    public sealed partial class WebTab : Page
    {
        public static WebTab ThisPage { get; set; }
        private AutoResetEvent Locker2 = new AutoResetEvent(true);
        public ObservableCollection<TabViewItem> TabCollection = new ObservableCollection<TabViewItem>();

        public WebTab()
        {
            InitializeComponent();
            ThisPage = this;
            TabControl.ItemsSource = TabCollection;
            TabCollection.CollectionChanged += (s, e) =>
            {
                if (TabCollection.Count == 1)
                {
                    (TabCollection[0] as TabViewItem).IsClosable = false;
                }
                else if ((TabCollection[0] as TabViewItem).IsClosable == false)
                {
                    (TabCollection[0] as TabViewItem).IsClosable = true;
                }
            };
            CreateNewTab();
        }

        private void CreateNewTab(Uri uri = null)
        {
            Locker2.WaitOne();
            var temp = new WebPage(uri);
            TabCollection.Add(new TabViewItem()
            {
                Header = "空白页",
                Icon = new SymbolIcon(Symbol.Document),
                Content = temp
            });
            TabControl.SelectedIndex = TabCollection.Count - 1;
            Locker2.Set();
        }


        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is Uri uri)
            {
                CreateNewTab(uri);
            }
        }

        private void AddTabButtonUpper_Click(object sender, RoutedEventArgs e)
        {
            CreateNewTab();
        }

        private void TabControl_TabClosing(object sender, TabClosingEventArgs e)
        {
            (e.Tab.Content as WebPage).Dispose();
            e.Tab.Content = null;
        }
    }
}
