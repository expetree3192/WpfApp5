using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using System.Windows.Data;
using System.ComponentModel;
using WpfApp5.Models;
using WpfApp5.Services;

namespace WpfApp5.Controls
{
    /// <summary>
    /// 🚀 極致性能DataGrid - 虛擬化版本
    /// 檔案位置: WpfApp5/Controls/UltraPerformanceComponents.cs
    /// </summary>
    public class UltraPerformanceDataGrid : DataGrid
    {
        private bool _isUpdating = false;
        private readonly DispatcherTimer _updateTimer; // 🔧 修正：移除 readonly 以允許在建構函式中初始化
        private readonly Queue<Action> _pendingUpdates = new(); // 🔧 修正：改為 readonly

        public UltraPerformanceDataGrid()
        {
            InitializePerformanceSettings();

            // 🔧 修正：在建構函式中初始化 _updateTimer
            _updateTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _updateTimer.Tick += ProcessPendingUpdates;
        }

        private void InitializePerformanceSettings()
        {
            // 🎯 啟用所有虛擬化功能
            VirtualizingPanel.SetIsVirtualizing(this, true);
            VirtualizingPanel.SetVirtualizationMode(this, VirtualizationMode.Recycling);
            VirtualizingPanel.SetIsContainerVirtualizable(this, true);
            VirtualizingPanel.SetScrollUnit(this, ScrollUnit.Item);

            // 🎯 性能優化設定
            EnableRowVirtualization = true;
            EnableColumnVirtualization = true;
            CanUserSortColumns = false; // 暫時禁用排序以提升性能

            // 🎯 滾動優化
            ScrollViewer.SetCanContentScroll(this, true);
            ScrollViewer.SetIsDeferredScrollingEnabled(this, true);
        }

        /// <summary>
        /// 🚀 超高速更新方法
        /// </summary>
        public async Task UpdateItemsUltraFastAsync<T>(IEnumerable<T> items, IProgress<int>? progress = null)
        {
            if (_isUpdating) return;

            _isUpdating = true;
            var startTime = DateTime.Now;

            try
            {
                var itemList = items.ToList();
                var totalCount = itemList.Count;

                // 🎯 策略選擇基於數據量
                if (totalCount <= 1000)
                {
                    await UpdateSmallDataAsync(itemList, progress);
                }
                else if (totalCount <= 5000)
                {
                    await UpdateMediumDataAsync(itemList, progress);
                }
                else
                {
                    await UpdateLargeDataAsync(itemList, progress);
                }

                var elapsed = DateTime.Now - startTime;
                System.Diagnostics.Debug.WriteLine($"🚀 DataGrid更新完成: {totalCount}項, 耗時: {elapsed.TotalMilliseconds:F1}ms");
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private async Task UpdateSmallDataAsync<T>(List<T> items, IProgress<int>? progress)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                ItemsSource = items;
                progress?.Report(items.Count);
            }, DispatcherPriority.Normal);
        }

        private async Task UpdateMediumDataAsync<T>(List<T> items, IProgress<int>? progress)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                // 先設定空集合啟用虛擬化
                ItemsSource = new List<T>();
            });

            // 等待UI穩定
            await Task.Delay(10);

            await Dispatcher.InvokeAsync(() =>
            {
                // 一次性設定完整數據
                ItemsSource = items;
                progress?.Report(items.Count);
            }, DispatcherPriority.Background);
        }

        private async Task UpdateLargeDataAsync<T>(List<T> items, IProgress<int>? progress)
        {
            // 🎯 大數據量使用特殊處理
            await Dispatcher.InvokeAsync(() =>
            {
                // 暫時禁用UI更新
                BeginInit();

                try
                {
                    ItemsSource = items;
                    progress?.Report(items.Count);
                }
                finally
                {
                    EndInit();
                }
            }, DispatcherPriority.Background);

            // 強制垃圾回收
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        private void ProcessPendingUpdates(object? sender, EventArgs e)
        {
            _updateTimer.Stop();

            while (_pendingUpdates.Count > 0 && _pendingUpdates.TryDequeue(out var update))
            {
                update.Invoke();
            }
        }

        // 🔧 修正：使用 Unloaded 事件而不是覆寫方法
        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            // 訂閱 Unloaded 事件
            this.Unloaded += UltraPerformanceDataGrid_Unloaded;
        }

        private void UltraPerformanceDataGrid_Unloaded(object sender, RoutedEventArgs e)
        {
            // 清理資源
            _updateTimer?.Stop();
            _pendingUpdates.Clear();

            // 取消訂閱事件
            this.Unloaded -= UltraPerformanceDataGrid_Unloaded;
        }
    }

    /// <summary>
    /// 🚀 高性能進度報告器
    /// </summary>
    public class HighPerformanceProgress(Action<int> handler, Dispatcher? dispatcher = null) : IProgress<int>
    {
        private readonly Action<int> _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        private readonly Dispatcher _dispatcher = dispatcher ?? Application.Current.Dispatcher;
        private int _lastReportedValue = -1;
        private DateTime _lastReportTime = DateTime.MinValue;

        public void Report(int value)
        {
            // 🎯 避免過於頻繁的進度更新
            var now = DateTime.Now;
            if (value == _lastReportedValue ||
                (now - _lastReportTime).TotalMilliseconds < 50)
            {
                return;
            }

            _lastReportedValue = value;
            _lastReportTime = now;

            _dispatcher.BeginInvoke(_handler, DispatcherPriority.Background, value);
        }
    }

    /// <summary>
    /// 🚀 增強型性能監控器
    /// </summary>
    public static class EnhancedPerformanceMonitor
    {
        private static readonly Dictionary<string, List<double>> _measurements = [];
        private static readonly object _lock = new();

        public static void Record(string operation, double milliseconds)
        {
            lock (_lock)
            {
                if (!_measurements.ContainsKey(operation))
                {
                    _measurements[operation] = [];
                }

                _measurements[operation].Add(milliseconds);

                // 只保留最近100次記錄
                if (_measurements[operation].Count > 100)
                {
                    _measurements[operation].RemoveAt(0);
                }
            }
        }

        public static string GetStatistics(string operation)
        {
            lock (_lock)
            {
                if (!_measurements.ContainsKey(operation) || _measurements[operation].Count == 0)
                {
                    return $"{operation}: 無數據";
                }

                var measurements = _measurements[operation];
                var avg = measurements.Average();
                var min = measurements.Min();
                var max = measurements.Max();
                var count = measurements.Count;

                return $"{operation}: 平均 {avg:F1}ms, 最小 {min:F1}ms, 最大 {max:F1}ms, 次數 {count}";
            }
        }

        public static string GetAllStatistics()
        {
            lock (_lock)
            {
                var stats = new System.Text.StringBuilder();
                stats.AppendLine("=== 增強型性能統計 ===");

                foreach (var operation in _measurements.Keys)
                {
                    stats.AppendLine(GetStatistics(operation));
                }

                return stats.ToString();
            }
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _measurements.Clear();
            }
        }
    }
}