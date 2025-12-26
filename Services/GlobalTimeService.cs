using System;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfApp5.Services
{
    // 全域時間服務 - 提供統一的時間管理和格式化
    public partial class GlobalTimeService : ObservableObject, IDisposable
    {
        #region 屬性

        [ObservableProperty]
        private DateTime _currentDateTime = DateTime.Now;

        // 僅顯示時間 (HH:mm:ss)
        public string TimeOnly => CurrentDateTime.ToString("HH:mm:ss");

        // 僅顯示日期 (MM/dd)
        public string DateOnly => CurrentDateTime.ToString("MM/dd");

        // 完整日期時間 (yyyy/MM/dd HH:mm:ss)
        public string FullDateTime => CurrentDateTime.ToString("yyyy/MM/dd HH:mm:ss");

        // 交易用時間格式 (HH:mm:ss.fff)
        public string TradingTime => CurrentDateTime.ToString("HH:mm:ss.fff");

        // 檔案名稱用時間格式 (yyyyMMdd_HHmmss)
        public string FileNameTime => CurrentDateTime.ToString("yyyyMMdd_HHmmss");

        #endregion

        #region 私有成員

        private DispatcherTimer? _timeTimer;
        private bool _isInitialized = false;
        private TimeSpan _updateInterval = TimeSpan.FromSeconds(1);
        private bool _lastTradingStatus = false;
        private bool _disposed = false;

        #endregion

        #region 事件

        // 時間更新事件
        public event Action<DateTime>? TimeUpdated;

        // 交易時間狀態變化事件
        public event Action<bool>? TradingTimeStatusChanged;

        #endregion

        #region 初始化

        // 初始化時間服務
        public void Initialize()
        {
            if (_isInitialized) return;

            InitializeTimer();
            _isInitialized = true;

            System.Diagnostics.Debug.WriteLine($"[GlobalTimeService] 已初始化 - 更新間隔: {_updateInterval.TotalMilliseconds}ms");
        }

        private void InitializeTimer()
        {
            _timeTimer = new DispatcherTimer
            {
                Interval = _updateInterval
            };

            _timeTimer.Tick += OnTimerTick;
            _timeTimer.Start();
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            var previousTime = CurrentDateTime;
            CurrentDateTime = DateTime.Now;

            // 通知所有格式化屬性更新
            OnPropertyChanged(nameof(TimeOnly));
            OnPropertyChanged(nameof(DateOnly));
            OnPropertyChanged(nameof(FullDateTime));
            OnPropertyChanged(nameof(TradingTime));
            OnPropertyChanged(nameof(FileNameTime));

            // 觸發時間更新事件
            TimeUpdated?.Invoke(CurrentDateTime);

            // 檢查交易時間狀態變化
            CheckTradingTimeStatus();
        }

        private void CheckTradingTimeStatus()
        {
            bool currentTradingStatus = IsTradingTime();

            if (_lastTradingStatus != currentTradingStatus)
            {
                _lastTradingStatus = currentTradingStatus;
                TradingTimeStatusChanged?.Invoke(currentTradingStatus);

                System.Diagnostics.Debug.WriteLine($"[GlobalTimeService] 交易時間狀態變化: {(currentTradingStatus ? "進入" : "離開")}交易時間 ({TimeOnly})");
            }
        }

        #endregion

        #region 配置方法

        // 設定更新頻率
        public void SetUpdateInterval(TimeSpan interval)
        {
            _updateInterval = interval;

            if (_timeTimer != null)
            {
                _timeTimer.Interval = interval;
                System.Diagnostics.Debug.WriteLine($"[GlobalTimeService] 更新間隔已變更為: {interval.TotalMilliseconds}ms");
            }
        }

        // 暫停時間更新
        public void Pause()
        {
            _timeTimer?.Stop();
            System.Diagnostics.Debug.WriteLine("[GlobalTimeService] 已暫停");
        }

        // 恢復時間更新
        public void Resume()
        {
            _timeTimer?.Start();
            System.Diagnostics.Debug.WriteLine("[GlobalTimeService] 已恢復");
        }

        #endregion

        #region 時間判斷方法

        // 判斷是否為交易時間 (台股交易時間：09:00-13:30)
        public bool IsTradingTime()
        {
            return IsWithinTimeRange("09:00:00", "13:30:00");
        }

        // 判斷是否在指定時間範圍內
        public bool IsWithinTimeRange(string startTime, string endTime)
        {
            if (!TimeSpan.TryParse(startTime, out var start) || !TimeSpan.TryParse(endTime, out var end))
                return false;

            var currentTime = CurrentDateTime.TimeOfDay;

            if (start <= end)
            {
                return currentTime >= start && currentTime <= end;
            }
            else
            {
                // 跨日情況
                return currentTime >= start || currentTime <= end;
            }
        }

        #endregion

        #region 時間運算方法

        // 判斷時間間隔是否超過指定秒數
        public bool IsTimeIntervalExceeded(DateTime startTime, double intervalSeconds)
        {
            return (CurrentDateTime - startTime).TotalSeconds >= intervalSeconds;
        }

        #endregion

        #region 格式化方法

        // 獲取自定義格式的時間字串
        public string GetFormattedTime(string format)
        {
            try
            {
                return CurrentDateTime.ToString(format);
            }
            catch
            {
                return CurrentDateTime.ToString("yyyy/MM/dd HH:mm:ss");
            }
        }

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 釋放託管資源
                    _timeTimer?.Stop();
                    _timeTimer = null;
                    _isInitialized = false;

                    System.Diagnostics.Debug.WriteLine("[GlobalTimeService] 已釋放資源");
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
