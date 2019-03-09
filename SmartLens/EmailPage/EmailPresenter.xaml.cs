using MailKit;
using MailKit.Search;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace SmartLens
{
    public sealed partial class EmailPresenter : Page
    {
        List<EmailItem> EmailAllItemCollection = new List<EmailItem>();
        List<EmailItem> EmailNotSeenItemCollection = new List<EmailItem>();
        ObservableCollection<IGrouping<string, EmailItem>> EmailDisplayCollection;

        public CancellationTokenSource ConnectionCancellation;
        public HashSet<UniqueId> NotSeenDictionary = new HashSet<UniqueId>();
        public EmailItem LastSelectedItem = null;
        public AutoResetEvent ExitLocker = null;
        public static EmailPresenter ThisPage { get; private set; }
        EmailProtocolServiceProvider EmailService;
        bool Updating = false;

        public EmailPresenter()
        {
            InitializeComponent();
            Loaded += Email_Loaded;
            ThisPage = this;
            DisplayMode.SelectedIndex = 0;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            EmailService = EmailProtocolServiceProvider.GetInstance();
            if(e.Parameter is EmailLoginData data)
            {
                EmailService.SetCredential(new System.Net.NetworkCredential(data.EmailAddress, data.Password),data.CallName);
                KeyValuePair<EmailProtocol, KeyValuePair<string, int>> IMAP = new KeyValuePair<EmailProtocol, KeyValuePair<string, int>>(EmailProtocol.IMAP, new KeyValuePair<string, int>(data.IMAPAddress, data.IMAPPort));
                KeyValuePair<EmailProtocol, KeyValuePair<string, int>> SMTP = new KeyValuePair<EmailProtocol, KeyValuePair<string, int>>(EmailProtocol.SMTP, new KeyValuePair<string, int>(data.SMTPAddress, data.SMTPPort));
                EmailService.SetEmailServerAddress(new List<KeyValuePair<EmailProtocol, KeyValuePair<string, int>>> { IMAP, SMTP }, data.IsEnableSSL);
            }
        }

        public void DisplayMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (DisplayMode.SelectedIndex)
            {
                case 0:
                    {
                        EmailDisplayCollection.Clear();
                        EmailDisplayCollection = null;
                        EmailDisplayCollection = new ObservableCollection<IGrouping<string, EmailItem>>(from t in EmailAllItemCollection group t by t.Date into g orderby g.Key descending select g);
                        if (EmailDisplayCollection.Count == 0)
                        {
                            NothingDisplayControl.Visibility = Visibility.Visible;
                            MarkRead.IsEnabled = false;
                        }
                        else
                        {
                            NothingDisplayControl.Visibility = Visibility.Collapsed;
                        }
                        CVS.Source = EmailDisplayCollection;
                        break;
                    }
                case 1:
                    {
                        EmailDisplayCollection.Clear();
                        EmailDisplayCollection = null;
                        EmailDisplayCollection = new ObservableCollection<IGrouping<string, EmailItem>>(from t in EmailNotSeenItemCollection group t by t.Date into g orderby g.Key descending select g);
                        if (EmailDisplayCollection.Count == 0)
                        {
                            NothingDisplayControl.Visibility = Visibility.Visible;
                            MarkRead.IsEnabled = false;
                        }
                        else
                        {
                            NothingDisplayControl.Visibility = Visibility.Collapsed;
                        }
                        CVS.Source = EmailDisplayCollection;
                        break;
                    }
            }
            EmailList.SelectedIndex = -1;
        }

        private async void ActivateSyncNotification(bool IsActivate)
        {
            if (IsActivate)
            {
                bool isTemplatePresent = Resources.TryGetValue("InAppNotificationTemplate", out object NotificationTemplate);

                if (isTemplatePresent && NotificationTemplate is DataTemplate template)
                {
                    SyncNotification.Show(template);
                }
            }
            else
            {
                SyncNotification.Dismiss();

                await Task.Delay(1000);

                bool isTemplatePresent = Resources.TryGetValue("NewestNotificationTemplate", out object NotificationTemplate);

                if (isTemplatePresent && NotificationTemplate is DataTemplate template)
                {
                    SyncNotification.Show(template, 5000);
                }

            }
        }

        private async void Email_Loaded(object sender, RoutedEventArgs e)
        {
            Updating = true;
            Nav.Navigate(typeof(EmailDetail), null, new DrillInNavigationTransitionInfo());

            DisplayMode.SelectionChanged += DisplayMode_SelectionChanged;
            ConnectionCancellation = new CancellationTokenSource();
            ExitLocker = new AutoResetEvent(false);
            ActivateSyncNotification(true);

            try
            {
                await EmailService.ConnectAllServiceAsync(ConnectionCancellation);
            }
            catch (TaskCanceledException)
            {
                ActivateSyncNotification(false);
                ExitLocker.Set();
            }
            catch (SocketException)
            {
                SyncNotification.Dismiss();

                await Task.Delay(1000);

                bool isTemplatePresent = Resources.TryGetValue("ErrorNotificationTemplate", out object NotificationTemplate);

                if (isTemplatePresent && NotificationTemplate is DataTemplate template)
                {
                    SyncNotification.Show(template, 5000);
                }
                Updating = false;
                ExitLocker.Set();
                return;
            }

            if (!ConnectionCancellation.IsCancellationRequested)
            {
                await LoadEmailData();
                ActivateSyncNotification(false);
                ExitLocker.Set();
            }

            Updating = false;
        }

        private async Task LoadEmailData()
        {
            var Inbox = EmailService.GetMailFolder();
            await Inbox.OpenAsync(FolderAccess.ReadWrite);

            var NotSeenSearchResult = await Inbox.SearchAsync(SearchQuery.NotSeen.And(SearchQuery.Not(SearchQuery.FromContains("zhuxb711@yeah.net"))));
            EmailNotSeenItemCollection.Clear();
            NotSeenDictionary.Clear();
            foreach (var uid in NotSeenSearchResult)
            {
                if (ConnectionCancellation.IsCancellationRequested)
                {
                    goto FF;
                }
                var message = await Inbox.GetMessageAsync(uid);
                NotSeenDictionary.Add(uid);
                EmailNotSeenItemCollection.Add(new EmailItem(message, uid));
            }

            if (ConnectionCancellation.IsCancellationRequested)
            {
                return;
            }

            var SearchResult = await Inbox.SearchAsync(SearchQuery.All.And(SearchQuery.Not(SearchQuery.FromContains("zhuxb711@yeah.net"))));
            EmailAllItemCollection.Clear();
            foreach (var uid in SearchResult)
            {
                if (ConnectionCancellation.IsCancellationRequested)
                {
                    goto FF;
                }
                var message = await Inbox.GetMessageAsync(uid);
                EmailAllItemCollection.Add(new EmailItem(message, uid));
            }

            switch (DisplayMode.SelectedIndex)
            {
                case 0: EmailDisplayCollection = new ObservableCollection<IGrouping<string, EmailItem>>(from t in EmailAllItemCollection group t by t.Date into g orderby g.Key descending select g); break;
                case 1: EmailDisplayCollection = new ObservableCollection<IGrouping<string, EmailItem>>(from t in EmailNotSeenItemCollection group t by t.Date into g orderby g.Key descending select g); break;
            }

            NothingDisplayControl.Visibility = Visibility.Collapsed;

            CVS.Source = EmailDisplayCollection;
            EmailList.SelectedIndex = -1;
        FF: return;
        }

        private void EmailList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (EmailList.SelectionMode == ListViewSelectionMode.Multiple)
            {
                return;
            }
            if (e.ClickedItem is EmailItem item)
            {
                if (item == LastSelectedItem)
                {
                    return;
                }
                LastSelectedItem = item;
                HtmlPreviewVisitor visitor = new HtmlPreviewVisitor(ApplicationData.Current.TemporaryFolder.Path);
                item.Message.Accept(visitor);

                if (item.FileEntitys.Count() != 0)
                {
                    EmailDetail.ThisPage.FileExpander.Visibility = Visibility.Visible;

                    EmailDetail.ThisPage.FileCollection.Clear();
                    foreach (var Entity in item.Message.Attachments)
                    {
                        EmailDetail.ThisPage.FileCollection.Add(new EmailAttachment(Entity));
                    }
                }
                else
                {
                    EmailDetail.ThisPage.FileExpander.IsExpanded = false;
                    EmailDetail.ThisPage.FileExpander.Visibility = Visibility.Collapsed;
                }

                if (EmailDetail.ThisPage.WebBrowser == null)
                {
                    EmailDetail.ThisPage.WebBrowser = new WebView(WebViewExecutionMode.SeparateProcess)
                    {
                        Visibility = Visibility.Collapsed
                    };
                    EmailDetail.ThisPage.Gr.Children.Add(EmailDetail.ThisPage.WebBrowser);
                    EmailDetail.ThisPage.WebBrowser.SetValue(Grid.RowProperty, 1);
                }

                if (EmailDetail.ThisPage.WebBrowser.Visibility == Visibility.Collapsed)
                {
                    EmailDetail.ThisPage.WebBrowser.Visibility = Visibility.Visible;
                    EmailDetail.ThisPage.CommandBarContorl.Visibility = Visibility.Visible;
                }

                EmailDetail.ThisPage.WebBrowser.NavigateToString(visitor.HtmlBody);

                if (item.IsNotSeenIndicator == 1)
                {
                    item.SetSeenIndicator(Visibility.Collapsed);

                    for (int i = 0; i < EmailNotSeenItemCollection.Count; i++)
                    {
                        if (EmailNotSeenItemCollection[i].Id == item.Id)
                        {
                            EmailNotSeenItemCollection.RemoveAt(i);
                        }
                    }
                }
            }
        }

        private async void Sync_Click(object sender, RoutedEventArgs e)
        {
            if(Updating)
            {
                return;
            }
            if (!EmailService.IsIMAPConnected)
            {
                try
                {
                    await EmailService.ConnectAllServiceAsync(ConnectionCancellation);
                }
                catch (SocketException)
                {
                    SyncNotification.Dismiss();

                    await Task.Delay(1000);

                    bool isTemplatePresent = Resources.TryGetValue("ErrorNotificationTemplate", out object NotificationTemplate);

                    if (isTemplatePresent && NotificationTemplate is DataTemplate template)
                    {
                        SyncNotification.Show(template, 5000);
                    }
                    return;
                }
                ActivateSyncNotification(true);

                await LoadEmailData();

                ActivateSyncNotification(false);
                return;
            }

            ActivateSyncNotification(true);

            var Inbox = EmailService.GetMailFolder();

            var NotSeenSearchResult = await Inbox.SearchAsync(SearchQuery.NotSeen.And(SearchQuery.Not(SearchQuery.FromContains("zhuxb711@yeah.net"))));
            EmailNotSeenItemCollection.Clear();
            NotSeenDictionary.Clear();
            foreach (var uid in NotSeenSearchResult)
            {
                var message = await Inbox.GetMessageAsync(uid);
                NotSeenDictionary.Add(uid);
                EmailNotSeenItemCollection.Add(new EmailItem(message, uid));
            }

            var SearchResult = await Inbox.SearchAsync(SearchQuery.All.And(SearchQuery.Not(SearchQuery.FromContains("zhuxb711@yeah.net"))));
            EmailAllItemCollection.Clear();
            foreach (var uid in SearchResult)
            {
                var message = await Inbox.GetMessageAsync(uid);
                EmailAllItemCollection.Add(new EmailItem(message, uid));
            }

            switch (DisplayMode.SelectedIndex)
            {
                case 0:
                    {
                        var result = from t in EmailAllItemCollection group t by t.Date into g orderby g.Key descending select g;
                        for (int i = 0; i < EmailDisplayCollection.Count; i++)
                        {
                            foreach (var item1 in result)
                            {
                                if (EmailDisplayCollection[i].Key == item1.Key)
                                {
                                    if (EmailDisplayCollection[i].Count() != item1.Count())
                                    {
                                        EmailDisplayCollection.Remove(EmailDisplayCollection[i]);
                                        EmailDisplayCollection.Insert(i, item1);
                                        break;
                                    }
                                }
                            }
                        }
                        break;
                    }
                case 1:
                    {
                        var result = from t in EmailNotSeenItemCollection group t by t.Date into g orderby g.Key descending select g;
                        for (int i = 0; i < EmailDisplayCollection.Count; i++)
                        {
                            foreach (var item1 in result)
                            {
                                if (EmailDisplayCollection[i].Key == item1.Key)
                                {
                                    if (EmailDisplayCollection[i].Count() != item1.Count())
                                    {
                                        EmailDisplayCollection.Remove(EmailDisplayCollection[i]);
                                        EmailDisplayCollection.Insert(i, item1);
                                        break;
                                    }
                                }
                            }
                        }
                        break;
                    }
            }

            ActivateSyncNotification(false);
        }

        public async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var item = EmailList.SelectedItem as EmailItem;

            for (int i = 0; i < EmailDisplayCollection.Count; i++)
            {
                if (EmailDisplayCollection[i].Key == item.Date)
                {
                    List<EmailItem> list = EmailDisplayCollection[i].ToList();
                    list.Remove(item);
                    if (list.Count == 0)
                    {
                        EmailDisplayCollection.Remove(EmailDisplayCollection[i]);
                    }
                    else
                    {
                        var result = from t in list group t by t.Date into g orderby g.Key descending select g;
                        EmailDisplayCollection.Remove(EmailDisplayCollection[i]);
                        EmailDisplayCollection.Insert(i, result.First());
                    }
                    break;
                }
            }

            if (NotSeenDictionary.Contains(item.Id))
            {
                for (int i = 0; i < EmailNotSeenItemCollection.Count; i++)
                {
                    if (EmailNotSeenItemCollection[i].Id == item.Id)
                    {
                        EmailNotSeenItemCollection.RemoveAt(i);
                        break;
                    }
                }
            }

            for (int i = 0; i < EmailAllItemCollection.Count; i++)
            {
                if (EmailAllItemCollection[i].Id == item.Id)
                {
                    EmailAllItemCollection.RemoveAt(i);
                    break;
                }
            }

            await EmailService.GetMailFolder().SetFlagsAsync(item.Id, MessageFlags.Deleted, true);

            if (EmailDisplayCollection.Count == 0)
            {
                NothingDisplayControl.Visibility = Visibility.Visible;
                MarkRead.IsEnabled = false;
            }
            else
            {
                NothingDisplayControl.Visibility = Visibility.Collapsed;
            }
        }

        public void LoadingActivation(bool IsLoading,string Text=null)
        {
            if(IsLoading)
            {
                LoadingControl.IsLoading = true;
                LoadingText.Text = Text;
            }
            else
            {
                LoadingControl.IsLoading = false;
            }
        }

        private void MarkRead_Click(object sender, RoutedEventArgs e)
        {
            var item = EmailList.SelectedItem as EmailItem;

            if (MarkRead.Label == "标记为已读")
            {
                item.SetSeenIndicator(Visibility.Collapsed);

                for (int i = 0; i < EmailNotSeenItemCollection.Count; i++)
                {
                    if (EmailNotSeenItemCollection[i].Id == item.Id)
                    {
                        EmailNotSeenItemCollection.RemoveAt(i);
                    }
                }
            }
            else
            {
                item.SetSeenIndicator(Visibility.Visible);

                EmailNotSeenItemCollection.Add(item);
            }
        }

        private void EmailList_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            EmailList.SelectedItem = (e.OriginalSource as FrameworkElement)?.DataContext as EmailItem;
        }

        private void EmailList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EmailList.SelectedIndex == -1)
            {
                MarkRead.IsEnabled = false;
            }
            else
            {
                MarkRead.IsEnabled = true;
                if ((EmailList.SelectedItem as EmailItem).IsNotSeenIndicator == 0)
                {
                    if (DisplayMode.SelectedIndex != 1)
                    {
                        MarkRead.Label = "标记为未读";
                    }
                }
                else
                {
                    MarkRead.Label = "标记为已读";
                }
            }
            if(Nav.CurrentSourcePageType.Name=="EmailSender")
            {
                Nav.GoBack();
            }
        }

        private void NewEmail_Click(object sender, RoutedEventArgs e)
        {
            Nav.Navigate(typeof(EmailSender), new InfomationDeliver(EmailService.UserName), new DrillInNavigationTransitionInfo());
            EmailSender.ThisPage.SendEmailRequested += EmailDetail.ThisPage.ThisPage_SendEmailRequested;
        }

        private async void Logout_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = "警告",
                PrimaryButtonText = "继续",
                CloseButtonText = "取消",
                Content = "此操作将注销当前账户\r\r可能需要重新输入相关信息，是否继续？",
                Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
            };
            if((await dialog.ShowAsync())!=ContentDialogResult.Primary)
            {
                return;
            }
            ConnectionCancellation?.Cancel();
            await Task.Run(() =>
            {
                ExitLocker?.WaitOne();
            });
            ExitLocker?.Dispose();
            ExitLocker = null;
            ConnectionCancellation?.Dispose();
            ConnectionCancellation = null;

            DisplayMode.SelectionChanged -= DisplayMode_SelectionChanged;
            LastSelectedItem = null;

            if (EmailProtocolServiceProvider.CheckWhetherInstanceExist())
            {
                EmailProtocolServiceProvider.GetInstance().Dispose();
            }

            ApplicationData.Current.LocalSettings.Values["EmailStartup"] = null;
            ApplicationData.Current.LocalSettings.Values["EmailCredentialName"] = null;
            ApplicationData.Current.LocalSettings.Values["EmailCredentialPassword"] = null;
            ApplicationData.Current.LocalSettings.Values["EmailIMAPAddress"] = null;
            ApplicationData.Current.LocalSettings.Values["EmailIMAPPort"] = null;
            ApplicationData.Current.LocalSettings.Values["EmailSMTPAddress"] = null;
            ApplicationData.Current.LocalSettings.Values["EmailSMTPPort"] = null;
            ApplicationData.Current.LocalSettings.Values["EmailEnableSSL"] = null;
            ApplicationData.Current.LocalSettings.Values["EmailCallName"] = null;

            EmailPage.ThisPage.Nav.Navigate(typeof(EmailStartupOne), EmailPage.ThisPage.Nav, new DrillInNavigationTransitionInfo());
        }
    }
}
