using System;
using System.Runtime.CompilerServices;
using WpfApp5.Services;
using WpfApp5.Utils;

namespace WpfApp5.Models.MarketData
{
    /// <summary>
    /// 【方案 C】股票五檔資料模型 - 極致優化版本
    /// 優化重點：
    /// 1. 預分配陣列（避免 Lazy 開銷）
    /// 2. 使用 DateTimeCache（快取 + 自訂解析）
    /// 3. 減少例外處理開銷
    /// 4. 統一資料初始化邏輯
    /// </summary>
    public sealed class STKBidAskData
    {
        #region 基本屬性
        public string Code { get; private set; } = string.Empty;
        public string Symbol => Code;
        public string DateTime { get; private set; } = string.Empty;

        public bool Suspend { get; private set; }
        public bool IsSimTrade { get; private set; }
        public bool IsIntradayOdd { get; private set; }
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

        #region 陣列屬性（預分配，避免 Lazy 開銷）
        // ✅ 預分配陣列，在建構時就建立好
        private readonly decimal[] _bidPrices = new decimal[5];
        private readonly decimal[] _askPrices = new decimal[5];
        private readonly int[] _bidVolumes = new int[5];
        private readonly int[] _askVolumes = new int[5];
        private readonly int[] _diffBidVolumes = new int[5];
        private readonly int[] _diffAskVolumes = new int[5];

        // ✅ 直接返回預分配的陣列
        public decimal[] BidPrices => _bidPrices;
        public decimal[] AskPrices => _askPrices;
        public int[] BidVolumes => _bidVolumes;
        public int[] AskVolumes => _askVolumes;
        public int[] DiffBidVolumes => _diffBidVolumes;
        public int[] DiffAskVolumes => _diffAskVolumes;
        #endregion

        #region 建構子（極致優化）
        /// <summary>
        /// 從API資料建構 - 極致優化版本
        /// </summary>
        public STKBidAskData(dynamic data, bool isOddLot)
        {
            try
            {
                IsIntradayOdd = isOddLot;

                // ✅ 基本屬性（直接賦值）
                Code = data.code;
                DateTime = data.datetime;
                Suspend = DataTypeConverter.SafeConvertToBool(data.suspend);
                IsSimTrade = DataTypeConverter.SafeConvertToBool(data.simtrade);

                // ✅ 五檔資料（優化版本）
                InitializeBidAskDataOptimized(data);
            }
            catch (Exception ex)
            {
                LogService.Instance.LogError(ex, "STKBidAskData 建構失敗", "STKBidAskData", LogDisplayTarget.DebugOutput);
                Code = "ERROR";
                DateTime = System.DateTime.Now.ToString();
            }
        }

        public STKBidAskData()
        {
            // 預設建構子（陣列已在欄位初始化時建立）
        }
        #endregion

        #region 私有方法 - 極致優化的資料初始化
        /// <summary>
        /// 【極致優化】初始化五檔資料
        /// 統一例外處理，直接賦值到陣列
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeBidAskDataOptimized(dynamic data)
        {
            try
            {
                // ✅ 買價（直接賦值 + 填充陣列）
                if (data.bid_price != null && data.bid_price.Length >= 5)
                {
                    BidPrice1 = _bidPrices[0] = DataTypeConverter.SafeConvertToDecimal(data.bid_price[0]);
                    BidPrice2 = _bidPrices[1] = DataTypeConverter.SafeConvertToDecimal(data.bid_price[1]);
                    BidPrice3 = _bidPrices[2] = DataTypeConverter.SafeConvertToDecimal(data.bid_price[2]);
                    BidPrice4 = _bidPrices[3] = DataTypeConverter.SafeConvertToDecimal(data.bid_price[3]);
                    BidPrice5 = _bidPrices[4] = DataTypeConverter.SafeConvertToDecimal(data.bid_price[4]);
                }

                // ✅ 買量
                if (data.bid_volume != null && data.bid_volume.Length >= 5)
                {
                    BidVolume1 = _bidVolumes[0] = DataTypeConverter.SafeConvertToInt(data.bid_volume[0]);
                    BidVolume2 = _bidVolumes[1] = DataTypeConverter.SafeConvertToInt(data.bid_volume[1]);
                    BidVolume3 = _bidVolumes[2] = DataTypeConverter.SafeConvertToInt(data.bid_volume[2]);
                    BidVolume4 = _bidVolumes[3] = DataTypeConverter.SafeConvertToInt(data.bid_volume[3]);
                    BidVolume5 = _bidVolumes[4] = DataTypeConverter.SafeConvertToInt(data.bid_volume[4]);
                }

                // ✅ 賣價
                if (data.ask_price != null && data.ask_price.Length >= 5)
                {
                    AskPrice1 = _askPrices[0] = DataTypeConverter.SafeConvertToDecimal(data.ask_price[0]);
                    AskPrice2 = _askPrices[1] = DataTypeConverter.SafeConvertToDecimal(data.ask_price[1]);
                    AskPrice3 = _askPrices[2] = DataTypeConverter.SafeConvertToDecimal(data.ask_price[2]);
                    AskPrice4 = _askPrices[3] = DataTypeConverter.SafeConvertToDecimal(data.ask_price[3]);
                    AskPrice5 = _askPrices[4] = DataTypeConverter.SafeConvertToDecimal(data.ask_price[4]);
                }

                // ✅ 賣量
                if (data.ask_volume != null && data.ask_volume.Length >= 5)
                {
                    AskVolume1 = _askVolumes[0] = DataTypeConverter.SafeConvertToInt(data.ask_volume[0]);
                    AskVolume2 = _askVolumes[1] = DataTypeConverter.SafeConvertToInt(data.ask_volume[1]);
                    AskVolume3 = _askVolumes[2] = DataTypeConverter.SafeConvertToInt(data.ask_volume[2]);
                    AskVolume4 = _askVolumes[3] = DataTypeConverter.SafeConvertToInt(data.ask_volume[3]);
                    AskVolume5 = _askVolumes[4] = DataTypeConverter.SafeConvertToInt(data.ask_volume[4]);
                }

                // ✅ 買量變化
                if (data.diff_bid_vol != null && data.diff_bid_vol.Length >= 5)
                {
                    DiffBidVolume1 = _diffBidVolumes[0] = DataTypeConverter.SafeConvertToInt(data.diff_bid_vol[0]);
                    DiffBidVolume2 = _diffBidVolumes[1] = DataTypeConverter.SafeConvertToInt(data.diff_bid_vol[1]);
                    DiffBidVolume3 = _diffBidVolumes[2] = DataTypeConverter.SafeConvertToInt(data.diff_bid_vol[2]);
                    DiffBidVolume4 = _diffBidVolumes[3] = DataTypeConverter.SafeConvertToInt(data.diff_bid_vol[3]);
                    DiffBidVolume5 = _diffBidVolumes[4] = DataTypeConverter.SafeConvertToInt(data.diff_bid_vol[4]);
                }

                // ✅ 賣量變化
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
                LogService.Instance.LogError(ex, "初始化五檔資料失敗", "STKBidAskData", LogDisplayTarget.DebugOutput);
            }
        }
        #endregion

