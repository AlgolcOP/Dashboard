using System;
using System.Diagnostics;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;

namespace Timer.ViewModels
{
    /// <summary>
    ///     MiniWindowViewModel 迷你窗口的视图模型，负责迷你模式下的业务逻辑
    /// </summary>
    public sealed class MiniWindowViewModel : ViewModelBase, IDisposable
    {
        private readonly MainWindowViewModel _mainViewModel;
        private readonly System.Threading.Timer _syncTimer;

        // 显示属性
        private string _displayTime = "00:00:00";
        private bool _disposed;
        private string _playPauseText = "开始";

        public MiniWindowViewModel(MainWindowViewModel mainViewModel, bool isCountdownMode)
        {
            _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));
            IsCountdownMode = isCountdownMode;

            InitializeCommands();

            // 启动高频同步计时器，每50ms同步一次状态
            _syncTimer = new System.Threading.Timer(SyncWithMainViewModel, null, 0, 50);
        }

        // 属性
        /// <summary>
        ///     显示的时间字符串
        /// </summary>
        public string DisplayTime
        {
            get => _displayTime;
            set => this.RaiseAndSetIfChanged(ref _displayTime, value);
        }

        /// <summary>
        ///     播放/暂停按钮的文本
        /// </summary>
        public string PlayPauseText
        {
            get => _playPauseText;
            set => this.RaiseAndSetIfChanged(ref _playPauseText, value);
        }

        /// <summary>
        ///     是否为倒计时模式
        /// </summary>
        public bool IsCountdownMode { get; }

        // 命令
        /// <summary>
        ///     播放/暂停命令
        /// </summary>
        public ICommand PlayPauseCommand { get; private set; } = null!;

        /// <summary>
        ///     停止命令
        /// </summary>
        public ICommand StopCommand { get; private set; } = null!;

        /// <summary>
        ///     返回主界面命令
        /// </summary>
        public ICommand ReturnCommand { get; private set; } = null!;

        /// <summary>
        ///     退出应用命令
        /// </summary>
        public ICommand ExitCommand { get; private set; } = null!;

        /// <summary>
        ///     释放资源方法
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void InitializeCommands()
        {
            // 命令直接使用主界面的命令，确保完全同步
            if (IsCountdownMode)
            {
                PlayPauseCommand = _mainViewModel.CountdownStartStopCommand;
                StopCommand = _mainViewModel.CountdownStopCommand;
            }
            else
            {
                PlayPauseCommand = _mainViewModel.TimerStartStopCommand;
                StopCommand = _mainViewModel.TimerStopCommand;
            }

            ReturnCommand = ReactiveCommand.Create(Return);
            ExitCommand = ReactiveCommand.Create(Exit);
        }

        // 事件
        /// <summary>
        ///     请求返回主界面的事件
        /// </summary>
        public event EventHandler? ReturnToMainRequested;

        /// <summary>
        ///     请求退出应用的事件
        /// </summary>
        public event EventHandler? ExitRequested;

        private void SyncWithMainViewModel(object? state)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                // 在UI线程中更新显示，避免跨线程操作
                Dispatcher.UIThread.Post(() =>
                {
                    if (_disposed)
                    {
                        return;
                    }

                    if (IsCountdownMode)
                    {
                        DisplayTime = _mainViewModel.CountdownDisplayTime;
                        PlayPauseText = _mainViewModel.CountdownStartButtonText;
                    }
                    else
                    {
                        DisplayTime = _mainViewModel.TimerDisplayTime;
                        PlayPauseText = _mainViewModel.TimerStartButtonText;
                    }
                });
            }
            catch (ObjectDisposedException)
            {
                // 忽略已释放对象的异常
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"同步失败: {ex.Message}");
            }
        }

        private void Return()
        {
            ReturnToMainRequested?.Invoke(this, EventArgs.Empty);
        }

        private void Exit()
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed || !disposing)
            {
                return;
            }

            _disposed = true;
            _syncTimer.Dispose();
        }
    }
}