using MimeKit;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;


namespace SmartLens
{
    public sealed partial class EmailSender : Page, INotifyPropertyChanged
    {
        public event EventHandler<SendEmailData> SendEmailRequested;
        public event PropertyChangedEventHandler PropertyChanged;

        EmailSendType SendType;
        List<MimePart> Attachments;
        string To { get; set; }
        string From { get; set; }
        string Title { get; set; }
        public static EmailSender ThisPage { get; private set; }

        public EmailSender()
        {
            InitializeComponent();
            ThisPage = this;
            Loaded += EmailSender_Loaded;
        }

        private void EmailSender_Loaded(object sender, RoutedEventArgs e)
        {
            Attachments = new List<MimePart>();
        }

        private void OnPropertyChanged()
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs("To"));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs("From"));
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs("Title"));
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is InfomationDeliver info)
            {
                SendType = info.SendType;
                switch (info.SendType)
                {
                    case EmailSendType.NormalSend:
                        {
                            From = info.From;
                            ToWho.IsReadOnly = false;
                            EmailText.IsReadOnly = false;
                            Insert.IsEnabled = true;
                            break;
                        }
                    case EmailSendType.Reply:
                        {
                            To = info.To;
                            From = info.From;
                            Title = info.Title;
                            ToWho.IsReadOnly = true;
                            EmailText.IsReadOnly = false;
                            Insert.IsEnabled = true;
                            break;
                        }
                    case EmailSendType.ReplyToAll:
                        {
                            To = info.To;
                            From = info.From;
                            Title = info.Title;
                            ToWho.IsReadOnly = true;
                            EmailText.IsReadOnly = false;
                            Insert.IsEnabled = true;
                            break;
                        }
                    case EmailSendType.Forward:
                        {
                            From = info.From;
                            Title = info.Title;
                            ToWho.IsReadOnly = false;
                            EmailText.IsReadOnly = true;
                            Insert.IsEnabled = false;
                            break;
                        }
                }
            }
            OnPropertyChanged();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            To = "";
            From = "";
            Title = "";
            EmailText.Text = "";
            InsertText.Text = "";
            OnPropertyChanged();

            Attachments.Clear();
            Attachments = null;
            SendEmailRequested -= EmailDetail.ThisPage.ThisPage_SendEmailRequested;
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(To) || string.IsNullOrWhiteSpace(Title))
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "Oops...",
                    Content = "收件人和主题不能为空哦",
                    CloseButtonText = "确定",
                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                };
                await dialog.ShowAsync();
                return;
            }

            //使用正则表达式确定Email地址是否是符合Email地址规范的
            Regex EmailCheck = new Regex("^\\s*([A-Za-z0-9_-]+(\\.\\w+)*@(\\w+\\.)+\\w{2,5})\\s*$");
            string[] Address = To.Split(";");
            if (Address.Where(x => EmailCheck.IsMatch(x)).Count() != Address.Length)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "Oops...",
                    Content = "Email地址不合法哦\r\r多个地址之间用分号隔开",
                    CloseButtonText = "确定",
                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                };
                await dialog.ShowAsync();
                return;
            }

            switch(SendType)
            {
                case EmailSendType.NormalSend:
                    {
                        SendEmailRequested?.Invoke(null, new SendEmailData(To, Title, EmailText.Text, Attachments.Count == 0 ? null : Attachments));
                        break;
                    }
                case EmailSendType.Reply:
                    {
                        SendEmailRequested?.Invoke(null, new SendEmailData(EmailText.Text, SendType, Attachments.Count == 0 ? null : Attachments));
                        break;
                    }
                case EmailSendType.ReplyToAll:
                    {
                        SendEmailRequested?.Invoke(null, new SendEmailData(EmailText.Text, SendType, Attachments.Count == 0 ? null : Attachments));
                        break;
                    }
                case EmailSendType.Forward:
                    {
                        SendEmailRequested?.Invoke(null, new SendEmailData(To));
                        break;
                    }
            }

            EmailPresenter.ThisPage.Nav.Navigate(typeof(EmailDetail), null, new DrillInNavigationTransitionInfo());
        }

        private async void Insert_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker Picker = new FileOpenPicker
            {
                CommitButtonText = "插入附件",
                SuggestedStartLocation = PickerLocationId.Desktop,
                ViewMode = PickerViewMode.Thumbnail
            };
            Picker.FileTypeFilter.Add("*");
            if ((await Picker.PickMultipleFilesAsync()) is IReadOnlyList<StorageFile> FileList)
            {
                foreach (var File in FileList)
                {
                    var attachment = new MimePart(File.ContentType, File.FileType)
                    {
                        Content = new MimeContent(await File.OpenStreamForReadAsync(), ContentEncoding.Default),
                        ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                        ContentTransferEncoding = ContentEncoding.Base64,
                        FileName = File.Name
                    };
                    Attachments.Add(attachment);
                }
                InsertText.Text = "(共" + Attachments.Count + "个)";
            }
        }

        private void Abort_Click(object sender, RoutedEventArgs e)
        {
            EmailPresenter.ThisPage.Nav.Navigate(typeof(EmailDetail), null, new DrillInNavigationTransitionInfo());
        }
    }
}
