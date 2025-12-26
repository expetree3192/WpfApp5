using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfApp5.Models;
using WpfApp5.ViewModels;
using WpfApp5.Services;

namespace WpfApp5.Views
{
    /// <summary>
    /// 🔍 商品檔查詢視窗 - 整合 LogService 版本 (移除進度顯示功能)
    /// </summary>
    public partial class ContractSearchWindow : Window
    {
        private readonly ContractSearchViewModel _viewModel;
        private readonly LogService _logService;
        private const string LOG_SOURCE = "ContractSearch";
        public string WindowId { get; set; } = string.Empty;    // 新增屬性以便識別視窗
        /// <summary>
        /// 合約選擇事件
        /// </summary>
        public event EventHandler<ContractSelectedEventArgs>? ContractSelected;

        public ContractSearchWindow()
        {
            InitializeComponent();
            _logService = LogService.Instance;  // 初始化 LogService
            _viewModel = new ContractSearchViewModel();
            DataContext = _viewModel;

            InitializeWindow();
        }

        /// <summary>
        /// 🔧 簡化版初始化視窗方法
        /// </summary>
        private void InitializeWindow()
        {
            // 使用 SelectedItem 而不是 SelectedValue
            foreach (ComboBoxItem item in ProductTypeComboBox.Items)
            {
                if (item.Tag?.ToString() == "Futures")
                {
                    ProductTypeComboBox.SelectedItem = item;
                    break;
                }
            }

            ResultDataGrid.ItemsSource = _viewModel.SearchResults;      // 綁定結果到 DataGrid
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;    // 訂閱 ViewModel 事件

            // (延遲執行)，設定個股期貨為預設顯示
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateUIForProductType("Futures");
            }), System.Windows.Threading.DispatcherPriority.Loaded);

            // 簡化版初始化完成訊息
            _logService.LogInfo("ContractSearchWindow 初始化完成 - UI元件已綁定，預設商品類型：Futures",
                LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
        }

        #region 商品類型選擇事件

        private void ProductType_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string productType)
            {
                // 更新 ComboBox 選擇
                foreach (ComboBoxItem item in ProductTypeComboBox.Items)
                {
                    if (item.Tag?.ToString() == productType)
                    {
                        ProductTypeComboBox.SelectedItem = item;
                        break;
                    }
                }

                UpdateUIForProductType(productType);

                // 更新按鈕樣式
                ResetCategoryButtonStyles();
                button.Background = System.Windows.Media.Brushes.Orange;

                // 重置特殊模式
                _viewModel.IsStockFuturesMode = false;
                _viewModel.IsETFFuturesMode = false;

                _logService.LogDebug($"商品類型已變更為: {productType}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
        }

        private void StockFutures_Click(object sender, RoutedEventArgs e)
        {
            // 設定為期貨類型
            foreach (ComboBoxItem item in ProductTypeComboBox.Items)
            {
                if (item.Tag?.ToString() == "Futures")
                {
                    ProductTypeComboBox.SelectedItem = item;
                    break;
                }
            }

            // 設定交易所為 TXF（如果用戶沒有輸入的話）
            if (string.IsNullOrEmpty(_viewModel.SelectedExchange))
            {
                _viewModel.SelectedExchange = "TXF";
            }

            UpdateUIForProductType("Futures");

            // 設定個股期貨模式
            _viewModel.IsStockFuturesMode = true;
            _viewModel.IsETFFuturesMode = false;

            _logService.LogInfo("已切換到個股期貨模式", LOG_SOURCE, LogDisplayTarget.SourceWindow);
        }

        private void ProductTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;

            if (ProductTypeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string productType)
            {
                UpdateUIForProductType(productType);

                // 重置特殊模式 (除非是透過特殊按鈕觸發)
                if (!_viewModel.IsStockFuturesMode && !_viewModel.IsETFFuturesMode)
                {
                    ResetCategoryButtonStyles();
                }

                _logService.LogDebug($"ComboBox 商品類型已變更為: {productType}", LOG_SOURCE, LogDisplayTarget.SourceWindow);
            }
        }

        private void UpdateUIForProductType(string productType)
        {
            if (StockFuturesLabel == null || StockFuturesPanel == null)
                return;

            // 根據商品類型調整UI顯示
            bool isFutures = productType == "Futures";
            StockFuturesLabel.Visibility = isFutures ? Visibility.Visible : Visibility.Collapsed;
            StockFuturesPanel.Visibility = isFutures ? Visibility.Visible : Visibility.Collapsed;

            // 更新交易所選項
            UpdateExchangeOptions(productType);

            // 更新 ViewModel
            if (_viewModel != null)
            {
                _viewModel.SelectedProductType = productType;
            }
        }

        private void UpdateExchangeOptions(string productType)
        {
            // 這個方法現在主要用於觸發 ViewModel 的更新
            if (_viewModel != null)
            {
                _viewModel.SelectedProductType = productType;
            }
        }

        private void ResetCategoryButtonStyles()
        {
            var defaultBrush = System.Windows.Media.Brushes.CornflowerBlue;
            StocksButton.Background = defaultBrush;
            FuturesButton.Background = defaultBrush; //System.Windows.Media.Brushes.Orange;
            OptionsButton.Background = defaultBrush;
            IndexsButton.Background = defaultBrush;
        }

        #endregion

        #region 查詢相關事件

        /// <summary>
        /// 🔧 簡化版查詢商品檔按鈕功能
        /// </summary>
        private async void QueryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 取得查詢條件
                var productType = (ProductTypeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
                var exchange = ExchangeComboBox.Text?.Trim() ?? "";
                var symbol = SymbolTextBox.Text.Trim() ?? "";
                var keyword = KeywordTextBox.Text.Trim() ?? "";

                // 設定查詢參數
                _viewModel.SelectedProductType = productType;
                _viewModel.SelectedExchange = exchange;
                _viewModel.SubscribeSymbol = symbol;
                _viewModel.SearchKeyword = keyword;

                _logService.LogInfo($"開始執行查詢 - 類型:{productType}, 交易所:{exchange}, 代號:{symbol}, 關鍵字:{keyword}",
                    LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                // 🎯 UI 狀態更新
                QueryButton.IsEnabled = false;
                QueryButton.Content = "🔍 查詢中...";

                // ✅ 使用 MVVM Toolkit 自動生成的 SearchCommand
                if (_viewModel.SearchCommand.CanExecute(null))
                {
                    await _viewModel.SearchCommand.ExecuteAsync(null);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "查詢按鈕處理失敗", LOG_SOURCE,
                    LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                MessageBox.Show($"查詢失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 🎯 恢復 UI 狀態
                QueryButton.IsEnabled = true;
                QueryButton.Content = "🔍 查詢商品檔";
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            SymbolTextBox.Clear();
            KeywordTextBox.Clear();
            _viewModel.ClearResults();
            _logService.LogInfo("已清除搜尋條件和結果", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
        }

        private void QuickSymbol_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string symbol)
            {
                SymbolTextBox.Text = symbol;
                _logService.LogInfo($"使用快速符號: {symbol}", LOG_SOURCE, LogDisplayTarget.SourceWindow);

                // 自動執行查詢
                QueryButton_Click(sender, e);
            }
        }

        #endregion

        #region 日誌相關事件處理

        private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
        {
            _logService.ClearLogs("ContractSearch");
            _logService.LogInfo("使用者手動清除日誌", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
        }

        private void ScrollToBottomButton_Click(object sender, RoutedEventArgs e)
        {
            // 🔧 修正：滾動 TextBox 到底部
            LogTextBox.ScrollToEnd();
            _logService.LogDebug("手動滾動日誌到底部", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
        }

        // 複製日誌功能
        private void CopyLogsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var logContent = LogTextBox.Text;
                if (!string.IsNullOrEmpty(logContent))
                {
                    Clipboard.SetText(logContent);
                    _logService.LogInfo("日誌內容已複製到剪貼簿", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                    // 顯示成功訊息
                    MessageBox.Show("日誌內容已複製到剪貼簿！", "複製成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    _logService.LogWarning("沒有日誌內容可複製", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    MessageBox.Show("目前沒有日誌內容可複製", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "複製日誌失敗", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                MessageBox.Show($"複製日誌失敗: {ex.Message}", "錯誤",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportLogsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "匯出 ContractSearch 日誌",
                    Filter = "文字檔案 (*.txt)|*.txt|所有檔案 (*.*)|*.*",
                    DefaultExt = "txt",
                    FileName = $"ContractSearch_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    _logService.ExportLogs(saveFileDialog.FileName, "ContractSearch");
                    MessageBox.Show($"日誌已成功匯出到:\n{saveFileDialog.FileName}", "匯出成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "匯出日誌失敗", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                MessageBox.Show($"匯出日誌失敗: {ex.Message}", "錯誤",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 🆕 簡化版清空查詢結果按鈕事件
        /// </summary>
        private void ClearAllSearchResultsButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("確定要清空所有查詢結果嗎？\n此操作無法復原。",
                "確認清空", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // 最簡單的實現方式
                _viewModel.SearchResults.Clear();
                _logService.LogInfo("使用者清空所有查詢結果", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
        }

        #endregion

        #region 結果處理事件

        private void ResultDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSelectionCount();
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var result in _viewModel.SearchResults)
            {
                result.IsSelected = true;
            }
            ResultDataGrid.Items.Refresh();
            UpdateSelectionCount();
            _logService.LogInfo($"已全選 {_viewModel.SearchResults.Count} 個合約", LOG_SOURCE, LogDisplayTarget.SourceWindow);
        }

        private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var result in _viewModel.SearchResults)
            {
                result.IsSelected = false;
            }
            ResultDataGrid.Items.Refresh();
            UpdateSelectionCount();
            _logService.LogInfo("已清除所有選擇", LOG_SOURCE, LogDisplayTarget.SourceWindow);
        }

        private void ViewDetailsButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = _viewModel.SearchResults.Where(r => r.IsSelected).ToList();
            // 使用 Count > 0 而不是 Any()
            if (selectedItems.Count == 0)
            {
                _logService.LogWarning("請先選擇要查看的商品", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                MessageBox.Show("請先選擇要查看的商品", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _logService.LogInfo($"查看 {selectedItems.Count} 個合約的詳細資訊", LOG_SOURCE, LogDisplayTarget.SourceWindow);

            // 顯示詳細資訊視窗
            var detailsWindow = new ContractDetailsWindow(selectedItems);
            detailsWindow.ShowDialog();
        }

        private void CreateQuickButtonsButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = _viewModel.SearchResults.Where(r => r.IsSelected).ToList();
            // 使用 Count > 0 而不是 Any()
            if (selectedItems.Count == 0)
            {
                _logService.LogWarning("請先選擇要建立快速按鈕的商品", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                MessageBox.Show("請先選擇要建立快速按鈕的商品", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _logService.LogInfo($"建立 {selectedItems.Count} 個快速按鈕", LOG_SOURCE, LogDisplayTarget.SourceWindow);

            // 觸發合約選擇事件
            var eventArgs = new ContractSelectedEventArgs(selectedItems);
            ContractSelected?.Invoke(this, eventArgs);

            MessageBox.Show($"已建立 {selectedItems.Count} 個快速按鈕", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _logService.LogInfo("使用者取消操作，關閉視窗", LOG_SOURCE, LogDisplayTarget.SourceWindow);
            this.Close();
        }

        #endregion
        #region 輔助方法

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(_viewModel.SearchResults) ||
                e.PropertyName == nameof(_viewModel.SearchResultCount))
            {
                UpdateResultCount();
                ResultDataGrid.Items.Refresh();
            }
            // 日誌更新時自動滾動到底部
            if (e.PropertyName == nameof(_viewModel.LogMessages))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    LogTextBox.ScrollToEnd();
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void UpdateResultCount()
        {
            var count = _viewModel.SearchResults?.Count ?? 0;
            ResultCountTextBlock.Text = $"查詢結果: {count} 筆";
        }

        private void UpdateSelectionCount()
        {
            var selectedCount = _viewModel.SearchResults?.Count(r => r.IsSelected) ?? 0;
            SelectedCountTextBlock.Text = $"已選擇: {selectedCount} 筆";
        }

        /// <summary>
        /// 取得選中的合約清單 (供外部呼叫)
        /// </summary>
        public List<ContractSearchResult> GetSelectedContracts()
        {
            return _viewModel.GetSelectedContracts();
        }

        /// <summary>
        /// 匯出搜尋結果 (供外部呼叫)
        /// </summary>
        public string ExportSearchResults()
        {
            return _viewModel.ExportSearchResults();
        }

        #endregion

        #region 測試方法
        private void Test01_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 後續開發 GetContractByOneParameter 測試功能
        }

        private void Test02_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 後續開發 GetContractByTwoParameter 測試功能
        }
        #endregion

        #region 視窗生命週期

        /// <summary>
        /// 🔧 簡化版視窗關閉事件
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // 清理 ViewModel
                _viewModel?.Dispose();

                _logService.LogInfo("ContractSearchWindow 已關閉並釋放所有資源", LOG_SOURCE,
                    LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "關閉視窗時發生錯誤", LOG_SOURCE,
                    LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
            finally
            {
                base.OnClosed(e);
            }
        }

        #endregion
    }

    /// <summary>
    /// 合約選擇事件參數
    /// </summary>
    public class ContractSelectedEventArgs(List<ContractSearchResult> selectedContracts) : EventArgs
    {
        // 使用主要建構函式
        public List<ContractSearchResult> SelectedContracts { get; } = selectedContracts;
    }
}