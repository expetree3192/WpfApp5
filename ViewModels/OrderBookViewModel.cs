using CommunityToolkit.Mvvm.ComponentModel;
using HandyControl.Controls;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using WpfApp5.Models;
using WpfApp5.Models.MarketData;
using WpfApp5.Services;
using WpfApp5.Utils;

namespace WpfApp5.ViewModels
{
    // 📊 報價表格 ViewModel - 繼承 BaseViewModel，專注於報價表格功能
    public partial class OrderBookViewModel : BaseViewModel
    {
        #region 🔧 私有字段

        private ListView? _orderBookListView;
        private bool _isListViewInitialized = false;
        private const double FIXED_ROW_HEIGHT = 20.0;

        private readonly decimal[] _lastBidPrices = new decimal[10];
        private readonly decimal[] _lastAskPrices = new decimal[10];
        private int _lastBidCount = 0;
        private int _lastAskCount = 0;

        private decimal _lastCenteredPrice = 0;
        private readonly int _centerThresholdTicks = 4;
        private DateTime _lastCenteringTime = DateTime.MinValue;
        private readonly TimeSpan _centeringCooldown = TimeSpan.FromMilliseconds(300);

        private readonly ConcurrentDictionary<decimal, PriceRowViewModel> _priceRowLookup = new();
        private readonly ObjectPool<PriceRowViewModel> _rowPool;
        private PriceRowViewModel? _currentPriceRow;

        #endregion

        #region 🎨 計算屬性（OrderBookViewModel 特有）

        public ScrollBarVisibility ScrollBarVisibility
        {
            get
            {
                if (IsCentered || IsViewLocked)
                    return ScrollBarVisibility.Hidden;
                return ScrollBarVisibility.Auto;
            }
        }

        public decimal GetBidRatio()
        {
            if (BidTotalVolume == 0 && AskTotalVolume == 0) return 0m;
            var total = BidTotalVolume + AskTotalVolume;
            return Math.Round(((decimal)BidTotalVolume / total) * 100m, 2, MidpointRounding.AwayFromZero);
        }

        #endregion

        #region 🎯 事件

        public event Action<decimal, string>? PriceRowDoubleClicked;

        #endregion

        #region 🏗️ 建構函數

        // 修改建構函數接受 windowId 參數
        public OrderBookViewModel(string windowId) : base(windowId)
        {
            _rowPool = new ObjectPool<PriceRowViewModel>(
                createFunc: () => new PriceRowViewModel(),
                actionOnReturn: row =>
                {
                    row.Price = 0;
                    row.PriceText = string.Empty;
                    row.BidVolume = 0;
                    row.AskVolume = 0;
                    row.IsBestBid = false;
                    row.IsBestAsk = false;
                    row.IsReference = false;
                    row.IsLastTrade = false;
                    row.TickVolume = 0;
                    row.TickType = 0;
                    row.IsLimitUp = false;
                    row.IsLimitDown = false;
                    row.IsOpen = false;
                    row.IsHigh = false;
                    row.IsLow = false;
                    return true;
                }
            );

            _logService.LogDebug($"OrderBookViewModel 初始化，視窗ID: {windowId}", "OrderBookViewModel");
        }

        #endregion

        #region 🔧 覆寫 BaseViewModel 的市場數據處理方法

        protected override void OnSTKTickDataReceived(STKTickData data)
        {
            try
            {
                UpdateTradeData(data.Close, data.High, data.Low, data.Open, data.TotalVolume, data.TickType, data.Volume, data.BidSideTotalVolume, data.AskSideTotalVolume);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "處理股票成交事件失敗", "OrderBookViewModel");
            }
        }

        protected override void OnSTKBidAskDataReceived(STKBidAskData data)
        {
            try
            {
                var view = CollectionViewSource.GetDefaultView(PriceRows);
                using (view.DeferRefresh())
                {
                    UpdateBidAskData(data.BidPrices, data.BidVolumes, data.AskPrices, data.AskVolumes, data.BidTotalVolume, data.AskTotalVolume);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "處理股票五檔事件失敗", "OrderBookViewModel");
            }
        }

