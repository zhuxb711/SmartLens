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
        public ObservableCollection<TabViewItem> TabCollection = new ObservableCollection<TabViewItem>();
        private readonly object SyncRoot = new object();
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

        /// <summary>
        /// 创建新的WebTab标签页
        /// </summary>
        /// <param name="uri">导航网址</param>
        private void CreateNewTab(Uri uri = null)
        {
            lock (SyncRoot)
            {
                TabCollection.Add(new TabViewItem()
                {
                    Header = "空白页",
                    Icon = new SymbolIcon(Symbol.Document),
                    Content = new WebPage(uri)
                });
                TabControl.SelectedIndex = TabCollection.Count - 1;
            }
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
