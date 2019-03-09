using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
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
        bool IsAdding = false;
        string RootFolderId;
        CancellationTokenSource CancelToken;
        AutoResetEvent ResetEvent;

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
            ResetEvent = new AutoResetEvent(false);
            Nav.Navigate(typeof(USBFilePresenter),Nav,new DrillInNavigationTransitionInfo());
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ResetEvent.Dispose();
            CancelToken.Dispose();
        }

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
                if(RemovableNode.Children.Count==0)
                {
                    RemovableNode.Children.Add(new TreeViewNode() { Content = new EmptyDeviceDisplay() { DisplayName="无USB设备接入"} });
                }
            }
        }

        private async Task FillTreeNode(TreeViewNode node)
        {
            StorageFolder folder = null;
            if (node.HasUnrealizedChildren == true)
            {
                folder = node.Content as StorageFolder;
            }
            else
            {
                return;
            }

            IReadOnlyList<StorageFolder> list;
            if (FolderDictionary.ContainsKey(folder.FolderRelativeId))
            {
                list = FolderDictionary[folder.FolderRelativeId];
            }
            else
            {
                list = await folder.GetFoldersAsync();
                if(folder.FolderRelativeId!= RootFolderId)
                {
                    FolderDictionary.Add(folder.FolderRelativeId, new List<StorageFolder>(list));
                }
                else
                {
                    if(list.Count==0)
                    {
                        node.Children.Clear();
                    }
                }
            }

            if (list.Count == 0)
            {
                return;
            }

            foreach (var item in list)
            {
                IReadOnlyList<StorageFolder> list1;
                if (FolderDictionary.ContainsKey(item.FolderRelativeId))
                {
                    list1 = FolderDictionary[item.FolderRelativeId];
                }
                else
                {
                    list1 = await item.GetFoldersAsync();
                    FolderDictionary.Add(item.FolderRelativeId, new List<StorageFolder>(list1));
                }

                var newNode = new TreeViewNode
                {
                    Content = item
                };
                if (list1.Count == 0)
                {
                    newNode.HasUnrealizedChildren = false;
                }
                else
                {
                    newNode.HasUnrealizedChildren = true;
                }

                node.Children.Add(newNode);
            }
            node.HasUnrealizedChildren = false;
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

        private async void FileTree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            if (IsAdding)
            {
                CancelToken.Cancel();
                await Task.Run(() =>
                {
                    ResetEvent.WaitOne();
                });
            }
            IsAdding = true;

            if ((args.InvokedItem as TreeViewNode).Content is StorageFolder folder)
            {
                if (folder.FolderRelativeId == CurrentFolder?.FolderRelativeId)
                {
                    IsAdding = false;
                    return;
                }

                CurrentFolder = folder;
                CurrentNode = args.InvokedItem as TreeViewNode;

                if (Nav.CurrentSourcePageType.Name == "ZipExplorer" || Nav.CurrentSourcePageType.Name=="USBPhotoViewer" || Nav.CurrentSourcePageType.Name == "USBMediaPlayer")
                {
                    Nav.GoBack();
                }

                foreach (RemovableDeviceFile item in USBFilePresenter.ThisPage.FileCollection)
                {
                    if (item.File.FileType == ".zip")
                    {
                        GridViewItem GridItem = USBFilePresenter.ThisPage.GridViewControl.ContainerFromItem(item) as GridViewItem;
                        GridItem.Drop -= USBControl_Drop;
                        GridItem.DragOver -= Item_DragOver;
                    }
                }

                USBFilePresenter.ThisPage.FileCollection.Clear();
                var FileList = await folder.GetFilesAsync();
                if (FileList.Count == 0)
                {
                    USBFilePresenter.ThisPage.HasFile.Visibility = Visibility.Visible;
                }
                else
                {
                    USBFilePresenter.ThisPage.HasFile.Visibility = Visibility.Collapsed;
                }
                foreach (var file in FileList)
                {
                    if (CancelToken.IsCancellationRequested)
                    {
                        CancelToken.Dispose();
                        CancelToken = new CancellationTokenSource();
                        ResetEvent.Set();
                        break;
                    }
                    var Thumbnail = await USBFilePresenter.ThisPage.GetThumbnail(file);
                    if (Thumbnail != null)
                    {
                        USBFilePresenter.ThisPage.FileCollection.Add(new RemovableDeviceFile(await USBFilePresenter.ThisPage.GetSize(file), file, Thumbnail));
                        if (file.FileType==".zip")
                        {
                            await Task.Delay(200);
                            GridViewItem item = (USBFilePresenter.ThisPage.GridViewControl.ContainerFromIndex(USBFilePresenter.ThisPage.FileCollection.Count - 1) as GridViewItem);
                            item.AllowDrop = true;
                            item.Drop += USBControl_Drop;
                            item.DragOver += Item_DragOver;
                        }
                    }
                    else
                    {
                        USBFilePresenter.ThisPage.FileCollection.Add(new RemovableDeviceFile(await USBFilePresenter.ThisPage.GetSize(file), file, new BitmapImage(new Uri("ms-appx:///Assets/DocIcon.png")) { DecodePixelHeight = 60, DecodePixelWidth = 60 }));
                    }
                }
            }
            IsAdding = false;
        }

        public void Item_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "添加至Zip文件";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsContentVisible = true;
        }

        public async void USBControl_Drop(object sender, DragEventArgs e)
        {
            RemovableDeviceFile file = (e.OriginalSource as GridViewItem).Content as RemovableDeviceFile;
            await USBFilePresenter.ThisPage.AddFileToZip(file);
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
                if (FolderTree.RootNodes[0].Children.Contains(CurrentNode) || FolderTree.RootNodes[0] == CurrentNode)
                {
                    CreateFolder.IsEnabled = false;
                }
                else
                {
                    CreateFolder.IsEnabled = true;
                }
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
            var NewFolder=await CurrentFolder.CreateFolderAsync("新建文件夹", CreationCollisionOption.GenerateUniqueName);
            CurrentNode.Children.Add(new TreeViewNode
            {
                Content = NewFolder,
                HasUnrealizedChildren = false
            });
            if(FolderDictionary.ContainsKey(CurrentFolder.FolderRelativeId))
            {
                FolderDictionary[CurrentFolder.FolderRelativeId].Add(NewFolder);
            }
        }
    }

}