using System;
using System.Runtime.CompilerServices;
using WpfApp5.Services;
using WpfApp5.Utils;

namespace WpfApp5.Models.MarketData
{
    public sealed class STKQuoteData
    {
        #region 基本屬性
        public string Code { get; private set; } = string.Empty;
        public string DateTime { get; private set; } = string.Empty;

        public decimal Open { get; private set; }
        public decimal AveragePrice { get; private set; }
        public decimal Close { get; private set; }
        public decimal High { get; private set; }
        public decimal Low { get; private set; }

        public decimal Amount { get; private set; }
        public decimal TotalAmount { get; private set; }
        public long Volume { get; private set; }
        public long TotalVolume { get; private set; }

        public int TickType { get; private set; }
        public int ChangeType { get; private set; }
        public decimal PriceChange { get; private set; }
        public decimal PercentageChange { get; private set; }

        public int BidSideTotalVolume { get; private set; }
        public int AskSideTotalVolume { get; private set; }
        public int BidSideTotalCount { get; private set; }
        public int AskSideTotalCount { get; private set; }

        public int ClosingOddLotShares { get; private set; }
        public decimal ClosingOddLotClose { get; private set; }
        public decimal ClosingOddLotAmount { get; private set; }
        public decimal ClosingOddLotBidPrice { get; private set; }
        public decimal ClosingOddLotAskPrice { get; private set; }
        public int FixedTradeVolume { get; private set; }
        public decimal FixedTradeAmount { get; private set; }

        public long AvailableBorrowing { get; private set; }

        public bool IsSuspended { get; private set; }
        public bool IsSimTrade { get; private set; }
        #endregion

        #region 五檔掛單屬性
        public decimal BidPrice1 { get; private set; }
        public decimal BidPrice2 { get; private set; }
        public decimal BidPrice3 { get; private set; }
        public decimal BidPrice4 { get; private set; }
        public decimal BidPrice5 { get; private set; }

        public int BidVolume1 { get; private set; }
        public int BidVolume2 { get; private set; }
        public int BidVolume3 { get; private set; }
        public int BidVolume4 { get; private set; }
        public int BidVolume5 { get; private set; }
        public int BidTotalVolume => BidVolume1 + BidVolume2 + BidVolume3 + BidVolume4 + BidVolume5;

        public decimal AskPrice1 { get; private set; }
        public decimal AskPrice2 { get; private set; }
        public decimal AskPrice3 { get; private set; }
        public decimal AskPrice4 { get; private set; }
        public decimal AskPrice5 { get; private set; }

        public int AskVolume1 { get; private set; }
        public int AskVolume2 { get; private set; }
        public int AskVolume3 { get; private set; }
        public int AskVolume4 { get; private set; }
        public int AskVolume5 { get; private set; }
        public int AskTotalVolume => AskVolume1 + AskVolume2 + AskVolume3 + AskVolume4 + AskVolume5;

        public int DiffBidVolume1 { get; private set; }
        public int DiffBidVolume2 { get; private set; }
        public int DiffBidVolume3 { get; private set; }
        public int DiffBidVolume4 { get; private set; }
        public int DiffBidVolume5 { get; private set; }

        public int DiffAskVolume1 { get; private set; }
        public int DiffAskVolume2 { get; private set; }
        public int DiffAskVolume3 { get; private set; }
        public int DiffAskVolume4 { get; private set; }
        public int DiffAskVolume5 { get; private set; }
        #endregion

        #region 陣列屬性（預分配）
        private readonly decimal[] _bidPrices = new decimal[5];
        private readonly decimal[] _askPrices = new decimal[5];
        private readonly int[] _bidVolumes = new int[5];
        private readonly int[] _askVolumes = new int[5];
        private readonly int[] _diffBidVolumes = new int[5];
        private readonly int[] _diffAskVolumes = new int[5];

        public decimal[] BidPrices => _bidPrices;
        public decimal[] AskPrices => _askPrices;
        public int[] BidVolumes => _bidVolumes;
        public int[] AskVolumes => _askVolumes;
        public int[] DiffBidVolumes => _diffBidVolumes;
        public int[] DiffAskVolumes => _diffAskVolumes;
        #endregion
        #region CalculatedTickType 屬性

