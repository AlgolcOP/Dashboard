using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using ReactiveUI;
using Timer.Models;
using Timer.Services;

namespace Timer.ViewModels
{
    /// <summary>
    /// 主窗口视图模型类 - 负责管理计时器和倒计时器的业务逻辑
    /// 实现了IDisposable接口以确保资源正确释放
    /// </summary>
    public sealed class MainWindowViewModel : ViewModelBase, IDisposable
    {
        // 静态计数器 - 线程安全
        /// <summary>
        /// 计时器记录的计数器 - 用于生成唯一的计时器名称
        /// </summary>
        private static int _timerCounter = 1;
        /// <summary>
        /// 倒计时器记录的计数器 - 用于生成唯一的倒计时器名称
        /// </summary>
        private static int _countdownCounter = 1;
        /// <summary>
        /// 倒计时器的线程同步信号量 - 确保倒计时操作的线程安全
        /// </summary>
        private readonly SemaphoreSlim _countdownSemaphore = new(1, 1);
        /// <summary>
        /// 倒计时器的定时器对象 - 用于定期更新倒计时显示
        /// </summary>
        private readonly System.Threading.Timer _countdownTimer;
        /// <summary>
        /// 历史记录服务 - 负责保存和管理计时记录
        /// </summary>
        private readonly TimerHistoryService _historyService;
        /// <summary>
        /// 计时器的定时器对象 - 用于定期更新计时显示
        /// </summary>
        private readonly System.Threading.Timer _timer;
        /// <summary>
        /// 计时器的线程同步信号量 - 确保计时操作的线程安全
        /// </summary>
        private readonly SemaphoreSlim _timerSemaphore = new(1, 1);
        /// <summary>
        /// 倒计时器的显示模式 - 控制时间显示格式（时分秒/分秒/秒）
        /// </summary>
        private TimerMode _countdownDisplayMode = TimerMode.HhMmSs;
        /// <summary>
        /// 倒计时器显示的时间文本
        /// </summary>
        private string _countdownDisplayTime = "00:00:30";
        /// <summary>
        /// 倒计时器已经过的时间
        /// </summary>
        private TimeSpan _countdownElapsedTime;

        // 倒计时设置
        /// <summary>
        /// 倒计时设置中的小时文本输入
        /// </summary>
        private string _countdownHoursText = "0";
        /// <summary>
        /// 倒计时设置中的分钟文本输入
        /// </summary>
        private string _countdownMinutesText = "0";
        /// <summary>
        /// 倒计时设置中的秒数文本输入 - 默认30秒
        /// </summary>
        private string _countdownSecondsText = "30";
        /// <summary>
        /// 倒计时器开始按钮的显示文本
        /// </summary>
        private string _countdownStartButtonText = "开始";

        // 倒计时器相关变量
        /// <summary>
        /// 倒计时器开始的时间点
        /// </summary>
        private DateTime _countdownStartTime;
        /// <summary>
        /// 倒计时器的剩余时间
        /// </summary>
        private TimeSpan _countdownTime;
        /// <summary>
        /// 对象是否已被释放的标志
        /// </summary>
        private bool _disposed;
        /// <summary>
        /// 倒计时器是否正在运行的标志
        /// </summary>
        private bool _isCountdownRunning;
        /// <summary>
        /// 计时器是否正在运行的标志
        /// </summary>
        private bool _isTimerRunning;
        /// <summary>
        /// 倒计时器的原始设定时间 - 用于重置
        /// </summary>
        private TimeSpan _originalCountdownTime;
        /// <summary>
        /// 计时器的显示模式 - 控制时间显示格式（时分秒/分秒/秒）
        /// </summary>
        private TimerMode _timerDisplayMode = TimerMode.HhMmSs;
        /// <summary>
        /// 计时器显示的时间文本
        /// </summary>
        private string _timerDisplayTime = "00:00:00";
        /// <summary>
        /// 计时器已经过的时间
        /// </summary>
        private TimeSpan _timerElapsedTime;
        /// <summary>
        /// 计时器开始按钮的显示文本
        /// </summary>
        private string _timerStartButtonText = "开始";

        // 计时器相关变量
        /// <summary>
        /// 计时器开始的时间点
        /// </summary>
        private DateTime _timerStartTime;

