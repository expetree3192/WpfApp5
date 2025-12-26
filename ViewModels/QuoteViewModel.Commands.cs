using CommunityToolkit.Mvvm.Input;
using Sinopac.Shioaji;
using System.Windows;
using WpfApp5.Services;

namespace WpfApp5.ViewModels
{
    public partial class QuoteViewModel
    {
        #region 報價專用命令

        [RelayCommand]
        private async Task SmartSubscribe()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(SubscribeSymbol))
                {
                    _logService.LogWarning("[錯誤] 請輸入商品代號", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    return;
                }

                var result = await _marketService.SmartSubscribeProduct(SelectedProductType, SelectedExchange, SubscribeSymbol, WindowId, IsSubscribingOddLot);

                if (result.IsSuccess)
                {
                    _logService.LogInfo($"[智能訂閱] {result.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    OnPropertyChanged(nameof(SubscribedCount));
                    OnPropertyChanged(nameof(SubscriptionStatus));

                    QuoteType primaryQuoteType = SelectedProductType.ToUpper() switch
                    {
                        "STOCKS" => QuoteType.quote,
                        "FUTURES" => QuoteType.tick,
                        "OPTIONS" => QuoteType.tick,
                        _ => QuoteType.tick
                    };

                    SubscribeToContract(SubscribeSymbol, primaryQuoteType);
                    UpdateOrderPreparationContract();   //  更新委託準備的商品
                }
                else
                {
                    _logService.LogError($"❌ [智能訂閱失敗] {result.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "❌ [錯誤] 智能訂閱失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
        }

        [RelayCommand]
        private void Subscribe()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(SubscribeSymbol))
                {
                    _logService.LogWarning("[錯誤] 請輸入商品代號", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    return;
                }

                var result = _marketService.SubscribeProduct(SelectedProductType, SelectedExchange, SubscribeSymbol, WindowId, SelectedQuoteType, IsSubscribingOddLot);

                if (result.IsSuccess)
                {
                    _logService.LogInfo($"[訂閱] {result.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    OnPropertyChanged(nameof(SubscribedCount));
                    SubscribeToContract(SubscribeSymbol, SelectedQuoteType);
                    UpdateOrderPreparationContract();   //  更新委託準備的商品
                }
                else
                {
                    _logService.LogError($"[錯誤] {result.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[錯誤] 訂閱失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
        }

        [RelayCommand]
        private void Unsubscribe()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(SubscribeSymbol))
                {
                    _logService.LogWarning("[錯誤] 請輸入要取消訂閱的商品代號", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    return;
                }

                string actualCode = GetActualCode(SelectedProductType, SelectedExchange, SubscribeSymbol);
                var result = _marketService.UnsubscribeProduct(actualCode, WindowId, SelectedQuoteType, IsSubscribingOddLot);

                if (result.IsSuccess)
                {
                    _logService.LogInfo($"[取消訂閱] {result.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    OnPropertyChanged(nameof(SubscribedCount));
                    OnPropertyChanged(nameof(SubscriptionStatus));
                }
                else
                {
                    _logService.LogError($"[錯誤] 取消訂閱失敗: {result.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[錯誤] 取消訂閱失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
        }

        /// <summary>
        /// 🗑️ 取消當前視窗的所有訂閱 - 智能取消該視窗目前訂閱的所有報價類型
        /// 
        /// 功能說明：
        /// - 股票商品：取消 quote 訂閱
        /// - 期貨/選擇權商品：取消 tick + bidask 訂閱
        /// - 只影響當前視窗，不影響其他視窗的訂閱
        /// - 取消後會自動重置 OrderBookViewModel
        /// </summary>
        [RelayCommand]
        private void UnsubscribeWindow()
        {
            try
            {
                _logService.LogInfo($"[取消訂閱] 🗑️ 開始取消視窗 {WindowId} 的所有訂閱...並調用 MarketService 的 UnsubscribeAllForWindow 方法", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                // 調用 MarketService 的 UnsubscribeAllForWindow 方法
                var result = _marketService.UnsubscribeAllForWindow(WindowId);

                if (result.IsSuccess)
                {
                    _logService.LogInfo($"[取消訂閱] ✅ {result.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                    // 更新 UI 屬性
                    OnPropertyChanged(nameof(SubscribedCount));
                    OnPropertyChanged(nameof(SubscriptionStatus));

                    // 🔧 重置 OrderBookViewModel（這個操作會在 MarketService.UnsubscribeAllForWindow 中自動執行）
                    // 但我們這裡也可以手動確保重置完成
                    if (OrderBookViewModel != null)
                    {
                        // OrderBookViewModel 應該已經被 MarketService 重置了
                        // 這裡只需要確保 UI 狀態一致
                        _logService.LogInfo($"[取消訂閱] 發現OrderBookViewModel != null", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    }
                }
                else
                {
                    _logService.LogError($"[取消訂閱] ❌ 取消視窗訂閱失敗: {result.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[取消訂閱] 取消視窗訂閱時發生異常", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                MessageBox.Show($"取消訂閱時發生錯誤: {ex.Message}", "取消訂閱錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 其他命令...

        #endregion
        #region 其他命令
        [RelayCommand]
        private void ToggleCenter()
        {
            try
            {
                if (OrderBookViewModel == null)
                {
                    _logService.LogWarning("[錯誤] OrderBookViewModel 未初始化", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    return;
                }

                bool newCenteredState = !IsFiveDepthCentered;

                if (newCenteredState)
                {
                    IsFiveDepthCentered = true;
                    OrderBookViewModel.IsCentered = true;
                    OrderBookViewModel.IsViewLocked = false;
                    OrderBookViewModel.CenterToCurrentPrice();
                    _logService.LogInfo("[設定] 已開啟置中功能（回到即時價格）", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }
                else
                {
                    IsFiveDepthCentered = false;
                    OrderBookViewModel.IsCentered = false;
                    OrderBookViewModel.IsViewLocked = false;
                    _logService.LogInfo("[設定] 已關閉置中功能（允許手動滾動）", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }

                // 🔧 修正：確保屬性變更通知
                OnPropertyChanged(nameof(CenterButtonText));
                OnPropertyChanged(nameof(IsFiveDepthCentered));

                _logService.LogDebug($"[置中切換] 新狀態: {IsFiveDepthCentered}, 按鈕文字: {CenterButtonText}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[錯誤] 切換置中功能失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
        }

        [RelayCommand]
        private void GoToPrice(string? parameter)
        {
            try
            {
                _logService.LogDebug($"[導航] GoToPrice 被調用，參數: '{parameter}'", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                if (OrderBookViewModel == null)
                {
                    _logService.LogWarning("[錯誤] OrderBookViewModel 未初始化", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    return;
                }

                if (string.IsNullOrWhiteSpace(parameter))
                {
                    _logService.LogWarning("[錯誤] GoToPrice 參數為空", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    return;
                }

                if (decimal.TryParse(parameter, out decimal price))
                {
                    // 確保價格在有效範圍內
                    decimal limitUp = OrderBookViewModel.LimitUp;
                    decimal limitDown = OrderBookViewModel.LimitDown;

                    if (limitUp > 0 && limitDown > 0)
                    {
                        price = Math.Min(limitUp, Math.Max(limitDown, price));
                    }

                    // 設定狀態
                    IsFiveDepthCentered = false;
                    OrderBookViewModel.IsCentered = false;
                    OrderBookViewModel.IsViewLocked = true;

                    // 執行導航
                    OrderBookViewModel.GoToPrice(price);

                    // 更新 UI 狀態
                    OnPropertyChanged(nameof(CenterButtonText));
                    OnPropertyChanged(nameof(IsFiveDepthCentered));

                    _logService.LogInfo($"[導航] 前往價格: {price}（視圖已鎖定，置中已關閉）", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }
                else
                {
                    _logService.LogWarning($"[錯誤] 無效的價格格式: '{parameter}'", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"[錯誤] 前往價格失敗，參數: '{parameter}'", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
        }
        protected override void ToggleWindowSize()
        {
            try
            {
                _logService.LogInfo("[視窗] QuoteViewModel 切換視窗大小", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                base.ToggleWindowSize();    // 呼叫基類的實作（會自動處理 IsWindowExpanded 切換）

                // 🔧 QuoteViewModel 特有的額外處理
                // 觸發 QuoteViewModel 專用的視窗大小切換事件
                WindowSizeToggleRequested?.Invoke(this, IsWindowExpanded);

                _logService.LogInfo($"[視窗] QuoteViewModel 視窗狀態: {(IsWindowExpanded ? "展開" : "收合")}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[錯誤] QuoteViewModel 切換視窗大小失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
        }
        #endregion
    }
}
