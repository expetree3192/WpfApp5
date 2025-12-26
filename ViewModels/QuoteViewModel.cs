using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sinopac.Shioaji;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows;
using WpfApp5.Models;
using WpfApp5.Models.MarketData;
using WpfApp5.Services;
using WpfApp5.Services.Common;

namespace WpfApp5.ViewModels
{
    // 配合 OrderService 重構的報價視窗 ViewModel
    public partial class QuoteViewModel : BaseViewModel
    {
        #region 報價專用屬性

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SubscriptionStatus))]
        private string _subscribeSymbol = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SubscriptionStatus))]
        private bool _isSubscribingOddLot = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SubscriptionStatus))]
        private QuoteType _selectedQuoteType = QuoteType.tick;

        [ObservableProperty]
        private bool _isFiveDepthCentered = true;

        [ObservableProperty]
        private OrderBookViewModel? _orderBookViewModel;

        public ObservableCollection<object> MarketData { get; } = [];
        public ObservableCollection<object> TickData { get; } = [];
        public ObservableCollection<QuoteType> QuoteTypes { get; } = [QuoteType.tick, QuoteType.bidask, QuoteType.quote];

        #endregion

        #region 報價專用 UI 屬性

        public string CenterButtonText => IsFiveDepthCentered ? "㊣ 置中已啟用" : "❌ 置中已關閉";

        public string SubscriptionStatus
        {
            get
            {
                if (string.IsNullOrWhiteSpace(SubscribeSymbol))
                {
                    return "請輸入商品代號";
                }

                string actualCode = GetActualCode(SelectedProductType, SelectedExchange, SubscribeSymbol);
                bool isSubscribed = _marketService.IsSubscribed(actualCode, SelectedQuoteType, IsSubscribingOddLot);

                string displayKey = $"{SelectedProductType}.{SelectedExchange}.{SubscribeSymbol}.{SelectedQuoteType}" + (IsSubscribingOddLot ? ".ODD" : "");
                return isSubscribed ? $"已訂閱: {displayKey}" : $"取消訂閱: {displayKey}";
            }
        }

        //  public int SubscribedCount => _marketService.SubscribedCount;
        public int SubscribedCount => _marketService.SubscriptionManager.GetAllUniqueSubscriptions().Count;

        #endregion

        #region 事件

        public event EventHandler<(string contractCode, QuoteType quoteType)>? ContractSubscriptionRequested;
        public event EventHandler<bool>? WindowSizeToggleRequested;

        #endregion

        #region 建構函數

        public QuoteViewModel(string windowId) : base(windowId)
        {
            _orderBookViewModel = new OrderBookViewModel(windowId)
            {
                IsCentered = _isFiveDepthCentered
            };

            // 預設值
            SubscribeSymbol = "MXF202601";
            SelectedProductType = ProductTypes[1];
            SelectedExchange = AvailableExchanges[3];
            SelectedQuoteType = QuoteTypes[2];
            SelectedPriceType = PriceTypes[0];
            SelectedOrderType = OrderTypes[0];
            SelectedOcType = OcTypes[0];

            _logService.LogInfo("QuoteViewModel 已初始化", "QuoteViewModel", LogDisplayTarget.SourceWindow);
        }

        #endregion

        #region 🔧 覆寫 BaseViewModel 的市場數據處理方法

        protected override void OnSTKTickDataReceived(STKTickData data)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    SetBidAskSnapshot(data, BidPrice1, AskPrice1, BidVolume1, AskVolume1);
                    MarketData.Insert(0, data);
                    if (MarketData.Count > 100)
                        MarketData.RemoveAt(MarketData.Count - 1);
                });
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "處理 STK Tick 資料失敗", "QuoteViewModel", LogDisplayTarget.DebugOutput);
            }
        }

        protected override void OnSTKBidAskDataReceived(STKBidAskData data)
        {
            // BaseViewModel 已經更新了所有 BidAsk 屬性
            // 如果需要額外處理，可以在這裡添加
        }

        protected override void OnSTKQuoteDataReceived(STKQuoteData data)
        {
            if (data.Volume == 0) return;
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MarketData.Insert(0, data);
                    if (MarketData.Count > 100)
                        MarketData.RemoveAt(MarketData.Count - 1);
                });
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "處理 STK Quote 資料失敗", "QuoteViewModel", LogDisplayTarget.DebugOutput);
            }
        }

        protected override void OnFOPTickDataReceived(FOPTickData data)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    SetBidAskSnapshot(data, BidPrice1, AskPrice1, BidVolume1, AskVolume1);
                    // 直接插入原始 FOPTickData 物件
                    MarketData.Insert(0, data);
                    if (MarketData.Count > 100)
                        MarketData.RemoveAt(MarketData.Count - 1);
                });
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "處理 FOP Tick 資料失敗", "QuoteViewModel", LogDisplayTarget.DebugOutput);
            }
        }

        protected override void OnFOPBidAskDataReceived(FOPBidAskData data)
        {
            // BaseViewModel 已經更新了所有 BidAsk 屬性
            // 如果需要額外處理，可以在這裡添加
        }
        #region 買賣價快照設定方法

        // 買賣價快照設定方法（支援所有實作 IBidAskSnapshot 的類型）
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetBidAskSnapshot<T>(T tickData, decimal bidPrice1, decimal askPrice1, int bidVolume1, int askVolume1)
          where T : IBidAskSnapshot
        {
            tickData.BidPrice1 = bidPrice1;
            tickData.AskPrice1 = askPrice1;
            tickData.BidVolume1 = bidVolume1;
            tickData.AskVolume1 = askVolume1;
        }

        #endregion
        protected override void OnOrderBookInitializationDataReceived(ContractInfo contractInfo, string windowId)
        {
            try
            {
                // QuoteViewModel 特有的處理邏輯
                //  UpdateOrderPreparationContract();   //  OrderBook初始化後

                // 自動執行-合約資料初始化
                _ = Task.Run(async () =>
                {
                    try
                    {
                        _logService.LogInfo($"[OrderBook初始化] 🚀 自動執行合約 {contractInfo.Code} 資料初始化...", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                        // 檢查是否有選擇帳戶
                        if (SelectedAccount?.Account != null)
                        {
                            await InitializeContractData();
                        }
                        else
                        {
                            _logService.LogWarning($"[OrderBook初始化] 未選擇帳戶，跳過自動初始化", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.LogError(ex, $"[OrderBook初始化] 自動初始化失敗: {contractInfo.Code}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    }
                });
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"QuoteViewModel 處理訂單簿初始化請求時發生錯誤: {ex.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
        }

        #endregion

        #region 🔄 屬性變更處理

        partial void OnIsFiveDepthCenteredChanged(bool value)
        {
            if (OrderBookViewModel != null)
            {
                OrderBookViewModel.IsCentered = value;
            }
            OnPropertyChanged(nameof(CenterButtonText));
        }

        // 覆寫基類方法，加入停利停損檢查
        protected override void OnPriceChanged(decimal newPrice)
        {
            CheckStopLossAndTakeProfit(newPrice);
        }

        #endregion

        #region 報價專用命令

        [RelayCommand]
        private void UnsubscribeAll()
        {
            try
            {
                var result = _marketService.UnsubscribeAll();

                if (result.IsSuccess)
                {
                    _logService.LogInfo($"[取消訂閱] {result.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    OnPropertyChanged(nameof(SubscribedCount));
                    OnPropertyChanged(nameof(SubscriptionStatus));
                    ResetOrderBookViewModel();  // 🔧 選項 A：完全重置
                    // OrderBookViewModel?.ClearAllData();  // 🔧 選項 B：只清空數據，保持 ListView 連接
                }
                else
                {
                    _logService.LogError($"[錯誤] 取消所有訂閱失敗: {result.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[錯誤] 取消所有訂閱失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
        }

        #region 報價專用命令


        #endregion

        // 觸發 UpdateStatus (所有帳戶) - 強制更新所有帳戶的委託狀態
        [RelayCommand]
        private async Task UpdateAllStatus()
        {
            try
            {
                await UpdateTradingStatus("觸發所有帳戶更新", forceUpdate: true);   // 🚀 使用現有的 UpdateTradingStatus 方法，強制更新所有帳戶
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[UpdateStatus] ❌ 所有帳戶 UpdateStatus 失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                throw new Exception($"所有帳戶狀態更新失敗: {ex.Message}", ex);   // 🔧 重新拋出異常，讓調用方知道更新失敗
            }
        }
        #endregion

        #region 整合合約資料初始化

        // 整合合約資料初始化 - 同時更新部位和掛單統計（只執行一次 UpdateStatus）
        [RelayCommand]
        private async Task InitializeContractData()
        {
            try
            {
                if (SelectedAccount?.Account == null)
                {
                    MessageBox.Show("請先選擇帳戶", "合約資料初始化", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrEmpty(CurrentSubscribedCode))
                {
                    MessageBox.Show("請先訂閱商品", "合約資料初始化", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var account = SelectedAccount.Account;
                _logService.LogInfo($"[合約資料初始化] 🚀 開始初始化合約 {CurrentSubscribedCode} 的完整資料...", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                _logService.LogInfo($"[合約資料初始化] 帳戶: {account.account_type}-{account.account_id}", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                // 步驟1：先執行一次 UpdateAllStatus
                try
                {
                    _logService.LogInfo($"[合約資料初始化] 🔄 步驟1: 執行 UpdateAllStatus...", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    await UpdateAllStatus();
                    _logService.LogInfo($"[合約資料初始化] ✅ 步驟1: UpdateAllStatus 完成", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }
                catch (Exception updateEx)
                {
                    _logService.LogError(updateEx, "[合約資料初始化] UpdateAllStatus 失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    MessageBox.Show($"狀態更新失敗，無法繼續初始化: {updateEx.Message}", "合約資料初始化錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 步驟2：並行執行部位查詢和掛單統計
                _logService.LogInfo($"[合約資料初始化] 🔄 步驟2: 並行執行部位查詢和掛單統計...", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                var tasks = new List<Task>
                {
                    // 任務1：查詢部位
                    Task.Run(() =>
                    {
                        try
                        {
                            _logService.LogInfo($"[合約資料初始化] 📊 查詢部位...開始", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                            if (account.account_type.ToString() == "S")
                            {
                                QueryStockPositionsForContract(account, CurrentSubscribedCode);
                            }
                            else if (account.account_type.ToString() == "F")
                            {
                                QueryFuturePositionsForContract(account, CurrentSubscribedCode);
                            }

                            _logService.LogInfo($"[合約資料初始化] ✅ 查詢部位...完成", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                        }
                        catch (Exception ex)
                        {
                            _logService.LogError(ex, "[合約資料初始化] 部位查詢失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                        }
                    }),

                    // 任務2：查詢掛單統計並同步到 OrderBook UI
                    Task.Run(async () => // 🔧 修正：加上 async
                    {
                        try
                        {
                            _logService.LogInfo($"[合約資料初始化] 📋 查詢掛單統計...開始", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                            var result = OrderService.GetContractPendingOrderStatsSync(CurrentSubscribedCode);
                            if (result.IsSuccess && result.Data != null)
                            {
                                var stats = result.Data;

                                // 更新 UI 屬性（需要在 UI 線程中執行）
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    PendingBuyOrders = stats.PendingBuyQuantity;            // 數量
                                    PendingSellOrders = stats.PendingSellQuantity;          // 數量
                                    PendingBuyOrderCount = stats.PendingBuyOrderCount;      // 筆數
                                    PendingSellOrderCount = stats.PendingSellOrderCount;    // 筆數

                                    _logService.LogInfo($"[合約資料初始化] ✅ 查詢掛單統計...更新完成 - {stats}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                                });

                                // 同步詳細掛單資訊到 OrderBook UI
                                await SyncDetailedPendingOrdersToOrderBookAsync(CurrentSubscribedCode);  // 🎯 直接呼叫 BaseViewModel 的方法
                            }
                            else
                            {
                                _logService.LogWarning($"[合約資料初始化] ❌ 掛單統計查詢失敗: {result.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logService.LogError(ex, "[合約資料初始化] 掛單統計查詢失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                        }
                    })
                };

                // 等待所有任務完成
                await Task.WhenAll(tasks);

                _logService.LogInfo($"[合約資料初始化] 🎉 合約 {CurrentSubscribedCode} 資料初始化完成！", "QuoteViewModel", LogDisplayTarget.SourceWindow);

            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[合約資料初始化] 初始化失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                MessageBox.Show($"初始化失敗: {ex.Message}", "合約資料初始化錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 針對特定合約查詢股票部位
        private void QueryStockPositionsForContract(Account account, string contractCode)
        {
            try
            {
                bool foundPosition = false;

                // 查詢整股部位
                try
                {
                    dynamic stockPositions = ShioajiService.ListStockPositions();
                    if (stockPositions != null)
                    {
                        foundPosition = ProcessStockPositionsForContract(stockPositions, contractCode, "整股") || foundPosition;
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogWarning($"[合約資料初始化] 查詢整股部位失敗: {ex.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }

                // 查詢零股部位
                try
                {
                    dynamic sharePositions = ShioajiService.ListStockPositions(Unit.Share);
                    if (sharePositions != null)
                    {
                        foundPosition = ProcessStockPositionsForContract(sharePositions, contractCode, "零股") || foundPosition;
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogWarning($"[合約資料初始化] 查詢零股部位失敗: {ex.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }

                if (!foundPosition)
                {
                    _logService.LogInfo($"[合約資料初始化] 股票合約 {contractCode} 目前沒有部位", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    UpdateUIPositionFromShioaji(0, 0);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"[合約資料初始化] 查詢股票合約 {contractCode} 部位失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
        }

        // 針對特定合約查詢期貨部位
        private void QueryFuturePositionsForContract(Account account, string contractCode)
        {
            try
            {
                dynamic futurePositions = ShioajiService.ListFuturePositions();

                if (futurePositions == null)
                {
                    _logService.LogInfo($"[合約資料初始化] 期貨帳戶目前沒有任何部位", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    UpdateUIPositionFromShioaji(0, 0);
                    return;
                }

                var futurePositionsList = (List<FuturePosition>)futurePositions;
                if (futurePositionsList.Count == 0)
                {
                    UpdateUIPositionFromShioaji(0, 0);
                    return;
                }

                _logService.LogInfo($"[合約資料初始化] 期貨部位: {account.account_id} (共 {futurePositionsList.Count} 筆)", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                bool foundMatchingPosition = false;
                long matchedNetPosition = 0;
                decimal FOPAvgCost = 0;
                decimal matchedPnL = 0;

                foreach (var position in futurePositionsList)
                {
                    string positionCode = position.code;
                    bool isMatching = positionCode.Equals(contractCode, StringComparison.OrdinalIgnoreCase);

                    _logService.LogInfo($"  {position.code}: {position.direction} {position.quantity}口 @ {position.price} pnl:{position.pnl} {(isMatching ? "✅" : "❌")}", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                    if (isMatching)
                    {
                        matchedNetPosition = position.direction.ToString().ToUpper() == "BUY" ? position.quantity : -position.quantity;
                        FOPAvgCost = position.price;
                        matchedPnL = position.pnl;
                        foundMatchingPosition = true;
                    }
                }

                // 更新UI
                if (foundMatchingPosition)
                {
                    _logService.LogInfo($"[合約資料初始化] 匹配部位 {contractCode}: 淨部位={matchedNetPosition} 成本={FOPAvgCost} 損益={matchedPnL}", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TargetPosition = matchedNetPosition;
                        ProfitLoss = matchedPnL;
                        AvgCost = FOPAvgCost;
                    });

                    UpdateUIPositionFromShioaji(matchedNetPosition, FOPAvgCost);
                }
                else
                {
                    _logService.LogInfo($"[合約資料初始化] 未找到匹配部位 {contractCode}", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        TargetPosition = 0;
                        ProfitLoss = 0;
                        AvgCost = 0;
                    });

                    UpdateUIPositionFromShioaji(0, 0);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"[合約資料初始化] 查詢期貨合約 {contractCode} 部位失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
        }

        // 處理指定合約的股票部位（返回是否找到部位）
        private bool ProcessStockPositionsForContract(dynamic positions, string contractCode, string positionType)
        {
            try
            {
                if (positions == null) return false;

                var stockPositions = (List<StockPosition>)positions;

                foreach (var position in stockPositions)
                {
                    if (position.code.Equals(contractCode, StringComparison.OrdinalIgnoreCase))
                    {
                        long netPosition = position.direction.ToString().ToUpper() == "BUY" ? position.quantity : -position.quantity;
                        decimal STKAvgCost = position.price;
                        decimal pnl = position.pnl;

                        // 更新 UI
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            TargetPosition = netPosition;
                            ProfitLoss = pnl;
                            AvgCost = STKAvgCost;
                        });

                        UpdateUIPositionFromShioaji(netPosition, STKAvgCost);

                        _logService.LogInfo($"[合約資料初始化] ✅ 股票{positionType}部位 {contractCode}: 淨部位={netPosition}, 成本={STKAvgCost}, 損益={pnl}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"[合約資料初始化] 處理股票{positionType}部位失敗: {contractCode}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                return false;
            }
        }

        #endregion

        #region 下單命令

        [RelayCommand]
        private async Task PlaceBuyOrder()
        {
            try
            {
                var result = _orderPrep.GetBuyOrder(ManualOrderPrice);

                if (!result.IsSuccess || result.Data == null)
                {
                    _logService.LogWarning($"[下單] {result.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    MessageBox.Show(result.Message, "下單錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var package = result.Data;
                _logService.LogInfo($"[下單-打印Data] {result.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                // 異步下單
                var orderResult = await OrderService.PlaceOrderAsync(contract: package.Contract, order: package.Order, windowId: WindowId, useNonBlocking: true);

                if (orderResult.IsSuccess)
                {
                    _logService.LogInfo($"[下單] 買單已送出: {orderResult.Data?.order?.id}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }
                else
                {
                    _logService.LogError($"[下單] 買單失敗: {orderResult.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    MessageBox.Show($"買單失敗: {orderResult.Message}", "下單錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[下單] 買單失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                MessageBox.Show($"買單失敗: {ex.Message}", "下單錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task PlaceSellOrder()
        {
            try
            {
                var result = _orderPrep.GetSellOrder(ManualOrderPrice);
                if (!result.IsSuccess || result.Data == null)
                {
                    MessageBox.Show(result.Message, "下單錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var package = result.Data;
                _logService.LogInfo($"[下單-打印Data] {result.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                // 異步下單
                var orderResult = await OrderService.PlaceOrderAsync(contract: package.Contract, order: package.Order, windowId: WindowId, useNonBlocking: true);

                if (orderResult.IsSuccess)
                {
                    _logService.LogInfo($"[下單] 賣單已送出: {orderResult.Data?.order?.id}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }
                else
                {
                    _logService.LogError($"[下單] 賣單失敗: {orderResult.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    MessageBox.Show($"賣單失敗: {orderResult.Message}", "下單錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[下單] 賣單失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                MessageBox.Show($"賣單失敗: {ex.Message}", "下單錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 異步刪除所有買單
        [RelayCommand]
        private async Task CancelBuyOrders()
        {
            try
            {
                string? contractCode = OrderBookViewModel?.CurrentSubscribedCode;

                if (string.IsNullOrEmpty(contractCode))
                {
                    _logService.LogWarning("[批量刪單] ⚠️ 未訂閱任何合約，無法刪單", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    return;
                }

                _logService.LogInfo($"[批量刪單] >> 開始刪除合約 {contractCode} 的所有買單...", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                // 異步批量刪單_Cancel Buy
                var result = await OrderService.CancelContractOrdersAsync(contractCode, "Buy");

                if (result.IsSuccess)
                {
                    _logService.LogInfo($"[批量刪單] ✔ {result.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }
                else
                {
                    _logService.LogError($"[批量刪單] ❌ {result.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[批量刪單] 刪除所有買單失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
        }

        // 異步刪除所有賣單
        [RelayCommand]
        private async Task CancelSellOrders()
        {
            try
            {
                string? contractCode = OrderBookViewModel?.CurrentSubscribedCode;
                if (string.IsNullOrEmpty(contractCode))
                {
                    _logService.LogWarning("[批量刪單] ⚠️ 未訂閱任何合約，無法刪單", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    return;
                }

                _logService.LogInfo($"[批量刪單] >> 開始刪除合約 {contractCode} 的所有賣單...", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                // 異步批量刪單_Cancel Sell
                var result = await OrderService.CancelContractOrdersAsync(contractCode, "Sell");

                if (result.IsSuccess)
                {
                    _logService.LogInfo($"[批量刪單] ✔ {result.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }
                else
                {
                    _logService.LogError($"[批量刪單] ❌ {result.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[批量刪單] 刪除所有賣單失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
        }
        private async Task UpdateTradingStatus(string operation = "操作", bool forceUpdate = false)
        {
            try
            {
                _logService.LogInfo($"[狀態更新] 🔄 開始更新 {operation} 後的狀態...", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                if (forceUpdate)
                {
                    await ShioajiService.UpdateAllAccountStatusAsync(); // 強制更新所有帳戶 - 直接 await，不需要 Task.Run
                    _logService.LogInfo("[狀態更新] ✔ 所有帳戶狀態強制更新完成", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }
                else
                {
                    // 只更新當前帳戶 - 包裝成 async
                    if (SelectedAccount?.Account != null)
                    {
                        await Task.Run(() =>
                        {
                            ShioajiService.UpdateAccountStatus(SelectedAccount.Account, timeout: 3000);
                        });
                        _logService.LogInfo($"[狀態更新] ✔ 當前帳戶狀態更新完成", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"[狀態更新] {operation} 後狀態更新異常", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                // 🔧 重新拋出異常，讓調用方知道更新失敗
                throw new Exception($"狀態更新失敗: {ex.Message}", ex);
            }
        }


        // 市價買進命令
        [RelayCommand]
        private async Task MarketBuy()
        {
            try
            {
                _logService.LogInfo("🚀 執行市價買進", GetType().Name, LogDisplayTarget.SourceWindow);

                var result = _orderPrep.GetMarketBuyOrder();
                if (!result.IsSuccess)
                {
                    _logService.LogError($"❌ 建立市價買單失敗: {result.Message}", GetType().Name, LogDisplayTarget.SourceWindow);
                    return;
                }

                var package = result.Data!;

                // 使用異步下單 API
                var orderResult = await OrderService.PlaceOrderAsync(contract: package.Contract, order: package.Order, windowId: WindowId, useNonBlocking: true);

                if (orderResult.IsSuccess)
                {
                    _logService.LogInfo($"市價買單已送出: ordno={orderResult.Data?.order?.ordno}", GetType().Name, LogDisplayTarget.SourceWindow);
                }
                else
                {
                    _logService.LogError($"市價買單失敗: {orderResult.Message}", GetType().Name, LogDisplayTarget.SourceWindow);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "市價買進操作失敗", GetType().Name, LogDisplayTarget.SourceWindow);
            }
        }

        // 市價賣出命令
        [RelayCommand]
        private async Task MarketSell()
        {
            try
            {
                _logService.LogInfo("🚀 執行市價賣出", GetType().Name, LogDisplayTarget.SourceWindow);

                var result = _orderPrep.GetMarketSellOrder();
                if (!result.IsSuccess)
                {
                    _logService.LogError($"❌ 建立市價賣單失敗: {result.Message}", GetType().Name, LogDisplayTarget.SourceWindow);
                    return;
                }

                var package = result.Data!;

                // 🚀 使用新的異步下單 API
                var orderResult = await OrderService.PlaceOrderAsync(contract: package.Contract, order: package.Order, windowId: WindowId, useNonBlocking: true);

                if (orderResult.IsSuccess)
                {
                    _logService.LogInfo($"市價賣單已送出: ordno={orderResult.Data?.order?.ordno}", GetType().Name, LogDisplayTarget.SourceWindow);
                }
                else
                {
                    _logService.LogError($"市價賣單失敗: {orderResult.Message}", GetType().Name, LogDisplayTarget.SourceWindow);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "市價賣出操作失敗", GetType().Name, LogDisplayTarget.SourceWindow);
            }
        }

        #endregion
        #region 智能下單命令

        // 智能下單核心方法
        public async Task ExecuteSmartOrderAsync(decimal price, string action, int columnIndex)
        {
            try
            {
                _logService.LogInfo($"[智能下單] 🚀 執行智能下單: {action} @ {price} (欄位: {columnIndex})", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                ServiceResult<OrderPreparationService.OrderPackage> result;

                // 根據動作類型準備訂單
                if (action.Equals("Buy", StringComparison.OrdinalIgnoreCase))
                {
                    result = _orderPrep.GetBuyOrder(price);
                }
                else if (action.Equals("Sell", StringComparison.OrdinalIgnoreCase))
                {
                    result = _orderPrep.GetSellOrder(price);
                }
                else
                {
                    _logService.LogError($"[智能下單] ❌ 不支援的動作類型: {action}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    return;
                }

                if (!result.IsSuccess || result.Data == null)
                {
                    _logService.LogWarning($"[智能下單] ❌ 準備訂單失敗: {result.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    MessageBox.Show(result.Message, "智能下單錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var package = result.Data;
                _logService.LogInfo($"[智能下單] ✅ 訂單準備完成: {action} {CurrentSubscribedCode} @ {price} x {OrderQuantity}", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                // 執行異步下單
                var orderResult = await OrderService.PlaceOrderAsync(contract: package.Contract, order: package.Order, windowId: WindowId, useNonBlocking: true);

                if (orderResult.IsSuccess)
                {
                    string actionText = action == "Buy" ? "買單" : "賣單";
                    _logService.LogInfo($"[智能下單] 🎉 {actionText}已送出: ordno={orderResult.Data?.order?.ordno}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }
                else
                {
                    string actionText = action == "Buy" ? "買單" : "賣單";
                    _logService.LogError($"[智能下單] ❌ {actionText}失敗: {orderResult.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    MessageBox.Show($"{actionText}失敗: {orderResult.Message}", "智能下單錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[智能下單] 執行智能下單失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                MessageBox.Show($"智能下單失敗: {ex.Message}", "智能下單錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 智能刪單方法
        public async Task ExecuteSmartCancelAsync(decimal price, string action, int columnIndex)
        {
            try
            {
                _logService.LogInfo($"[智能刪單] 🗑️ 執行智能刪單: {action} @ {price} (欄位: {columnIndex})", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                string contractCode = CurrentSubscribedCode;
                if (string.IsNullOrEmpty(contractCode))
                {
                    _logService.LogWarning("[智能刪單] ⚠️ 未訂閱任何合約，無法刪單", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    return;
                }

                ServiceResult<OrderService.BatchCancelResult> result;

                if (action == "All")
                {
                    // 刪除該價格的所有委託
                    _logService.LogInfo($"[智能刪單] 🗑️ 刪除價格 {price} 的所有委託...", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                    // 先取得該價格的所有可刪除委託
                    var cancellableResult = await OrderService.GetCancellableOrdersAsync(contractCode);
                    if (cancellableResult.IsSuccess && cancellableResult.Data != null)
                    {
                        // 篩選出指定價格的委託
                        var priceOrders = cancellableResult.Data.Where(trade =>
                        {
                            var orderPrice = (decimal)(trade.order?.price ?? 0.0);
                            return Math.Abs(orderPrice - price) < 0.01m; // 允許小數點誤差
                        }).ToList();

                        if (priceOrders.Count > 0)
                        {
                            result = await OrderService.CancelOrdersBatchAsync(priceOrders);
                        }
                        else
                        {
                            _logService.LogInfo($"[智能刪單] ℹ️ 價格 {price} 沒有可刪除的委託", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                            return;
                        }
                    }
                    else
                    {
                        _logService.LogWarning($"[智能刪單] ❌ 查詢可刪除委託失敗: {cancellableResult.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                        return;
                    }
                }
                else
                {
                    // 刪除該價格的指定方向委託
                    string actionFilter = action.Equals("Buy", StringComparison.OrdinalIgnoreCase) ? "Buy" : "Sell";
                    _logService.LogInfo($"[智能刪單] 🗑️ 刪除價格 {price} 的{actionFilter}委託...", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                    // 先取得該價格該方向的可刪除委託
                    var cancellableResult = await OrderService.GetCancellableOrdersAsync(contractCode, actionFilter);
                    if (cancellableResult.IsSuccess && cancellableResult.Data != null)
                    {
                        // 篩選出指定價格的委託
                        var priceOrders = cancellableResult.Data.Where(trade =>
                        {
                            var orderPrice = (decimal)(trade.order?.price ?? 0.0);
                            return Math.Abs(orderPrice - price) < 0.01m; // 允許小數點誤差
                        }).ToList();

                        if (priceOrders.Count > 0)
                        {
                            result = await OrderService.CancelOrdersBatchAsync(priceOrders);
                        }
                        else
                        {
                            string actionText = actionFilter == "Buy" ? "買單" : "賣單";
                            _logService.LogInfo($"[智能刪單] ℹ️ 價格 {price} 沒有可刪除的{actionText}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                            return;
                        }
                    }
                    else
                    {
                        _logService.LogWarning($"[智能刪單] ❌ 查詢可刪除委託失敗: {cancellableResult.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                        return;
                    }
                }

                if (result.IsSuccess)
                {
                    string actionText = action == "All" ? "所有委託" : (action == "Buy" ? "買單" : "賣單");
                    _logService.LogInfo($"[智能刪單] ✅ 價格 {price} 的{actionText}刪除完成: 成功 {result.Data?.SuccessCount} 筆，失敗 {result.Data?.FailCount} 筆", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }
                else
                {
                    _logService.LogError($"[智能刪單] ❌ 刪單失敗: {result.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[智能刪單] 執行智能刪單失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
        }

        // 設定價格到 PriceTextBox
        public void SetPriceTextBox(decimal price)
        {
            try
            {
                ManualOrderPrice = price;
                _logService.LogInfo($"[價格設定] 💰 已設定價格: {price}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[價格設定] 設定價格失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
        }

        #endregion

        #region Position 查詢

        // 🔍 查詢當前選擇帳戶的 Position（先更新狀態再查詢）
        [RelayCommand]
        private async Task QueryAccountPositions()
        {
            try
            {
                if (SelectedAccount?.Account == null)
                {
                    MessageBox.Show("請先選擇帳戶", "Position查詢", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var account = SelectedAccount.Account;
                _logService.LogInfo($"[Position查詢] 查詢帳戶: {account.account_type}-{account.account_id}", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                // 步驟1：先更新狀態
                try
                {
                    await UpdateAllStatus();
                }
                catch (Exception updateEx)
                {
                    _logService.LogError(updateEx, "[Position查詢] 狀態更新失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    MessageBox.Show($"狀態更新失敗，無法繼續查詢: {updateEx.Message}", "Position查詢錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 步驟2：查詢 Position
                if (account.account_type.ToString() == "S")
                {
                    QueryStockPositions(account);
                }
                else if (account.account_type.ToString() == "F")
                {
                    QueryFuturePositions(account);
                }
                else
                {
                    _logService.LogWarning($"[Position查詢] 不支援的帳戶類型: {account.account_type}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[Position查詢] 查詢失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                MessageBox.Show($"查詢 Position 失敗: {ex.Message}", "Position查詢錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 🔍 查詢所有帳戶的 Position（先更新狀態再查詢）
        [RelayCommand]
        private async Task QueryAllAccountsPositions()
        {
            try
            {
                _logService.LogInfo($"[Position查詢] 開始查詢所有帳戶 Position", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                // 步驟1：先更新狀態
                try
                {
                    await UpdateAllStatus();
                }
                catch (Exception updateEx)
                {
                    _logService.LogError(updateEx, "[Position查詢] 狀態更新失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    MessageBox.Show($"狀態更新失敗，無法繼續查詢: {updateEx.Message}", "Position查詢錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 步驟2：查詢所有帳戶
                int successCount = 0;
                int failCount = 0;

                // 查詢股票帳戶
                var stockAccount = ShioajiService.StockAccount;
                if (stockAccount != null)
                {
                    try
                    {
                        QueryStockPositions(stockAccount);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        _logService.LogError(ex, "[Position查詢] 查詢股票帳戶失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                        failCount++;
                    }
                }

                // 查詢期貨帳戶
                var futureAccount = ShioajiService.FutureAccount;
                if (futureAccount != null)
                {
                    try
                    {
                        QueryFuturePositions(futureAccount);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        _logService.LogError(ex, "[Position查詢] 查詢期貨帳戶失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                        failCount++;
                    }
                }

                _logService.LogInfo($"[Position查詢] 完成 (成功: {successCount}, 失敗: {failCount})", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                MessageBox.Show($"查詢完成\n成功: {successCount} 個帳戶\n失敗: {failCount} 個帳戶", "Position查詢完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[Position查詢] 查詢所有帳戶失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                MessageBox.Show($"查詢失敗: {ex.Message}", "Position查詢錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 📈 查詢股票部位
        private void QueryStockPositions(Account account)
        {
            try
            {
                bool hasAnyPosition = false;

                // 查詢整股部位
                try
                {
                    dynamic stockPositions = ShioajiService.ListStockPositions();
                    if (stockPositions != null)
                    {
                        ProcessStockPositions(stockPositions, account, "整股");
                        hasAnyPosition = true;
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogWarning($"[Position查詢] 查詢整股部位失敗: {ex.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }

                // 查詢零股部位
                try
                {
                    dynamic sharePositions = ShioajiService.ListStockPositions(Unit.Share);
                    if (sharePositions != null)
                    {
                        ProcessStockPositions(sharePositions, account, "零股");
                        hasAnyPosition = true;
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogWarning($"[Position查詢] 查詢零股部位失敗: {ex.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }

                if (!hasAnyPosition)
                {
                    _logService.LogInfo($"[Position查詢] 股票帳戶目前沒有任何部位", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[Position查詢] 查詢股票部位失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
        }

        // 📊 查詢期貨部位
        private void QueryFuturePositions(Account account)
        {
            try
            {
                dynamic futurePositions = ShioajiService.ListFuturePositions();

                if (futurePositions == null)
                {
                    _logService.LogInfo($"[Position查詢] 期貨帳戶目前沒有任何部位", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    UpdateUIPositionFromShioaji(0, 0);
                    return;
                }

                ProcessFuturePositions(futurePositions, account);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[Position查詢] 查詢期貨部位失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
        }

        // 📈 處理股票部位資訊
        private void ProcessStockPositions(dynamic positions, Account account, string positionType = "")
        {
            try
            {
                List<StockPosition>? stockPositions = null;

                try
                {
                    stockPositions = (List<StockPosition>)positions;
                }
                catch (InvalidCastException)
                {
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(positions);
                    stockPositions = Newtonsoft.Json.JsonConvert.DeserializeObject<List<StockPosition>>(json);
                }

                if (stockPositions == null || stockPositions.Count == 0)
                {
                    return;
                }

                _logService.LogInfo($"[Position查詢] 股票{positionType}部位: {account.account_id} (共 {stockPositions.Count} 筆)", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                decimal totalPnL = 0;
                foreach (var position in stockPositions)
                {
                    totalPnL += position.pnl;
                    _logService.LogInfo($"  {position.code}: {position.direction} {position.quantity:N0}股 @ {position.price:N2} 損益:{position.pnl:+#,0;-#,0;0}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }

                _logService.LogInfo($"[Position查詢] {positionType}總損益: {totalPnL:+#,0;-#,0;0} 元", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[Position查詢] 處理股票部位失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
        }

        // 📊 處理期貨部位資訊
        private void ProcessFuturePositions(dynamic positions, Account account)
        {
            try
            {
                string currentCode = CurrentSubscribedCode;
                if (string.IsNullOrEmpty(currentCode))
                {
                    _logService.LogWarning($"[Position查詢] 當前視窗未訂閱任何商品，無法過濾部位", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    return;
                }

                var futurePositionsList = (List<FuturePosition>)positions;
                if (futurePositionsList.Count == 0)
                {
                    UpdateUIPositionFromShioaji(0, 0);
                    return;
                }

                _logService.LogInfo($"[Position查詢] 期貨部位: {account.account_id} (共 {futurePositionsList.Count} 筆)", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                bool foundMatchingPosition = false;
                long matchedNetPosition = 0;
                decimal matchedAvgCost = 0;
                decimal matchedPnL = 0;

                foreach (var position in futurePositionsList)
                {
                    string positionCode = position.code?.ToString() ?? "";
                    bool isMatching = positionCode.Equals(currentCode, StringComparison.OrdinalIgnoreCase);

                    _logService.LogInfo($"  {position.code}: {position.direction} {position.quantity}口 @ {position.price} 損益:{position.pnl} {(isMatching ? "✅" : "❌")}", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                    if (isMatching)
                    {
                        matchedNetPosition = position.direction.ToString().ToUpper() == "BUY" ? position.quantity : -position.quantity;
                        matchedAvgCost = position.price;
                        matchedPnL = position.pnl;
                        foundMatchingPosition = true;
                    }
                }

                // 更新UI
                if (foundMatchingPosition)
                {
                    _logService.LogInfo($"[Position查詢] 匹配部位 {currentCode}: 淨部位={matchedNetPosition} 成本={matchedAvgCost} 損益={matchedPnL}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    TargetPosition = matchedNetPosition;
                    ProfitLoss = matchedPnL;
                    UpdateUIPositionFromShioaji(matchedNetPosition, matchedAvgCost);
                }
                else
                {
                    _logService.LogInfo($"[Position查詢] 未找到匹配部位 {currentCode}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    TargetPosition = 0;
                    ProfitLoss = 0;
                    UpdateUIPositionFromShioaji(0, 0);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[Position查詢] 處理期貨部位失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
        }

        // 🔧 更新 UI 部位顯示
        private void UpdateUIPositionFromShioaji(long netPosition, decimal avgCost)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ActualPosition = netPosition;
                    AvgCost = avgCost;

                    if (TargetPosition == 0)
                    {
                        TargetPosition = netPosition;
                    }
                });
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[UI更新] 更新部位顯示失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
        }

        #endregion

        #region 覆寫基類方法

        protected override void OnWindowOrderCallback(string windowId, OrderDataInfo orderDataInfo)
        {
            base.OnWindowOrderCallback(windowId, orderDataInfo);

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (orderDataInfo.IsError) return;

                switch (orderDataInfo.State)
                {
                    case OrderState.StockOrder:
                    case OrderState.FuturesOrder:
                        HandleOrderReport(orderDataInfo.OrderData);
                        break;

                    case OrderState.StockDeal:
                    case OrderState.FuturesDeal:
                        HandleDealReport(orderDataInfo.OrderData);
                        break;
                }
            });
        }

        protected override void OnWindowSizeToggled(bool isExpanded)
        {
            WindowSizeToggleRequested?.Invoke(this, isExpanded);
        }

        #endregion

        #region 🔧 私有方法

        private void HandleOrderReport(dynamic orderData)
        {
            try
            {
                // ⚡ 直接存取 - 最快的方式
                string? customField = orderData.order.custom_field;
                string? ordno = orderData.order.ordno;
                string? action = orderData.order.action;
                string? opCode = orderData.operation.op_code;
                string? opMsg = orderData.operation.op_msg;

                // 取得其他需要的資訊
                string? contractCode = orderData.contract.full_code;
                string? opType = orderData.operation.op_type;
                string? price = orderData.order.price.ToString();
                string? quantity = orderData.order.quantity.ToString();

                _logService.LogInfo($"[委託回報] 委託單已送出", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                _logService.LogInfo($"  ├─ Done: {opType} {action} {contractCode} @ {price} * {quantity}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                _logService.LogInfo($"  ├─ custom_field: {customField}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                _logService.LogInfo($"  ├─ ordno: {ordno}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                _logService.LogInfo($"  ├─ op_code: {opCode}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                _logService.LogInfo($"  └─ op_msg: {opMsg}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ex)
            {
                _logService.LogWarning($"[委託回報] 動態存取失敗，資料結構可能不符預期: {ex.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[委託回報] 處理委託回報失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
        }

        private void HandleDealReport(dynamic orderData)
        {
            try
            {
                // ⚡ 直接存取 - 最快的方式
                string? customField = orderData.custom_field;
                string? code = orderData.code;
                double? price = orderData.price;
                int? quantity = orderData.quantity;
                string? action = orderData.action;

                _logService.LogInfo($"[成交回報] 委託單已成交", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                _logService.LogInfo($"  ├─ custom_field: {customField}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                _logService.LogInfo($"  ├─ code: {code}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                _logService.LogInfo($"  ├─ action: {action}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                _logService.LogInfo($"  ├─ price: {price}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                _logService.LogInfo($"  └─ quantity: {quantity}", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                if (price.HasValue && quantity.HasValue)
                {
                    decimal dealPrice = (decimal)price.Value;
                    int dealQty = quantity.Value;
                    UpdatePosition(action, dealPrice, dealQty);
                }

                if (OrderBookViewModel != null && price.HasValue && quantity.HasValue)
                {
                    decimal dealPrice = (decimal)price.Value;
                    int dealQty = quantity.Value;

                    if (action == "Buy")
                    {
                        OrderBookViewModel.UpdateBuyOrder(dealPrice, 0, dealQty);
                    }
                    else if (action == "Sell")
                    {
                        OrderBookViewModel.UpdateSellOrder(dealPrice, 0, dealQty);
                    }
                }
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ex)
            {
                _logService.LogWarning($"[成交回報] 動態存取失敗，資料結構可能不符預期: {ex.Message}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[成交回報] 處理成交回報失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
        }

        private static void CheckStopLossAndTakeProfit(decimal currentPrice)
        {
            // TODO: 實作停利停損邏輯
        }

        private void UpdateOrderPreparationContract()
        {
            try
            {
                var contract = GetCurrentContract();

                if (contract != null)
                {
                    _orderPrep.UpdateContract(contract);    //  內部會執行RebuildOrders()
                    _logService.LogInfo($"[下單準備] 已執行更新合約: {contract.target_code} ({SelectedProductType})", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }
                else
                {
                    _logService.LogWarning($"[下單準備] 無法取得視窗 {WindowId} 的合約", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[下單準備] 更新合約失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
        }

        private string GetActualCode(string productType, string exchange, string symbol)
        {
            // 優先使用 BaseViewModel 的共用屬性 CurrentSubscribedCode
            if (!string.IsNullOrEmpty(CurrentSubscribedCode))
            {
                return CurrentSubscribedCode;
            }

            return _marketService.GetActualCode(productType, exchange, symbol);
        }

        // 查詢此視窗的專屬訂閱狀況命令
        [RelayCommand]
        private void CheckWindowSubscriptionStatus()
        {
            try
            {
                _logService.LogInfo($"=== 🪟 視窗 {WindowId} 訂閱狀況查詢 ===", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                // 檢查 API 是否已登入
                if (!ShioajiService.IsApiLoggedIn)
                {
                    _logService.LogWarning("❌ API 尚未登入，無法查詢訂閱狀況", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    return;
                }

                // 獲取 SubscriptionManager
                var subscriptionManager = _marketService.SubscriptionManager;

                // 獲取此視窗的專屬訂閱
                var windowSubscriptions = subscriptionManager.GetWindowSubscriptions(WindowId);

                if (windowSubscriptions.Count == 0)
                {
                    _logService.LogInfo("📋 此視窗目前沒有任何訂閱", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    _logService.LogInfo("=========================", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    return;
                }

                _logService.LogInfo($"📊 視窗訂閱統計：共 {windowSubscriptions.Count} 個訂閱", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                _logService.LogInfo("", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                // 按商品代碼分組顯示
                var groupedByCode = windowSubscriptions
                    .GroupBy(ws => ws.ActualCode)
                    .OrderBy(g => g.Key);

                int contractIndex = 1;
                foreach (var contractGroup in groupedByCode)
                {
                    string contractCode = contractGroup.Key;
                    var subscriptions = contractGroup.ToList();

                    _logService.LogInfo($"📈 [{contractIndex}] 合約: {contractCode}", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                    // 顯示合約基本資訊
                    var firstSub = subscriptions.First();
                    _logService.LogInfo($"   ├─ 商品類型: {firstSub.ProductType}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    _logService.LogInfo($"   ├─ 交易所: {firstSub.ActualExchange}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    _logService.LogInfo($"   ├─ SecurityType: {firstSub.SecurityType}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    _logService.LogInfo($"   ├─ 漲停價: {firstSub.LimitUp}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    _logService.LogInfo($"   ├─ 跌停價: {firstSub.LimitDown}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    _logService.LogInfo($"   └─ 參考價: {firstSub.Reference}", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                    // 顯示此視窗的訂閱詳情
                    _logService.LogInfo($"   📋 此視窗的訂閱類型:", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                    foreach (var subscription in subscriptions.OrderBy(s => s.QuoteType))
                    {
                        string oddLotText = subscription.IsOddLot ? " [零股]" : " [整股]";

                        // 🎯 使用 SubscriptionManager.IsWindowSubscribed 進行特定視窗檢查
                        bool isWindowSubscribed = subscriptionManager.IsWindowSubscribed(
                            subscription.ActualCode,
                            WindowId,
                            subscription.QuoteType,
                            subscription.IsOddLot);

                        string statusIcon = isWindowSubscribed ? "✅" : "❌";

                        _logService.LogInfo($"      {statusIcon} {subscription.QuoteType}{oddLotText}", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                        // 🔍 額外資訊：檢查是否有其他視窗也訂閱了相同的合約組合
                        bool hasOtherWindowSubscriptions = subscriptionManager.HasOtherWindowSubscriptions(
                            subscription.ActualCode,
                            WindowId,
                            subscription.QuoteType,
                            subscription.IsOddLot);

                        if (hasOtherWindowSubscriptions)
                        {
                            _logService.LogInfo($"         🔗 其他視窗也有訂閱此組合", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                        }
                        else
                        {
                            _logService.LogInfo($"         🎯 僅此視窗訂閱", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                        }
                    }

                    _logService.LogInfo("", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                    contractIndex++;
                }

                // 顯示視窗專屬統計
                _logService.LogInfo("📊 視窗專屬統計:", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                _logService.LogInfo($"   ├─ 此視窗訂閱合約數: {groupedByCode.Count()}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                _logService.LogInfo($"   ├─ 此視窗訂閱組合數: {windowSubscriptions.Count}", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                // 檢查是否有任何訂閱
                bool hasAnySubscriptions = subscriptionManager.HasWindowAnySubscriptions(WindowId);
                _logService.LogInfo($"   └─ 視窗訂閱狀態: {(hasAnySubscriptions ? "有訂閱" : "無訂閱")}", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                _logService.LogInfo("=========================", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                // 🎯 示範：檢查特定合約的訂閱狀況
                if (!string.IsNullOrEmpty(CurrentSubscribedCode))
                {
                    string currentCode = CurrentSubscribedCode;
                    _logService.LogInfo($"🔍 當前合約 {currentCode} 的詳細檢查:", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                    // 檢查各種報價類型的訂閱狀況
                    var quoteTypes = new[] { QuoteType.tick, QuoteType.bidask, QuoteType.quote };
                    var oddLotOptions = new[] { false, true };

                    foreach (var quoteType in quoteTypes)
                    {
                        foreach (var isOddLot in oddLotOptions)
                        {
                            bool isSubscribed = subscriptionManager.IsWindowSubscribed(
                                currentCode, WindowId, quoteType, isOddLot);

                            if (isSubscribed)
                            {
                                string lotText = isOddLot ? "零股" : "整股";
                                _logService.LogInfo($"   ✅ {quoteType} ({lotText})", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                            }
                        }
                    }
                }

                _logService.LogInfo($"已完成視窗 {WindowId} 訂閱狀況查詢", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"查詢視窗 {WindowId} 訂閱狀況失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
        }
        public IContract? GetCurrentContract()
        {
            try
            {
                if (!string.IsNullOrEmpty(CurrentSubscribedCode))
                {
                    var contract = _marketService.SubscriptionManager.GetContractByActualCode(CurrentSubscribedCode);
                    if (contract != null)
                    {
                        _logService.LogDebug($"✅ 從 BaseViewModel.CurrentSubscribedCode 取得合約: {CurrentSubscribedCode}，打印contract {contract}", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                        return contract;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "取得當前合約失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
                return null;
            }
        }

        public void ResetOrderBookViewModel()
        {
            try
            {
                _logService.LogInfo("開始重置 OrderBookViewModel", "QuoteViewModel", LogDisplayTarget.SourceWindow);

                // 1. 釋放舊的 OrderBookViewModel
                if (OrderBookViewModel is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                // 2. 創建新的 OrderBookViewModel
                var newOrderBookViewModel = new OrderBookViewModel(WindowId)
                {
                    IsCentered = IsFiveDepthCentered  // 保持當前的置中狀態
                };

                // 3. 🔧 修復：確保 UI 線程更新並觸發 ListView 重新綁定
                Application.Current.Dispatcher.Invoke(() =>
                {
                    OrderBookViewModel = newOrderBookViewModel;
                    OnPropertyChanged(nameof(OrderBookViewModel)); // 確保 UI 綁定更新
                });

                _logService.LogInfo("已重置 OrderBookViewModel，ListView 將重新連接", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "重置 OrderBookViewModel 失敗", "QuoteViewModel", LogDisplayTarget.SourceWindow);
            }
        }

        public void SubscribeToContract(string contractCode, QuoteType quoteType)
        {
            ContractSubscriptionRequested?.Invoke(this, (contractCode, quoteType));
        }

        #endregion

        #region 🗑️ 資源釋放

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    // 清空集合
                    MarketData?.Clear();
                    TickData?.Clear();

                    // 釋放 OrderBookViewModel 資源
                    if (OrderBookViewModel is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    OrderBookViewModel = null;

                    _logService.LogInfo("QuoteViewModel 已釋放資源", "QuoteViewModel");
                }
                catch (Exception ex)
                {
                    _logService.LogError(ex, "QuoteViewModel 釋放資源時發生錯誤", "QuoteViewModel");
                }
            }

            base.Dispose(disposing);
        }

        #endregion
    }
}