        /// <summary>
        /// 构造函数 - 初始化所有组件和默认值
        /// </summary>
        public MainWindowViewModel()
        {
            // 初始化历史记录服务
            _historyService = new TimerHistoryService();
            // 创建计时器定时器，刷新频率50ms以获得更流畅的显示
            _timer = new System.Threading.Timer(OnTimerTick, null, Timeout.Infinite, 50); // 提高刷新频率到50ms
            // 创建倒计时器定时器，刷新频率50ms
            _countdownTimer = new System.Threading.Timer(OnCountdownTick, null, Timeout.Infinite, 50);

            // 设置默认倒计时时间为30秒
            _originalCountdownTime = TimeSpan.FromSeconds(30);
            _countdownTime = _originalCountdownTime;

            // 初始化历史记录集合
            TimerHistory = new ObservableCollection<TimerRecord>();

            // 初始化所有命令
            InitializeCommands();
            // 更新倒计时显示
            UpdateCountdownDisplay();
            // 异步加载历史记录
            LoadHistoryAsync();
        }

        // 计时器属性
        /// <summary>
        /// 计时器显示的时间文本属性 - 绑定到UI显示
        /// </summary>
        public string TimerDisplayTime
        {
            get => _timerDisplayTime;
            set => this.RaiseAndSetIfChanged(ref _timerDisplayTime, value);
        }

        /// <summary>
        /// 计时器开始按钮的文本属性 - 动态显示"开始"/"暂停"/"继续"
        /// </summary>
        public string TimerStartButtonText
        {
            get => _timerStartButtonText;
            set => this.RaiseAndSetIfChanged(ref _timerStartButtonText, value);
        }

        /// <summary>
        /// 计时器显示模式属性 - 控制时间格式显示
        /// </summary>
        public TimerMode TimerDisplayMode
        {
            get => _timerDisplayMode;
            set
            {
                this.RaiseAndSetIfChanged(ref _timerDisplayMode, value);
                UpdateTimerDisplayFormat();
            }
        }

        /// <summary>
        /// 计时器显示模式索引 - 用于UI绑定下拉框选择
        /// </summary>
        public int TimerDisplayModeIndex
        {
            get => (int)_timerDisplayMode;
            set
            {
                var newMode = (TimerMode)value;
                if (_timerDisplayMode == newMode)
                {
                    return;
                }

                _timerDisplayMode = newMode;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(TimerDisplayMode));
                UpdateTimerDisplayFormat();
            }
        }

        // 倒计时器属性
        /// <summary>
        /// 倒计时器显示的时间文本属性 - 绑定到UI显示
        /// </summary>
        public string CountdownDisplayTime
        {
            get => _countdownDisplayTime;
            set => this.RaiseAndSetIfChanged(ref _countdownDisplayTime, value);
        }

        /// <summary>
        /// 倒计时器开始按钮的文本属性 - 动态显示"开始"/"暂停"/"继续"
        /// </summary>
        public string CountdownStartButtonText
        {
            get => _countdownStartButtonText;
            set => this.RaiseAndSetIfChanged(ref _countdownStartButtonText, value);
        }

        /// <summary>
        /// 倒计时器显示模式属性 - 控制时间格式显示
        /// </summary>
        public TimerMode CountdownDisplayMode
        {
            get => _countdownDisplayMode;
            set
            {
                this.RaiseAndSetIfChanged(ref _countdownDisplayMode, value);
                UpdateCountdownDisplayFormat();
            }
        }

