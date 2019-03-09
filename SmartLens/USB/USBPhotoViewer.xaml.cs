using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace SmartLens
{
    public sealed partial class USBPhotoViewer : Page
    {
        ObservableCollection<PhotoDisplaySupport> PhotoCollection;
        string SelectedPhotoID;

        public USBPhotoViewer()
        {
            InitializeComponent();
            ImageList.ItemsSource = PhotoCollection;
            Loaded += USBPhotoViewer_Loaded;
        }

        private async void USBPhotoViewer_Loaded(object sender, RoutedEventArgs e)
        {
            DisplayImage.Source = new BitmapImage();

            var FileCollection =await USBControl.ThisPage.CurrentFolder.GetFilesAsync();
            PhotoDisplaySupport temp = null;
            foreach(var item in FileCollection)
            {
                if(item.FileType==".png"||item.FileType==".jpg"||item.FileType==".jpeg"||item.FileType==".bmp")
                {
                    using (var Thumbnail = await item.GetThumbnailAsync(ThumbnailMode.PicturesView))
                    {
                        PhotoCollection.Add(new PhotoDisplaySupport(Thumbnail,item));
                    }
                    if(item.FolderRelativeId==SelectedPhotoID)
                    {
                        temp = PhotoCollection.Last();
                    }
                }
            }
            await Task.Delay(800);
            ImageList.ScrollIntoViewSmoothly(temp, ScrollIntoViewAlignment.Leading);
            ImageList.SelectedItem = temp;

            await Task.Delay(500);
            ChangeDisplayImage(ImageList.SelectedItem as PhotoDisplaySupport);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if(e.Parameter is string ID)
            {
                SelectedPhotoID = ID;
            }

            PhotoCollection = new ObservableCollection<PhotoDisplaySupport>();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            PhotoCollection.Clear();
            PhotoCollection = null;
            SelectedPhotoID = string.Empty;
            DisplayImage.Source = null;
            FileName.Text = "";
        }

        private void ImageList_ItemClick(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as PhotoDisplaySupport;
            if (item.PhotoFile.FolderRelativeId != SelectedPhotoID)
            {
                ChangeDisplayImage(item);
            }
        }

        private async void ChangeDisplayImage(PhotoDisplaySupport e)
        {
            FileName.Text = e.FileName;

            Image image = ((ImageList.ContainerFromItem(e) as ListViewItem).ContentTemplateRoot as FrameworkElement).FindName("Photo") as Image;
            ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("PhotoAnimation", image).Configuration = new BasicConnectedAnimationConfiguration();

            ConnectedAnimationService.GetForCurrentView().DefaultDuration = TimeSpan.FromMilliseconds(700);

            FadeOut.Begin();
            await Task.Delay(200);

            using (var stream = await e.PhotoFile.OpenAsync(FileAccessMode.Read))
            {
                await (DisplayImage.Source as BitmapImage).SetSourceAsync(stream);
            }

            FadeIn.Begin();

            try
            {
                ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("PhotoAnimation");
                animation?.TryStart(DisplayImage);
            }
            catch (Exception) { }

            SelectedPhotoID = e.PhotoFile.FolderRelativeId;
        }
    }
}
