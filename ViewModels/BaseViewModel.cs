using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Sinopac.Shioaji;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using WpfApp5.Models;
using WpfApp5.Models.MarketData;
using WpfApp5.Services;
using WpfApp5.Services.Common;

namespace WpfApp5.ViewModels
{
    // ViewModel 基礎類別 - 提供共用功能和屬性，統一處理市場數據
    // 抑制 CA1822 警告，因為這些屬性需要支援 WPF 數據綁定
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "WPF data binding requires instance properties")]
    public abstract partial class BaseViewModel : ObservableObject, IDisposable
    {
        #region 全域服務存取
        protected GlobalTimeService TimeService => App.TimeService; // 全域時間服務 - 保持非靜態以支援數據綁定
        protected readonly LogService _logService = LogService.Instance;
        protected readonly MarketService _marketService = MarketService.Instance;
        protected readonly OrderPreparationService _orderPrep;
        #endregion

        #region 時間相關屬性

        public DateTime CurrentDateTime => TimeService.CurrentDateTime; // 當前日期時間
        public string TimeOnly => TimeService.TimeOnly; // 僅顯示時間 (HH:mm:ss)
        public string DateOnly => TimeService.DateOnly; // 僅顯示日期 (MM/dd)
        public string FullDateTime => TimeService.FullDateTime; // 完整日期時間
        public string TradingTime => TimeService.TradingTime;   // 交易用時間格式
        public string FileNameTime => TimeService.FileNameTime; // 檔案名稱用時間格式

        #endregion

        #region 視窗與識別
        private readonly string _windowId;
        public string WindowId => _windowId;
        [ObservableProperty] private string _currentSubscribedCode = "";
        [ObservableProperty] private bool _isWindowExpanded = false;
        [ObservableProperty] private bool _isLeftTopVisible = true;
        [ObservableProperty] private bool _isRightPanelVisible = true;
        #endregion

        #region 市場數據共享屬性
        [ObservableProperty] private long _tickVolume;
        [ObservableProperty] private long _totalVolume;
        [ObservableProperty] private decimal _limitUp;
        [ObservableProperty] private decimal _limitDown;
        [ObservableProperty] private decimal _reference;
        [ObservableProperty] private decimal _lastTradePrice = 0;
        [ObservableProperty] private decimal _bestBidPrice = 0;
        [ObservableProperty] private decimal _bestAskPrice = 0;
        [ObservableProperty] private int _bestBidVolume;
        [ObservableProperty] private int _bestAskVolume;
        [ObservableProperty] private DateTime _tradeTime = DateTime.MinValue;
        [ObservableProperty] private string _tradeDataTime = "";
        #endregion

        #region 報價表格共用屬性
        [ObservableProperty] private string _symbol = "";
        [ObservableProperty] private string _code = "";
        [ObservableProperty] private decimal _open;
        [ObservableProperty] private decimal _high;
        [ObservableProperty] private decimal _low;
        [ObservableProperty] private decimal _close;
        [ObservableProperty] private decimal _tickSize;
        [ObservableProperty] private ObservableCollection<PriceRowViewModel> _priceRows = [];
        [ObservableProperty] private decimal[] _bidPrices = [];
        [ObservableProperty] private decimal[] _askPrices = [];
        [ObservableProperty] private int _bidTotalVolume;
        [ObservableProperty] private int _askTotalVolume;
        [ObservableProperty] private long _bidSideTotalVolume;
        [ObservableProperty] private long _askSideTotalVolume;
        [ObservableProperty] private DateTime _timeNow = DateTime.Now;
        [ObservableProperty] private bool _isCentered = true;
        [ObservableProperty] private bool _isViewLocked = false;
        [ObservableProperty] private string _securityType = "";
        [ObservableProperty] private ViewMode _currentViewMode = ViewMode.Dynamic;
        [ObservableProperty] private int _visibleRowsCount = 19;
        #endregion
        #region BidAsk 五檔屬性
        [ObservableProperty] private decimal _bidPrice1 = 0;
        [ObservableProperty] private decimal _bidPrice2 = 0;
        [ObservableProperty] private decimal _bidPrice3 = 0;
        [ObservableProperty] private decimal _bidPrice4 = 0;
        [ObservableProperty] private decimal _bidPrice5 = 0;
        [ObservableProperty] private int _bidVolume1 = 0;
        [ObservableProperty] private int _bidVolume2 = 0;
        [ObservableProperty] private int _bidVolume3 = 0;
        [ObservableProperty] private int _bidVolume4 = 0;
        [ObservableProperty] private int _bidVolume5 = 0;
        [ObservableProperty] private decimal _askPrice1 = 0;
        [ObservableProperty] private decimal _askPrice2 = 0;
        [ObservableProperty] private decimal _askPrice3 = 0;
        [ObservableProperty] private decimal _askPrice4 = 0;
        [ObservableProperty] private decimal _askPrice5 = 0;
        [ObservableProperty] private int _askVolume1 = 0;
        [ObservableProperty] private int _askVolume2 = 0;
        [ObservableProperty] private int _askVolume3 = 0;
        [ObservableProperty] private int _askVolume4 = 0;
        [ObservableProperty] private int _askVolume5 = 0;
        [ObservableProperty] private int _diffBidVolume1 = 0;
        [ObservableProperty] private int _diffBidVolume2 = 0;
        [ObservableProperty] private int _diffBidVolume3 = 0;
        [ObservableProperty] private int _diffBidVolume4 = 0;
        [ObservableProperty] private int _diffBidVolume5 = 0;
        [ObservableProperty] private int _diffAskVolume1 = 0;
        [ObservableProperty] private int _diffAskVolume2 = 0;
        [ObservableProperty] private int _diffAskVolume3 = 0;
        [ObservableProperty] private int _diffAskVolume4 = 0;
        [ObservableProperty] private int _diffAskVolume5 = 0;
        #endregion

        #region 報價表格共用計算屬性

        public decimal InnerPercent
        {
            get
            {
                if (BidTotalVolume + AskTotalVolume == 0) return 0m;    // 防止分母為0時的錯誤(防止除零錯誤)
                return Math.Round(((decimal)BidTotalVolume / (BidTotalVolume + AskTotalVolume)) * 100m, 2, MidpointRounding.AwayFromZero);
            }
        }

        public decimal OuterPercent
        {
            get
            {
                if (BidTotalVolume + AskTotalVolume == 0) return 0m;    // 防止分母為0時的錯誤(防止除零錯誤)
                return Math.Round(((decimal)AskTotalVolume / (BidTotalVolume + AskTotalVolume)) * 100m, 2, MidpointRounding.AwayFromZero);
            }
        }

        public int BidAskTotalVolumeDifference
        {
            get
            {
                return BidTotalVolume - AskTotalVolume;
            }
        }

        public decimal BidAskTotalVolumeDifferencePercent
        {
            get
            {
                int total = BidTotalVolume + AskTotalVolume;
                if (total == 0) return 0m;  // 防止分母為0時的錯誤(防止除零錯誤)
                return Math.Round(((decimal)BidAskTotalVolumeDifference / total) * 100m, 2, MidpointRounding.AwayFromZero);
            }
        }

        public string InnerDealPercent
        {
            get
            {
                if (BidSideTotalVolume + AskSideTotalVolume == 0) return "0.0%";
                return $"{(decimal)BidSideTotalVolume / (BidSideTotalVolume + AskSideTotalVolume) * 100:F1}%";
            }
        }

        public string OuterDealPercent
        {
            get
            {
                if (BidSideTotalVolume + AskSideTotalVolume == 0) return "0.0%";
                return $"{(decimal)AskSideTotalVolume / (BidSideTotalVolume + AskSideTotalVolume) * 100:F1}%";
            }
        }

        public string ViewModeText => CurrentViewMode == ViewMode.Dynamic ? "Dynamic" : "Full";

        #endregion

        #region 報價表格共用枚舉

        public enum ViewMode
        {
            Dynamic,
            Full
        }

        #endregion

        #region 帳戶管理（共用）

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AccountStatusText))]
        [NotifyPropertyChangedFor(nameof(AccountStatusColor))]
        [NotifyPropertyChangedFor(nameof(AccountStatusIcon))]
        private AccountDisplayModel? _selectedAccount;

        public ObservableCollection<AccountDisplayModel> AvailableAccounts { get; } = [];

        public string AccountStatusText => SelectedAccount?.StatusText ?? "未選擇帳戶";
        public Brush AccountStatusColor => SelectedAccount?.StatusColor ?? Brushes.Gray;
        public string AccountStatusIcon => SelectedAccount?.StatusIcon ?? "?";

        #endregion

        #region 交易參數（共用）

        [ObservableProperty]
        private string _selectedProductType = "";

        [ObservableProperty]
        private string _selectedExchange = "";

        [ObservableProperty]
        private string _selectedPriceType = "LMT";

        [ObservableProperty]
        private string _selectedOrderType = "ROD";

        [ObservableProperty]
        private string _selectedOcType = "Auto";

        [ObservableProperty]
        private int _orderQuantity = 1;

        [ObservableProperty]
        private decimal? _manualOrderPrice = null;

        // 共用集合
        public ObservableCollection<string> ProductTypes { get; } = ["Stocks", "Futures", "Options", "Indexs"];
        public ObservableCollection<string> AvailableExchanges { get; } = ["TSE", "OTC", "TXF", "MXF", "CDF", "TXO"];
        public ObservableCollection<string> PriceTypes { get; } = ["LMT", "MKT", "MKP"];
        public ObservableCollection<string> OrderTypes { get; } = ["ROD", "IOC", "FOK"];
        public ObservableCollection<string> OcTypes { get; } = ["Auto", "New", "Cover", "DayTrade"];

        #endregion

        #region 即時損益（共用）
        [ObservableProperty] private decimal _profitLoss = 0;
        [ObservableProperty] private decimal _profitLossPercent = 0;
        [ObservableProperty] private decimal _avgCost = 0;
        [ObservableProperty] private long _targetPosition = 0;
        [ObservableProperty] private long _actualPosition = 0;
        #endregion

        #region 委託單統計（共用）
        [ObservableProperty] private long _pendingBuyOrders = 0;  // 掛單中的買單數量（不是筆數）
        [ObservableProperty] private long _pendingSellOrders = 0; // 掛單中的賣單數量（不是筆數）
        [ObservableProperty] private long _pendingBuyPrice = 0;  // 掛單中的買單價格
        [ObservableProperty] private long _pendingSellPrice = 0;  // 掛單中的賣單價格
        [ObservableProperty] private long _filledBuyOrders = 0;   // 已成交的買單數量
        [ObservableProperty] private long _filledSellOrders = 0;  // 已成交的賣單數量
        [ObservableProperty] private long _totalBuyQuantity = 0;  // 已成交買單總量
        [ObservableProperty] private long _totalSellQuantity = 0; // 已成交賣單總量
        [ObservableProperty] private long _pendingBuyOrderCount = 0;  // 掛單中的買單筆數
        [ObservableProperty] private long _pendingSellOrderCount = 0; // 掛單中的賣單筆數
        #endregion

        #region 當沖和委託條件

        private bool _isUpdatingDayTrade = false;
        private bool _isUpdatingOcType = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DayTradeButtonText))]
        [NotifyPropertyChangedFor(nameof(DayTradeTextColor))]
        [NotifyPropertyChangedFor(nameof(DayTradeBorderBrush))]
        [NotifyPropertyChangedFor(nameof(DayTradeTooltip))]
        [NotifyPropertyChangedFor(nameof(DayTradeTooltipDetail))]
        private bool _isDayTradeEnabled = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(OrderCondText))]
        [NotifyPropertyChangedFor(nameof(OrderCondColor))]
        [NotifyPropertyChangedFor(nameof(OrderCondStatusText))]
        private StockOrderCond _selectedOrderCond = StockOrderCond.Cash;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(OrderLotTypeText))]
        [NotifyPropertyChangedFor(nameof(OrderLotTypeColor))]
        private StockOrderLot _orderLotType = StockOrderLot.Common;

        #endregion

        #region UI 顯示屬性（共用）

        public string DayTradeButtonText => SelectedProductType == "Stocks" ? "先賣" : "當沖";
        public Brush DayTradeTextColor => IsDayTradeEnabled
            ? new SolidColorBrush(Color.FromRgb(255, 215, 0))
            : new SolidColorBrush(Color.FromRgb(128, 128, 128));
        public Brush DayTradeBorderBrush => IsDayTradeEnabled
            ? new SolidColorBrush(Color.FromRgb(255, 215, 0))
            : new SolidColorBrush(Color.FromRgb(128, 128, 128));
        public string DayTradeTooltip
        {
            get
            {
                if (SelectedProductType == "Stocks")
                {
                    return IsDayTradeEnabled ? "已啟用：先賣後買當沖" : "未啟用：一般交易";
                }
                else
                {
                    return IsDayTradeEnabled ? "已啟用：當日沖銷" : "未啟用：自動判斷";
                }
            }
        }

        public string DayTradeTooltipDetail
        {
            get
            {
                if (SelectedProductType == "Stocks")
                {
                    return IsDayTradeEnabled ? "參數：daytrade_short = Yes" : "參數：daytrade_short = No";
                }
                else
                {
                    return IsDayTradeEnabled ? "參數：octype = DayTrade" : "參數：octype = Auto";
                }
            }
        }

        public bool IsStockProduct => SelectedProductType == "Stocks";

        public string OrderCondText => SelectedOrderCond switch
        {
            StockOrderCond.Cash => "現股",
            StockOrderCond.MarginTrading => "融資",
            StockOrderCond.ShortSelling => "融券",
            _ => "現股"
        };

        public Brush OrderCondColor => SelectedOrderCond switch
        {
            StockOrderCond.Cash => new SolidColorBrush(Color.FromRgb(255, 215, 0)),
            StockOrderCond.MarginTrading => new SolidColorBrush(Color.FromRgb(255, 99, 71)),
            StockOrderCond.ShortSelling => new SolidColorBrush(Color.FromRgb(50, 205, 50)),
            _ => new SolidColorBrush(Color.FromRgb(255, 215, 0))
        };

        public string OrderCondStatusText => SelectedOrderCond switch
        {
            StockOrderCond.Cash => "現股交易（一般買賣）",
            StockOrderCond.MarginTrading => "融資交易（借錢買股）",
            StockOrderCond.ShortSelling => "融券交易（借券賣出）",
            _ => "現股交易"
        };

        public string OrderLotTypeText => OrderLotType switch
        {
            StockOrderLot.Common => "整股",
            StockOrderLot.IntradayOdd => "盤中零股",
            StockOrderLot.Odd => "盤後零股",
            StockOrderLot.Fixing => "定盤",
            _ => "整股"
        };

        public Brush OrderLotTypeColor => OrderLotType switch
        {
            StockOrderLot.Common => new SolidColorBrush(Color.FromRgb(255, 215, 0)),
            StockOrderLot.IntradayOdd => new SolidColorBrush(Color.FromRgb(100, 149, 237)),
            StockOrderLot.Odd => new SolidColorBrush(Color.FromRgb(147, 112, 219)),
            StockOrderLot.Fixing => new SolidColorBrush(Color.FromRgb(255, 140, 0)),
            _ => Brushes.Yellow
        };

        public string LeftTopButtonText => IsLeftTopVisible ? "▲" : "▼";
        public string RightPanelButtonText => IsRightPanelVisible ? "Hide" : "Show";
        public string WindowSizeButtonText => IsWindowExpanded ? "◀◀" : "▶▶";

        #endregion

        #region 建構函數

        protected BaseViewModel(string windowId)
        {
            if (string.IsNullOrWhiteSpace(windowId))
            {
                throw new ArgumentException("WindowId 不可為空", nameof(windowId));
            }

            _windowId = windowId;
            _orderPrep = new OrderPreparationService(windowId);

            // 🔧 統一訂閱市場數據事件
            _marketService.STK_TickReceived += OnSTKTickReceived;
            _marketService.STK_BidAskReceived += OnSTKBidAskReceived;
            _marketService.STK_QuoteReceived += OnSTKQuoteReceived;
            _marketService.FOP_TickReceived += OnFOPTickReceived;
            _marketService.FOP_BidAskReceived += OnFOPBidAskReceived;
            _marketService.OrderBookInitializationRequested += OnOrderBookInitializationRequested;

            SubscribeToTimeService();   // 訂閱時間服務事件
            OrderService.OrderStatsUpdateRequested += OnOrderStatsUpdateRequested;  // 訂閱委託統計更新事件
            _logService.LogInfo($"[統計更新] ✅ 事件訂閱成功 - 視窗: {WindowId}", GetType().Name, LogDisplayTarget.DebugOutput);
            OrderService.Instance.WindowOrderCallback += OnWindowOrderCallback; // 訂閱共用事件
            
            _logService.LogDebug($"BaseViewModel 初始化，視窗ID: {windowId}", GetType().Name, LogDisplayTarget.DebugOutput);
            LoadAccountsFromService();  // 載入帳戶
        }

        #endregion

        #region 時間服務整合

        // 訂閱時間服務事件
        private void SubscribeToTimeService()
        {
            try
            {
                TimeService.PropertyChanged += OnTimeServicePropertyChanged;    // 訂閱時間服務的屬性變化
                TimeService.TradingTimeStatusChanged += OnTradingTimeStatusChanged; // 訂閱交易時間狀態變化（如果子類別需要）

                System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] 已訂閱時間服務事件");
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "訂閱時間服務事件失敗", GetType().Name, LogDisplayTarget.DebugOutput);
            }
        }

        // 時間服務屬性變化處理
        private void OnTimeServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            try
            {
                // 當時間服務的時間更新時，通知相關屬性變化
                switch (e.PropertyName)
                {
                    case nameof(GlobalTimeService.CurrentDateTime):
                        OnPropertyChanged(nameof(CurrentDateTime));
                        break;

                    case nameof(GlobalTimeService.TimeOnly):
                        OnPropertyChanged(nameof(TimeOnly));
                        break;

                    case nameof(GlobalTimeService.DateOnly):
                        OnPropertyChanged(nameof(DateOnly));
                        break;

                    case nameof(GlobalTimeService.FullDateTime):
                        OnPropertyChanged(nameof(FullDateTime));
                        break;

                    case nameof(GlobalTimeService.TradingTime):
                        OnPropertyChanged(nameof(TradingTime));
                        break;

                    case nameof(GlobalTimeService.FileNameTime):
                        OnPropertyChanged(nameof(FileNameTime));
                        break;
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "處理時間服務屬性變化失敗", GetType().Name, LogDisplayTarget.DebugOutput);
            }
        }

        // 交易時間狀態變化處理（子類別可覆寫）
        protected virtual void OnTradingTimeStatusChanged(bool isTradingTime)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[{GetType().Name}] 交易時間狀態變化: {(isTradingTime ? "進入" : "離開")}交易時間");

                // 記錄交易時間變化
                string statusText = isTradingTime ? "進入交易時間" : "離開交易時間";
                _logService.LogInfo($"[時間] {statusText} - {TimeOnly}", GetType().Name, LogDisplayTarget.SourceWindow);

                OnTradingTimeChanged(isTradingTime);    // 呼叫虛擬方法，供子類別進行特殊處理
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "處理交易時間狀態變化失敗", GetType().Name, LogDisplayTarget.DebugOutput);
            }
        }

        // 供子類別覆寫的交易時間變化處理
        protected virtual void OnTradingTimeChanged(bool isTradingTime)
        {
            // 預設實作：無操作，子類別可覆寫進行特殊處理
        }

        #endregion

        #region 🔥 時間工具方法

        // 判斷時間間隔是否超過指定秒數
        protected bool IsTimeIntervalExceeded(DateTime startTime, double intervalSeconds)
        {
            return TimeService.IsTimeIntervalExceeded(startTime, intervalSeconds);
        }

        // 判斷是否在指定時間範圍內
        protected bool IsWithinTimeRange(string startTime, string endTime)
        {
            return TimeService.IsWithinTimeRange(startTime, endTime);
        }

        #endregion

        #region 下單統計更新事件處理

        // 處理委託統計更新事件
        private void OnOrderStatsUpdateRequested(OrderStatsUpdateEventArgs args)
        {
            try
            {
                // 1. 檢查是否為自己的視窗
                if (!args.WindowIds.Contains(WindowId))
                {
                    _logService.LogDebug($"[統計更新] ❌ 視窗 ID 不匹配 (視窗:{WindowId}, 事件視窗:{string.Join(",", args.WindowIds)})", GetType().Name, LogDisplayTarget.DebugOutput);
                    return; // 不是自己的視窗，忽略
                }

                _logService.LogInfo($"[統計更新] ✅ 視窗 ID 匹配: {WindowId}", GetType().Name, LogDisplayTarget.SourceWindow);

                // 2. 檢查合約代碼是否匹配
                if (args.ContractCode != CurrentSubscribedCode)
                {
                    _logService.LogDebug($"[統計更新] ❌ 合約代碼不匹配 (視窗:{CurrentSubscribedCode}, 事件:{args.ContractCode})", GetType().Name, LogDisplayTarget.DebugOutput);
                    return;
                }

                _logService.LogInfo($"[統計更新] ✅ 合約代碼匹配: {CurrentSubscribedCode}", GetType().Name, LogDisplayTarget.SourceWindow);

                // 3. 檢查操作是否成功（委託回報才需要檢查）
                if (args.IsOrderReport && !args.IsSuccess)
                {
                    _logService.LogInfo($"[統計更新] ❌ 操作失敗，跳過統計更新 (op_code: {args.OpCode})", GetType().Name, LogDisplayTarget.SourceWindow);
                    return;
                }

                _logService.LogInfo($"[統計更新] ✅ 操作成功，準備更新統計", GetType().Name, LogDisplayTarget.SourceWindow);

                // 直接更新統計（因為已經在 UI 線程中）
                _logService.LogInfo($"[統計更新] 🚀 直接更新統計（已在 UI 線程）", GetType().Name, LogDisplayTarget.SourceWindow);
                UpdateOrderStatsFromEvent(args);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"處理委託統計更新事件失敗: {args}", GetType().Name, LogDisplayTarget.SourceWindow);
            }
        }

        // 根據事件更新委託統計
        private void UpdateOrderStatsFromEvent(OrderStatsUpdateEventArgs args)
        {
            try
            {
                _logService.LogInfo($"[統計更新] 🔄 開始更新統計: {args}", GetType().Name, LogDisplayTarget.SourceWindow);

                if (args.IsOrderReport)
                {
                    HandleOrderReport(args);
                }
                else if (args.IsDealReport)
                {
                    HandleDealReport(args);
                }

                // 🎯 直接使用強制更新
                _ = Task.Run(() => SyncDetailedPendingOrdersToOrderBookAsync(args.ContractCode, forceUpdate: true));
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[統計更新] 更新統計失敗", GetType().Name, LogDisplayTarget.SourceWindow);
            }
        }

        // 處理委託回報 - 更新目標部位和掛單統計
        private void HandleOrderReport(OrderStatsUpdateEventArgs args)
        {
            switch (args.OpType.ToUpper())
            {
                case "NEW":
                    // 新單：更新目標部位和掛單
                    var oldTargetPosition = TargetPosition;

                    if (args.IsBuy)
                    {
                        TargetPosition += args.Quantity;  // 🔧 目標部位增加
                        PendingBuyOrders += args.Quantity;
                        _logService.LogInfo($"[統計更新] 📈 買單委託: 目標部位 {oldTargetPosition}→{TargetPosition}, 掛單 +{args.Quantity}", GetType().Name, LogDisplayTarget.SourceWindow);
                    }
                    else
                    {
                        TargetPosition -= args.Quantity;  // 🔧 目標部位減少
                        PendingSellOrders += args.Quantity;
                        _logService.LogInfo($"[統計更新] 📉 賣單委託: 目標部位 {oldTargetPosition}→{TargetPosition}, 掛單 +{args.Quantity}", GetType().Name, LogDisplayTarget.SourceWindow);
                    }
                    break;

                case "CANCEL":
                    // 刪單：恢復目標部位，減少掛單
                    var oldCancelTargetPosition = TargetPosition;

                    if (args.IsBuy)
                    {
                        TargetPosition = Math.Max(ActualPosition, TargetPosition - args.CancelQuantity);  // 🔧 目標部位不能低於實際部位
                        PendingBuyOrders = Math.Max(0L, PendingBuyOrders - args.CancelQuantity);
                        _logService.LogInfo($"[統計更新] ❌ 買單刪除: 目標部位 {oldCancelTargetPosition}→{TargetPosition}, 掛單 -{args.CancelQuantity}", GetType().Name, LogDisplayTarget.SourceWindow);
                    }
                    else
                    {
                        TargetPosition = Math.Min(ActualPosition, TargetPosition + args.CancelQuantity);  // 🔧 目標部位不能高於實際部位
                        PendingSellOrders = Math.Max(0L, PendingSellOrders - args.CancelQuantity);
                        _logService.LogInfo($"[統計更新] ❌ 賣單刪除: 目標部位 {oldCancelTargetPosition}→{TargetPosition}, 掛單 -{args.CancelQuantity}", GetType().Name, LogDisplayTarget.SourceWindow);
                    }
                    break;

                case "UPDATEPRICE":
                case "UPDATEQTY":
                    // 改單：目前只記錄，不影響部位
                    _logService.LogInfo($"[統計更新] 🔄 改單操作 {(args.IsBuy ? "買" : "賣")} {args.Quantity}", GetType().Name, LogDisplayTarget.SourceWindow);
                    break;
            }
        }

        // 處理成交回報 - 更新實際成交部位和成交統計
        private void HandleDealReport(OrderStatsUpdateEventArgs args)
        {
            var oldActualPosition = ActualPosition;

            if (args.IsBuy)
            {
                // 買單成交：實際部位增加
                ActualPosition += args.Quantity;  // 🔧 實際成交部位增加

                // 更新統計
                PendingBuyOrders = Math.Max(0, PendingBuyOrders - args.Quantity);
                FilledBuyOrders += args.Quantity;
                TotalBuyQuantity += args.Quantity;

                _logService.LogInfo($"[統計更新] 💰 買單成交: 實際部位 {oldActualPosition}→{ActualPosition} (+{args.Quantity})", GetType().Name, LogDisplayTarget.SourceWindow);
            }
            else
            {
                // 賣單成交：實際部位減少
                ActualPosition -= args.Quantity;  // 🔧 實際成交部位減少（賣出為負）

                // 更新統計
                PendingSellOrders = Math.Max(0, PendingSellOrders - args.Quantity);
                FilledSellOrders += args.Quantity;
                TotalSellQuantity += args.Quantity;

                _logService.LogInfo($"[統計更新] 💰 賣單成交: 實際部位 {oldActualPosition}→{ActualPosition} (-{args.Quantity})", GetType().Name, LogDisplayTarget.SourceWindow);
            }

            // 顯示部位差異
            var positionDifference = TargetPosition - ActualPosition;
            string statusText = positionDifference == 0 ? "✅ 已達目標" :
                               positionDifference > 0 ? $"📈 待買進 {positionDifference}" :
                                                       $"📉 待賣出 {Math.Abs(positionDifference)}";

            _logService.LogInfo($"[統計更新] 📊 目標部位: {TargetPosition}, 實際部位: {ActualPosition}, 差異: {statusText}", GetType().Name, LogDisplayTarget.SourceWindow);
        }

        #endregion
        #region 掛單資訊同步方法

        // 同步掛單資訊到 OrderBook UI
        protected async Task SyncDetailedPendingOrdersToOrderBookAsync(string contractCode, bool forceUpdate = false)
        {
            try
            {
                _logService.LogInfo($"[掛單同步] 🔄 開始高效同步合約 {contractCode} 的掛單 (強制更新: {forceUpdate})...", GetType().Name, LogDisplayTarget.SourceWindow);

                var detailsResult = OrderService.GetContractDetailedPendingOrdersSync(contractCode, forceUpdateStatus: forceUpdate);

                if (!detailsResult.IsSuccess || detailsResult.Data == null)
                {
                    _logService.LogWarning($"[掛單同步] ❌ 查詢詳細掛單失敗: {detailsResult.Message}", GetType().Name, LogDisplayTarget.SourceWindow);
                    return;
                }

                _logService.LogInfo($"[掛單同步] 📊 查詢到 {detailsResult.Data.Count} 個價格的掛單資訊", GetType().Name, LogDisplayTarget.SourceWindow);

                // 🚀 簡化邏輯：只處理 QuoteViewModel
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (this is QuoteViewModel quoteVM && quoteVM.OrderBookViewModel != null)
                    {
                        _logService.LogDebug($"[掛單同步] 🎯 使用 QuoteViewModel 的 OrderBookViewModel", GetType().Name, LogDisplayTarget.SourceWindow);

                        var view = CollectionViewSource.GetDefaultView(quoteVM.OrderBookViewModel.PriceRows);
                        using (view?.DeferRefresh())
                        {
                            quoteVM.OrderBookViewModel.UpdatePendingOrdersBatch(detailsResult.Data);
                        }

                        _logService.LogInfo($"[掛單同步] ✅ {GetType().Name} 高效掛單同步完成: {detailsResult.Data.Count} 個價格", GetType().Name, LogDisplayTarget.SourceWindow);
                    }
                    else
                    {
                        _logService.LogWarning($"[掛單同步] ⚠️ 當前 ViewModel ({GetType().Name}) 不是 QuoteViewModel 或沒有 OrderBookViewModel，跳過掛單同步", GetType().Name, LogDisplayTarget.SourceWindow);
                    }
                });
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[掛單同步] 高效同步詳細掛單失敗", GetType().Name, LogDisplayTarget.SourceWindow);
            }
        }
        #endregion

        #region 🔧 市場數據統一處理

        private void OnSTKTickReceived(STKTickData data)
        {
            // 只處理當前視窗訂閱的合約
            if (data.Code != CurrentSubscribedCode) return;
            OnSTKTickDataReceived(data);    // 轉發通知給子類
        }

        private void OnSTKBidAskReceived(STKBidAskData data)
        {
            if (data.Code != CurrentSubscribedCode) return;

            UpdateBidAskProperties(data);   // 直接更新 BaseViewModel 的屬性
            OnSTKBidAskDataReceived(data);  // 轉發通知給子類
        }

        private void OnSTKQuoteReceived(STKQuoteData data)
        {
            if (data.Code != CurrentSubscribedCode) return;
            OnSTKQuoteDataReceived(data);   // 通知子類處理
        }

        private void OnFOPTickReceived(FOPTickData data)
        {
            if (data.Code != CurrentSubscribedCode) return;
            OnFOPTickDataReceived(data);    // 通知子類處理
        }

        private void OnFOPBidAskReceived(FOPBidAskData data)
        {
            if (data.Code != CurrentSubscribedCode) return;
            UpdateBidAskProperties(data);
            OnFOPBidAskDataReceived(data);  // 通知子類處理
        }

        private void OnOrderBookInitializationRequested(ContractInfo contractInfo, string targetWindowId)
        {
            // 🔒 鎖定 WindowId：只有目標視窗的 ViewModel 應該響應
            // 這樣 MainWindow 就不會因為 QuoteWindow 的初始化而被干擾
            // QuoteWindow 內的 QuoteViewModel 和 OrderBookViewModel 擁有相同的 WindowId，所以它們都會收到，這是正常的
            if (targetWindowId != WindowId)

            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CurrentSubscribedCode = contractInfo.Code;  // 設定此 ViewModel 現在要監聽的商品
                    _logService.LogDebug($"[DEBUG] BaseViewModel - 更新後: Code={contractInfo.Code}, CurrentSubscribedCode={CurrentSubscribedCode}", GetType().Name, LogDisplayTarget.DebugOutput);
                    OnOrderBookInitializationDataReceived(contractInfo, targetWindowId);  // 通知子類處理
                });
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"處理訂單簿初始化請求時發生錯誤: {ex.Message}", GetType().Name, LogDisplayTarget.SourceWindow);
            }
        }

        #endregion

        #region 🚀 通用 BidAsk 更新方法
        /// <summary>
        /// 🎯 最高效的通用 BidAsk 更新方法
        /// 特點：
        /// 1. 單一方法處理 STK 和 FOP
        /// 2. 使用 Tuple 批次賦值，減少重複的 dynamic 解析
        /// 3. 直接存取已處理好的屬性（不使用陣列）
        /// 4. 最少的記憶體分配和最佳的執行效能
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateBidAskProperties<T>(T data) where T : class
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 使用 dynamic 一次性存取，然後批次更新
                    dynamic d = data;

                    // Tuple 批次賦值 - 最高效的方式
                    (BidPrice1, BidPrice2, BidPrice3, BidPrice4, BidPrice5) = (d.BidPrice1, d.BidPrice2, d.BidPrice3, d.BidPrice4, d.BidPrice5);

                    (BidVolume1, BidVolume2, BidVolume3, BidVolume4, BidVolume5) = (d.BidVolume1, d.BidVolume2, d.BidVolume3, d.BidVolume4, d.BidVolume5);

                    (AskPrice1, AskPrice2, AskPrice3, AskPrice4, AskPrice5) = (d.AskPrice1, d.AskPrice2, d.AskPrice3, d.AskPrice4, d.AskPrice5);

                    (AskVolume1, AskVolume2, AskVolume3, AskVolume4, AskVolume5) = (d.AskVolume1, d.AskVolume2, d.AskVolume3, d.AskVolume4, d.AskVolume5);

                    (DiffBidVolume1, DiffBidVolume2, DiffBidVolume3, DiffBidVolume4, DiffBidVolume5) = (d.DiffBidVolume1, d.DiffBidVolume2, d.DiffBidVolume3, d.DiffBidVolume4, d.DiffBidVolume5);

                    (DiffAskVolume1, DiffAskVolume2, DiffAskVolume3, DiffAskVolume4, DiffAskVolume5) = (d.DiffAskVolume1, d.DiffAskVolume2, d.DiffAskVolume3, d.DiffAskVolume4, d.DiffAskVolume5);

                    (BidTotalVolume, AskTotalVolume) = (d.BidTotalVolume, d.AskTotalVolume);    // 🎯 總量更新
                    BestBidPrice = BidPrice1;
                    BestBidVolume = BidVolume1;
                    BestAskPrice = AskPrice1;
                    BestAskVolume = AskVolume1;
                });
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"BidAsk更新失敗-{typeof(T).Name}", GetType().Name, LogDisplayTarget.DebugOutput);
            }
        }

        #endregion
        #region 🔧 虛擬方法供子類覆寫 - 市場數據處理

        protected virtual void OnSTKTickDataReceived(STKTickData data) { }
        protected virtual void OnSTKBidAskDataReceived(STKBidAskData data) { }
        protected virtual void OnSTKQuoteDataReceived(STKQuoteData data) { }
        protected virtual void OnFOPTickDataReceived(FOPTickData data) { }
        protected virtual void OnFOPBidAskDataReceived(FOPBidAskData data) { }
        protected virtual void OnOrderBookInitializationDataReceived(ContractInfo contractInfo, string windowId) { }

        #endregion

        #region ✅ 報價表格共用屬性變更處理

        partial void OnBidTotalVolumeChanged(int value)
        {
            OnPropertyChanged(nameof(InnerPercent));
            OnPropertyChanged(nameof(OuterPercent));
            OnPropertyChanged(nameof(BidAskTotalVolumeDifference));
            OnPropertyChanged(nameof(BidAskTotalVolumeDifferencePercent));
        }

        partial void OnAskTotalVolumeChanged(int value)
        {
            OnPropertyChanged(nameof(InnerPercent));
            OnPropertyChanged(nameof(OuterPercent));
            OnPropertyChanged(nameof(BidAskTotalVolumeDifference));
            OnPropertyChanged(nameof(BidAskTotalVolumeDifferencePercent));
        }

        partial void OnBidSideTotalVolumeChanged(long value)
        {
            OnPropertyChanged(nameof(InnerDealPercent));
            OnPropertyChanged(nameof(OuterDealPercent));
        }

        partial void OnAskSideTotalVolumeChanged(long value)
        {
            OnPropertyChanged(nameof(InnerDealPercent));
            OnPropertyChanged(nameof(OuterDealPercent));
        }

        //使用 partial void 觸發虛擬方法
        partial void OnIsCenteredChanged(bool value)
        {
            OnIsCenteredChangedCore(value);
        }

        // 虛擬方法供子類覆寫
        protected virtual void OnIsCenteredChangedCore(bool value)
        {
            if (value)
            {
                IsViewLocked = false;
            }
        }

        // IsViewLocked 的 partial void
        partial void OnIsViewLockedChanged(bool value)
        {
            OnIsViewLockedChangedCore(value);
        }

        // 虛擬方法供子類覆寫
        protected virtual void OnIsViewLockedChangedCore(bool value)
        {
            // 基類預設實作：無操作
        }

        #endregion

        #region 屬性變更處理（共用邏輯）

        partial void OnManualOrderPriceChanged(decimal? value)
        {
            if (value.HasValue && value.Value > 0)
            {
                _orderPrep.UpdatePrice(value.Value);
            }
        }

        partial void OnOrderQuantityChanged(int value)
        {
            if (value < 1)
            {
                OrderQuantity = 1;
                return;
            }
            if (value > 999)
            {
                OrderQuantity = 999;
                return;
            }

            _orderPrep.UpdateQuantity(value);
        }

        partial void OnSelectedPriceTypeChanged(string value)
        {
            _orderPrep.UpdatePriceType(value);
        }

        partial void OnSelectedOrderTypeChanged(string value)
        {
            _orderPrep.UpdateOrderType(value);
        }

        partial void OnSelectedOrderCondChanged(StockOrderCond value)
        {
            _orderPrep.UpdateOrderCond(value);
            OnPropertyChanged(nameof(OrderCondText));
            OnPropertyChanged(nameof(OrderCondColor));
            OnPropertyChanged(nameof(OrderCondStatusText));
        }

        partial void OnOrderLotTypeChanged(StockOrderLot value)
        {
            _orderPrep.UpdateOrderLot(value);
            OnPropertyChanged(nameof(OrderLotTypeText));
            OnPropertyChanged(nameof(OrderLotTypeColor));
        }

        partial void OnIsDayTradeEnabledChanged(bool value)
        {
            if (_isUpdatingDayTrade) return;

            try
            {
                _isUpdatingDayTrade = true;
                _orderPrep.UpdateDayTrade(value);

                OnPropertyChanged(nameof(DayTradeButtonText));
                OnPropertyChanged(nameof(DayTradeTextColor));
                OnPropertyChanged(nameof(DayTradeBorderBrush));
                OnPropertyChanged(nameof(DayTradeTooltip));
                OnPropertyChanged(nameof(DayTradeTooltipDetail));

                if (SelectedProductType != "Stocks")
                {
                    string targetOcType = value ? "DayTrade" : "Auto";
                    if (SelectedOcType != targetOcType)
                    {
                        SelectedOcType = targetOcType;
                    }
                }
            }
            finally
            {
                _isUpdatingDayTrade = false;
            }
        }

        partial void OnSelectedOcTypeChanged(string value)
        {
            if (_isUpdatingOcType) return;

            try
            {
                _isUpdatingOcType = true;
                _orderPrep.UpdateOcType(value);

                if (SelectedProductType != "Stocks")
                {
                    bool targetDayTradeState = (value == "DayTrade");
                    if (IsDayTradeEnabled != targetDayTradeState)
                    {
                        IsDayTradeEnabled = targetDayTradeState;
                    }
                }
            }
            finally
            {
                _isUpdatingOcType = false;
            }
        }

        partial void OnSelectedAccountChanged(AccountDisplayModel? value)
        {
            if (value?.Account != null)
            {
                _orderPrep.UpdateAccount(value.Account);
            }
        }

        partial void OnSelectedProductTypeChanged(string value)
        {
            SelectAccountByProductType(value);
            OnPropertyChanged(nameof(IsStockProduct));
            OnPropertyChanged(nameof(DayTradeButtonText));
            OnPropertyChanged(nameof(DayTradeTooltip));
        }

        partial void OnIsWindowExpandedChanged(bool value)
        {
            OnPropertyChanged(nameof(WindowSizeButtonText));
        }

        partial void OnIsLeftTopVisibleChanged(bool value)
        {
            OnPropertyChanged(nameof(LeftTopButtonText));
        }

        partial void OnIsRightPanelVisibleChanged(bool value)
        {
            OnPropertyChanged(nameof(RightPanelButtonText));
        }

        // 當即時報價變動時，自動觸發損益計算
        partial void OnLastTradePriceChanged(decimal value)
        {
            UpdateProfitLoss(value);
            OnPriceChanged(value); // 虛擬方法，供子類覆寫
        }

        #endregion

        #region 共用命令

        [RelayCommand]
        protected virtual void ToggleWindowSize()
        {
            try
            {
                IsWindowExpanded = !IsWindowExpanded;
                OnWindowSizeToggled(IsWindowExpanded);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "切換視窗尺寸失敗", GetType().Name, LogDisplayTarget.SourceWindow);
            }
        }

        [RelayCommand]
        protected virtual void ToggleLeftTop()
        {
            try
            {
                IsLeftTopVisible = !IsLeftTopVisible;
                string status = IsLeftTopVisible ? "顯示" : "隱藏";
                _logService.LogInfo($"[UI] 左側訂閱控制區已{status}", GetType().Name, LogDisplayTarget.SourceWindow);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "切換左側上區域失敗", GetType().Name, LogDisplayTarget.SourceWindow);
            }
        }

        [RelayCommand]
        protected virtual void ToggleRightPanel()
        {
            try
            {
                IsRightPanelVisible = !IsRightPanelVisible;
                string status = IsRightPanelVisible ? "顯示" : "隱藏";
                _logService.LogInfo($"[UI] 右側資料區已{status}", GetType().Name, LogDisplayTarget.SourceWindow);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "切換右側區域失敗", GetType().Name, LogDisplayTarget.SourceWindow);
            }
        }

        [RelayCommand]
        protected virtual void ToggleDayTrade()
        {
            try
            {
                IsDayTradeEnabled = !IsDayTradeEnabled;
                string status = IsDayTradeEnabled ? "啟用" : "關閉";
                _logService.LogInfo($"[設定] 當沖功能已{status}", GetType().Name, LogDisplayTarget.SourceWindow);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "切換當沖功能失敗", GetType().Name, LogDisplayTarget.SourceWindow);
            }
        }

        [RelayCommand]
        protected virtual void ToggleOrderCond()
        {
            try
            {
                SelectedOrderCond = SelectedOrderCond switch
                {
                    StockOrderCond.Cash => StockOrderCond.MarginTrading,
                    StockOrderCond.MarginTrading => StockOrderCond.ShortSelling,
                    StockOrderCond.ShortSelling => StockOrderCond.Cash,
                    _ => StockOrderCond.Cash
                };
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "切換委託條件失敗", GetType().Name, LogDisplayTarget.SourceWindow);
            }
        }

        [RelayCommand]
        protected virtual void ToggleOrderLotType()
        {
            try
            {
                OrderLotType = OrderLotType switch
                {
                    StockOrderLot.Common => StockOrderLot.IntradayOdd,
                    StockOrderLot.IntradayOdd => StockOrderLot.Odd,
                    StockOrderLot.Odd => StockOrderLot.Fixing,
                    StockOrderLot.Fixing => StockOrderLot.Common,
                    _ => StockOrderLot.Common
                };
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "切換下單類型失敗", GetType().Name, LogDisplayTarget.SourceWindow);
            }
        }

        [RelayCommand]
        public virtual void RefreshAccounts()
        {
            try
            {
                LoadAccountsFromService();
                _logService.LogInfo("帳戶資料已重新整理", GetType().Name, LogDisplayTarget.SourceWindow);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "重新整理帳戶資料失敗", GetType().Name, LogDisplayTarget.SourceWindow);
            }
        }

        [RelayCommand]
        protected virtual void Clear()
        {
            try
            {
                _logService.ClearLogs("QuoteWindow");
                _logService.LogInfo("🗑️ 已清空所有資料", GetType().Name, LogDisplayTarget.SourceWindow);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "清空資料操作失敗", GetType().Name, LogDisplayTarget.SourceWindow);
            }
        }

        #endregion

        #region 損益計算（共用邏輯）

        protected virtual void UpdatePosition(string? action, decimal dealPrice, long dealQty)
        {
            if (action == "Buy")
            {
                decimal totalCost = (AvgCost * Math.Abs(ActualPosition)) + (dealPrice * dealQty);
                long newActualPosition = ActualPosition + dealQty;  // 🔧 實際成交增加
                AvgCost = newActualPosition > 0 ? totalCost / Math.Abs(newActualPosition) : 0;
            }
            else if (action == "Sell")
            {
                long newActualPosition = ActualPosition - dealQty;  // 🔧 實際成交減少
                if (newActualPosition == 0)
                {
                    AvgCost = 0;  // 平倉後重置成本
                }
            }

            UpdateProfitLoss(LastTradePrice);
        }

        protected virtual void UpdateProfitLoss(decimal currentPrice)
        {
            if (ActualPosition == 0 || AvgCost == 0)
            {
                ProfitLoss = 0;
                ProfitLossPercent = 0;
                return;
            }

            decimal profitLoss = (currentPrice - AvgCost) * ActualPosition;
            ProfitLoss = profitLoss;
            ProfitLossPercent = AvgCost > 0 ? (profitLoss / (AvgCost * Math.Abs(ActualPosition))) * 100 : 0;
        }

        #endregion

        #region 共用方法

        protected virtual void LoadAccountsFromService()
        {
            try
            {
                AvailableAccounts.Clear();

                if (!ShioajiService.Instance.IsLoggedIn)
                {
                    _logService.LogWarning("[帳戶] API 尚未登入，無法載入帳戶資料", GetType().Name, LogDisplayTarget.SourceWindow);
                    return;
                }

                var accounts = ShioajiService.Instance.GetAccounts();
                if (accounts == null || accounts.Count == 0)
                {
                    _logService.LogWarning("[帳戶] 沒有可用的帳戶", GetType().Name, LogDisplayTarget.SourceWindow);
                    return;
                }

                var displayModels = AccountDisplayModel.FromAccounts(accounts);
                foreach (var model in displayModels)
                {
                    AvailableAccounts.Add(model);
                }

                if (AvailableAccounts.Count > 0)
                {
                    SelectedAccount = AvailableAccounts[0];
                }

                _logService.LogInfo($"[帳戶] 已載入 {AvailableAccounts.Count} 個帳戶", GetType().Name, LogDisplayTarget.SourceWindow);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[錯誤] 載入帳戶資料操作失敗", GetType().Name, LogDisplayTarget.SourceWindow);
            }
        }

        public virtual void SelectAccountByProductType(string productType)
        {
            try
            {
                if (AvailableAccounts == null || AvailableAccounts.Count == 0) return;

                string targetAccountType = productType == "Stocks" ? "S" : "F";
                var targetAccount = AvailableAccounts.FirstOrDefault(account =>
                    account.AccountType.Equals(targetAccountType, StringComparison.OrdinalIgnoreCase));

                if (targetAccount != null)
                {
                    SelectedAccount = targetAccount;
                    _logService.LogInfo($"[帳戶] 已自動選擇 {targetAccountType} 類型帳戶: {targetAccount.AccountId}", GetType().Name, LogDisplayTarget.SourceWindow);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[錯誤] 自動選擇帳戶操作失敗", GetType().Name, LogDisplayTarget.SourceWindow);
            }
        }

        #endregion


        #region 虛擬方法（供子類覆寫）

        protected virtual void OnWindowOrderCallback(string windowId, OrderDataInfo orderDataInfo)
        {
            // 基礎實作：記錄日誌
            Application.Current.Dispatcher.Invoke(() =>
            {
                orderDataInfo.PrintToLog(GetType().Name, LogDisplayTarget.SourceWindow);
            });
        }

        protected virtual void OnWindowSizeToggled(bool isExpanded)
        {
            // 供子類覆寫，例如觸發事件通知 Window
        }

        protected virtual void OnPriceChanged(decimal newPrice)
        {
            // 供子類覆寫，例如停利停損檢查
        }

        #endregion

        #region IDisposable 實作

        private bool _disposed = false;

        public virtual void Dispose()
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
                    try
                    {
                        // 取消訂閱時間服務事件
                        if (TimeService != null)
                        {
                            TimeService.PropertyChanged -= OnTimeServicePropertyChanged;
                            TimeService.TradingTimeStatusChanged -= OnTradingTimeStatusChanged;
                        }

                        // 取消訂閱市場數據事件
                        _marketService.STK_TickReceived -= OnSTKTickReceived;
                        _marketService.STK_BidAskReceived -= OnSTKBidAskReceived;
                        _marketService.STK_QuoteReceived -= OnSTKQuoteReceived;
                        _marketService.FOP_TickReceived -= OnFOPTickReceived;
                        _marketService.FOP_BidAskReceived -= OnFOPBidAskReceived;
                        _marketService.OrderBookInitializationRequested -= OnOrderBookInitializationRequested;

                        // 取消訂閱委託統計更新事件
                        OrderService.OrderStatsUpdateRequested -= OnOrderStatsUpdateRequested;
                        _logService.LogInfo($"[統計更新] ✅ 事件取消訂閱 - 視窗: {WindowId}", GetType().Name, LogDisplayTarget.DebugOutput);

                        OrderService.Instance.WindowOrderCallback -= OnWindowOrderCallback;
                        AvailableAccounts?.Clear();
                        PriceRows?.Clear();

                        _logService.LogInfo($"{GetType().Name} 已釋放資源", GetType().Name);
                    }
                    catch (Exception ex)
                    {
                        _logService.LogError(ex, $"{GetType().Name} 釋放資源時發生錯誤", GetType().Name);
                    }
                }
                _disposed = true;
            }
        }

        #endregion
    }
}