        /// <summary>
        /// 倒计时器显示模式索引 - 用于UI绑定下拉框选择
        /// </summary>
        public int CountdownDisplayModeIndex
        {
            get => (int)_countdownDisplayMode;
            set
            {
                var newMode = (TimerMode)value;
                if (_countdownDisplayMode == newMode)
                {
                    return;
                }

                _countdownDisplayMode = newMode;
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(CountdownDisplayMode));
                UpdateCountdownDisplayFormat();
            }
        }

        // 可见性属性
        /// <summary>
        /// 是否显示倒计时小时设置 - 只有在时分秒模式下才显示
        /// </summary>
        public bool ShowCountdownHours => _countdownDisplayMode == TimerMode.HhMmSs;

        /// <summary>
        /// 是否显示倒计时分钟设置 - 在时分秒和分秒模式下显示
        /// </summary>
        public bool ShowCountdownMinutes =>
            _countdownDisplayMode == TimerMode.HhMmSs || _countdownDisplayMode == TimerMode.MmSs;

        // 倒计时设置属性
        /// <summary>
        /// 倒计时小时设置的文本输入����
        /// </summary>
        public string CountdownHoursText
        {
            get => _countdownHoursText;
            set => this.RaiseAndSetIfChanged(ref _countdownHoursText, value);
        }

        /// <summary>
        /// 倒计时分钟设置的文本输入属性
        /// </summary>
        public string CountdownMinutesText
        {
            get => _countdownMinutesText;
            set => this.RaiseAndSetIfChanged(ref _countdownMinutesText, value);
        }

        /// <summary>
        /// 倒计时秒数设置的文本输入属性
        /// </summary>
        public string CountdownSecondsText
        {
            get => _countdownSecondsText;
            set => this.RaiseAndSetIfChanged(ref _countdownSecondsText, value);
        }

        /// <summary>
        /// 计时历史记录集合 - 绑定到UI列表显示
        /// </summary>
        public ObservableCollection<TimerRecord> TimerHistory { get; }

        // 命令
        /// <summary>
        /// 计时器开始/停止命令
        /// </summary>
        public ICommand TimerStartStopCommand { get; private set; } = null!;
        /// <summary>
        /// 计时器停止命令
        /// </summary>
        public ICommand TimerStopCommand { get; private set; } = null!;
        /// <summary>
        /// 计时器进入迷你模式命令
        /// </summary>
        public ICommand TimerMiniModeCommand { get; private set; } = null!;

        /// <summary>
        /// 倒计时器开始/停止命令
        /// </summary>
        public ICommand CountdownStartStopCommand { get; private set; } = null!;
        /// <summary>
        /// 倒计时器停止命令
        /// </summary>
        public ICommand CountdownStopCommand { get; private set; } = null!;
        /// <summary>
        /// 倒计时器进入迷你模式命令
        /// </summary>
        public ICommand CountdownMiniModeCommand { get; private set; } = null!;
        /// <summary>
        /// 设置倒计时时间命令
        /// </summary>
        public ICommand SetCountdownCommand { get; private set; } = null!;

        /// <summary>
        /// 清空历史记录命令
        /// </summary>
        public ICommand ClearHistoryCommand { get; private set; } = null!;
        /// <summary>
        /// 删除单条记录命令
        /// </summary>
        public ICommand DeleteRecordCommand { get; private set; } = null!;

        /// <summary>
        /// 实现IDisposable接口 - 释放所有资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 初始化所有命令 - 绑定命令到对应的方法
        /// </summary>
        private void InitializeCommands()
        {
            // 计时器命令
            TimerStartStopCommand = ReactiveCommand.CreateFromTask(TimerStartStopAsync);
            TimerStopCommand = ReactiveCommand.CreateFromTask(TimerStopAsync);
            TimerMiniModeCommand = ReactiveCommand.Create(EnterTimerMiniMode);

            // 倒计时器命令
            CountdownStartStopCommand = ReactiveCommand.CreateFromTask(CountdownStartStopAsync);
            CountdownStopCommand = ReactiveCommand.CreateFromTask(CountdownStopAsync);
            CountdownMiniModeCommand = ReactiveCommand.Create(EnterCountdownMiniMode);
            SetCountdownCommand = ReactiveCommand.Create(SetCountdown);

            // 历史记录命令
            ClearHistoryCommand = ReactiveCommand.CreateFromTask(ClearHistoryAsync);
            DeleteRecordCommand = ReactiveCommand.CreateFromTask<TimerRecord>(DeleteRecordAsync);
        }

        // 事件
        /// <summary>
        /// 计时器请求进入迷你模式事件
        /// </summary>
        public event EventHandler? TimerMiniModeRequested;
        /// <summary>
        /// 倒计时器请求进入迷你模式事件
        /// </summary>
        public event EventHandler? CountdownMiniModeRequested;

        // 公共方法，用于迷你模式同步
        /// <summary>
        /// 获取当前倒计时剩余时间 - 供迷你模式使用
        /// </summary>
        public TimeSpan GetCurrentCountdownTime() => _countdownTime;
        /// <summary>
        /// 获取计时器运行状态 - 供迷你模式使用
        /// </summary>
        public bool GetIsTimerRunning() => _isTimerRunning;
        /// <summary>
        /// 获取倒计时器运行状态 - 供迷你模式使用
        /// </summary>
        public bool GetIsCountdownRunning() => _isCountdownRunning;
        /// <summary>
        /// 获取计时器已过时间 - 供迷你模式使用
        /// </summary>
        public TimeSpan GetTimerElapsedTime() => _timerElapsedTime;
        /// <summary>
        /// 获取倒计时器已过时间 - 供迷你模式使用
        /// </summary>
        public TimeSpan GetCountdownElapsedTime() => _countdownElapsedTime;
        /// <summary>
        /// 获取计时器开始时间 - 供迷你模式使用
        /// </summary>
        public DateTime GetTimerStartTime() => _timerStartTime;
        /// <summary>
        /// 获取倒计时器开始时间 - 供迷你模式使用
        /// </summary>
        public DateTime GetCountdownStartTime() => _countdownStartTime;

        // 计时器方法 - 添加异步支持和线程安全
        /// <summary>
        /// 计时器开始/停止的异步方法 - 使用信号量确保线程安全
        /// </summary>
        private async Task TimerStartStopAsync()
        {
            await _timerSemaphore.WaitAsync();
            try
            {
                if (!_isTimerRunning)
                {
                    await TimerStartAsync();
                }
                else
                {
                    TimerPause();
                }
            }
            finally
            {
                _timerSemaphore.Release();
            }
        }

        /// <summary>
        /// 启动计时器的异步方法
        /// </summary>
        private async Task TimerStartAsync()
        {
            _isTimerRunning = true;
            // 计算开始时间，考虑已经过的时间
            _timerStartTime = DateTime.Now - _timerElapsedTime;
            // 启动定时器，每50毫秒更新一次
            _timer.Change(0, 50);
            TimerStartButtonText = "暂停";
            await Task.CompletedTask;
        }

        /// <summary>
        /// 暂停计时器方法
        /// </summary>
        private void TimerPause()
        {
            _isTimerRunning = false;
            // 停止定时器
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            TimerStartButtonText = "继续";
        }

        /// <summary>
        /// 停止计时器的异步方法 - 保存记录并重置
        /// </summary>
        private async Task TimerStopAsync()
        {
            await _timerSemaphore.WaitAsync();
            try
            {
                _isTimerRunning = false;
                _timer.Change(Timeout.Infinite, Timeout.Infinite);

                // 保存计时记录
                if (_timerElapsedTime > TimeSpan.Zero)
                {
                    var record = new TimerRecord
                    {
                        Name = $"计时{Interlocked.Increment(ref _timerCounter)}",
                        IsCountdown = false,
                        StartTime = _timerStartTime,
                        EndTime = DateTime.Now,
                        Duration = _timerElapsedTime,
                        CountdownTime = null
                    };

                    await _historyService.SaveRecordAsync(record);

                    // 在UI线程中更新集合
                    Dispatcher.UIThread.Post(() => { TimerHistory.Insert(0, record); });
                }

                // 重置计时器
                _timerElapsedTime = TimeSpan.Zero;
                TimerStartButtonText = "开始";
                UpdateTimerDisplay();
            }
            finally
            {
                _timerSemaphore.Release();
            }
        }

        /// <summary>
        /// 进入计时器迷你模式方法
        /// </summary>
        private void EnterTimerMiniMode()
        {
            TimerMiniModeRequested?.Invoke(this, EventArgs.Empty);
        }

        // 倒计时器方法 - 添加异步支持和线程安全
        /// <summary>
        /// 倒计时器开始/停止的异步方法 - 使用信号量确保线程安全
        /// </summary>
        private async Task CountdownStartStopAsync()
        {
            await _countdownSemaphore.WaitAsync();
            try
            {
                if (!_isCountdownRunning)
                {
                    await CountdownStartAsync();
                }
                else
                {
                    CountdownPause();
                }
            }
            finally
            {
                _countdownSemaphore.Release();
            }
        }

        /// <summary>
        /// 启动倒计时器的异步方法
        /// </summary>
        private async Task CountdownStartAsync()
        {
            _isCountdownRunning = true;
            // 计算开始时间，考虑已经过的时间
            _countdownStartTime = DateTime.Now - _countdownElapsedTime;

            // 如果是第一次启动，重置剩余时间
            if (_countdownElapsedTime == TimeSpan.Zero)
            {
                _countdownTime = _originalCountdownTime;
            }

            // 启动定时器，每50毫秒更新一次
            _countdownTimer.Change(0, 50);
            CountdownStartButtonText = "暂停";
            await Task.CompletedTask;
        }

        /// <summary>
        /// 暂停倒计时器方法
        /// </summary>
        private void CountdownPause()
        {
            _isCountdownRunning = false;
            // 停止定时器
            _countdownTimer.Change(Timeout.Infinite, Timeout.Infinite);
            CountdownStartButtonText = "继续";
        }

        /// <summary>
        /// 停止倒计时器的异步方法 - 保存记录并重置
        /// </summary>
        private async Task CountdownStopAsync()
        {
            await _countdownSemaphore.WaitAsync();
            try
            {
                _isCountdownRunning = false;
                _countdownTimer.Change(Timeout.Infinite, Timeout.Infinite);

                // 保存倒计时记录
                if (_countdownElapsedTime > TimeSpan.Zero)
                {
                    var record = new TimerRecord
                    {
                        Name = $"倒计时{Interlocked.Increment(ref _countdownCounter)}",
                        IsCountdown = true,
                        StartTime = _countdownStartTime,
                        EndTime = DateTime.Now,
                        Duration = _countdownElapsedTime,
                        CountdownTime = _originalCountdownTime
                    };

                    await _historyService.SaveRecordAsync(record);

                    // 在UI线程中更新集合
                    Dispatcher.UIThread.Post(() => { TimerHistory.Insert(0, record); });
                }

                // 重置倒计时器
                _countdownElapsedTime = TimeSpan.Zero;
                _countdownTime = _originalCountdownTime;
                CountdownStartButtonText = "开始";
                UpdateCountdownDisplay();
            }
            finally
            {
                _countdownSemaphore.Release();
            }
        }

        /// <summary>
        /// 进入倒计时器迷你模式方法
        /// </summary>
        private void EnterCountdownMiniMode()
        {
            CountdownMiniModeRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 设置倒计时时间方法 - 解析用户输入并更新倒计时时间
        /// </summary>
        private void SetCountdown()
        {
            // 尝试解析用户输入的时间值
            if (!int.TryParse(CountdownHoursText, out var hours) ||
                !int.TryParse(CountdownMinutesText, out var minutes) ||
                !int.TryParse(CountdownSecondsText, out var seconds))
            {
                return;
            }

            // 限制时间值在合理范围内
            hours = Math.Max(0, Math.Min(23, hours));
            minutes = Math.Max(0, Math.Min(59, minutes));
            seconds = Math.Max(0, Math.Min(59, seconds));

            // 计算总秒数并更新倒计时时间
            var totalSeconds = hours * 3600 + minutes * 60 + seconds;
            _originalCountdownTime = TimeSpan.FromSeconds(totalSeconds);
            _countdownTime = _originalCountdownTime;
            UpdateCountdownDisplay();
        }

        /// <summary>
        /// 计时器定时器回调方法 - 每50毫秒执行一次
        /// </summary>
        private void OnTimerTick(object? state)
        {
            // 检查计时器是否正在运行且对象未被释放
            if (!_isTimerRunning || _disposed)
            {
                return;
            }

            try
            {
                var now = DateTime.Now;
                // 计算已经过的时间
                _timerElapsedTime = now - _timerStartTime;

                // 在UI线程中更新显示
                Dispatcher.UIThread.Post(UpdateTimerDisplay);
            }
            catch (ObjectDisposedException)
            {
                // 忽略已释放对象的异常
            }
        }

        /// <summary>
        /// 倒计时器定时器回调方法 - 每50毫秒执行一次
        /// </summary>
        private void OnCountdownTick(object? state)
        {
            // 检查倒计时��是否正在运行且对象未被释放
            if (!_isCountdownRunning || _disposed)
            {
                return;
            }

            try
            {
                var now = DateTime.Now;
                // 计算已经过的时间
                _countdownElapsedTime = now - _countdownStartTime;
                // 计算剩余时间
                _countdownTime = _originalCountdownTime - _countdownElapsedTime;

                // 检查倒计时是否结束
                if (_countdownTime <= TimeSpan.Zero)
                {
                    _countdownTime = TimeSpan.Zero;
                    // 使用异步方式停止倒计时
                    Task.Run(CountdownStopAsync);
                    return;
                }

                // 在UI线程中更新显示
                Dispatcher.UIThread.Post(UpdateCountdownDisplay);
            }
            catch (ObjectDisposedException)
            {
                // 忽略已释放对象的异常
            }
        }

        /// <summary>
        /// 更新计时器显示方法
        /// </summary>
        private void UpdateTimerDisplay()
        {
            if (_disposed)
            {
                return;
            }

            TimerDisplayTime = FormatTime(_timerElapsedTime, _timerDisplayMode);
        }

        /// <summary>
        /// 更新倒计时器显示方法
        /// </summary>
        private void UpdateCountdownDisplay()
        {
            if (_disposed)
            {
                return;
            }

            CountdownDisplayTime = FormatTime(_countdownTime, _countdownDisplayMode);
        }

        /// <summary>
        /// 格式化时间显示的静态方法 - 根据模式返回不同格式的时间字符串
        /// </summary>
        /// <param name="time">要格式化的时间</param>
        /// <param name="mode">显示模式</param>
        /// <returns>格式化后的时间字符串</returns>
        private static string FormatTime(TimeSpan time, TimerMode mode)
        {
            return mode switch
            {
                TimerMode.HhMmSs => $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}",
                TimerMode.MmSs => $"{(int)time.TotalMinutes:D2}:{time.Seconds:D2}",
                TimerMode.Ss => $"{(int)time.TotalSeconds:D2}",
                _ => $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}"
            };
        }

        /// <summary>
        /// 更新计时器显示格式方法
        /// </summary>
        private void UpdateTimerDisplayFormat()
        {
            UpdateTimerDisplay();
        }

        /// <summary>
        /// 更新倒计时器显示格式方法 - 同时更新相关的可见性属性
        /// </summary>
        private void UpdateCountdownDisplayFormat()
        {
            UpdateCountdownDisplay();

            // 触发可见性属性通知
            this.RaisePropertyChanged(nameof(ShowCountdownHours));
            this.RaisePropertyChanged(nameof(ShowCountdownMinutes));

            switch (CountdownDisplayMode)
            {
                // 重置倒计时设置显示
                case TimerMode.Ss:
                    // 只显示秒数时，隐藏小时和分钟
                    CountdownHoursText = "0";
                    CountdownMinutesText = "0";
                    break;
                case TimerMode.MmSs:
                    // 显示分秒时，隐藏小时
                    CountdownHoursText = "0";
                    break;
                case TimerMode.HhMmSs:
                    // 显示时分秒时，所有都可见
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// 异步加载历史记录方法
        /// </summary>
        private async void LoadHistoryAsync()
        {
            try
            {
                // 从服务中获取历史记录
                var history = await _historyService.GetHistoryAsync();

                // 在UI线程中更新集合
                Dispatcher.UIThread.Post(() =>
                {
                    TimerHistory.Clear();
                    foreach (var record in history)
                    {
                        TimerHistory.Add(record);
                    }
                });
            }
            catch (Exception ex)
            {
                // 记录错误但不中断应用程序
                Debug.WriteLine($"加载历史记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清空历史记录的异步方法
        /// </summary>
        private async Task ClearHistoryAsync()
        {
            try
            {
                await _historyService.ClearHistoryAsync();
                TimerHistory.Clear();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"清空历史记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 删除单条记录的异步方法
        /// </summary>
        /// <param name="record">要删除的记录</param>
        private async Task DeleteRecordAsync(TimerRecord record)
        {
            try
            {
                await _historyService.DeleteRecordAsync(record.Id);
                TimerHistory.Remove(record);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"删除记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 资源释放的内部方法
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源</param>
        private void Dispose(bool disposing)
        {
            if (_disposed || !disposing)
            {
                return;
            }

            _disposed = true;

            // 释放所有定时器和信号量资源
            _timer.Dispose();
            _countdownTimer.Dispose();
            _timerSemaphore.Dispose();
            _countdownSemaphore.Dispose();
        }
    }

    /// <summary>
    /// 计时器显示模式枚举
    /// </summary>
    public enum TimerMode
    {
        /// <summary>
        /// 时分秒格式 (HH:MM:SS)
        /// </summary>
        HhMmSs,
        /// <summary>
        /// 分秒格式 (MM:SS)
        /// </summary>
        MmSs,
        /// <summary>
        /// ���数格式 (SS)
        /// </summary>
        Ss
    }
}

