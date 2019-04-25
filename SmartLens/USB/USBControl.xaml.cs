using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace SmartLens
{
    public sealed partial class USBControl : Page
    {
        public TreeViewNode CurrentNode { get; private set; }
        public StorageFolder CurrentFolder { get; private set; }
        public static USBControl ThisPage { get; private set; }
        public Dictionary<string, List<StorageFolder>> FolderDictionary;
        private bool IsAdding = false;
        private string RootFolderId;
        private CancellationTokenSource CancelToken;
        private AutoResetEvent Locker;
        private readonly object SyncRoot = new object();

        public USBControl()
        {
            InitializeComponent();
            ThisPage = this;
            InitializeTreeView();
            Loaded += USBControl_Loaded;
        }


        private void USBControl_Loaded(object sender, RoutedEventArgs e)
        {
            CancelToken = new CancellationTokenSource();
            Locker = new AutoResetEvent(false);
            Nav.Navigate(typeof(USBFilePresenter), Nav, new DrillInNavigationTransitionInfo());
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Locker.Dispose();
            CancelToken.Dispose();
        }

        /// <summary>
        /// 执行文件目录的初始化，查找USB设备
        /// </summary>
        private async void InitializeTreeView()
        {
            FolderDictionary = new Dictionary<string, List<StorageFolder>>();
            StorageFolder RemovableFolder = KnownFolders.RemovableDevices;
            RootFolderId = RemovableFolder.FolderRelativeId;
            if (RemovableFolder != null)
            {
                TreeViewNode RemovableNode = new TreeViewNode
                {
                    Content = RemovableFolder,
                    IsExpanded = true,
                    HasUnrealizedChildren = true
                };
                FolderTree.RootNodes.Add(RemovableNode);
                await FillTreeNode(RemovableNode);
                if (RemovableNode.Children.Count == 0)
                {
                    RemovableNode.Children.Add(new TreeViewNode() { Content = new EmptyDeviceDisplay() { DisplayName = "无USB设备接入" } });
                }
            }
        }

        /// <summary>
        /// 向特定TreeViewNode节点下添加子节点
        /// </summary>
        /// <param name="Node">节点</param>
        /// <returns></returns>
        private async Task FillTreeNode(TreeViewNode Node)
        {
            StorageFolder folder;
            if (Node.HasUnrealizedChildren == true)
            {
                folder = Node.Content as StorageFolder;
            }
            else
            {
                return;
            }

            IReadOnlyList<StorageFolder> StorageFolderList;

            /*
             * 在FolderDictionary中查找对应文件夹的唯一ID
             * 若存在则直接提取其下的文件夹列表
             * 若不存在则重新查找
             * 此处FolderDictionary作用类似缓存，任何文件夹展开一次后再次展开无需任何查询操作
             */
            if (FolderDictionary.ContainsKey(folder.FolderRelativeId))
            {
                StorageFolderList = FolderDictionary[folder.FolderRelativeId];
            }
            else
            {
                StorageFolderList = await folder.GetFoldersAsync();
                if (folder.FolderRelativeId != RootFolderId)
                {
                    //非根节点加入缓存，根节点因为USB设备会变动，所以不加入缓存
                    FolderDictionary.Add(folder.FolderRelativeId, new List<StorageFolder>(StorageFolderList));
                }
                else
                {
                    //若当前节点为根节点，且在根节点下无任何文件夹被发现，说明无USB设备插入
                    //因此清除根文件夹下的节点
                    if (StorageFolderList.Count == 0)
                    {
                        Node.Children.Clear();
                    }
                }
            }

            if (StorageFolderList.Count == 0)
            {
                return;
            }

            /*
             * 每展开一次文件夹时，将自动遍历该文件夹下面的 所有子文件夹 的 所有子文件夹
             * 一来超前缓存一级文件夹内容，二来能够确定哪些子文件夹下是不存在子文件夹的
             * 从而决定子文件夹是否要显示展开按钮，缺点是当存在大量子文件夹嵌套时展开速度可能会比较慢
             */
            foreach (var SubFolder in StorageFolderList)
            {
                IReadOnlyList<StorageFolder> SubSubStorageFolderList;
                if (FolderDictionary.ContainsKey(SubFolder.FolderRelativeId))
                {
                    SubSubStorageFolderList = FolderDictionary[SubFolder.FolderRelativeId];
                }
                else
                {
                    SubSubStorageFolderList = await SubFolder.GetFoldersAsync();
                    FolderDictionary.Add(SubFolder.FolderRelativeId, new List<StorageFolder>(SubSubStorageFolderList));
                }

                TreeViewNode NewNode = new TreeViewNode
                {
                    Content = SubFolder,
                    HasUnrealizedChildren = SubSubStorageFolderList.Count != 0
                };

                Node.Children.Add(NewNode);
            }
            Node.HasUnrealizedChildren = false;
        }

        private async void FileTree_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
        {
            if (args.Node.HasUnrealizedChildren)
            {
                await FillTreeNode(args.Node);
            }
            if ((args.Node.Content as StorageFolder).FolderRelativeId == RootFolderId)
            {
                if (args.Node.Children.Count == 0)
                {
                    args.Node.Children.Add(new TreeViewNode() { Content = new EmptyDeviceDisplay() { DisplayName = "无USB设备接入" } });
                }
            }
        }

        /// <summary>
        /// 获得指定文件的缩略图图像
        /// </summary>
        /// <param name="file">文件</param>
        /// <returns>缩略图图像</returns>
        public async Task<BitmapImage> GetThumbnailAsync(StorageFile file)
        {
            var Thumbnail = await file.GetThumbnailAsync(ThumbnailMode.ListView);
            if (Thumbnail == null)
            {
                return null;
            }
            BitmapImage bitmapImage = new BitmapImage
            {
                DecodePixelHeight = 60,
                DecodePixelWidth = 60
            };
            await bitmapImage.SetSourceAsync(Thumbnail);
            return bitmapImage;
        }

        private async void FileTree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            /*
             * 同一文件夹内可能存在大量文件
             * 因此切换不同文件夹时极有可能遍历文件夹仍未完成
             * 此处激活取消指令，等待当前遍历结束，再开始下一次文件遍历
             * 确保不会出现异常
             */
            //防止多次点击同一文件夹导致的多重查找            
            if ((args.InvokedItem as TreeViewNode).Content is StorageFolder folder)
            {
                if (folder.FolderRelativeId == CurrentFolder?.FolderRelativeId)
                {
                    IsAdding = false;
                    return;
                }

                if (IsAdding)
                {
                    await Task.Run(() =>
                    {
                        lock (SyncRoot)
                        {
                            CancelToken.Cancel();
                            Locker.WaitOne();
                        }
                    });
                }
                IsAdding = true;

                CurrentFolder = folder;
                CurrentNode = args.InvokedItem as TreeViewNode;

                //当处于USB其他附加功能的页面时，若点击文件目录则自动执行返回导航
                if (Nav.CurrentSourcePageType.Name != "USBFilePresenter")
                {
                    Nav.GoBack();
                }

                USBFilePresenter.ThisPage.FileCollection.Clear();

                QueryOptions Options = new QueryOptions(CommonFileQuery.DefaultQuery, null);
                Options.SetThumbnailPrefetch(ThumbnailMode.ListView, 60, ThumbnailOptions.ResizeThumbnail);
                Options.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, new string[] { "System.Size" });

                StorageFileQueryResult QueryResult = folder.CreateFileQueryWithOptions(Options);

                var FileList = await QueryResult.GetFilesAsync();

                USBFilePresenter.ThisPage.HasFile.Visibility = FileList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                foreach (var file in FileList)
                {
                    if (CancelToken.IsCancellationRequested)
                    {
                        goto FLAG;
                    }

                    IDictionary<string, object> PropertyResults = await file.Properties.RetrievePropertiesAsync(new string[] { "System.Size" });
                    ulong PropertiesSize = Convert.ToUInt64(PropertyResults["System.Size"]);
                    string Size = GetSizeDescription(PropertiesSize);

                    BitmapImage Thumbnail = await GetThumbnailAsync(file);
                    if (Thumbnail != null)
                    {
                        RemovableDeviceFile File = new RemovableDeviceFile(Size, file, Thumbnail);
                        USBFilePresenter.ThisPage.FileCollection.Add(File);
                    }
                    else
                    {
                        RemovableDeviceFile File = new RemovableDeviceFile(Size, file, new BitmapImage(new Uri("ms-appx:///Assets/DocIcon.png")) { DecodePixelHeight = 60, DecodePixelWidth = 60 });
                        USBFilePresenter.ThisPage.FileCollection.Add(File);
                    }
                }
            }

        FLAG:
            if (CancelToken.IsCancellationRequested)
            {
                CancelToken.Dispose();
                CancelToken = new CancellationTokenSource();
                Locker.Set();
            }
            else
            {
                IsAdding = false;
            }
        }

        /// <summary>
        /// 从文件大小获取标准描述
        /// </summary>
        /// <param name="PropertiesSize">文件大小</param>
        /// <returns></returns>
        private string GetSizeDescription(ulong PropertiesSize)
        {
            return PropertiesSize / 1024 < 1024 ? Math.Round(PropertiesSize / 1024f, 2).ToString() + " KB" : (PropertiesSize / 1048576 >= 1024 ? Math.Round(PropertiesSize / 1073741824f, 2).ToString() + " GB" : Math.Round(PropertiesSize / 1048576f, 2).ToString() + " MB");
        }

        private async void FolderDelete_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentNode == null)
            {
                return;
            }
            try
            {
                await (CurrentNode.Content as StorageFolder).DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
            catch (Exception)
            {
                ContentDialog contentDialog = new ContentDialog
                {
                    Title = "错误",
                    Content = "仅支持删除空文件夹\r请先删除文件夹内文件",
                    CloseButtonText = "确定",
                    Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                };
                await contentDialog.ShowAsync();
                return;
            }
            CurrentNode.Parent.Children.Remove(CurrentNode);
        }

        private void FileTree_Collapsed(TreeView sender, TreeViewCollapsedEventArgs args)
        {
            args.Node.Children.Clear();
            args.Node.HasUnrealizedChildren = true;
        }

        private void FolderTree_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if ((CurrentNode = (e.OriginalSource as FrameworkElement)?.DataContext as TreeViewNode) != null)
            {
                CreateFolder.IsEnabled = !FolderTree.RootNodes[0].Children.Contains(CurrentNode) && FolderTree.RootNodes[0] != CurrentNode;
            }
            else
            {
                CreateFolder.IsEnabled = false;
            }
        }

        private async void FolderRename_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentNode == null)
            {
                return;
            }
            var Folder = CurrentNode.Content as StorageFolder;
            RenameDialog renameDialog = new RenameDialog(Folder.Name);
            if (await renameDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                if (renameDialog.DesireName == "")
                {
                    ContentDialog content = new ContentDialog
                    {
                        Title = "错误",
                        Content = "文件夹名不能为空，重命名失败",
                        CloseButtonText = "确定",
                        Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                    };
                    await content.ShowAsync();
                    return;
                }

                //重命名后需要去除原文件夹的缓存
                FolderDictionary.Remove(Folder.FolderRelativeId);

                await Folder.RenameAsync(renameDialog.DesireName, NameCollisionOption.GenerateUniqueName);

                var ChildCollection = CurrentNode.Parent.Children;
                int index = CurrentNode.Parent.Children.IndexOf(CurrentNode);
                ChildCollection.Insert(index, new TreeViewNode() { Content = Folder, HasUnrealizedChildren = CurrentNode.HasChildren });
                ChildCollection.Remove(CurrentNode);
            }
        }

        private async void CreateFolder_Click(object sender, RoutedEventArgs e)
        {
            var CurrentFolder = (CurrentNode.Content as StorageFolder);
            var NewFolder = await CurrentFolder.CreateFolderAsync("新建文件夹", CreationCollisionOption.GenerateUniqueName);
            CurrentNode.Children.Add(new TreeViewNode
            {
                Content = NewFolder,
                HasUnrealizedChildren = false
            });
            if (FolderDictionary.ContainsKey(CurrentFolder.FolderRelativeId))
            {
                FolderDictionary[CurrentFolder.FolderRelativeId].Add(NewFolder);
            }
        }
    }

}