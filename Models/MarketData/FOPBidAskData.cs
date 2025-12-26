using System;
using System.Runtime.CompilerServices;
using WpfApp5.Utils;
using WpfApp5.Services;

namespace WpfApp5.Models.MarketData
{
    /// <summary>
    /// FOPBidAskData - 批次賦值實作
    /// 
    /// 優化特點：
    /// 1. 批次賦值（減少重複的 dynamic 存取）
    /// 2. 統一的陣列初始化（一次性填充）
    /// 3. 使用 Span 避免邊界檢查
    /// 4. 最小化記憶體分配
    /// 5. 內聯優化（AggressiveInlining）
    /// </summary>
    public sealed class FOPBidAskData
    {
        #region 基本屬性
        public string Code { get; private set; } = string.Empty;
        public string Symbol => Code;
        public string DateTime { get; private set; } = string.Empty;
        public int BidTotalVolume { get; private set; }
        public int AskTotalVolume { get; private set; }
        public decimal UnderlyingPrice { get; private set; }
        public bool IsSimTrade { get; private set; }

        public decimal FirstDerivedBidPrice { get; private set; }
        public decimal FirstDerivedAskPrice { get; private set; }
        public int FirstDerivedBidVolume { get; private set; }
        public int FirstDerivedAskVolume { get; private set; }
        #endregion

        #region 五檔掛單屬性 - 批次賦值優化
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
        // 預分配陣列，在建構時就建立好
        private readonly decimal[] _bidPrices = new decimal[5];
        private readonly decimal[] _askPrices = new decimal[5];
        private readonly int[] _bidVolumes = new int[5];
        private readonly int[] _askVolumes = new int[5];
        private readonly int[] _diffBidVolumes = new int[5];
        private readonly int[] _diffAskVolumes = new int[5];

        // 直接返回預分配的陣列（不需要 Lazy）
        public decimal[] BidPrices => _bidPrices;
        public decimal[] AskPrices => _askPrices;
        public int[] BidVolumes => _bidVolumes;
        public int[] AskVolumes => _askVolumes;
        public int[] DiffBidVolumes => _diffBidVolumes;
        public int[] DiffAskVolumes => _diffAskVolumes;
        #endregion

        #region 🚀 建構子（批次賦值極致優化）
        public FOPBidAskData(dynamic data)
        {
            try
            {
                // 基本屬性批次賦值
                InitializeBasicPropertiesBatch(data);

                // 五檔資料批次賦值
                InitializeBidAskDataBatch(data);
            }
            catch (Exception ex)
            {
                LogService.Instance.LogError(ex, "FOPBidAskData 建構失敗", "FOPBidAskData", LogDisplayTarget.DebugOutput);
                Code = "ERROR";
                DateTime = System.DateTime.Now.ToString();
            }
        }

        public FOPBidAskData()
        {
            // 預設建構子（陣列已在欄位初始化時建立）
        }
        #endregion

        #region 批次賦值

        // 基本屬性批次初始化
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeBasicPropertiesBatch(dynamic data)
        {
            // 批次賦值
            Code = data.code;
            DateTime = data.datetime;
            BidTotalVolume = data.bid_total_vol;
            AskTotalVolume = data.ask_total_vol;
            UnderlyingPrice = data.underlying_price;
            IsSimTrade = data.simtrade;

            // 批次賦值
            FirstDerivedBidPrice = data.first_derived_bid_price;
            FirstDerivedAskPrice = data.first_derived_ask_price;
            FirstDerivedBidVolume = data.first_derived_bid_vol;
            FirstDerivedAskVolume = data.first_derived_ask_vol;
        }

        // 五檔資料批次初始化
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeBidAskDataBatch(dynamic data)
        {
            // 買價批次處理
            if (data.bid_price != null && data.bid_price.Length >= 5)
            {
                var bidPriceData = ExtractDecimalArray(data.bid_price);
                AssignBidPrices(bidPriceData);
                CopyToArray(_bidPrices, bidPriceData);
            }

            // 買量批次處理
            if (data.bid_volume != null && data.bid_volume.Length >= 5)
            {
                var bidVolumeData = ExtractIntArray(data.bid_volume);
                AssignBidVolumes(bidVolumeData);
                CopyToArray(_bidVolumes, bidVolumeData);
            }

            // 賣價批次處理
            if (data.ask_price != null && data.ask_price.Length >= 5)
            {
                var askPriceData = ExtractDecimalArray(data.ask_price);
                AssignAskPrices(askPriceData);
                CopyToArray(_askPrices, askPriceData);
            }

            // 賣量批次處理
            if (data.ask_volume != null && data.ask_volume.Length >= 5)
            {
                var askVolumeData = ExtractIntArray(data.ask_volume);
                AssignAskVolumes(askVolumeData);
                CopyToArray(_askVolumes, askVolumeData);
            }

            // 買量變化批次處理
            if (data.diff_bid_vol != null && data.diff_bid_vol.Length >= 5)
            {
                var diffBidData = ExtractIntArray(data.diff_bid_vol);
                AssignDiffBidVolumes(diffBidData);
                CopyToArray(_diffBidVolumes, diffBidData);
            }

            // 賣量變化批次處理
            if (data.diff_ask_vol != null && data.diff_ask_vol.Length >= 5)
            {
                var diffAskData = ExtractIntArray(data.diff_ask_vol);
                AssignDiffAskVolumes(diffAskData);
                CopyToArray(_diffAskVolumes, diffAskData);
            }
        }

