using System;
using System.Runtime.CompilerServices;
using WpfApp5.Services;
using WpfApp5.Utils;

namespace WpfApp5.Models.MarketData
{
    public sealed class STKTickData : IBidAskSnapshot
    {
        #region 基本屬性
        public required string Code { get; set; }
        public string Symbol => Code;
        public required string DateTime { get; set; }
        public decimal Open { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal Close { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Amount { get; set; }
        public decimal TotalAmount { get; set; }
        public long Volume { get; set; }
        public long TotalVolume { get; set; }
        public int TickType { get; set; }
        public int ChangeType { get; set; }
        public decimal PriceChange { get; set; }
        public decimal PercentageChange { get; set; }
        public long BidSideTotalVolume { get; set; }
        public long AskSideTotalVolume { get; set; }
        public long BidSideTotalCount { get; set; }
        public long AskSideTotalCount { get; set; }

        public int ClosingOddLotShares { get; set; }
        public int FixedTradeVolume { get; set; }

        public bool IsSuspended { get; set; }
        public bool IsSimTrade { get; set; }
        public bool IsIntradayOdd { get; set; }
        public decimal BidPrice1 { get; set; }
        public decimal AskPrice1 { get; set; }
        public int BidVolume1 { get; set; }
        public int AskVolume1 { get; set; }
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
        #region 建構子（極致優化）
        public STKTickData(dynamic data, bool isOddLot)
        {
            try
            {
                Code = data.code;
                DateTime = data.datetime;

                Open = data.open;
                AveragePrice = data.avg_price;
                Close = data.close;
                High = data.high;
                Low = data.low;

                Amount = data.amount;
                TotalAmount = data.total_amount;
                Volume = data.volume;
                TotalVolume = data.total_volume;

                TickType = ConvertTickType(data.tick_type);
                ChangeType = data.chg_type;
                PriceChange = data.price_chg;
                PercentageChange = data.pct_chg;

                BidSideTotalVolume = data.bid_side_total_vol;
                AskSideTotalVolume = data.ask_side_total_vol;
                BidSideTotalCount = data.bid_side_total_cnt;
                AskSideTotalCount = data.ask_side_total_cnt;

                ClosingOddLotShares = data.closing_oddlot_shares;
                FixedTradeVolume = data.fixed_trade_vol;

                IsSuspended = data.suspend;
                IsSimTrade = data.simtrade;
                IsIntradayOdd = data.intraday_odd;
            }
            catch (Exception ex)
            {
                string dataType = isOddLot ? "零股" : "一般";
                LogService.Instance.LogError(ex, $"解析{dataType}交易資料失敗", "STKTickData", LogDisplayTarget.DebugOutput);
                Code = "ERROR";
                DateTime = System.DateTime.Now.ToString();
            }
        }

        public STKTickData()
        {
            Code = string.Empty;
            DateTime = string.Empty;
        }
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
        public int GetNormalizedTickType()
        {
            return TickType switch
            {
                1 => 1,
                2 => -1,
                _ => 0
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetBidRatio()
        {
            var totalVolume = BidSideTotalVolume + AskSideTotalVolume;
            if (totalVolume == 0) return 50m;
            return ((decimal)BidSideTotalVolume / totalVolume) * 100m;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetOrderFlowAnalysis()
        {
            if (BidSideTotalVolume == 0 && AskSideTotalVolume == 0) return 0m;
            var total = BidSideTotalVolume + AskSideTotalVolume;
            return Math.Round(((decimal)BidSideTotalVolume / total) * 100m, 2);
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
        public bool IsValidTrade() => Close > 0 && Volume > 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsUpperLimit() => ChangeType == 1 || PercentageChange >= 9.5m;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsLowerLimit() => ChangeType == 5 || PercentageChange <= -9.5m;

        #endregion
    }
}
