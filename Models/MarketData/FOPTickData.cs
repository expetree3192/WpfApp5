using System;
using System.Runtime.CompilerServices;
using WpfApp5.Services;
using WpfApp5.Utils;

namespace WpfApp5.Models.MarketData
{
    public sealed class FOPTickData : IBidAskSnapshot
    {
        #region 基本屬性
        public required string Code { get; set; }
        public string Symbol => Code;
        public required string DateTime { get; set; }
        public decimal Open { get; set; }
        public decimal UnderlyingPrice { get; set; }
        public long BidSideTotalVolume { get; set; }
        public long AskSideTotalVolume { get; set; }
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
        public bool IsSimTrade { get; set; }
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
        #region 建構子
        public FOPTickData(dynamic data)
        {
            try
            {
                Code = data.code;
                DateTime = data.datetime;
                Open = data.open;
                UnderlyingPrice = data.underlying_price;
                BidSideTotalVolume = data.bid_side_total_vol;
                AskSideTotalVolume = data.ask_side_total_vol;
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
                IsSimTrade = data.simtrade;
            }
            catch (Exception ex)
            {
                LogService.Instance.LogError(ex, "FOPTickData 建構失敗", "FOPTickData", LogDisplayTarget.DebugOutput);
                Code = "ERROR";
                DateTime = System.DateTime.Now.ToString();
            }
        }

        public FOPTickData()
        {
            Code = "";
            DateTime = "";
        }

        // 轉換 Tick 類型
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ConvertTickType(int originalTickType)
        {
            // 直接使用 int 參數，編譯器內聯
            return originalTickType == 2 ? -1 : originalTickType;
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

        #endregion
    }
}
