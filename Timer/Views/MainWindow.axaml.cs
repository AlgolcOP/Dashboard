using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Timer.Models;
using Timer.ViewModels;

namespace Timer.Views
{
    /// <summary>
    /// MainWindow 主窗口视图类，继承自 Window
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// 默认构造函数，初始化组件
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // 在数据上下文设置后订阅事件
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            // 分别订阅计时器和倒计时器的迷你模���事件
            vm.TimerMiniModeRequested += OnTimerMiniModeRequested;
            vm.CountdownMiniModeRequested += OnCountdownMiniModeRequested;
        }

        private void DeleteRecord_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is TimerRecord record && DataContext is MainWindowViewModel vm)
            {
                vm.DeleteRecordCommand.Execute(record);
            }
        }

        private void RecordName_LostFocus(object? sender, RoutedEventArgs e)
        {
            if (sender is not TextBox { Tag: TimerRecord record } textBox)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(textBox.Text) && textBox.Text != record.Name)
            {
                // 直接更新记录
                record.Name = textBox.Text;
            }
        }

        private void ConfirmNotes_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not TimerRecord record)
            {
                return;
            }

            // 找到对应的TextBox
            var parent = button.Parent as Grid;
            var textBox = parent?.Children.OfType<TextBox>().FirstOrDefault();

            if (textBox != null)
            {
                record.Notes = textBox.Text ?? "";
            }
        }

        private void OnTimerMiniModeRequested(object? sender, EventArgs e)
        {
            if (DataContext is not MainWindowViewModel mainVm)
            {
                return;
            }

            // 创建计时器迷你窗口ViewModel，传递主界面ViewModel引用
            var miniVm = new MiniWindowViewModel(mainVm, false); // false表示计时器模式

            // 创建并显示迷你窗口
            var miniWindow = new MiniWindow(miniVm);
            miniWindow.Show();

            // 隐藏主窗口
            Hide();
        }

        private void OnCountdownMiniModeRequested(object? sender, EventArgs e)
        {
            if (DataContext is not MainWindowViewModel mainVm)
            {
                return;
            }

            // 创建倒计时器迷你窗口ViewModel，传递主界面ViewModel引用
            var miniVm = new MiniWindowViewModel(mainVm, true); // true表示倒计时器模式

            // 创建并显示迷你窗口
            var miniWindow = new MiniWindow(miniVm);
            miniWindow.Show();

            // 隐藏主窗口
            Hide();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // 取消订阅事件
            if (DataContext is not MainWindowViewModel vm)
            {
                return;
            }

            vm.TimerMiniModeRequested -= OnTimerMiniModeRequested;
            vm.CountdownMiniModeRequested -= OnCountdownMiniModeRequested;
        }

        private void CountdownHours_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox)
            {
                return;
            }

            if (int.TryParse(textBox.Text, out var hours))
            {
                if (hours < 0 || hours > 23)
                {
                    textBox.Text = "0";
                }
            }
            else
            {
                textBox.Text = "0";
            }
        }

        private void CountdownMinutes_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox)
            {
                return;
            }

            if (int.TryParse(textBox.Text, out var minutes))
            {
                if (minutes < 0 || minutes > 59)
                {
                    textBox.Text = "0";
                }
            }
            else
            {
                textBox.Text = "0";
            }
        }

        private void CountdownSeconds_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox)
            {
                return;
            }

            if (int.TryParse(textBox.Text, out var seconds))
            {
                if (seconds < 0 || seconds > 59)
                {
                    textBox.Text = "0";
                }
            }
            else
            {
                textBox.Text = "0";
            }
        }
    }
}