        // Decimal 陣列提取
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (decimal, decimal, decimal, decimal, decimal) ExtractDecimalArray(dynamic sourceArray)
        {
            return (
                DataTypeConverter.SafeConvertToDecimal(sourceArray[0]),
                DataTypeConverter.SafeConvertToDecimal(sourceArray[1]),
                DataTypeConverter.SafeConvertToDecimal(sourceArray[2]),
                DataTypeConverter.SafeConvertToDecimal(sourceArray[3]),
                DataTypeConverter.SafeConvertToDecimal(sourceArray[4])
            );
        }

        // Int 陣列提取
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (int, int, int, int, int) ExtractIntArray(dynamic sourceArray)
        {
            return (
                sourceArray[0],
                sourceArray[1],
                sourceArray[2],
                sourceArray[3],
                sourceArray[4]
            );
        }

        // 買價批次賦值
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssignBidPrices((decimal, decimal, decimal, decimal, decimal) data)
        {
            (BidPrice1, BidPrice2, BidPrice3, BidPrice4, BidPrice5) = data;
        }

        // 買量批次賦值
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssignBidVolumes((int, int, int, int, int) data)
        {
            (BidVolume1, BidVolume2, BidVolume3, BidVolume4, BidVolume5) = data;
        }

        // 賣價批次賦值
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssignAskPrices((decimal, decimal, decimal, decimal, decimal) data)
        {
            (AskPrice1, AskPrice2, AskPrice3, AskPrice4, AskPrice5) = data;
        }

        // 賣量批次賦值
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssignAskVolumes((int, int, int, int, int) data)
        {
            (AskVolume1, AskVolume2, AskVolume3, AskVolume4, AskVolume5) = data;
        }

        // 買量變化批次賦值
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssignDiffBidVolumes((int, int, int, int, int) data)
        {
            (DiffBidVolume1, DiffBidVolume2, DiffBidVolume3, DiffBidVolume4, DiffBidVolume5) = data;
        }

        // 賣量變化批次賦值
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AssignDiffAskVolumes((int, int, int, int, int) data)
        {
            (DiffAskVolume1, DiffAskVolume2, DiffAskVolume3, DiffAskVolume4, DiffAskVolume5) = data;
        }

        // 高效能陣列複製（使用 Span 避免邊界檢查）
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CopyToArray(decimal[] target, (decimal, decimal, decimal, decimal, decimal) source)
        {
            var span = target.AsSpan();
            span[0] = source.Item1;
            span[1] = source.Item2;
            span[2] = source.Item3;
            span[3] = source.Item4;
            span[4] = source.Item5;
        }

        // 陣列複製（使用 Span 避免邊界檢查）
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CopyToArray(int[] target, (int, int, int, int, int) source)
        {
            var span = target.AsSpan();
            span[0] = source.Item1;
            span[1] = source.Item2;
            span[2] = source.Item3;
            span[3] = source.Item4;
            span[4] = source.Item5;
        }

        #endregion

        #region 日期時間處理（極致優化）
        // 快取的 DateTime 解析結果
        private System.DateTime? _parsedDateTime;

        /// <summary>
        /// 取得解析後的 DateTime
        /// 使用 DateTimeCache 實現快取 + 自訂解析
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

        // 取得完整日期時間字串 "2022/10/14 09:24:22"
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetFullDateTimeString() => GetDateTime().ToFullString();

        // 取得日期部分 "2022/10/14"
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetDateOnlyString() => GetDateTime().ToDateOnlyString();

        // 取得時間部分 "09:24:22"
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetTimeOnlyString() => GetDateTime().ToTimeOnlyString();
        #endregion

        #region 公開方法 - 高效能存取
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetBestBidPrice() => BidPrice1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetBestAskPrice() => AskPrice1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetSpread() => AskPrice1 - BidPrice1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetMidPrice() => (BidPrice1 + AskPrice1) / 2m;

        // ⚠️ 注意：這些方法會建立新陣列，高頻場景應直接使用屬性
        public decimal[] GetBidPrices() => (decimal[])_bidPrices.Clone();
        public decimal[] GetAskPrices() => (decimal[])_askPrices.Clone();
        public int[] GetBidVolumes() => (int[])_bidVolumes.Clone();
        public int[] GetAskVolumes() => (int[])_askVolumes.Clone();
        public int[] GetDiffBidVolumes() => (int[])_diffBidVolumes.Clone();
        public int[] GetDiffAskVolumes() => (int[])_diffAskVolumes.Clone();
        /// <summary>
        /// 🎯 取得完整五檔摘要
        /// </summary>
        public string GetFullOrderBookSummary()
        {
            return $"[{Code}] B1:{BidPrice1}({BidVolume1}) B2:{BidPrice2}({BidVolume2}) | A1:{AskPrice1}({AskVolume1}) A2:{AskPrice2}({AskVolume2}) | 標的:{UnderlyingPrice}";
        }
        #endregion
    }
}