        // CalculatedTickType - 基於成交價與買賣價距離分析
        public int CalculatedTickType
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // 避免 Math.Abs，採用平方計算比較，三元運算子保持簡潔
                decimal diffBid = Close - BidPrice1;
                decimal diffAsk = Close - AskPrice1;
                return diffBid * diffBid < diffAsk * diffAsk ? -1 : diffAsk * diffAsk < diffBid * diffBid ? 1 : 0;
            }
        }
        #endregion

        #region 建構子（極致優化 - 直接存取）
        public STKQuoteData(dynamic data)
        {
            try
            {
                // 基本字串屬性 - 直接存取
                Code = data.code;
                DateTime = data.datetime;

                // 價格相關 - 直接存取（假設 data 已經是正確的型別）
                Open = data.open;
                AveragePrice = data.avg_price;
                Close = data.close;
                High = data.high;
                Low = data.low;

                // 成交量金額 - 直接存取
                Amount = data.amount;
                TotalAmount = data.total_amount;
                Volume = data.volume;
                TotalVolume = data.total_volume;

                // 狀態相關 - TickType 需要轉換，其他直接存取
                TickType = ConvertTickType(data.tick_type);
                ChangeType = data.chg_type;
                PriceChange = data.price_chg;
                PercentageChange = data.pct_chg;

                // 買賣盤統計 - 直接存取
                BidSideTotalVolume = data.bid_side_total_vol;
                AskSideTotalVolume = data.ask_side_total_vol;
                BidSideTotalCount = data.bid_side_total_cnt;
                AskSideTotalCount = data.ask_side_total_cnt;

                // 零股相關 - 直接存取
                ClosingOddLotShares = data.closing_oddlot_shares;
                ClosingOddLotClose = data.closing_oddlot_close;
                ClosingOddLotAmount = data.closing_oddlot_amount;
                ClosingOddLotBidPrice = data.closing_oddlot_bid_price;
                ClosingOddLotAskPrice = data.closing_oddlot_ask_price;

                // 定盤相關 - 直接存取
                FixedTradeVolume = data.fixed_trade_vol;
                FixedTradeAmount = data.fixed_trade_amount;

                // 五檔資料初始化（陣列部分仍需安全轉換）
                InitializeBidAskDataOptimized(data);

                // 其他屬性 - 直接存取
                AvailableBorrowing = data.avail_borrowing;
                IsSuspended = data.suspend;
                IsSimTrade = data.simtrade;
            }
            catch (Exception ex)
            {
                LogService.Instance.LogError(ex, "STKQuoteData 建構失敗", "STKQuoteData", LogDisplayTarget.DebugOutput);
                Code = "ERROR";
                DateTime = System.DateTime.Now.ToString();
            }
        }

        public STKQuoteData()
        {
            // 預設建構子
        }
        #endregion

        #region TickType 轉換方法

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ConvertTickType(dynamic originalTickType)
        {
            int tickValue = DataTypeConverter.SafeConvertToInt(originalTickType);

            return tickValue switch
            {
                1 => 1,   // 買進 → 1
                2 => -1,  // 賣出 → -1  
                0 => 0,   // 平盤 → 0
                _ => 0    // 其他未知值 → 0
            };
        }
        #endregion

        #region 私有方法 - 五檔資料初始化（僅陣列部分需要安全轉換）
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeBidAskDataOptimized(dynamic data)
        {
            try
            {
                // 買方五檔價格 - 陣列存取需要安全轉換
                if (data.bid_price != null && data.bid_price.Length >= 5)
                {
                    BidPrice1 = _bidPrices[0] = DataTypeConverter.SafeConvertToDecimal(data.bid_price[0]);
                    BidPrice2 = _bidPrices[1] = DataTypeConverter.SafeConvertToDecimal(data.bid_price[1]);
                    BidPrice3 = _bidPrices[2] = DataTypeConverter.SafeConvertToDecimal(data.bid_price[2]);
                    BidPrice4 = _bidPrices[3] = DataTypeConverter.SafeConvertToDecimal(data.bid_price[3]);
                    BidPrice5 = _bidPrices[4] = DataTypeConverter.SafeConvertToDecimal(data.bid_price[4]);
                }

                // 買方五檔量 - 陣列存取需要安全轉換
                if (data.bid_volume != null && data.bid_volume.Length >= 5)
                {
                    BidVolume1 = _bidVolumes[0] = DataTypeConverter.SafeConvertToInt(data.bid_volume[0]);
                    BidVolume2 = _bidVolumes[1] = DataTypeConverter.SafeConvertToInt(data.bid_volume[1]);
                    BidVolume3 = _bidVolumes[2] = DataTypeConverter.SafeConvertToInt(data.bid_volume[2]);
                    BidVolume4 = _bidVolumes[3] = DataTypeConverter.SafeConvertToInt(data.bid_volume[3]);
                    BidVolume5 = _bidVolumes[4] = DataTypeConverter.SafeConvertToInt(data.bid_volume[4]);
                }

                // 買方五檔變化量 - 陣列存取需要安全轉換
                if (data.diff_bid_vol != null && data.diff_bid_vol.Length >= 5)
                {
                    DiffBidVolume1 = _diffBidVolumes[0] = DataTypeConverter.SafeConvertToInt(data.diff_bid_vol[0]);
                    DiffBidVolume2 = _diffBidVolumes[1] = DataTypeConverter.SafeConvertToInt(data.diff_bid_vol[1]);
                    DiffBidVolume3 = _diffBidVolumes[2] = DataTypeConverter.SafeConvertToInt(data.diff_bid_vol[2]);
                    DiffBidVolume4 = _diffBidVolumes[3] = DataTypeConverter.SafeConvertToInt(data.diff_bid_vol[3]);
                    DiffBidVolume5 = _diffBidVolumes[4] = DataTypeConverter.SafeConvertToInt(data.diff_bid_vol[4]);
                }

                // 賣方五檔價格 - 陣列存取需要安全轉換
                if (data.ask_price != null && data.ask_price.Length >= 5)
                {
                    AskPrice1 = _askPrices[0] = DataTypeConverter.SafeConvertToDecimal(data.ask_price[0]);
                    AskPrice2 = _askPrices[1] = DataTypeConverter.SafeConvertToDecimal(data.ask_price[1]);
                    AskPrice3 = _askPrices[2] = DataTypeConverter.SafeConvertToDecimal(data.ask_price[2]);
                    AskPrice4 = _askPrices[3] = DataTypeConverter.SafeConvertToDecimal(data.ask_price[3]);
                    AskPrice5 = _askPrices[4] = DataTypeConverter.SafeConvertToDecimal(data.ask_price[4]);
                }

                // 賣方五檔量 - 陣列存取需要安全轉換
                if (data.ask_volume != null && data.ask_volume.Length >= 5)
                {
                    AskVolume1 = _askVolumes[0] = DataTypeConverter.SafeConvertToInt(data.ask_volume[0]);
                    AskVolume2 = _askVolumes[1] = DataTypeConverter.SafeConvertToInt(data.ask_volume[1]);
                    AskVolume3 = _askVolumes[2] = DataTypeConverter.SafeConvertToInt(data.ask_volume[2]);
                    AskVolume4 = _askVolumes[3] = DataTypeConverter.SafeConvertToInt(data.ask_volume[3]);
                    AskVolume5 = _askVolumes[4] = DataTypeConverter.SafeConvertToInt(data.ask_volume[4]);
                }

                // 賣方五檔變化量 - 陣列存取需要安全轉換
                if (data.diff_ask_vol != null && data.diff_ask_vol.Length >= 5)
                {
                    DiffAskVolume1 = _diffAskVolumes[0] = DataTypeConverter.SafeConvertToInt(data.diff_ask_vol[0]);
                    DiffAskVolume2 = _diffAskVolumes[1] = DataTypeConverter.SafeConvertToInt(data.diff_ask_vol[1]);
                    DiffAskVolume3 = _diffAskVolumes[2] = DataTypeConverter.SafeConvertToInt(data.diff_ask_vol[2]);
                    DiffAskVolume4 = _diffAskVolumes[3] = DataTypeConverter.SafeConvertToInt(data.diff_ask_vol[3]);
                    DiffAskVolume5 = _diffAskVolumes[4] = DataTypeConverter.SafeConvertToInt(data.diff_ask_vol[4]);
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.LogError(ex, "初始化五檔資料失敗", "STKQuoteData", LogDisplayTarget.DebugOutput);
            }
        }
        #endregion

        #region 日期時間處理（極致優化）
        private System.DateTime? _parsedDateTime;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public System.DateTime GetDateTime()
        {
            if (!_parsedDateTime.HasValue)
            {
                _parsedDateTime = DateTimeCache.Parse(DateTime);
            }
            return _parsedDateTime.Value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetFullDateTimeString() => GetDateTime().ToFullString();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetDateOnlyString() => GetDateTime().ToDateOnlyString();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetTimeOnlyString() => GetDateTime().ToTimeOnlyString();
        #endregion

        #region 公開方法 - 高效能存取
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetBestBidPrice() => BidPrice1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetBestAskPrice() => AskPrice1;

        public decimal[] GetBidPrices() => (decimal[])_bidPrices.Clone();
        public decimal[] GetAskPrices() => (decimal[])_askPrices.Clone();
        public int[] GetBidVolumes() => (int[])_bidVolumes.Clone();
        public int[] GetAskVolumes() => (int[])_askVolumes.Clone();
        public int[] GetDiffBidVolumes() => (int[])_diffBidVolumes.Clone();
        public int[] GetDiffAskVolumes() => (int[])_diffAskVolumes.Clone();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetSpread()
        {
            var bestBid = GetBestBidPrice();
            var bestAsk = GetBestAskPrice();
            return (bestBid > 0 && bestAsk > 0) ? bestAsk - bestBid : 0m;
        }
        #endregion

        #region 分析方法
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetChangeStatus()
        {
            return ChangeType switch
            {
                1 => "漲停",
                2 => "漲",
                3 => "平盤",
                4 => "跌",
                5 => "跌停",
                _ => "未知"
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetTickTypeDescription()
        {
            return TickType switch
            {
                1 => "買進",
                -1 => "賣出",
                0 => "平盤",
                _ => "未知"
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetTradeStatusDescription()
        {
            if (IsSuspended) return "暫停交易";
            if (IsSimTrade) return "模擬交易";
            return "正常交易";
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetAmplitudePercentage()
        {
            if (Open <= 0) return 0m;
            return ((High - Low) / Open) * 100m;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetAmountRatio()
        {
            return TotalAmount > 0 ? (Amount / TotalAmount) * 100m : 0m;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetVolumeRatio()
        {
            return TotalVolume > 0 ? ((decimal)Volume / TotalVolume) * 100m : 0m;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetOrderFlowAnalysis()
        {
            if (BidSideTotalVolume == 0 && AskSideTotalVolume == 0) return 0m;
            var total = BidSideTotalVolume + AskSideTotalVolume;
            return Math.Round(((decimal)BidSideTotalVolume / total) * 100m, 2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValidTrade() => Close > 0 && Volume > 0;

        public string GetQuoteSummary()
        {
            return $"{Code} | {Close:F2} ({PriceChange:+0.00;-0.00;0.00}) | {GetChangeStatus()} | {GetTradeStatusDescription()} | Vol:{Volume:N0}";
        }

        public string GetFullQuoteInfo()
        {
            return $"""
                === 基本資訊 ===
                商品代碼: {Code}
                時間: {DateTime}
                交易狀態: {GetTradeStatusDescription()}
                
                === 價格資訊 ===
                開盤: {Open:F2}
                最高: {High:F2}
                最低: {Low:F2}
                收盤: {Close:F2}
                均價: {AveragePrice:F2}
                漲跌: {PriceChange:+0.00;-0.00;0.00} ({PercentageChange:+0.00%;-0.00%;0.00%})
                狀態: {GetChangeStatus()}
                振幅: {GetAmplitudePercentage():F2}%
                
                === 成交量資訊 ===
                單筆量: {Volume:N0}
                總量: {TotalVolume:N0}
                單筆額: {Amount:N0}
                總額: {TotalAmount:N0}
                
                === 買賣盤統計 ===
                買方總量: {BidSideTotalVolume:N0} (筆數: {BidSideTotalCount})
                賣方總量: {AskSideTotalVolume:N0} (筆數: {AskSideTotalCount})
                
                === 買方五檔 ===
                1. {BidPrice1:F2} x {BidVolume1:N0} (變化: {DiffBidVolume1:+0;-0;0})
                2. {BidPrice2:F2} x {BidVolume2:N0} (變化: {DiffBidVolume2:+0;-0;0})
                3. {BidPrice3:F2} x {BidVolume3:N0} (變化: {DiffBidVolume3:+0;-0;0})
                4. {BidPrice4:F2} x {BidVolume4:N0} (變化: {DiffBidVolume4:+0;-0;0})
                5. {BidPrice5:F2} x {BidVolume5:N0} (變化: {DiffBidVolume5:+0;-0;0})
                總量: {BidTotalVolume:N0}
                
                === 賣方五檔 ===
                1. {AskPrice1:F2} x {AskVolume1:N0} (變化: {DiffAskVolume1:+0;-0;0})
                2. {AskPrice2:F2} x {AskVolume2:N0} (變化: {DiffAskVolume2:+0;-0;0})
                3. {AskPrice3:F2} x {AskVolume3:N0} (變化: {DiffAskVolume3:+0;-0;0})
                4. {AskPrice4:F2} x {AskVolume4:N0} (變化: {DiffAskVolume4:+0;-0;0})
                5. {AskPrice5:F2} x {AskVolume5:N0} (變化: {DiffAskVolume5:+0;-0;0})
                總量: {AskTotalVolume:N0}
                
                === 零股資訊 ===
                零股股數: {ClosingOddLotShares}
                零股收盤: {ClosingOddLotClose:F2}
                零股金額: {ClosingOddLotAmount:N0}
                零股買價: {ClosingOddLotBidPrice:F2}
                零股賣價: {ClosingOddLotAskPrice:F2}
                
                === 定盤資訊 ===
                定盤量: {FixedTradeVolume:N0}
                定盤額: {FixedTradeAmount:N0}
                
                === 其他資訊 ===
                可借券: {AvailableBorrowing:N0}
                """;
        }
        #endregion
    }
}
