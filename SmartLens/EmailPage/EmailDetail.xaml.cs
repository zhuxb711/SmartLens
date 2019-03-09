using MimeKit;
using MimeKit.Text;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace SmartLens
{
    public sealed partial class EmailDetail : Page
    {
        public static EmailDetail ThisPage { get; private set; }
        public ObservableCollection<EmailAttachment> FileCollection = new ObservableCollection<EmailAttachment>();
        public WebView WebBrowser;
        public EmailProtocolServiceProvider EmailService = EmailProtocolServiceProvider.GetInstance();

        public EmailDetail()
        {
            InitializeComponent();
            ThisPage = this;
            Loaded += EmailDetail_Loaded;
        }

        private void EmailDetail_Loaded(object sender, RoutedEventArgs e)
        {
            FileGridView.ItemsSource = FileCollection;
        }

        private void FileExpander_Expanded(object sender, EventArgs e)
        {
            ExpanderGrid.Height = new GridLength(280);
        }

        private async void FileExpander_Collapsed(object sender, EventArgs e)
        {
            await Task.Delay(500);
            ExpanderGrid.Height = new GridLength(40);
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            var Attachment = FileGridView.SelectedItem as EmailAttachment;
            await SaveAttachMent(Attachment.Entity, Attachment.FileName, Attachment.Type);
        }

        public async Task SaveAttachMent(MimeEntity Attachments, string FileName, string Type)
        {
            FileSavePicker picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.Desktop,
                CommitButtonText = "保存",
                DefaultFileExtension = "." + Type.ToLower(),
                SuggestedFileName = FileName
            };
            picker.FileTypeChoices.Add(Type + "文件", new List<string> { "." + Type.ToLower() });

            if ((await picker.PickSaveFileAsync()) is StorageFile file)
            {
                EmailPresenter.ThisPage.LoadingActivation(true, "正在保存...");

                if (Attachments is MessagePart)
                {
                    var fileName = string.IsNullOrEmpty(Attachments.ContentDisposition?.FileName) ? Attachments.ContentType.Name ?? "attached.eml" : Attachments.ContentDisposition.FileName;
                    var rfc822 = (MessagePart)Attachments;

                    using (var stream = await file.OpenStreamForWriteAsync())
                    {
                        await rfc822.Message.WriteToAsync(stream);
                    }
                }
                else
                {
                    var part = (MimePart)Attachments;
                    var fileName = part.FileName;

                    using (var stream = await file.OpenStreamForWriteAsync())
                    {
                        await part.Content.DecodeToAsync(stream);
                    }
                }

                await Task.Delay(1000);

                EmailPresenter.ThisPage.LoadingActivation(false);
            }
        }


        private void FileGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FileGridView.SelectedIndex == -1)
            {
                Save.IsEnabled = false;
            }
            else
            {
                Save.IsEnabled = true;
            }
        }

        private void FileGridView_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            FileGridView.SelectedItem = (e.OriginalSource as FrameworkElement)?.DataContext as EmailAttachment;
        }

        private void CommandBarDelete_Click(object sender, RoutedEventArgs e)
        {
            WebBrowser.Visibility = Visibility.Collapsed;
            CommandBarContorl.Visibility = Visibility.Collapsed;
            EmailPresenter.ThisPage.Delete_Click(null, null);
        }

        private void CommandBarForward_Click(object sender, RoutedEventArgs e)
        {
            MimeMessage message = (EmailPresenter.ThisPage.EmailList.SelectedItem as EmailItem).Message;
            string Title;
            if (!message.Subject.StartsWith("FW:", StringComparison.OrdinalIgnoreCase))
            {
                Title = "FW: " + message.Subject;
            }
            else
            {
                Title = message.Subject;
            }
            EmailPresenter.ThisPage.Nav.Navigate(typeof(EmailSender), new InfomationDeliver(EmailService.UserName, Title), new DrillInNavigationTransitionInfo());
            EmailSender.ThisPage.SendEmailRequested += ThisPage_SendEmailRequested;
        }

        private void CommandBarReplyAll_Click(object sender, RoutedEventArgs e)
        {
            MimeMessage message = (EmailPresenter.ThisPage.EmailList.SelectedItem as EmailItem).Message;
            string To = string.Empty;
            string Title = string.Empty;
            if (message.ReplyTo.Count > 0)
            {
                To = message.ReplyTo.Mailboxes.FirstOrDefault().Address;
            }
            else if (message.From.Count > 0)
            {
                To = message.From.Mailboxes.FirstOrDefault().Address;
            }
            else if (message.Sender != null)
            {
                To = message.Sender.Address;
            }
            if (!message.Subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase))
            {
                Title = "Re: " + message.Subject;
            }
            else
            {
                Title = message.Subject;
            }

            EmailPresenter.ThisPage.Nav.Navigate(typeof(EmailSender), new InfomationDeliver(EmailService.UserName, To, Title, EmailSendType.ReplyToAll), new DrillInNavigationTransitionInfo());
            EmailSender.ThisPage.SendEmailRequested += ThisPage_SendEmailRequested;
        }

        private void CommandBarReply_Click(object sender, RoutedEventArgs e)
        {
            MimeMessage message = (EmailPresenter.ThisPage.EmailList.SelectedItem as EmailItem).Message;
            string To = string.Empty;
            string Title = string.Empty;
            if (message.ReplyTo.Count > 0)
            {
                To = message.ReplyTo.Mailboxes.FirstOrDefault().Address;
            }
            else if (message.From.Count > 0)
            {
                To = message.From.Mailboxes.FirstOrDefault().Address;
            }
            else if (message.Sender != null)
            {
                To = message.Sender.Address;
            }
            if (!message.Subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase))
            {
                Title = "Re: " + message.Subject;
            }
            else
            {
                Title = message.Subject;
            }

            EmailPresenter.ThisPage.Nav.Navigate(typeof(EmailSender), new InfomationDeliver(EmailService.UserName, To, Title, EmailSendType.Reply), new DrillInNavigationTransitionInfo());
            EmailSender.ThisPage.SendEmailRequested += ThisPage_SendEmailRequested;
        }

        public async void ThisPage_SendEmailRequested(object sender, SendEmailData e)
        {
            EmailPresenter.ThisPage.LoadingActivation(true, "正在发送...");

            switch (e.SendType)
            {
                case EmailSendType.NormalSend:
                    {
                        MimeMessage SendMessage = GetSendMessage(e.To, new MailboxAddress(EmailService.CallName, EmailService.UserName), e.Subject, e.Text, e.Attachments);
                        await EmailProtocolServiceProvider.GetInstance().SendEmailAsync(SendMessage);
                        break;
                    }
                case EmailSendType.Reply:
                    {
                        MimeMessage message = (EmailPresenter.ThisPage.EmailList.SelectedItem as EmailItem).Message;
                        MimeMessage ReplyMessage = GetReplyMessage(message, new MailboxAddress(EmailService.CallName, EmailService.UserName), e.Attachments, false, e.Text);
                        await EmailProtocolServiceProvider.GetInstance().SendEmailAsync(ReplyMessage);
                        break;
                    }
                case EmailSendType.ReplyToAll:
                    {
                        MimeMessage message = (EmailPresenter.ThisPage.EmailList.SelectedItem as EmailItem).Message;
                        MimeMessage ReplyMessage = GetReplyMessage(message, new MailboxAddress(EmailService.CallName, EmailService.UserName), e.Attachments, true, e.Text);
                        await EmailProtocolServiceProvider.GetInstance().SendEmailAsync(ReplyMessage);
                        break;
                    }
                case EmailSendType.Forward:
                    {
                        MimeMessage message = (EmailPresenter.ThisPage.EmailList.SelectedItem as EmailItem).Message;
                        MimeMessage ForwardMessage = GetForwardMessage(message, new MailboxAddress(EmailService.CallName, EmailService.UserName), e.To);
                        await EmailProtocolServiceProvider.GetInstance().SendEmailAsync(ForwardMessage);
                        break;
                    }
            }
            await Task.Delay(1000);
            EmailPresenter.ThisPage.LoadingActivation(false);
        }

        public MimeMessage GetForwardMessage(MimeMessage original, MailboxAddress from, IEnumerable<InternetAddress> to)
        {
            var message = new MimeMessage();
            message.From.Add(from);
            message.To.AddRange(to);

            if (!original.Subject.StartsWith("FW:", StringComparison.OrdinalIgnoreCase))
            {
                message.Subject = "FW: " + original.Subject;
            }
            else
            {
                message.Subject = original.Subject;
            }

            var text = new TextPart("plain") { Text = "以下是转发的消息:" };

            var rfc822 = new MessagePart { Message = original };

            var multipart = new Multipart("mixed")
            {
                text,
                rfc822
            };

            message.Body = multipart;

            return message;
        }

        private MimeMessage GetSendMessage(List<MailboxAddress> To, MailboxAddress From, string Subject, string Text, List<MimePart> Attachments)
        {
            MimeMessage SendMessage = new MimeMessage();
            SendMessage.From.Add(From);
            SendMessage.To.AddRange(To);

            SendMessage.Subject = Subject;
            var EmailText = new TextPart(TextFormat.Plain)
            {
                Text = Text.ToString()
            };

            var PartsCollection = new Multipart("mixed")
            {
                EmailText
            };

            if (Attachments != null)
            {
                foreach (var Att in Attachments)
                {
                    PartsCollection.Add(Att);
                }
            }
            SendMessage.Body = PartsCollection;
            return SendMessage;
        }

        private MimeMessage GetReplyMessage(MimeMessage Message, MailboxAddress from, List<MimePart> Attachments, bool replyToAll, string Text)
        {
            var ReplyMessage = new MimeMessage();

            ReplyMessage.From.Add(from);

            string EmailFrom = string.Empty;
            if (Message.ReplyTo.Count > 0)
            {
                ReplyMessage.To.AddRange(Message.ReplyTo);
                EmailFrom = Message.ReplyTo.First().ToString();
            }
            else if (Message.From.Count > 0)
            {
                ReplyMessage.To.AddRange(Message.From);
                EmailFrom = Message.From.First().ToString();
            }
            else if (Message.Sender != null)
            {
                ReplyMessage.To.Add(Message.Sender);
                EmailFrom = Message.Sender.Name + "<" + Message.Sender.Address + ">";
            }

            if (replyToAll)
            {
                ReplyMessage.To.AddRange(Message.To.Mailboxes.Where(x => x.Address != from.Address));
                ReplyMessage.Cc.AddRange(Message.Cc.Mailboxes.Where(x => x.Address != from.Address));
            }

            if (!Message.Subject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase))
            {
                ReplyMessage.Subject = "Re: " + Message.Subject;
            }
            else
            {
                ReplyMessage.Subject = Message.Subject;
            }

            if (!string.IsNullOrEmpty(Message.MessageId))
            {
                ReplyMessage.InReplyTo = Message.MessageId;
                foreach (var id in Message.References)
                {
                    ReplyMessage.References.Add(id);
                }
                ReplyMessage.References.Add(Message.MessageId);
            }


            using (var quoted = new StringWriter())
            {
                quoted.Write(Text + "\r");
                quoted.WriteLine("------------------ 原始邮件 ------------------");

                quoted.WriteLine("发件人: " + EmailFrom);
                quoted.WriteLine("发送时间: {0}", Message.Date.ToString("f"));
                quoted.WriteLine("收件人: \"" + from.Name + "\"<" + from.Address + ">");
                quoted.WriteLine(quoted.NewLine);
                quoted.Write("主题: " + Message.Subject);
                quoted.WriteLine(quoted.NewLine);
                using (var reader = new StringReader(Message.TextBody))
                {
                    string line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        quoted.WriteLine(line);
                    }
                }

                var EmailText = new TextPart(TextFormat.Plain)
                {
                    Text = quoted.ToString()
                };

                var PartsCollection = new Multipart("mixed")
                {
                    EmailText
                };

                if (Attachments != null)
                {
                    foreach (var Att in Attachments)
                    {
                        PartsCollection.Add(Att);
                    }
                }
                ReplyMessage.Body = PartsCollection;
            }
            return ReplyMessage;
        }
    }
}
