using Sinopac.Shioaji;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Text.RegularExpressions;
using System.Windows.Input;
using WpfApp5.Services;
using WpfApp5.ViewModels;


namespace WpfApp5.Views
{
    public partial class QuoteWindow : Window
    {
        private QuoteViewModel? _viewModel;
        private readonly LogService _logService;
        private readonly Dictionary<string, HashSet<QuoteType>> _subscribedContracts = [];
        private string _windowId = string.Empty;
        // 🔥 新增：防重複執行標記
        private bool _isSettingListView = false;
        private DateTime _lastSetListViewTime = DateTime.MinValue;

        // 使用 GeneratedRegex（.NET 7+ 推薦做法）
        [GeneratedRegex("[^0-9]+")]
        private static partial Regex NumberOnlyRegex();
        public string WindowId
        {
            get => _windowId;
            set
            {
                if (_windowId != value)
                {
                    _windowId = value;

                    // 創建 ViewModel 並設置 DataContext
                    if (_viewModel == null)
                    {
                        _viewModel = new QuoteViewModel(value);
                        DataContext = _viewModel;
                        _viewModel.WindowSizeToggleRequested += OnWindowSizeToggleRequested;    // 訂閱視窗尺寸切換事件
                        _viewModel.PropertyChanged += OnViewModelPropertyChanged;   // 🔥 新增：監聽 ViewModel 屬性變更
                        WindowManagerService.Instance.RegisterWindow(value, this);  // 註冊視窗

                        _logService.LogInfo($"視窗 ID 已設置: {value}", "QuoteWindow", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    }
                    else
                    {
                        // WindowId 是唯讀的，不需要再次設定，如果需要變更 WindowId，必須重新建立 ViewModel
                        _logService.LogWarning($"視窗 ID 已存在，無法變更 (當前: {_viewModel.WindowId}, 嘗試設定: {value})", "QuoteWindow", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    }
                }
            }
        }


        public QuoteWindow()
        {
            InitializeComponent();
            _logService = LogService.Instance ?? throw new InvalidOperationException("LogService.Instance 不能為 null");
            _logService.PropertyChanged += LogService_PropertyChanged;
            _logService.LogInfo($"報價視窗已開啟", "QuoteWindow", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
        }
        // 🔥 新增：監聽 ViewModel 屬性變更事件
        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            try
            {
                if (e.PropertyName == nameof(QuoteViewModel.OrderBookViewModel))
                {
                    // 🔥 使用 Background 優先級，減少重複觸發的可能性
                    Dispatcher.BeginInvoke(() =>
                    {
                        SetOrderBookListView();
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "處理 ViewModel 屬性變更失敗", "QuoteWindow");
            }
        }

        // 設置 OrderBookListView 引用的方法
        private void SetOrderBookListView()
        {
            try
            {
                // 🔥 防止短時間內重複執行（100ms 內視為重複）
                var now = DateTime.Now;
                if (_isSettingListView || (now - _lastSetListViewTime).TotalMilliseconds < 100)
                {
                    _logService.LogDebug($"SetOrderBookListView 跳過重複呼叫（間隔: {(now - _lastSetListViewTime).TotalMilliseconds:F1}ms）", "QuoteWindow");
                    return;
                }

                _isSettingListView = true;
                _lastSetListViewTime = now;

                if (_viewModel?.OrderBookViewModel != null)
                {
                    if (this.FindName("OrderBookListView") is ListView listView)
                    {
                        _viewModel.OrderBookViewModel.SetListView(listView);
                        _logService.LogDebug("已重新設置 OrderBookListView 引用", "QuoteWindow");
                    }
                    else
                    {
                        _logService.LogWarning("未找到 OrderBookListView", "QuoteWindow");
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "設置 OrderBookListView 失敗", "QuoteWindow");
            }
            finally
            {
                _isSettingListView = false;
            }
        }
        // 處理「委賣」欄位雙擊事件（執行賣出掛單）
        private void SellVolumeColumn_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // 檢查是否為雙擊
                if (e.ClickCount == 2 && sender is Border border && border.Tag is decimal price)
                {
                    var viewModel = DataContext as QuoteViewModel;
                    if (viewModel?.OrderBookViewModel != null)
                    {
                        viewModel.OrderBookViewModel.OnPriceRowDoubleClicked(price, "Sell");
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.LogError(ex, "[快速掛單] 處理賣出雙擊失敗", "QuoteWindow");
            }
        }
        // 數字驗證 - 使用 GeneratedRegex
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            e.Handled = NumberOnlyRegex().IsMatch(e.Text);
        }

        // 快捷設定數量按鈕
        private void SetQuantity_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string quantityStr)
            {
                if (int.TryParse(quantityStr, out int quantity))
                {
                    if (DataContext is QuoteViewModel viewModel)
                    {
                        viewModel.OrderQuantity = quantity;
                    }
                }
            }
        }
        // 處理視窗尺寸切換事件
        private void OnWindowSizeToggleRequested(object? sender, bool isExpanded)
        {
            try
            {
                // 在 UI 線程上執行尺寸變更
                Dispatcher.Invoke(() =>
                {
                    if (isExpanded)
                    {
                        this.Width = 1200;  // 展開視窗：寬度變為 1200
                    }
                    else
                    {
                        this.Width = 518;   // 收起視窗：寬度變為 520
                    }
                });
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "變更視窗尺寸時發生錯誤", "QuoteWindow", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
        }
        private void OrderBookListView_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            try
            {
                // 檢查是否啟用置中模式或視圖鎖定
                if (_viewModel?.OrderBookViewModel?.IsCentered == true ||
                    _viewModel?.OrderBookViewModel?.IsViewLocked == true)
                {
                    // 置中模式或鎖定模式：禁用滾輪
                    e.Handled = true;
                    _logService.LogDebug("置中/鎖定模式已啟用，滾輪事件已禁用", "QuoteWindow");
                    return;
                }

                //  _logService.LogDebug($"滾輪滾動: {e.Delta}", "QuoteWindow");    // 非置中模式：允許滾輪滾動（不處理事件，讓 ScrollViewer 處理）
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "處理滾輪事件失敗", "QuoteWindow");
            }
        }
        // LogService 屬性變更事件處理
        private void LogService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 當 QuoteLogsText 更新時，自動滾動到底部
            if (e.PropertyName == nameof(LogService.QuoteLogsText))
            {
                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    // 更新 TextBox 內容
                    DebugTextBox.Text = _logService.QuoteLogsText;
                    // 滾動到底部
                    DebugScrollViewer.ScrollToEnd();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        // 合約查詢按鈕的點擊事件
        private void OpenContractSearchWindow_Click(object sender, RoutedEventArgs e)
        {
            // 使用 LogService
            _logService.LogInfo("執行 OpenContractSearchWindow", "QuoteWindow", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

            try
            {
                var contractSearchWindow = new ContractSearchWindow();  // 建立並顯示合約查詢視窗
                contractSearchWindow.Show();    // 使用非模態視窗
                _logService.LogInfo("合約查詢視窗已成功開啟", "QuoteWindow", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "開啟合約查詢視窗失敗", "QuoteWindow", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                MessageBox.Show($"開啟合約查詢視窗時發生錯誤：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 複製系統訊息按鈕點擊事件
        private void CopyDebugMessages_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 複製 LogService 的 QuoteLogsText
                var logContent = _logService.QuoteLogsText;
                if (!string.IsNullOrEmpty(logContent))
                {
                    Clipboard.SetText(logContent);

                    // 使用 LogService 記錄操作
                    _logService.LogInfo("系統訊息已複製到剪貼簿", "QuoteWindow", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                }
                else
                {
                    _logService.LogWarning("沒有訊息可複製", "QuoteWindow", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "複製系統訊息失敗", "QuoteWindow", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
        }

        // 清除系統訊息按鈕點擊事件
        private void ClearDebugMessages_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 🔧 修正：清除 LogService 中的 QuoteWindow 日誌
                _logService.ClearLogs("QuoteWindow");
                _logService.LogInfo("QuoteWindow 系統訊息已清除", "QuoteWindow", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "清除系統訊息失敗", "QuoteWindow", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
        }

        // 快捷選擇按鈕事件處理
        private void QuickSelectButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tagData)
            {
                try
                {
                    // 檢查 _viewModel 是否為 null
                    if (_viewModel == null)
                    {
                        _logService.LogError("無法執行快捷選擇：ViewModel 尚未初始化", "QuoteWindow", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                        return;
                    }

                    // 解析 Tag 資料：格式為 "ProductType|Exchange|Symbol|AccountType"
                    var parts = tagData.Split('|');
                    if (parts.Length >= 3)  // 至少要有 3 個部分，第 4 個是可選的帳戶類型提示
                    {
                        string productTypeName = parts[0];
                        string exchange = parts[1];
                        string symbol = parts[2];

                        _viewModel.SelectedProductType = productTypeName;
                        _viewModel.SelectedExchange = exchange;
                        _viewModel.SubscribeSymbol = symbol;
                        // 根據商品類型自動選擇帳戶
                        _viewModel.SelectAccountByProductType(productTypeName);

                        // 🔧 使用 LogService 記錄操作
                        string quickSelectInfo = $"快速選擇: {button.Content} → {productTypeName}|{exchange}|{symbol}";
                        _logService.LogInfo(quickSelectInfo, "QuoteWindow", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    }
                    else
                    {
                        string errorMsg = $"快捷按鈕標籤格式錯誤: {tagData}";
                        _logService.LogError(errorMsg, "QuoteWindow", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogError(ex, "快捷選擇操作失敗", "QuoteWindow", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                }
            }
        }
        #region 智能下單事件處理

        // 雙擊檢測相關變數
        private DateTime _lastClickTime = DateTime.MinValue;
        private decimal _lastClickPrice = 0;
        private int _lastClickColumn = -1;
        private const int DOUBLE_CLICK_THRESHOLD_MS = 500; // 雙擊間隔閾值（毫秒）

        // 滑鼠左鍵按下事件處理
        private async void PriceRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is Border border && border.DataContext is PriceRowViewModel priceRow)
                {
                    var price = priceRow.Price;
                    var columnIndex = GetColumnIndex(border);
                    var currentTime = DateTime.Now;

                    _logService.LogDebug($"[事件處理] 左鍵點擊: 價格={price}, 欄位={columnIndex}, 點擊次數={e.ClickCount}", "QuoteWindow");

                    // 檢查是否為雙擊（同一價格、同一欄位、時間間隔小於閾值）
                    bool isDoubleClick = false;
                    if (_lastClickPrice == price &&
                        _lastClickColumn == columnIndex &&
                        (currentTime - _lastClickTime).TotalMilliseconds <= DOUBLE_CLICK_THRESHOLD_MS)
                    {
                        isDoubleClick = true;
                        _logService.LogDebug($"[事件處理] 檢測到雙擊: 價格={price}, 欄位={columnIndex}", "QuoteWindow");
                    }

                    // 更新最後點擊記錄
                    _lastClickTime = currentTime;
                    _lastClickPrice = price;
                    _lastClickColumn = columnIndex;

                    if (isDoubleClick)
                    {
                        // 處理雙擊事件
                        await HandleLeftDoubleClick(price, columnIndex);
                    }
                    else
                    {
                        // 處理單擊事件（僅價位欄位）
                        if (columnIndex == 2)
                        {
                            HandleLeftSingleClick(price, columnIndex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[事件處理] 處理左鍵點擊失敗", "QuoteWindow");
            }
        }

        // 滑鼠右鍵按下事件處理
        private async void PriceRow_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is Border border && border.DataContext is PriceRowViewModel priceRow)
                {
                    var price = priceRow.Price;
                    var columnIndex = GetColumnIndex(border);

                    _logService.LogDebug($"[事件處理] 右鍵點擊: 價格={price}, 欄位={columnIndex}", "QuoteWindow");

                    await HandleRightClick(price, columnIndex);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[事件處理] 處理右鍵點擊失敗", "QuoteWindow");
            }
        }

        // 處理左鍵雙擊事件
        private async Task HandleLeftDoubleClick(decimal price, int columnIndex)
        {
            try
            {
                // 🔧 提前檢查 _viewModel 是否為 null，提升效率
                if (_viewModel == null)
                {
                    _logService.LogWarning("[智能操作] ViewModel 未初始化，無法執行操作", "QuoteWindow");
                    return;
                }

                _logService.LogInfo($"[智能操作] 🖱️ 左鍵雙擊: 價格={price}, 欄位={columnIndex}", "QuoteWindow");

                // 🚀 使用 switch expression 提升效率，並避免重複的 await 呼叫 - 7個欄位版本
                var operationTask = columnIndex switch
                {
                    0 => ExecuteDeleteBuyOperation(price, columnIndex),      // 刪買
                    1 => ExecuteBuyOperation(price, columnIndex),            // 委買
                    2 => Task.CompletedTask,                                 // 價格%欄位不執行操作
                    3 => Task.CompletedTask,                                 // 價位欄位不執行操作
                    4 => Task.CompletedTask,                                 // 量欄位不執行操作
                    5 => ExecuteSellOperation(price, columnIndex),           // 委賣
                    6 => ExecuteDeleteSellOperation(price, columnIndex),     // 刪賣
                    _ => LogUnknownColumn(columnIndex)
                };

                await operationTask;
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[智能操作] 處理左鍵雙擊失敗", "QuoteWindow");
            }
        }

        // 處理左鍵單擊事件（修正 null 參考警告）
        private void HandleLeftSingleClick(decimal price, int columnIndex)
        {
            try
            {
                if (columnIndex == 3) // 欄位 3（價位）: 單擊 → 設定 PriceTextBox 價格
                {
                    if (_viewModel != null)
                    {
                        _logService.LogInfo($"[智能操作] 🖱️ 左鍵單擊價位: 設定價格={price}", "QuoteWindow");
                        _viewModel.SetPriceTextBox(price);
                    }
                    else
                    {
                        _logService.LogWarning("[智能操作] ViewModel 未初始化，無法設定價格", "QuoteWindow");
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[智能操作] 處理左鍵單擊失敗", "QuoteWindow");
            }
        }

        // 處理右鍵點擊事件（修正 null 參考警告）
        private async Task HandleRightClick(decimal price, int columnIndex)
        {
            try
            {
                // 🔧 提前檢查 _viewModel 是否為 null
                if (_viewModel == null)
                {
                    _logService.LogWarning("[智能操作] ViewModel 未初始化，無法執行操作", "QuoteWindow");
                    return;
                }

                _logService.LogInfo($"[智能操作] 🖱️ 右鍵點擊: 價格={price}, 欄位={columnIndex}", "QuoteWindow");

                // 🚀 使用 switch expression 提升效率 - 7個欄位版本
                var operationTask = columnIndex switch
                {
                    0 or 1 => ExecuteRightClickDeleteBuy(price, columnIndex),    // 刪買、委買
                    2 => Task.CompletedTask,                                     // 價格%欄位不執行操作
                    3 => ExecuteRightClickDeleteAll(price, columnIndex),         // 刪除所有（價位欄位）
                    4 => Task.CompletedTask,                                     // 量欄位不執行操作
                    5 or 6 => ExecuteRightClickDeleteSell(price, columnIndex),   // 委賣、刪賣
                    _ => LogUnknownColumn(columnIndex)
                };

                await operationTask;
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[智能操作] 處理右鍵點擊失敗", "QuoteWindow");
            }
        }

        // 執行買進操作
        private async Task ExecuteBuyOperation(decimal price, int columnIndex)
        {
            _logService.LogInfo($"[智能操作] 📈 執行買進操作: 價格={price}", "QuoteWindow");
            await _viewModel!.ExecuteSmartOrderAsync(price, "Buy", columnIndex);
        }

        // 執行賣出操作
        private async Task ExecuteSellOperation(decimal price, int columnIndex)
        {
            _logService.LogInfo($"[智能操作] 📉 執行賣出操作: 價格={price}", "QuoteWindow");
            await _viewModel!.ExecuteSmartOrderAsync(price, "Sell", columnIndex);
        }

        // 執行刪買操作
        private async Task ExecuteDeleteBuyOperation(decimal price, int columnIndex)
        {
            _logService.LogInfo($"[智能操作] 🗑️ 執行刪買操作: 價格={price}", "QuoteWindow");
            await _viewModel!.ExecuteSmartCancelAsync(price, "Buy", columnIndex);
        }

        // 執行刪賣操作
        private async Task ExecuteDeleteSellOperation(decimal price, int columnIndex)
        {
            _logService.LogInfo($"[智能操作] 🗑️ 執行刪賣操作: 價格={price}", "QuoteWindow");
            await _viewModel!.ExecuteSmartCancelAsync(price, "Sell", columnIndex);
        }

        // 右鍵刪買操作
        private async Task ExecuteRightClickDeleteBuy(decimal price, int columnIndex)
        {
            _logService.LogInfo($"[智能操作] 🗑️ 右鍵刪買: 價格={price}", "QuoteWindow");
            await _viewModel!.ExecuteSmartCancelAsync(price, "Buy", columnIndex);
        }

        // 右鍵刪除所有操作
        private async Task ExecuteRightClickDeleteAll(decimal price, int columnIndex)
        {
            _logService.LogInfo($"[智能操作] 🗑️ 右鍵刪除所有: 價格={price}", "QuoteWindow");
            await _viewModel!.ExecuteSmartCancelAsync(price, "All", columnIndex);
        }

        // 右鍵刪賣操作
        private async Task ExecuteRightClickDeleteSell(decimal price, int columnIndex)
        {
            _logService.LogInfo($"[智能操作] 🗑️ 右鍵刪賣: 價格={price}", "QuoteWindow");
            await _viewModel!.ExecuteSmartCancelAsync(price, "Sell", columnIndex);
        }

        // 記錄未知欄位
        private Task LogUnknownColumn(int columnIndex)
        {
            _logService.LogWarning($"[智能操作] ⚠️ 未知的欄位索引: {columnIndex}", "QuoteWindow");
            return Task.CompletedTask;
        }

        // 獲取欄位索引的輔助方法（優化版本）
        private int GetColumnIndex(Border border)
        {
            try
            {
                return border.Parent is Grid grid ? Grid.GetColumn(border) : -1;
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[輔助方法] 獲取欄位索引失敗", "QuoteWindow");
                return -1;
            }
        }

        #endregion

        // 公開方法：供外部呼叫以重新整理帳戶資料
        public void RefreshAccountData()
        {
            try
            {
                _viewModel?.RefreshAccounts();
                _logService.LogInfo("帳戶資料已重新整理", "QuoteWindow", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "重新整理帳戶資料失敗", "QuoteWindow", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
        }
        // 訂閱合約的特定報價類型
        public void SubscribeToContract(string contractCode, QuoteType quoteType)
        {
            // 如果合約不存在於訂閱列表，添加它
            if (!_subscribedContracts.TryGetValue(contractCode, out HashSet<QuoteType>? value))
            {
                value = [];
                _subscribedContracts[contractCode] = value;
            }

            value.Add(quoteType);

            // 在 WindowManagerService 中註冊訂閱
            WindowManagerService.Instance.SubscribeToContract(WindowId, contractCode, quoteType);

            _logService.LogInfo($"視窗 {WindowId} 已訂閱合約 {contractCode} 的 {quoteType} 報價", "QuoteWindow", LogDisplayTarget.SourceWindow);
        }

        // 取消合約的特定報價類型訂閱
        public void UnsubscribeFromContract(string contractCode, QuoteType quoteType)
        {
            if (_subscribedContracts.TryGetValue(contractCode, out var quoteTypes) &&
                quoteTypes.Contains(quoteType))
            {
                // 從集合中移除報價類型
                quoteTypes.Remove(quoteType);

                // 如果合約沒有任何報價類型的訂閱，移除整個合約
                if (quoteTypes.Count == 0)
                {
                    _subscribedContracts.Remove(contractCode);
                }

                // 在 WindowManagerService 中取消訂閱
                WindowManagerService.Instance.UnsubscribeFromContract(WindowId, contractCode, quoteType);

                _logService.LogInfo($"視窗 {WindowId} 已取消訂閱合約 {contractCode} 的 {quoteType} 報價", "QuoteWindow", LogDisplayTarget.SourceWindow);
            }
        }

        // 取消所有訂閱
        public void UnsubscribeFromAllContracts()
        {
            // 複製合約列表，避免在迭代過程中修改集合
            var contractsToUnsubscribe = new List<(string contractCode, QuoteType quoteType)>();

            foreach (var contractEntry in _subscribedContracts)
            {
                foreach (var quoteType in contractEntry.Value)
                {
                    contractsToUnsubscribe.Add((contractEntry.Key, quoteType));
                }
            }

            // 取消所有訂閱
            foreach (var (contractCode, quoteType) in contractsToUnsubscribe)
            {
                UnsubscribeFromContract(contractCode, quoteType);
            }

            // 清空訂閱列表
            _subscribedContracts.Clear();

            _logService.LogInfo($"視窗 {WindowId} 已取消所有訂閱", "QuoteWindow", LogDisplayTarget.SourceWindow);
        }
        // 視窗關閉事件處理
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                _logService.PropertyChanged -= LogService_PropertyChanged;

                // 🔥 新增：取消 ViewModel 事件訂閱
                if (_viewModel != null)
                {
                    _viewModel.WindowSizeToggleRequested -= OnWindowSizeToggleRequested;
                    _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                }

                UnsubscribeFromAllContracts();
                WindowManagerService.Instance.ClearWindowSubscriptions(WindowId);
                _logService.LogInfo($"報價視窗正在關閉: {this.Title}", "QuoteWindow", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
            catch (Exception ex)
            {
                _logService?.LogError(ex, "關閉報價視窗時發生錯誤", "QuoteWindow", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
        }

        // 視窗載入完成事件
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _logService.LogInfo($"報價視窗載入完成: {this.Title}", "QuoteWindow", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                // 初始化時載入現有的日誌內容
                DebugTextBox.Text = _logService.QuoteLogsText;

                // 🔧 使用新的方法設置 ListView 引用
                SetOrderBookListView();
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "報價視窗載入時發生錯誤", "QuoteWindow", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
        }

    }
}
