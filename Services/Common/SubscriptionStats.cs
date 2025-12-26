using System;
using System.Collections.Generic;

namespace WpfApp5.Services.Common
{
    /// <summary>
    /// 訂閱統計資訊
    /// </summary>
    public class SubscriptionStats
    {
        public int TotalCount { get; set; }
        public int StockCount { get; set; }
        public int FutureCount { get; set; }
        public int OptionCount { get; set; }
        public DateTime LastUpdateTime { get; set; } = DateTime.Now;
        public Dictionary<string, int> DetailStats { get; set; } = [];

        public override string ToString()
        {
            return $"總計:{TotalCount} (股票:{StockCount}, 期貨:{FutureCount}, 選擇權:{OptionCount})";
        }
    }

    /// <summary>
    /// 訂閱項目資訊
    /// </summary>
    public class SubscriptionItem
    {
        public string ContractCode { get; set; } = string.Empty;
        public string ContractName { get; set; } = string.Empty;
        public string ContractType { get; set; } = string.Empty;
        public string QuoteType { get; set; } = string.Empty;
        public DateTime SubscribeTime { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
    }
}