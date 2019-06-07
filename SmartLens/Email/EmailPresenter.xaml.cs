using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MailKit.Security;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Notifications;
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
        public static EmailPresenter ThisPage { get; private set; }
        public EmailProtocolServiceProvider EmailService;
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
            if (e.Parameter is EmailLoginData data)
            {
                EmailService.SetCredential(new System.Net.NetworkCredential(data.EmailAddress, data.Password), data.CallName);
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

        /// <summary>
        /// 激活或关闭"正在同步"的提示
        /// </summary>
        /// <param name="IsActivate">激活或关闭</param>
        private async Task ActivateSyncNotification(bool IsActivate)
        {
            if (ConnectionCancellation.IsCancellationRequested)
            {
                return;
            }
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

            await ActivateSyncNotification(true);

            try
            {
                if (EmailService == null)
                {
                    EmailService = EmailProtocolServiceProvider.GetInstance();
                }
                await EmailService.ConnectAllServiceAsync(ConnectionCancellation);
            }
            catch (TaskCanceledException)
            {
                await ActivateSyncNotification(false);
            }
            catch (OperationCanceledException)
            {
                await ActivateSyncNotification(false);
            }
            catch (SocketException)
            {
                SyncNotification.Dismiss();

                await Task.Delay(1000);

                bool isTemplatePresent = Resources.TryGetValue("NetWorkErrorNotificationTemplate", out object NotificationTemplate);

                if (isTemplatePresent && NotificationTemplate is DataTemplate template)
                {
                    SyncNotification.Show(template, 5000);
                }
                Updating = false;
                return;
            }
            catch (ImapProtocolException)
            {
                SyncNotification.Dismiss();

                await Task.Delay(1000);

                bool isTemplatePresent = Resources.TryGetValue("ProtocolErrorNotificationTemplate", out object NotificationTemplate);

                if (isTemplatePresent && NotificationTemplate is DataTemplate template)
                {
                    SyncNotification.Show(template, 5000);
                }
                Updating = false;
                return;
            }
            catch (SmtpProtocolException)
            {
                SyncNotification.Dismiss();

                await Task.Delay(1000);

                bool isTemplatePresent = Resources.TryGetValue("ProtocolErrorNotificationTemplate", out object NotificationTemplate);

                if (isTemplatePresent && NotificationTemplate is DataTemplate template)
                {
                    SyncNotification.Show(template, 5000);
                }
                Updating = false;
                return;
            }
            catch (SslHandshakeException)
            {
                SyncNotification.Dismiss();

                await Task.Delay(1000);

                bool isTemplatePresent = Resources.TryGetValue("SSLErrorNotificationTemplate", out object NotificationTemplate);

                if (isTemplatePresent && NotificationTemplate is DataTemplate template)
                {
                    SyncNotification.Show(template, 5000);
                }
                Updating = false;
                return;
            }
            catch (Exception)
            {
                SyncNotification.Dismiss();

                await Task.Delay(1000);

                bool isTemplatePresent = Resources.TryGetValue("AuthenticationErrorNotificationTemplate", out object NotificationTemplate);

                if (isTemplatePresent && NotificationTemplate is DataTemplate template)
                {
                    SyncNotification.Show(template, 5000);
                }
                Updating = false;
                return;
            }

            if (!ConnectionCancellation.IsCancellationRequested)
            {
                try
                {
                    await LoadEmailData();
                    await ActivateSyncNotification(false);
                }
                catch (Exception) { }
            }

            Updating = false;
        }

        /// <summary>
        /// 异步获取Email邮件
        /// </summary>
        /// <returns>无</returns>
        private async Task LoadEmailData()
        {
            var Inbox = EmailService.GetMailFolder();

            try
            {
                await Inbox?.OpenAsync(FolderAccess.ReadWrite, ConnectionCancellation.Token);

                //编写查询语句，查找邮箱中标记为“未读”且来源不等于UserName的邮件
                var NotSeenSearchResult = await Inbox?.SearchAsync(SearchQuery.NotSeen.And(SearchQuery.Not(SearchQuery.FromContains(EmailService.UserName))), ConnectionCancellation.Token);
                EmailNotSeenItemCollection.Clear();
                NotSeenDictionary.Clear();
                if (NotSeenSearchResult != null)
                {
                    foreach (var uid in NotSeenSearchResult)
                    {
                        var message = await Inbox.GetMessageAsync(uid, ConnectionCancellation.Token);
                        NotSeenDictionary.Add(uid);
                        EmailNotSeenItemCollection.Add(new EmailItem(message, uid));
                    }
                }

                //编写查询语句，查找邮箱中的所有，且来源不等于UserName的邮件
                var SearchResult = await Inbox?.SearchAsync(SearchQuery.All.And(SearchQuery.Not(SearchQuery.FromContains(EmailService.UserName))), ConnectionCancellation.Token);
                EmailAllItemCollection.Clear();

                if (EmailAllItemCollection != null)
                {
                    foreach (var uid in SearchResult)
                    {
                        var message = await Inbox.GetMessageAsync(uid, ConnectionCancellation.Token);
                        EmailAllItemCollection.Add(new EmailItem(message, uid));
                    }
                }
            }
            catch (TaskCanceledException)
            {
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }

            /*
             * 类似from EmailItem in EmailAllItemCollection group EmailItem by EmailItem.Date into GroupedItem orderby GroupedItem.Key descending select GroupedItem
             * Linq语句将EmailAllItemCollection中的元素 依据元素的Date成员 以降序的形式生成IGrouping<string, EmailItem>
             * 以便ListView能够根据这种排序构建按日期分类的项
             */
            switch (DisplayMode.SelectedIndex)
            {
                case 0:
                    EmailDisplayCollection = new ObservableCollection<IGrouping<string, EmailItem>>(from EmailItem
                                                                                                    in EmailAllItemCollection
                                                                                                    group EmailItem
                                                                                                    by EmailItem.Date
                                                                                                    into GroupedItem
                                                                                                    orderby GroupedItem.Key
                                                                                                    descending
                                                                                                    select GroupedItem);
                    break;
                case 1:
                    EmailDisplayCollection = new ObservableCollection<IGrouping<string, EmailItem>>(from EmailItem
                                                                                                    in EmailNotSeenItemCollection
                                                                                                    group EmailItem
                                                                                                    by EmailItem.Date
                                                                                                    into GroupedItem
                                                                                                    orderby GroupedItem.Key
                                                                                                    descending
                                                                                                    select GroupedItem);
                    break;
            }

            NothingDisplayControl.Visibility = Visibility.Collapsed;

            CVS.Source = EmailDisplayCollection;
            EmailList.SelectedIndex = -1;
        }

        private async void EmailList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (EmailList.SelectionMode == ListViewSelectionMode.Multiple)
            {
                return;
            }
            if (e.ClickedItem is EmailItem Email)
            {
                if (Email == LastSelectedItem)
                {
                    return;
                }
                LastSelectedItem = Email;

                //建立HtmlPreviewVisitor的实例以解析邮件中的HTML内容
                HtmlPreviewVisitor visitor = new HtmlPreviewVisitor(ApplicationData.Current.TemporaryFolder.Path);
                Email.Message.Accept(visitor);

                if (Email.FileEntitys.Count() != 0)
                {
                    EmailDetail.ThisPage.FileExpander.Visibility = Visibility.Visible;

                    EmailDetail.ThisPage.FileCollection.Clear();
                    foreach (var Entity in Email.Message.Attachments)
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
                    //为获得最大的流畅性能，将WebView控件设置为具有单独的进程执行
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

                //将解析出来的HTML交由WebBrowser进行读取解析呈现
                EmailDetail.ThisPage.WebBrowser.NavigateToString(visitor.HtmlBody);

                if (Email.IsNotSeenIndicator == 1)
                {
                    await Email.SetSeenIndicatorAsync(Visibility.Collapsed);

                    for (int i = 0; i < EmailNotSeenItemCollection.Count; i++)
                    {
                        if (EmailNotSeenItemCollection[i].Id == Email.Id)
                        {
                            EmailNotSeenItemCollection.RemoveAt(i);
                        }
                    }
                }
            }
        }

        private async void Sync_Click(object sender, RoutedEventArgs e)
        {
            if (Updating)
            {
                return;
            }
            Updating = true;

            if (!EmailService.IsIMAPConnected)
            {
                try
                {
                    await EmailService.ConnectAllServiceAsync(ConnectionCancellation);
                }
                catch (TaskCanceledException)
                {
                    await ActivateSyncNotification(false);
                    Updating = false;
                    return;
                }
                catch (OperationCanceledException)
                {
                    await ActivateSyncNotification(false);
                    Updating = false;
                    return;
                }
                catch (SocketException)
                {
                    SyncNotification.Dismiss();

                    await Task.Delay(1000);

                    bool isTemplatePresent = Resources.TryGetValue("NetWorkErrorNotificationTemplate", out object NotificationTemplate);

                    if (isTemplatePresent && NotificationTemplate is DataTemplate template)
                    {
                        SyncNotification.Show(template, 5000);
                    }
                    Updating = false;
                    return;
                }
                catch (ImapProtocolException)
                {
                    SyncNotification.Dismiss();

                    await Task.Delay(1000);

                    bool isTemplatePresent = Resources.TryGetValue("ProtocolErrorNotificationTemplate", out object NotificationTemplate);

                    if (isTemplatePresent && NotificationTemplate is DataTemplate template)
                    {
                        SyncNotification.Show(template, 5000);
                    }
                    Updating = false;
                    return;
                }
                catch (SmtpProtocolException)
                {
                    SyncNotification.Dismiss();

                    await Task.Delay(1000);

                    bool isTemplatePresent = Resources.TryGetValue("ProtocolErrorNotificationTemplate", out object NotificationTemplate);

                    if (isTemplatePresent && NotificationTemplate is DataTemplate template)
                    {
                        SyncNotification.Show(template, 5000);
                    }
                    Updating = false;
                    return;
                }
                catch (SslHandshakeException)
                {
                    SyncNotification.Dismiss();

                    await Task.Delay(1000);

                    bool isTemplatePresent = Resources.TryGetValue("SSLErrorNotificationTemplate", out object NotificationTemplate);

                    if (isTemplatePresent && NotificationTemplate is DataTemplate template)
                    {
                        SyncNotification.Show(template, 5000);
                    }
                    Updating = false;
                    return;
                }
                catch (Exception)
                {
                    SyncNotification.Dismiss();

                    await Task.Delay(1000);

                    bool isTemplatePresent = Resources.TryGetValue("AuthenticationErrorNotificationTemplate", out object NotificationTemplate);

                    if (isTemplatePresent && NotificationTemplate is DataTemplate template)
                    {
                        SyncNotification.Show(template, 5000);
                    }
                    Updating = false;
                    return;
                }

                await ActivateSyncNotification(true);

                try
                {
                    await LoadEmailData();
                }
                catch (Exception) { }

                await ActivateSyncNotification(false);
                Updating = false;

                return;
            }

            await ActivateSyncNotification(true);

            var Inbox = EmailService.GetMailFolder();

            try
            {
                var NotSeenSearchResult = await Inbox?.SearchAsync(SearchQuery.NotSeen.And(SearchQuery.Not(SearchQuery.FromContains(EmailService.UserName))), ConnectionCancellation.Token);
                EmailNotSeenItemCollection.Clear();
                NotSeenDictionary.Clear();

                if (EmailNotSeenItemCollection != null)
                {
                    foreach (var uid in NotSeenSearchResult)
                    {
                        var message = await Inbox.GetMessageAsync(uid, ConnectionCancellation.Token);
                        NotSeenDictionary.Add(uid);
                        EmailNotSeenItemCollection.Add(new EmailItem(message, uid));
                    }

                    if (NotSeenDictionary.Count > 0)
                    {
                        ShowEmailNotification(NotSeenDictionary.Count);
                    }
                }

                var SearchResult = await Inbox?.SearchAsync(SearchQuery.All.And(SearchQuery.Not(SearchQuery.FromContains(EmailService.UserName))), ConnectionCancellation.Token);
                EmailAllItemCollection.Clear();

                if (EmailAllItemCollection != null)
                {
                    foreach (var uid in SearchResult)
                    {
                        var message = await Inbox.GetMessageAsync(uid, ConnectionCancellation.Token);
                        EmailAllItemCollection.Add(new EmailItem(message, uid));
                    }
                }
            }
            catch (TaskCanceledException)
            {
                await ActivateSyncNotification(false);
                Updating = false;
                return;
            }
            catch (OperationCanceledException)
            {
                await ActivateSyncNotification(false);
                Updating = false;
                return;
            }
            catch (SocketException)
            {
                SyncNotification.Dismiss();

                await Task.Delay(1000);

                bool isTemplatePresent = Resources.TryGetValue("NetWorkErrorNotificationTemplate", out object NotificationTemplate);

                if (isTemplatePresent && NotificationTemplate is DataTemplate template)
                {
                    SyncNotification.Show(template, 5000);
                }
                Updating = false;
                return;
            }
            catch (ImapProtocolException)
            {
                SyncNotification.Dismiss();

                await Task.Delay(1000);

                bool isTemplatePresent = Resources.TryGetValue("ProtocolErrorNotificationTemplate", out object NotificationTemplate);

                if (isTemplatePresent && NotificationTemplate is DataTemplate template)
                {
                    SyncNotification.Show(template, 5000);
                }
                Updating = false;
                return;
            }
            catch (SmtpProtocolException)
            {
                SyncNotification.Dismiss();

                await Task.Delay(1000);

                bool isTemplatePresent = Resources.TryGetValue("ProtocolErrorNotificationTemplate", out object NotificationTemplate);

                if (isTemplatePresent && NotificationTemplate is DataTemplate template)
                {
                    SyncNotification.Show(template, 5000);
                }
                Updating = false;
                return;
            }
            catch (SslHandshakeException)
            {
                SyncNotification.Dismiss();

                await Task.Delay(1000);

                bool isTemplatePresent = Resources.TryGetValue("SSLErrorNotificationTemplate", out object NotificationTemplate);

                if (isTemplatePresent && NotificationTemplate is DataTemplate template)
                {
                    SyncNotification.Show(template, 5000);
                }
                Updating = false;
                return;
            }
            catch (Exception)
            {
                SyncNotification.Dismiss();

                await Task.Delay(1000);

                bool isTemplatePresent = Resources.TryGetValue("AuthenticationErrorNotificationTemplate", out object NotificationTemplate);

                if (isTemplatePresent && NotificationTemplate is DataTemplate template)
                {
                    SyncNotification.Show(template, 5000);
                }
                Updating = false;
                return;
            }


            /*
             * 更新邮件列表逻辑：
             * 将新获取到的所有邮件与现有邮件按日期分类来对比
             * 若某个日期内的邮件数量与新邮件对应日期内的数量不对应
             * 则认定有新邮件在该日期内出现
             * 由于IGrouping<out TKey, out TElement>集合无法进行任何修改
             * 因此采取删除日期内所有邮件并用新的IGrouping<out TKey, out TElement>替换的方式
             */
            switch (DisplayMode.SelectedIndex)
            {
                case 0:
                    {
                        for (int i = 0; i < EmailDisplayCollection.Count; i++)
                        {
                            foreach (var Grouping in from Grouping in from EmailItem in EmailAllItemCollection group EmailItem by EmailItem.Date into GroupedItem orderby GroupedItem.Key descending select GroupedItem
                                                     where EmailDisplayCollection[i].Key == Grouping.Key
                                                     where EmailDisplayCollection[i].Count() != Grouping.Count()
                                                     select Grouping)
                            {
                                EmailDisplayCollection.Remove(EmailDisplayCollection[i]);
                                EmailDisplayCollection.Insert(i, Grouping);
                                break;
                            }
                        }
                        break;
                    }
                case 1:
                    {
                        for (int i = 0; i < EmailDisplayCollection.Count; i++)
                        {
                            foreach (var Grouping in from Grouping in from EmailItem in EmailNotSeenItemCollection group EmailItem by EmailItem.Date into GroupedItem orderby GroupedItem.Key descending select GroupedItem
                                                     where EmailDisplayCollection[i].Key == Grouping.Key
                                                     where EmailDisplayCollection[i].Count() != Grouping.Count()
                                                     select Grouping)
                            {
                                EmailDisplayCollection.Remove(EmailDisplayCollection[i]);
                                EmailDisplayCollection.Insert(i, Grouping);
                                break;
                            }
                        }
                        break;
                    }
            }

            await ActivateSyncNotification(false);
            Updating = false;
        }

        private void ShowEmailNotification(int NotSeenEmailNum)
        {
            ToastNotificationManager.History.Clear();
            var Content = new ToastContent()
            {
                Scenario = ToastScenario.Default,
                Launch = "Email",
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = "有" + NotSeenEmailNum + "封未读邮件正在等待您查看"
                            },

                            new AdaptiveText()
                            {
                               Text = "SmartLens邮件模块发现新邮件"
                            },

                            new AdaptiveText()
                            {
                               Text = "已获取并准备好查看"
                            }
                        }
                    }
                },
            };
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
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

            //IMAP是双向协议，因此向服务器对应邮件设置Deleted标志
            await EmailService.GetMailFolder()?.SetFlagsAsync(item.Id, MessageFlags.Deleted, true);

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

        /// <summary>
        /// 显示或关闭正在加载的提示
        /// </summary>
        /// <param name="IsLoading">开启或关闭</param>
        /// <param name="Text">提示内容</param>
        public void LoadingActivation(bool IsLoading, string Text = null)
        {
            if (IsLoading)
            {
                LoadingControl.IsLoading = true;
                LoadingText.Text = Text;
            }
            else
            {
                LoadingControl.IsLoading = false;
            }
        }

        private async void MarkRead_Click(object sender, RoutedEventArgs e)
        {
            var Email = EmailList.SelectedItem as EmailItem;

            if (MarkRead.Label == "标记为已读")
            {
                await Email.SetSeenIndicatorAsync(Visibility.Collapsed);

                for (int i = 0; i < EmailNotSeenItemCollection.Count; i++)
                {
                    if (EmailNotSeenItemCollection[i].Id == Email.Id)
                    {
                        EmailNotSeenItemCollection.RemoveAt(i);
                    }
                }
            }
            else
            {
                await Email.SetSeenIndicatorAsync(Visibility.Visible);

                EmailNotSeenItemCollection.Add(Email);
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
            if (Nav.CurrentSourcePageType.Name == "EmailSender")
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
            if ((await dialog.ShowAsync()) != ContentDialogResult.Primary)
            {
                return;
            }

            LoadingText.Text = "正在注销...";
            LoadingControl.IsLoading = true;

            ConnectionCancellation?.Cancel();

            await Task.Delay(1000);

            ConnectionCancellation?.Dispose();
            ConnectionCancellation = null;

            DisplayMode.SelectionChanged -= DisplayMode_SelectionChanged;
            LastSelectedItem = null;

            if (EmailProtocolServiceProvider.CheckWhetherInstanceExist())
            {
                EmailProtocolServiceProvider.GetInstance().Dispose();
            }

            EmailService = null;

            ApplicationData.Current.LocalSettings.Values["EmailStartup"] = null;
            ApplicationData.Current.RoamingSettings.Values["EmailCredentialName"] = null;
            ApplicationData.Current.RoamingSettings.Values["EmailCredentialPassword"] = null;
            ApplicationData.Current.RoamingSettings.Values["EmailIMAPAddress"] = null;
            ApplicationData.Current.RoamingSettings.Values["EmailIMAPPort"] = null;
            ApplicationData.Current.RoamingSettings.Values["EmailSMTPAddress"] = null;
            ApplicationData.Current.RoamingSettings.Values["EmailSMTPPort"] = null;
            ApplicationData.Current.RoamingSettings.Values["EmailEnableSSL"] = null;
            ApplicationData.Current.RoamingSettings.Values["EmailCallName"] = null;

            LoadingControl.IsLoading = false;
            await Task.Delay(700);

            EmailAllItemCollection.Clear();
            EmailNotSeenItemCollection.Clear();
            EmailDisplayCollection?.Clear();

            EmailPage.ThisPage.Nav.Navigate(typeof(EmailStartupOne), EmailPage.ThisPage.Nav, new DrillInNavigationTransitionInfo());

            NothingDisplayControl.Visibility = Visibility.Visible;
        }
    }
}