        #region 日期時間處理（極致優化）
        private System.DateTime? _parsedDateTime;

        /// <summary>
        /// 【極致優化】使用 DateTimeCache
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public System.DateTime GetDateTime()
        {
            if (!_parsedDateTime.HasValue)
            {
                _parsedDateTime = DateTimeCache.Parse(DateTime);
            }
            return _parsedDateTime.Value;
        }

        /// <summary>
        /// 取得完整日期時間字串 "2022/10/14 09:24:22"
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetFullDateTimeString() => GetDateTime().ToFullString();

        /// <summary>
        /// 取得日期部分 "2022/10/14"
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetDateOnlyString() => GetDateTime().ToDateOnlyString();

        /// <summary>
        /// 取得時間部分 "09:24:22"
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetTimeOnlyString() => GetDateTime().ToTimeOnlyString();
        #endregion

        #region 公開方法 - 高效能存取
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetBestBidPrice() => BidPrice1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetBestAskPrice() => AskPrice1;

        // ⚠️ 注意：這些方法會建立新陣列，高頻場景應直接使用屬性
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetSpreadPercentage()
        {
            var bestBid = GetBestBidPrice();
            var spread = GetSpread();
            return (bestBid > 0 && spread > 0) ? (spread / bestBid) * 100m : 0m;
        }

        public string GetOrderBookSummary()
        {
            return $"[{Code}] 買:{BidPrice1} 賣:{AskPrice1} 買量:{BidVolume1} 賣量:{AskVolume1} 價差:{GetSpread():F2}";
        }

        public string GetFullOrderBookInfo()
        {
            return $"""
                商品代碼: {Code}
                時間: {DateTime}
                模擬交易: {IsSimTrade}
                暫停交易: {Suspend}
                零股交易: {IsIntradayOdd}
                
                === 買方五檔 ===
                1. {BidPrice1} x {BidVolume1} (變化: {DiffBidVolume1})
                2. {BidPrice2} x {BidVolume2} (變化: {DiffBidVolume2})
                3. {BidPrice3} x {BidVolume3} (變化: {DiffBidVolume3})
                4. {BidPrice4} x {BidVolume4} (變化: {DiffBidVolume4})
                5. {BidPrice5} x {BidVolume5} (變化: {DiffBidVolume5})
                總量: {BidTotalVolume}
                
                === 賣方五檔 ===
                1. {AskPrice1} x {AskVolume1} (變化: {DiffAskVolume1})
                2. {AskPrice2} x {AskVolume2} (變化: {DiffAskVolume2})
                3. {AskPrice3} x {AskVolume3} (變化: {DiffAskVolume3})
                4. {AskPrice4} x {AskVolume4} (變化: {DiffAskVolume4})
                5. {AskPrice5} x {AskVolume5} (變化: {DiffAskVolume5})
                總量: {AskTotalVolume}
                
                === 統計資訊 ===
                買賣價差: {GetSpread():F2}
                價差百分比: {GetSpreadPercentage():F4}%
                """;
        }
        #endregion
    }
}
