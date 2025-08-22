using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Timer.ViewModels;

namespace Timer.Views
{
    /// <summary>
    /// MiniWindow 迷你窗口视图类，继承自 Window
    /// </summary>
    public partial class MiniWindow : Window
    {
        /// <summary>
        /// 默认构造函数，初始化组件
        /// </summary>
        public MiniWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 带ViewModel参数的构造函数，设置数据上下文并订阅事件
        /// </summary>
        /// <param name="viewModel">迷你窗口的视图模型</param>
        public MiniWindow(MiniWindowViewModel viewModel) : this()
        {
            DataContext = viewModel;

            // 订阅ViewModel事件
            viewModel.ReturnToMainRequested += OnReturnToMainRequested;
            viewModel.ExitRequested += OnExitRequested;

            // 使窗口可拖动
            PointerPressed += OnPointerPressed;
        }

        /// <summary>
        /// 鼠标按下事件处理，使窗口可拖动
        /// </summary>
        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // 只在非按钮区域允许拖动
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        }

        private void OnReturnToMainRequested(object? sender, EventArgs e)
        {
            // 显示主窗口
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.Windows.FirstOrDefault(w => w is MainWindow);
                if (mainWindow != null)
                {
                    mainWindow.Show();
                    mainWindow.WindowState = WindowState.Normal;
                    mainWindow.Activate();
                }
            }

            // 关闭迷你窗口
            Close();
        }

        private void OnExitRequested(object? sender, EventArgs e)
        {
            // 完全退出应用程序
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // 清理资源
            if (DataContext is MiniWindowViewModel viewModel)
            {
                viewModel.ReturnToMainRequested -= OnReturnToMainRequested;
                viewModel.ExitRequested -= OnExitRequested;
                viewModel.Dispose();
            }

            base.OnClosed(e);
        }
    }
}