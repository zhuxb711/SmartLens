using System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace SmartLens
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class BlueScreen : Page
    {
        public BlueScreen()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if(e.Parameter is Exception exception)
            {
                Message.Text = "\r以下是错误信息：\r\rException Code错误代码：" + exception.HResult + "\r\rMessage错误消息：" + exception.Message + "\r\rSource来源：" + exception.Source + "\r\rStackTrace堆栈追踪：\r" + exception.StackTrace;
            }
        }
    }
}
