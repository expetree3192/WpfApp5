using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfApp5.Models;
using WpfApp5.Services;
using WpfApp5.Services.Common;

namespace WpfApp5.ViewModels
{
    /// <summary>
    /// 🚀 簡化版高性能 ObservableCollection - 專注核心功能
    /// </summary>
    public class HighPerformanceObservableCollection<T>(LogService? logService = null) : ObservableCollection<T>
    {
        private bool _suppressNotification = false;
        private readonly LogService? _logService = logService;
        private const string LOG_SOURCE = "ContractSearch";

        // 最佳化的 ReplaceAll - 針對小到中等數據量
        public void ReplaceAll(IEnumerable<T> items)
        {
            if (items == null) return;

            var itemList = items.ToList();
            var startTime = DateTime.Now;

            _suppressNotification = true;
            try
            {
                Items.Clear();
                foreach (var item in itemList)
                {
                    Items.Add(item);
                }
            }
            finally
            {
                _suppressNotification = false;
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }

            var elapsed = DateTime.Now - startTime;
            _logService?.LogDebug($"🚀 ReplaceAll 完成，{itemList.Count} 項，耗時: {elapsed.TotalMilliseconds:F1} ms",
                LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
        }

        // 🚀 批次添加 - 僅用於大數據量
        public void AddRangeBatched(IEnumerable<T> items, int batchSize = 1000)
        {
            if (items == null) return;

            var itemList = items.ToList();
            if (itemList.Count == 0) return;

            var startTime = DateTime.Now;
            _logService?.LogInfo($"🚀 [批次添加] 開始處理 {itemList.Count} 個項目，批次大小: {batchSize}",
                LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

            _suppressNotification = true;
            try
            {
                Items.Clear();

                // 分批處理
                for (int i = 0; i < itemList.Count; i += batchSize)
                {
                    var batch = itemList.Skip(i).Take(batchSize);
                    foreach (var item in batch)
                    {
                        Items.Add(item);
                    }

                    // 每批之間稍微讓出控制權
                    if (i + batchSize < itemList.Count)
                    {
                        Thread.Yield();
                    }
                }
            }
            finally
            {
                _suppressNotification = false;
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }

            var elapsed = DateTime.Now - startTime;
            _logService?.LogInfo($"🚀 [批次添加] 完成，耗時: {elapsed.TotalMilliseconds:F1} ms",
                LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!_suppressNotification)
            {
                base.OnCollectionChanged(e);
            }
        }
    }

    // 🔍 合約搜尋視窗 ViewModel
    public partial class ContractSearchViewModel : ObservableObject, IDisposable
    {
        #region Private Fields

        private readonly ContractQueryService _queryService = new();
        private readonly ProductTypeValidator _validator = new();
        private readonly LogService? _logService = LogService.Instance;
        private const string LOG_SOURCE = "ContractSearch";
        private bool _disposed = false;

        #endregion

        #region Observable Properties (使用 MVVM Toolkit)

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SearchResultCount))]
        [NotifyPropertyChangedFor(nameof(HasSearchResults))]
        private HighPerformanceObservableCollection<ContractSearchResult> _searchResults =
            new(LogService.Instance);

        [ObservableProperty]
        private ObservableCollection<string> _availableExchanges = [];

        [ObservableProperty]
        private string _selectedProductType = "Futures";

        [ObservableProperty]
        private string _selectedExchange = "";

        [ObservableProperty]
        private string _subscribeSymbol = "";

        [ObservableProperty]
        private string _searchKeyword = "";

        [ObservableProperty]
        private bool _isSearching = false;

        [ObservableProperty]
        private bool _showAllMonths = true;

        [ObservableProperty]
        private bool _showR1R2 = true;

        [ObservableProperty]
        private bool _isStockFuturesMode = false;

        [ObservableProperty]
        private bool _isETFFuturesMode = false;

        #endregion

        #region Computed Properties

        public int SearchResultCount => SearchResults.Count;
        public bool HasSearchResults => SearchResults.Count > 0;
        public string LogMessages => _logService?.ContractLogsText ?? string.Empty;

        #endregion

        #region Partial Methods - Property Changed Handlers

        partial void OnSelectedProductTypeChanged(string value)
        {
            UpdateAvailableExchanges();
        }

