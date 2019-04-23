using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace SmartLens
{
    public sealed partial class ChangeLog : Page
    {
        private int CurrentLine = LoadTextPerTimes;
        private const int LoadTextPerTimes = 300;
        private string[] Lines;
        private bool Running = false;
        private string CompleteString;
        private StringBuilder Builder = new StringBuilder();
        public ChangeLog()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Scroll.ViewChanging -= Scroll_ViewChanging;
            Builder.Clear();
            Builder = null;
            Lines = null;
            CompleteString = null;
            MarkdownControl.Text = null;
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            Lines = await File.ReadAllLinesAsync("About/ChangeLog.txt");

            await Task.Run(() =>
              {
                  if (Lines.Length <= LoadTextPerTimes)
                  {
                      for (int i = 0; i < Lines.Length; i++)
                      {
                          _ = Builder.AppendLine(Lines[i]);
                      }
                  }
                  else
                  {
                      Scroll.ViewChanging += Scroll_ViewChanging;

                      for (int i = 0; i < LoadTextPerTimes; i++)
                      {
                          _ = Builder.AppendLine(Lines[i]);
                      }
                  }
              });
            MarkdownControl.Text = Builder.ToString();
        }

        private async void Scroll_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
        {
            if (Scroll.VerticalOffset + 1000 >= Scroll.ScrollableHeight)
            {
                lock (SyncRootProvider.SyncRoot)
                {
                    if (Running)
                    {
                        return;
                    }
                    Running = true;
                }

                await Task.Run(() =>
                {
                    int Target = CurrentLine + LoadTextPerTimes;
                    for (; CurrentLine < Target && CurrentLine < Lines.Length; CurrentLine++)
                    {
                        _ = Builder.AppendLine(Lines[CurrentLine]);
                    }
                    CompleteString = Builder.ToString();
                });

                MarkdownControl.Text = CompleteString;

                if (CurrentLine == Lines.Length)
                {
                    Scroll.ViewChanging -= Scroll_ViewChanging;
                }

                Running = false;
            }
        }

        private void MarkdownControl_LinkClicked(object sender, Microsoft.Toolkit.Uwp.UI.Controls.LinkClickedEventArgs e)
        {
            MainPage.ThisPage.NavFrame.Navigate(typeof(WebTab), new Uri(e.Link), new DrillInNavigationTransitionInfo());
        }
    }
}
