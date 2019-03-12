using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace SmartLens
{
    public sealed partial class USBPdfReader : Page
    {
        private StorageFile PdfFile;
        private ObservableCollection<BitmapImage> PdfCollection;
        private PdfDocument Pdf;
        private int LastPageIndex = 0;
        private int IndexCounter = 0;
        private bool IsRunning = false;
        private Queue<int> LoadQueue;
        private AutoResetEvent ExitLocker;
        private CancellationTokenSource Cancellation;

        public USBPdfReader()
        {
            InitializeComponent();
            Loaded += USBPdfReader_Loaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is StorageFile file)
            {
                PdfFile = file;
            }
        }

        protected async override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Flip.SelectionChanged -= Flip_SelectionChanged;
            Flip.SelectionChanged -= Flip_SelectionChanged1;

            PdfFile = null;
            LoadQueue.Clear();
            LoadQueue = null;
            LastPageIndex = 0;
            IndexCounter = 0;
            IsRunning = false;

            await Task.Run(() =>
            {
                ExitLocker.WaitOne();
            });

            ExitLocker.Dispose();
            ExitLocker = null;

            Cancellation.Dispose();
            Cancellation = null;

            PdfCollection.Clear();
            PdfCollection = null;
            Pdf = null;
        }

        private async void USBPdfReader_Loaded(object sender, RoutedEventArgs e)
        {
            PdfCollection = new ObservableCollection<BitmapImage>();
            LoadQueue = new Queue<int>();
            ExitLocker = new AutoResetEvent(false);
            Cancellation = new CancellationTokenSource();
            Flip.SelectionChanged += Flip_SelectionChanged;
            Flip.SelectionChanged += Flip_SelectionChanged1;
            Flip.ItemsSource = PdfCollection;

            Pdf = await PdfDocument.LoadFromFileAsync(PdfFile);

            for (uint i = 0; i < 5 && i < Pdf.PageCount && !Cancellation.IsCancellationRequested; i++)
            {
                using (PdfPage Page = Pdf.GetPage(i))
                {
                    using (InMemoryRandomAccessStream PageStream = new InMemoryRandomAccessStream())
                    {
                        await Page.RenderToStreamAsync(PageStream);
                        BitmapImage DisplayImage = new BitmapImage();
                        await DisplayImage.SetSourceAsync(PageStream);
                        PdfCollection.Add(DisplayImage);
                    }
                }
            }
            ExitLocker.Set();
        }

        private void Flip_SelectionChanged1(object sender, SelectionChangedEventArgs e)
        {
            int CurrentPage = Flip.SelectedIndex + 1;
            PageNotification.Show(CurrentPage + " / (共 " + Pdf.PageCount+" 页)", 1200);
        }

        private async void Flip_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadQueue.Enqueue(IndexCounter++);

            if (IsRunning)
            {
                return;
            }
            IsRunning = true;

            await Task.Run(async () =>
            {
                while (LoadQueue.Count != 0)
                {
                    int CurrentIndex = LoadQueue.Dequeue();
                    
                    if (LastPageIndex < CurrentIndex)
                    {
                        uint CurrentLoading = (uint)(CurrentIndex + 4);
                        if (CurrentLoading == Pdf.PageCount)
                        {
                            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                            {
                                Flip.SelectionChanged -= Flip_SelectionChanged;
                            });
                            return;
                        }

                        using (PdfPage Page = Pdf.GetPage(CurrentLoading))
                        {
                            using (InMemoryRandomAccessStream PageStream = new InMemoryRandomAccessStream())
                            {
                                await Page.RenderToStreamAsync(PageStream);
                                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                                {
                                    BitmapImage DisplayImage = new BitmapImage();
                                    await DisplayImage.SetSourceAsync(PageStream);
                                    PdfCollection.Add(DisplayImage);
                                });
                            }
                        }
                    }
                    LastPageIndex = CurrentIndex;
                }
            });

            IsRunning = false;
        }

    }
}