        partial void OnSearchResultsChanged(HighPerformanceObservableCollection<ContractSearchResult> value)
        {
            // 監聽集合變更
            if (value != null)
            {
                value.CollectionChanged += (s, e) =>
                {
                    OnPropertyChanged(nameof(SearchResultCount));
                    OnPropertyChanged(nameof(HasSearchResults));
                };
            }
        }

        #endregion

        #region Constructor

        public ContractSearchViewModel()
        {
            // 初始化可用交易所
            InitializeAvailableExchanges();

            // 訂閱 LogService 的屬性變更事件
            if (_logService != null)
            {
                _logService.PropertyChanged += OnLogServicePropertyChanged;
                _logService.LogInfo("🔍 合約搜尋系統已初始化 (MVVM Toolkit 版)", LOG_SOURCE,
                    LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
        }

        #endregion

        #region Commands (使用 MVVM Toolkit)

        // 🔍 核心搜尋命令
        [RelayCommand(CanExecute = nameof(CanExecuteSearch))]
        private async Task SearchAsync()
        {
            var methodStartTime = DateTime.Now;
            _logService?.LogInfo($"🔧 SearchAsync 開始", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

            try
            {
                IsSearching = true;
                SearchResults.Clear();

                _logService?.LogInfo($"🔍 執行搜尋條件 - 商品類型: {SelectedProductType}, 交易所: {SelectedExchange}, " +
                    $"代號: {SubscribeSymbol}, 關鍵字: {SearchKeyword}", LOG_SOURCE,
                    LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                // 階段1: 查詢合約資訊
                var stage1StartTime = DateTime.Now;
                var contractInfos = await _queryService.QueryUniversalContractsWithApiDiscovery(
                    SelectedProductType, SelectedExchange, SubscribeSymbol);

                var stage1Elapsed = DateTime.Now - stage1StartTime;
                _logService?.LogInfo($"📊 階段1完成: 查詢到 {contractInfos.Count} 個合約，" +
                    $"耗時: {stage1Elapsed.TotalMilliseconds:F1} ms", LOG_SOURCE,
                    LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                // 🚨 動態調整處理上限
                var processingLimit = contractInfos.Count > 90000 ? 90000 :
                    (contractInfos.Count > 10000 ? 10000 : 1000);

                if (contractInfos.Count > processingLimit)
                {
                    _logService?.LogWarning($"⚠️ 查詢結果包含 {contractInfos.Count} 個合約，" +
                        $"超過處理上限 ({processingLimit})", LOG_SOURCE,
                        LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show(
                            $"查詢結果包含 {contractInfos.Count} 個合約，超過處理上限 ({processingLimit})！\n\n" +
                            $"建議使用更具體的搜尋條件來縮小範圍。",
                            "數據量過大",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                    });
                    return;
                }

                // 階段2: 關鍵字篩選
                if (!string.IsNullOrEmpty(SearchKeyword))
                {
                    var stage2StartTime = DateTime.Now;
                    var originalCount = contractInfos.Count;
                    contractInfos = [.. contractInfos.Where(info =>
                    (info.Name?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) == true) ||
                    info.Symbol.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase))];

                    var stage2Elapsed = DateTime.Now - stage2StartTime;
                    _logService?.LogInfo($"📊 階段2完成: 篩選 {originalCount} -> {contractInfos.Count}，" +
                        $"耗時: {stage2Elapsed.TotalMilliseconds:F1} ms", LOG_SOURCE,
                        LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                }

                // 階段3: 轉換處理
                var stage3StartTime = DateTime.Now;
                var results = new List<ContractSearchResult>(contractInfos.Count);
                var successCount = 0;

                // 🚀 使用並行處理加速轉換 (針對大數據量)
                if (contractInfos.Count > 1000)
                {
                    var parallelResults = contractInfos.AsParallel()
                        .WithDegreeOfParallelism(Environment.ProcessorCount)
                        .Select(contractInfo =>
                        {
                            try
                            {
                                return new ContractSearchResult(contractInfo);
                            }
                            catch
                            {
                                return null;
                            }
                        })
                        .Where(result => result != null)
                        .ToList();

                    results.AddRange(parallelResults!);
                    successCount = parallelResults.Count;
                }
                else
                {
                    // 小數據量使用順序處理
                    foreach (var contractInfo in contractInfos)
                    {
                        try
                        {
                            results.Add(new ContractSearchResult(contractInfo));
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            _logService?.LogError(ex, $"❌ 建立搜尋結果失敗: {contractInfo?.Symbol}",
                                LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                        }
                    }
                }

                var stage3Elapsed = DateTime.Now - stage3StartTime;
                _logService?.LogInfo($"📊 階段3完成: 轉換 {successCount} 個結果，" +
                    $"耗時: {stage3Elapsed.TotalMilliseconds:F1} ms", LOG_SOURCE,
                    LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                // 階段4: 排序
                var stage4StartTime = DateTime.Now;
                var sortedResults = results.OrderByDescending(r => r.Symbol).ToList();
                var stage4Elapsed = DateTime.Now - stage4StartTime;
                _logService?.LogInfo($"📊 階段4完成: 排序完成，" +
                    $"耗時: {stage4Elapsed.TotalMilliseconds:F1} ms", LOG_SOURCE,
                    LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                // 階段5: UI更新
                if (sortedResults.Count > 0)
                {
                    var stage5StartTime = DateTime.Now;
                    await UpdateSearchResultsAsync(sortedResults);
                    var stage5Elapsed = DateTime.Now - stage5StartTime;
                    _logService?.LogInfo($"📊 階段5完成: UI更新完成，" +
                        $"耗時: {stage5Elapsed.TotalMilliseconds:F1} ms", LOG_SOURCE,
                        LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                }

                var totalElapsed = DateTime.Now - methodStartTime;
                _logService?.LogInfo($"✅ 搜尋完成，找到 {SearchResults.Count} 個合約，" +
                    $"總耗時: {totalElapsed.TotalMilliseconds:F1} ms", LOG_SOURCE,
                    LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
            catch (Exception ex)
            {
                _logService?.LogError(ex, "❌ 搜尋失敗", LOG_SOURCE,
                    LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"搜尋失敗: {ex.Message}", "錯誤",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                IsSearching = false;
                var finalElapsed = DateTime.Now - methodStartTime;
                _logService?.LogInfo($"🔧 SearchAsync 總耗時: {finalElapsed.TotalMilliseconds:F1} ms",
                    LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
        }

        private bool CanExecuteSearch()
        {
            return !IsSearching && ShioajiService.Instance.IsLoggedIn;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 🚀 簡化版UI更新方法
        /// </summary>
        private async Task UpdateSearchResultsAsync(List<ContractSearchResult> results)
        {
            if (results == null || results.Count == 0) return;

            var startTime = DateTime.Now;
            var dataCount = results.Count;

            _logService?.LogInfo($"🚀 開始UI更新 {dataCount} 個結果", LOG_SOURCE,
                LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (dataCount <= 2000)
                    {
                        // 2000以下直接使用 ReplaceAll (最快)
                        SearchResults.ReplaceAll(results);
                        _logService?.LogInfo($"🚀 使用 ReplaceAll 直接更新", LOG_SOURCE,
                            LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    }
                    else
                    {
                        // 超過2000才使用批次處理
                        SearchResults.AddRangeBatched(results, 1000);
                        _logService?.LogInfo($"🚀 使用批次更新", LOG_SOURCE,
                            LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    }
                }, DispatcherPriority.Normal);

                var elapsed = DateTime.Now - startTime;
                _logService?.LogInfo($"🚀 UI更新完成，耗時: {elapsed.TotalMilliseconds:F1} ms", LOG_SOURCE,
                    LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
            catch (Exception ex)
            {
                _logService?.LogError(ex, "UI更新失敗", LOG_SOURCE,
                    LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                throw;
            }
        }

        public void ClearResults()
        {
            SearchResults.Clear();
            _logService?.LogInfo("🧹 已清除搜尋結果", LOG_SOURCE,
                LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
        }

        public List<ContractSearchResult> GetSelectedContracts()
        {
            return [.. SearchResults.Where(r => r.IsSelected)];
        }

        public void SelectAllContracts()
        {
            foreach (var result in SearchResults)
            {
                result.IsSelected = true;
            }
            _logService?.LogInfo($"✅ 已全選 {SearchResults.Count} 個合約", LOG_SOURCE,
                LogDisplayTarget.SourceWindow);
        }

        public void ClearSelection()
        {
            foreach (var result in SearchResults)
            {
                result.IsSelected = false;
            }
            _logService?.LogInfo("❌ 已清除所有選擇", LOG_SOURCE, LogDisplayTarget.SourceWindow);
        }

        public int GetSelectedCount()
        {
            return SearchResults.Count(r => r.IsSelected);
        }

        public string ExportSearchResults()
        {
            var export = new System.Text.StringBuilder();
            export.AppendLine($"=== 合約搜尋結果匯出 ===");
            export.AppendLine($"匯出時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            export.AppendLine($"商品類型: {SelectedProductType}");
            export.AppendLine($"交易所: {SelectedExchange}");
            export.AppendLine($"商品代號: {(string.IsNullOrEmpty(SubscribeSymbol) ? "無" : SubscribeSymbol)}");
            export.AppendLine($"搜尋關鍵字: {(string.IsNullOrEmpty(SearchKeyword) ? "無" : SearchKeyword)}");
            export.AppendLine($"結果數量: {SearchResults.Count}");
            export.AppendLine($"選中數量: {GetSelectedCount()}");
            export.AppendLine("");

            foreach (var result in SearchResults)
            {
                export.AppendLine($"--- {result.DisplayName} ---");
                export.AppendLine($"商品代號: {result.Symbol}");
                export.AppendLine($"商品名稱: {result.Name}");
                export.AppendLine($"交易所: {result.Exchange}");
                export.AppendLine($"類別: {result.Category}");
                export.AppendLine($"證券類型: {result.SecurityType}");

                if (result.LimitUp.HasValue && result.LimitDown.HasValue)
                {
                    export.AppendLine($"漲停: {result.LimitUp:F2} / 跌停: {result.LimitDown:F2}");
                }

                if (result.Reference.HasValue)
                {
                    export.AppendLine($"參考價: {result.Reference:F2}");
                }

                if (result.IsContinuousContract)
                {
                    export.AppendLine($"連續合約: 是 (實際代碼: {result.ActualContractCode})");
                }

                if (result.ProductType == "Options")
                {
                    if (result.StrikePrice.HasValue)
                        export.AppendLine($"履約價: {result.StrikePrice:F2}");
                    if (!string.IsNullOrEmpty(result.OptionRight))
                        export.AppendLine($"權利類型: {result.OptionRight}");
                    if (!string.IsNullOrEmpty(result.DeliveryMonth))
                        export.AppendLine($"到期月份: {result.DeliveryMonth}");
                }

                export.AppendLine($"選中狀態: {(result.IsSelected ? "是" : "否")}");
                export.AppendLine($"更新日期: {result.UpdateDate}");
                export.AppendLine("");
            }

            _logService?.LogInfo("📄 搜尋結果已匯出", LOG_SOURCE, LogDisplayTarget.SourceWindow);
            return export.ToString();
        }

        #endregion

        #region Private Methods

        private void InitializeAvailableExchanges()
        {
            UpdateAvailableExchanges();
        }

        private void UpdateAvailableExchanges()
        {
            AvailableExchanges.Clear();

            switch (SelectedProductType)
            {
                case "Futures":
                    AvailableExchanges.Add("CDF");
                    AvailableExchanges.Add("TXF");
                    break;
                case "Options":
                    AvailableExchanges.Add("TXO");
                    AvailableExchanges.Add("CDO");
                    break;
                case "Stocks":
                    AvailableExchanges.Add("TSE");
                    AvailableExchanges.Add("OTC");
                    AvailableExchanges.Add("OES");
                    break;
                case "Indexs":
                    AvailableExchanges.Add("TSE");
                    AvailableExchanges.Add("OTC");
                    AvailableExchanges.Add("TAIFEX");
                    break;
                default:
                    AvailableExchanges.Add("TSE");
                    AvailableExchanges.Add("OTC");
                    AvailableExchanges.Add("OES");
                    AvailableExchanges.Add("TXF");
                    break;
            }

            if (!string.IsNullOrEmpty(SelectedExchange) && !AvailableExchanges.Contains(SelectedExchange))
            {
                SelectedExchange = "";
            }

            _logService?.LogDebug($"📊 已更新交易所清單：{string.Join(", ", AvailableExchanges)}",
                LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
        }

        private void OnLogServicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ContractLogsText" || e.PropertyName == LOG_SOURCE)
            {
                OnPropertyChanged(nameof(LogMessages));
            }
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_logService != null)
                    {
                        _logService.PropertyChanged -= OnLogServicePropertyChanged;
                        _logService.LogInfo("🚀 ContractSearchViewModel 已釋放資源", LOG_SOURCE,
                            LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    }

                    // 清理搜尋結果以釋放記憶體
                    SearchResults?.Clear();
                }

                _disposed = true;
            }
        }

        ~ContractSearchViewModel()
        {
            Dispose(false);
        }

        #endregion
    }
}
