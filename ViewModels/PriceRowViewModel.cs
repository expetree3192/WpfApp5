using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace WpfApp5.ViewModels
{
    // 價格行 ViewModel
    public partial class PriceRowViewModel : ObservableObject
    {
        #region 靜態 Brush 資源（效能優化）

        // 紅色 Brush - 賣盤成交（外盤-主動買進）
        private static readonly Brush RedBrush = new SolidColorBrush(Color.FromRgb(255, 68, 68));

        // 綠色 Brush - 內盤成交（內盤-主動賣出）
        private static readonly Brush GreenBrush = new SolidColorBrush(Color.FromRgb(0, 204, 68));

        // 白色 Brush - 平盤成交
        private static readonly Brush WhiteBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));

        // 靜態建構函數 - Freeze Brush 以提升效能
        static PriceRowViewModel()
        {
            // Freeze Brush 使其變為不可變，可以跨執行緒使用，並提升效能
            RedBrush.Freeze();
            GreenBrush.Freeze();
            WhiteBrush.Freeze();
        }

        #endregion

        #region Observable 屬性

        [ObservableProperty]
        private decimal _price;

        [ObservableProperty]
        private string _priceText = string.Empty;

        [ObservableProperty]
        private int _bidVolume;

        [ObservableProperty]
        private int _askVolume;

        [ObservableProperty]
        private bool _isBestBid;

        [ObservableProperty]
        private bool _isBestAsk;

        [ObservableProperty]
        private bool _isReference;

        [ObservableProperty]
        private bool _isLastTrade;

        [ObservableProperty]
        private long _tickVolume;

        [ObservableProperty]
        private int _tickType;

        [ObservableProperty]
        private bool _isLimitUp;

        [ObservableProperty]
        private bool _isLimitDown;

        [ObservableProperty]
        private bool _isOpen;

        [ObservableProperty]
        private bool _isHigh;

        [ObservableProperty]
        private bool _isLow;

        [ObservableProperty]
        private int _pendingBuyQuantity;    // 買單掛單數量（未成交）

        [ObservableProperty]
        private int _filledBuyQuantity; // 買單已成交數量

        [ObservableProperty]
        private int _pendingSellQuantity;   // 賣單掛單數量（未成交）

        [ObservableProperty]
        private int _filledSellQuantity;    // 賣單已成交數量

        [ObservableProperty]
        private decimal _referencePrice;    // 參考價格（用於計算價格變動百分比）

        #endregion

        #region 計算屬性

        public bool HasBidVolume => BidVolume > 0;  // 是否有買量（用於控制顯示）
        public bool HasAskVolume => AskVolume > 0;  // 是否有賣量（用於控制顯示）
        public bool HasLastTradeVolume => TickVolume > 0;  // 是否有成交量（用於控制顯示）

        // 成交價顏色（根據 TickType 決定），使用靜態 Brush 資源，避免重複建立物件
        public Brush LastTradeColor
        {
            get
            {
                return TickType switch
                {
                    1 => RedBrush,      // 買盤成交（內盤）
                    -1 => GreenBrush,    // 賣盤成交（外盤）
                    _ => WhiteBrush     // 平盤成交
                };
            }
        }

        // 價格變動百分比
        public decimal PriceChangePercent
        {
            get
            {
                if (ReferencePrice <= 0) return 0;
                return ((Price - ReferencePrice) / ReferencePrice) * 100;
            }
        }

        // 價格變動百分比（絕對值，用於顯示）
        public decimal PriceChangePercentAbsolute
        {
            get
            {
                return Math.Abs(PriceChangePercent);
            }
        }
        // 買單掛單資訊文字（格式：未成交數量(總數量)）
        public string BuyOrderText
        {
            get
            {
                if (PendingBuyQuantity == 0 && FilledBuyQuantity == 0)
                    return string.Empty;

                int totalQuantity = PendingBuyQuantity + FilledBuyQuantity;
                return $"{PendingBuyQuantity}({totalQuantity})";    // 格式：未成交數量(總數量)

            }
        }

        // 賣單掛單資訊文字（格式：未成交數量(總數量)）
        public string SellOrderText
        {
            get
            {
                if (PendingSellQuantity == 0 && FilledSellQuantity == 0)
                    return string.Empty;

                int totalQuantity = PendingSellQuantity + FilledSellQuantity;
                return $"{PendingSellQuantity}({totalQuantity})";    // 格式：未成交數量(總數量)
            }
        }

        public bool HasBuyOrder => PendingBuyQuantity > 0 || FilledBuyQuantity > 0; // 是否有買單掛單

        public bool HasSellOrder => PendingSellQuantity > 0 || FilledSellQuantity > 0;  // 是否有賣單掛單

        #endregion

        #region 屬性變更通知

        // 當 BidVolume 變更時，通知 HasBidVolume 也變更
        partial void OnBidVolumeChanged(int value)
        {
            OnPropertyChanged(nameof(HasBidVolume));
        }

        // 當 AskVolume 變更時，通知 HasAskVolume 也變更
        partial void OnAskVolumeChanged(int value)
        {
            OnPropertyChanged(nameof(HasAskVolume));
        }

        // 當 LastTradeVolume 變更時，通知 HasLastTradeVolume 也變更
        partial void OnTickVolumeChanged(long value)
        {
            OnPropertyChanged(nameof(HasLastTradeVolume));
        }

        // 當 TickType 變更時，通知 LastTradeColor 也變更
        partial void OnTickTypeChanged(int value)
        {
            OnPropertyChanged(nameof(LastTradeColor));
        }

        // 當買單掛單數量變更時
        partial void OnPendingBuyQuantityChanged(int value)
        {
            OnPropertyChanged(nameof(BuyOrderText));
            OnPropertyChanged(nameof(HasBuyOrder));
        }

        // 當買單已成交數量變更時
        partial void OnFilledBuyQuantityChanged(int value)
        {
            OnPropertyChanged(nameof(BuyOrderText));
            OnPropertyChanged(nameof(HasBuyOrder));
        }

        // 當賣單掛單數量變更時
        partial void OnPendingSellQuantityChanged(int value)
        {
            OnPropertyChanged(nameof(SellOrderText));
            OnPropertyChanged(nameof(HasSellOrder));
        }

        // 當賣單已成交數量變更時
        partial void OnFilledSellQuantityChanged(int value)
        {
            OnPropertyChanged(nameof(SellOrderText));
            OnPropertyChanged(nameof(HasSellOrder));
        }

        // 當價格或參考價格變更時，通知 PriceChangePercent 也變更
        partial void OnPriceChanged(decimal value)
        {
            OnPropertyChanged(nameof(PriceChangePercent));
            OnPropertyChanged(nameof(PriceChangePercentAbsolute));  // 新增
        }

        partial void OnReferencePriceChanged(decimal value)
        {
            OnPropertyChanged(nameof(PriceChangePercent));
            OnPropertyChanged(nameof(PriceChangePercentAbsolute));  // 新增
        }

        #endregion
    }
}