        protected override void OnSTKQuoteDataReceived(STKQuoteData data)
        {
            // 只在 Volume > 0 時更新即時成交資料
            if (data.Volume > 0)
            {
                // 使用 InvokeAsync 提升高頻環境下的效能
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    UpdateTradeData(data.Close, data.High, data.Low, data.Open, data.TotalVolume, data.TickType, data.Volume, data.BidSideTotalVolume, data.AskSideTotalVolume);
                }, System.Windows.Threading.DispatcherPriority.Normal);
            }

            // 無論 Volume 是否 > 0，都要更新五檔資料(使用 InvokeAsync 提升高頻環境下的效能)
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var view = CollectionViewSource.GetDefaultView(PriceRows);
                using (view.DeferRefresh())
                {
                    UpdateBidAskData(data.BidPrices, data.BidVolumes, data.AskPrices, data.AskVolumes, data.BidTotalVolume, data.AskTotalVolume);
                }
            }, System.Windows.Threading.DispatcherPriority.Normal);
        }

        protected override void OnFOPTickDataReceived(FOPTickData data)
        {
            _logService.LogDebug($"test_print data {data.Volume}", "OrderBookViewModel");
            // 使用 InvokeAsync 提升高頻環境下的效能
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                UpdateTradeData(data.Close, data.High, data.Low, data.Open, data.TotalVolume, data.TickType, data.Volume, data.BidSideTotalVolume, data.AskSideTotalVolume);
            }, System.Windows.Threading.DispatcherPriority.Normal);
            // 更新共用屬性
            LastTradePrice = data.Close;
        }

        protected override void OnFOPBidAskDataReceived(FOPBidAskData data)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // 統一批次更新所有五檔資料(使用 DeferRefresh)
                var view = CollectionViewSource.GetDefaultView(PriceRows);
                using (view.DeferRefresh())
                {
                    UpdateBidAskData(data.BidPrices, data.BidVolumes, data.AskPrices, data.AskVolumes, data.BidTotalVolume, data.AskTotalVolume);
                }
            }, System.Windows.Threading.DispatcherPriority.Normal);
        }

        protected override void OnOrderBookInitializationDataReceived(ContractInfo contractInfo, string windowId)
        {
            try
            {
                decimal limitUp = contractInfo.LimitUp ?? 0m;
                decimal limitDown = contractInfo.LimitDown ?? 0m;
                decimal reference = contractInfo.Reference ?? 0m;

                string mappedProductType = contractInfo.SecurityType switch
                {
                    "STK" => "Stocks",
                    "FUT" => "Futures",
                    "OPT" => "Options",
                    "IND" => "Indexs",
                    _ => contractInfo.ProductType
                };

                InitializeOrderBook(limitUp, limitDown, reference, mappedProductType);
                Code = contractInfo.Code;
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"處理訂單簿初始化請求時發生錯誤: {ex.Message}", "OrderBookViewModel");
            }
        }

        #endregion

        #region 屬性變更處理（OrderBookViewModel 特有）

        // 覆寫基類的虛擬方法
        protected override void OnIsCenteredChangedCore(bool value)
        {
            base.OnIsCenteredChangedCore(value); // 呼叫基類邏輯

            // OrderBookViewModel 特有的邏輯
            if (value && CurrentViewMode == ViewMode.Dynamic)
            {
                CenterToCurrentPrice();
            }
            OnPropertyChanged(nameof(ScrollBarVisibility));
        }

        // 覆寫基類的虛擬方法
        protected override void OnIsViewLockedChangedCore(bool value)
        {
            base.OnIsViewLockedChangedCore(value); // 呼叫基類邏輯

            // OrderBookViewModel 特有的邏輯
            OnPropertyChanged(nameof(ScrollBarVisibility));
        }

        #endregion

        #region 🚀 高效掛單更新方法（參考 OnFOPBidAskDataReceived 模式）

        /// <summary>
        /// 直接更新買單掛單資訊（不使用 Dispatcher.Invoke，適用於已在 UI 執行緒的情況）
        /// </summary>
        public void UpdateBuyOrder(decimal price, int pendingQty, int filledQty)
        {
            try
            {
                if (_priceRowLookup.TryGetValue(price, out var row))
                {
                    row.PendingBuyQuantity = pendingQty;
                    row.FilledBuyQuantity = filledQty;
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"直接更新買單掛單資訊失敗 (價格: {price})", "OrderBookViewModel");
            }
        }

        /// <summary>
        /// 直接更新賣單掛單資訊（不使用 Dispatcher.Invoke，適用於已在 UI 執行緒的情況）
        /// </summary>
        public void UpdateSellOrder(decimal price, int pendingQty, int filledQty)
        {
            try
            {
                if (_priceRowLookup.TryGetValue(price, out var row))
                {
                    row.PendingSellQuantity = pendingQty;
                    row.FilledSellQuantity = filledQty;
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"直接更新賣單掛單資訊失敗 (價格: {price})", "OrderBookViewModel");
            }
        }

        /// <summary>
        /// 批次清除所有掛單資訊（高效版本，參考 ResetPreviousPrices 模式）
        /// </summary>
        public void ClearAllPendingOrders()
        {
            try
            {
                // 🎯 參考 ResetPreviousPrices 的高效模式
                foreach (var kvp in _priceRowLookup)
                {
                    var row = kvp.Value;
                    // 批次重置掛單資訊
                    row.PendingBuyQuantity = 0;
                    row.FilledBuyQuantity = 0;
                    row.PendingSellQuantity = 0;
                    row.FilledSellQuantity = 0;
                }

                _logService.LogDebug($"批次清除 {_priceRowLookup.Count} 個價格的掛單資訊", "OrderBookViewModel");
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "批次清除掛單資訊失敗", "OrderBookViewModel");
            }
        }

        /// <summary>
        /// 批次更新掛單資訊（類似 UpdateBidAskData 的高效模式）
        /// </summary>
        public void UpdatePendingOrdersBatch(Dictionary<decimal, OrderService.PriceOrderDetails> orderDetails)
        {
            try
            {
                // 🎯 第一步：批次清除
                ClearAllPendingOrders();

                // 🎯 第二步：批次更新
                foreach (var kvp in orderDetails)
                {
                    var price = kvp.Key;
                    var details = kvp.Value;

                    if (_priceRowLookup.TryGetValue(price, out var row))
                    {
                        // 批次設定所有掛單資訊
                        row.PendingBuyQuantity = details.BuyPendingQuantity;
                        row.FilledBuyQuantity = details.BuyFilledQuantity;
                        row.PendingSellQuantity = details.SellPendingQuantity;
                        row.FilledSellQuantity = details.SellFilledQuantity;
                    }
                }

                _logService.LogDebug($"批次更新 {orderDetails.Count} 個價格的掛單資訊", "OrderBookViewModel");
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "批次更新掛單資訊失敗", "OrderBookViewModel");
            }
        }

        #endregion

        #region 公開方法

        public void OnPriceRowDoubleClicked(decimal price, string action)
        {
            PriceRowDoubleClicked?.Invoke(price, action);
        }

        public void SetListView(ListView listView)
        {
            try
            {
                if (listView == null)
                {
                    _logService.LogWarning("嘗試設置 null 的 ListView", "OrderBookViewModel");
                    return;
                }

                _orderBookListView = listView;
                _isListViewInitialized = true;
                _logService.LogDebug("OrderBookListView 已成功設置", "OrderBookViewModel");
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "設置 OrderBookListView 失敗", "OrderBookViewModel");
            }
        }

        public void InitializeOrderBook(decimal limitUp, decimal limitDown, decimal reference, string securityType)
        {
            try
            {
                LimitUp = limitUp;
                LimitDown = limitDown;
                Reference = reference;
                SecurityType = securityType;

                TickSize = PriceUtils.CalculatePriceTick(reference);

                _ = GenerateFullPriceRowsAsync().ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        _logService.LogError(task.Exception?.GetBaseException(), "生成完整價格行失敗", "OrderBookViewModel");
                    }
                    else
                    {
                        Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            // 🔥 新增：設定所有價格行的參考價格
                            foreach (var row in PriceRows)
                            {
                                row.ReferencePrice = reference;
                            }

                            int retryCount = 0;
                            const int maxRetries = 30;

                            System.Windows.Threading.DispatcherTimer timer = new()
                            {
                                Interval = TimeSpan.FromMilliseconds(100)
                            };

                            timer.Tick += (s, e) =>
                            {
                                retryCount++;

                                if (_isListViewInitialized && _orderBookListView != null)
                                {
                                    timer.Stop();

                                    if (_priceRowLookup.TryGetValue(reference, out var referenceRow))
                                    {
                                        _lastCenteredPrice = reference;
                                        ScrollToRowCentered(referenceRow);
                                        _logService.LogInfo($"已自動置中到參考價: {reference} (OrderBookViewModel實例)", "OrderBookViewModel");
                                    }
                                }
                                else if (retryCount >= maxRetries)
                                {
                                    timer.Stop();
                                    _logService.LogWarning($"等待 ListView 初始化超時（{maxRetries * 100}ms），跳過自動置中", "OrderBookViewModel");
                                }
                            };

                            timer.Start();
                        }, System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                }, TaskScheduler.Default);

                _logService.LogInfo($"初始化報價表格完成，Tick間距: {TickSize}", "OrderBookViewModel");
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "初始化報價表格失敗", "OrderBookViewModel");
            }
        }

        public void UpdateClosePrice(decimal close, int tickType = 0, long volume = 0)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                decimal oldClose = Close;
                Close = close;

                if (oldClose > 0 && _priceRowLookup.TryGetValue(oldClose, out var oldRow))
                {
                    oldRow.IsLastTrade = false;
                    oldRow.TickVolume = 0;
                    oldRow.TickType = 0;
                }

                if (close > 0 && _priceRowLookup.TryGetValue(close, out var newRow))
                {
                    newRow.IsLastTrade = true;
                    newRow.TickVolume = volume;
                    newRow.TickType = tickType;
                }

                if (oldClose != close && IsCentered)
                {
                    decimal targetPrice = close;
                    if (close == 0 && BidPrices.Length > 0)
                    {
                        targetPrice = BidPrices[0];
                    }
                    else if (close == 0)
                    {
                        targetPrice = Reference;
                    }

                    HandleSmartCentering(targetPrice);
                }
            });
        }

        public void GoToPrice(decimal price)
        {
            try
            {
                price = Math.Min(LimitUp, Math.Max(LimitDown, price));

                Application.Current.Dispatcher.Invoke(() =>
                {
                    IsViewLocked = true;

                    if (_priceRowLookup.TryGetValue(price, out var targetRow))
                    {
                        ScrollToRowCentered(targetRow);
                        _logService.LogInfo($"已前往價格: {price}（視圖已鎖定）", "OrderBookViewModel");
                    }
                    else
                    {
                        _logService.LogWarning($"找不到價格 {price} 的行", "OrderBookViewModel");
                    }
                });
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"前往價格 {price} 失敗", "OrderBookViewModel");
            }
        }

        public void CenterToCurrentPrice()
        {
            try
            {
                IsViewLocked = false;

                Application.Current.Dispatcher.Invoke(() =>
                {
                    decimal targetPrice = Close > 0 ? Close : Reference;

                    if (_priceRowLookup.TryGetValue(targetPrice, out var targetRow))
                    {
                        ScrollToRowCentered(targetRow);
                        _lastCenteredPrice = targetPrice;
                        _logService.LogInfo($"已回到即時價格: {targetPrice}（視圖已解鎖）", "OrderBookViewModel");
                    }
                    else
                    {
                        _logService.LogWarning($"找不到價格 {targetPrice} 的行", "OrderBookViewModel");
                    }
                });
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "回到即時價格失敗", "OrderBookViewModel");
            }
        }

        public void CenterOrderBook()
        {
            if (!IsCentered || PriceRows.Count == 0 || _priceRowLookup.IsEmpty) return;

            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    PriceRowViewModel? centerRow = null;

                    if (Close > 0 && _priceRowLookup.TryGetValue(Close, out var closeRow))
                    {
                        centerRow = closeRow;
                    }
                    else if (BidPrices.Length > 0 && AskPrices.Length > 0)
                    {
                        decimal midPrice = BidPrices[0];
                        decimal minDifference = decimal.MaxValue;
                        decimal closestPrice = 0;

                        foreach (var price in _priceRowLookup.Keys)
                        {
                            decimal difference = Math.Abs(price - midPrice);
                            if (difference < minDifference)
                            {
                                minDifference = difference;
                                closestPrice = price;
                            }
                        }

                        if (minDifference != decimal.MaxValue)
                        {
                            centerRow = _priceRowLookup[closestPrice];
                        }
                    }

                    if (centerRow != null)
                    {
                        ScrollToRow(centerRow);
                    }
                });
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "置中報價表格失敗", "OrderBookViewModel");
            }
        }

        #endregion

        #region 私有方法

        private async Task GenerateFullPriceRowsAsync()
        {
            try
            {
                var newRows = await Task.Run(() =>
                {
                    var rows = new List<PriceRowViewModel>();
                    var lookup = new Dictionary<decimal, PriceRowViewModel>();

                    if (TickSize <= 0 || LimitUp <= 0 || LimitDown >= LimitUp)
                        return (rows, lookup);

                    decimal halfTick = TickSize / 2;
                    HashSet<decimal> markerPrices = [];

                    if (High > 0) markerPrices.Add(Math.Round(High / TickSize) * TickSize);
                    if (Low > 0) markerPrices.Add(Math.Round(Low / TickSize) * TickSize);
                    if (Open > 0) markerPrices.Add(Math.Round(Open / TickSize) * TickSize);
                    if (Close > 0) markerPrices.Add(Math.Round(Close / TickSize) * TickSize);

                    decimal currentPrice = LimitUp;
                    while (currentPrice >= LimitDown)
                    {
                        var row = _rowPool.Get();
                        row.Price = currentPrice;
                        row.PriceText = PriceUtils.FormatPrice(currentPrice, PriceUtils.CalculatePriceTick(currentPrice));
                        row.IsLimitUp = (currentPrice == LimitUp);
                        row.IsLimitDown = (currentPrice == LimitDown);
                        row.IsReference = (currentPrice == Reference);
                        row.ReferencePrice = Reference;  // 設定參考價格

                        if (markerPrices.Contains(currentPrice))
                        {
                            if (High > 0 && Math.Abs(currentPrice - High) < halfTick)
                                row.IsHigh = true;

                            if (Low > 0 && Math.Abs(currentPrice - Low) < halfTick)
                                row.IsLow = true;

                            if (Open > 0 && Math.Abs(currentPrice - Open) < halfTick)
                                row.IsOpen = true;

                            if (Close > 0 && Math.Abs(currentPrice - Close) < halfTick)
                                row.IsLastTrade = true;
                        }

                        rows.Add(row);
                        lookup[currentPrice] = row;

                        currentPrice = PriceUtils.GetNextPriceDown(currentPrice);
                    }

                    return (rows, lookup);
                });

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ClearPriceRows();

                    foreach (var row in newRows.rows)
                    {
                        PriceRows.Add(row);
                    }

                    _priceRowLookup.Clear();
                    foreach (var kvp in newRows.lookup)
                    {
                        _priceRowLookup[kvp.Key] = kvp.Value;
                    }

                    _logService.LogInfo($"生成 {PriceRows.Count} 個完整價格行 (OrderBookViewModel實例)", "OrderBookViewModel");
                });
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "生成完整價格行失敗", "OrderBookViewModel");
            }
        }

        private void ClearPriceRows()
        {
            foreach (var row in PriceRows)
            {
                _rowPool.Return(row);
            }
            PriceRows.Clear();
            _priceRowLookup.Clear();
        }

        #endregion
        private void ResetPreviousPrices()
        {
            int maxCount = Math.Max(_lastBidCount, _lastAskCount);

            for (int i = 0; i < maxCount; i++)
            {
                if (i < _lastBidCount && _priceRowLookup.TryGetValue(_lastBidPrices[i], out var bidRow))
                {
                    bidRow.BidVolume = 0;
                    bidRow.IsBestBid = false;
                }

                if (i < _lastAskCount && _priceRowLookup.TryGetValue(_lastAskPrices[i], out var askRow))
                {
                    askRow.AskVolume = 0;
                    askRow.IsBestAsk = false;
                }
            }
        }

        private void UpdateTradeData(decimal close, decimal high, decimal low, decimal open, long totalVolume, int tickType, long volume, long bidSideTotalVolume = 0, long askSideTotalVolume = 0)
        {
            // 記錄舊價格
            decimal oldClose = Close;
            decimal oldHigh = High;
            decimal oldLow = Low;
            decimal oldOpen = Open;

            // 批次更新所有價格屬性（減少 PropertyChanged 觸發次數）
            Close = close;
            High = high;
            Low = low;
            Open = open;
            TickVolume = volume;
            TotalVolume = totalVolume;
            LastTradePrice = close;         //  更新全域參數 LastTradePrice

            // 更新內外盤成交總量（只在有效值時更新，避免覆蓋為 0）
            if (bidSideTotalVolume > 0 || askSideTotalVolume > 0)
            {
                BidSideTotalVolume = bidSideTotalVolume;
                AskSideTotalVolume = askSideTotalVolume;
            }

            // 更新價格標記（High/Low/Open）
            if (oldHigh != High && High > 0)
            {
                UpdatePriceMarker(oldHigh, High, (r, v) => r.IsHigh = v);
            }

            if (oldLow != Low && Low > 0)
            {
                UpdatePriceMarker(oldLow, Low, (r, v) => r.IsLow = v);
            }

            if (oldOpen != Open && Open > 0)
            {
                UpdatePriceMarker(oldOpen, Open, (r, v) => r.IsOpen = v);
            }

            // 更新成交價標記（核心邏輯）
            if (TickVolume > 0)
            {
                // 清除舊成交價標記
                if (_currentPriceRow != null)
                {
                    _currentPriceRow.IsLastTrade = false;
                    _currentPriceRow.TickVolume = 0;
                    _currentPriceRow.TickType = 0;
                }
                // 設置新成交價標記
                if (_priceRowLookup.TryGetValue(Close, out var newRow))
                {
                    newRow.IsLastTrade = true;
                    newRow.TickVolume = volume;
                    newRow.TickType = tickType;
                    _currentPriceRow = newRow;

                    // 智能置中邏輯（帶閥值控制）
                    if (IsCentered && !IsViewLocked)
                    {
                        HandleSmartCentering(Close);
                    }
                }
            }
        }

        private void UpdateBidAskData(decimal[] bidPrices, int[] bidVolumes, decimal[] askPrices, int[] askVolumes, int bidTotal, int askTotal)
        {
            BidTotalVolume = bidTotal;      //  更新全域參數 BidTotalVolume
            AskTotalVolume = askTotal;      //  更新全域參數 AskTotalVolume
            BestBidPrice = bidPrices[0];    //  更新全域參數 BestBidPrice
            BestAskPrice = askPrices[0];    //  更新全域參數 BestAskPrice

            ResetPreviousPrices();
            UpdatePrices(bidPrices, bidVolumes, isBid: true);
            UpdatePrices(askPrices, askVolumes, isBid: false);

            if (bidPrices.Length > 0 && askPrices.Length > 0)
            {
                BidPrices = [bidPrices[0]];
                AskPrices = [askPrices[0]];
            }
        }

        private void UpdatePrices(decimal[] prices, int[] volumes, bool isBid)
        {
            int count = Math.Min(prices.Length, 10);
            var lastPrices = isBid ? _lastBidPrices : _lastAskPrices;

            for (int i = 0; i < count; i++)
            {
                decimal price = prices[i];
                if (price <= 0) continue;

                if (_priceRowLookup.TryGetValue(price, out var row))
                {
                    if (isBid)
                    {
                        row.BidVolume = volumes[i];
                        row.IsBestBid = (i == 0);
                    }
                    else
                    {
                        row.AskVolume = volumes[i];
                        row.IsBestAsk = (i == 0);
                    }
                }

                lastPrices[i] = price;
            }

            if (isBid)
                _lastBidCount = count;
            else
                _lastAskCount = count;
        }

        private void UpdatePriceMarker(decimal oldPrice, decimal newPrice, Action<PriceRowViewModel, bool> setMarker)
        {
            if (oldPrice == newPrice) return;

            if (oldPrice > 0 && _priceRowLookup.TryGetValue(oldPrice, out var oldRow))
            {
                setMarker(oldRow, false);
            }

            if (newPrice > 0 && _priceRowLookup.TryGetValue(newPrice, out var newRow))
            {
                setMarker(newRow, true);
            }
        }

        private void HandleSmartCentering(decimal targetPrice)
        {
            if (!IsCentered || IsViewLocked || PriceRows.Count == 0)
                return;

            try
            {
                decimal priceDifference = Math.Abs(targetPrice - _lastCenteredPrice);
                int ticksDifference = (int)(priceDifference / TickSize);

                if (ticksDifference >= _centerThresholdTicks)
                {
                    var now = DateTime.Now;

                    if (now - _lastCenteringTime >= _centeringCooldown)
                    {
                        if (_priceRowLookup.TryGetValue(targetPrice, out var targetRow))
                        {
                            ScrollToRowCentered(targetRow);
                            _lastCenteredPrice = targetPrice;
                            _lastCenteringTime = now;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "智能置中失敗", "OrderBookViewModel");
            }
        }

        private void ScrollToRow(PriceRowViewModel row)
        {
            try
            {
                if (!_isListViewInitialized || _orderBookListView == null)
                {
                    _logService.LogDebug("OrderBookListView 未初始化，跳過滾動", "OrderBookViewModel");
                    return;
                }

                if (row == null)
                {
                    _logService.LogWarning("目標行為 null，無法滾動", "OrderBookViewModel");
                    return;
                }

                if (!PriceRows.Contains(row))
                {
                    _logService.LogWarning($"目標行 (價格: {row.Price}) 不在 PriceRows 集合中", "OrderBookViewModel");
                    return;
                }

                ScrollToRowCentered(row);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "滾動到指定行失敗", "OrderBookViewModel");
            }
        }

        private void ScrollToRowCentered(PriceRowViewModel row)
        {
            try
            {
                if (_orderBookListView == null || row == null)
                {
                    _logService.LogWarning("ListView 或目標行為 null", "OrderBookViewModel");
                    return;
                }

                int targetIndex = PriceRows.IndexOf(row);
                if (targetIndex < 0)
                {
                    _logService.LogWarning($"找不到價格 {row.Price} 的索引", "OrderBookViewModel");
                    return;
                }

                // 由於行高從 22 減少到 20，可能需要調整 OFFSET_ROWS
                const int OFFSET_ROWS = 10;  // 從原本的 9 調整為 10
                int scrollToIndex = Math.Max(0, targetIndex - OFFSET_ROWS);
                double targetOffset = scrollToIndex * FIXED_ROW_HEIGHT;

                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var scrollViewer = FindVisualChild<System.Windows.Controls.ScrollViewer>(_orderBookListView);
                    if (scrollViewer == null)
                    {
                        _logService.LogWarning("找不到 ScrollViewer，使用 ScrollIntoView 備用方案", "OrderBookViewModel");

                        if (scrollToIndex < PriceRows.Count)
                        {
                            var scrollToRow = PriceRows[scrollToIndex];
                            _orderBookListView.ScrollIntoView(scrollToRow);
                            _orderBookListView.SelectedItem = null;
                        }
                        return;
                    }

                    scrollViewer.ScrollToVerticalOffset(targetOffset);

                }, System.Windows.Threading.DispatcherPriority.Loaded);

                _orderBookListView.SelectedItem = null;
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "置中滾動失敗", "OrderBookViewModel");
            }
        }

        private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                    return typedChild;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }

            return null;
        }

        #region 資源釋放

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    ClearPriceRows();
                    _logService.LogInfo($"OrderBookViewModel 已釋放: {Symbol}", "OrderBookViewModel");
                }
                catch (Exception ex)
                {
                    _logService.LogError(ex, "釋放 OrderBookViewModel 失敗", "OrderBookViewModel");
                }
            }

            base.Dispose(disposing);
        }

        #endregion
    }
}
