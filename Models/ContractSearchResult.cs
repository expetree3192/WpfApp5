using System;
using System.Linq;
using WpfApp5.Models;

namespace WpfApp5.Models
{
    // 🔍 合約查詢結果模型 - 繼承 ContractInfo 並加入查詢專用屬性
    public class ContractSearchResult : ContractInfo
    {
        private bool _isSelected;

        // 是否被選中 (用於 UI 選擇)
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        // 查詢時間
        public DateTime QueryTime { get; set; } = DateTime.Now;

        // 查詢關鍵字 (用於標記這個結果是透過什麼關鍵字找到的)
        public string QueryKeyword { get; set; } = "";

        // 合約月份顯示，考慮連續合約
        public string ContractMonthDisplay
        {
            get
            {
                if (ProductType == "Futures")
                {
                    // 連續合約優先顯示
                    if (IsContinuousContract)
                    {
                        if (Symbol.Contains("R1")) return "近月(R1)";
                        if (Symbol.Contains("R2")) return "次月(R2)";
                        return "連續合約";
                    }

                    // 選擇權也有到期月份
                    if (!string.IsNullOrEmpty(DeliveryMonth))
                    {
                        return FormatDeliveryMonth(DeliveryMonth);
                    }

                    // 從 Symbol 解析月份資訊
                    return ExtractMonthFromSymbol(Symbol);
                }

                // 選擇權月份顯示
                if (ProductType == "Options" && !string.IsNullOrEmpty(DeliveryMonth))
                {
                    return FormatDeliveryMonth(DeliveryMonth);
                }

                return "";
            }
        }

        // 格式化到期月份顯示 - 標記為靜態
        private static string FormatDeliveryMonth(string deliveryMonth)
        {
            try
            {
                if (string.IsNullOrEmpty(deliveryMonth) || deliveryMonth.Length != 6)
                    return deliveryMonth;

                var year = deliveryMonth[..4];
                var month = deliveryMonth[4..6];
                return $"{year}/{month}";
            }
            catch
            {
                return deliveryMonth;
            }
        }

        // 🗓️ 計算指定年月的結算日期 (每月第三個禮拜三)
        public static DateTime GetSettlementDate(int year, int month)
        {
            try
            {
                return CalculateThirdWednesday(year, month);
            }
            catch
            {
                // 如果計算失敗，返回該月最後一天
                return new DateTime(year, month, DateTime.DaysInMonth(year, month));
            }
        }

        // 🔧 修正：計算指定年月的第三個禮拜三 - 標記為靜態
        private static DateTime CalculateThirdWednesday(int year, int month)
        {
            var firstDay = new DateTime(year, month, 1);
            var firstWednesday = 1;

            // 找到第一個禮拜三
            while (firstDay.AddDays(firstWednesday - 1).DayOfWeek != DayOfWeek.Wednesday)
            {
                firstWednesday++;
            }

            // 第三個禮拜三 = 第一個禮拜三 + 14天
            var thirdWednesday = firstWednesday + 14;
            return new DateTime(year, month, thirdWednesday);
        }

        // ⚠️ 是否即將到期 (7天內)
        public bool IsNearExpiry
        {
            get
            {
                if (ProductType != "Futures" && ProductType != "Options") return false;
                if (IsContinuousContract) return false; // 連續合約不會到期

                return DaysToSettlement <= 7 && DaysToSettlement > 0;
            }
        }

        // ❌ 是否已到期
        public bool IsExpired
        {
            get
            {
                if (ProductType != "Futures" && ProductType != "Options") return false;
                if (IsContinuousContract) return false; // 連續合約不會到期

                return DaysToSettlement < 0;
            }
        }

        // 📊 距離結算天數
        public int DaysToSettlement
        {
            get
            {
                try
                {
                    var settlementDate = GetContractSettlementDate();
                    if (settlementDate == null) return int.MaxValue;

                    var currentTime = DateTime.Now;
                    var settlementDateTime = settlementDate.Value.Date.AddHours(13).AddMinutes(30);

                    return (int)(settlementDateTime - currentTime).TotalDays;
                }
                catch
                {
                    return int.MaxValue;
                }
            }
        }

        // 取得合約的結算日期
        private DateTime? GetContractSettlementDate()
        {
            try
            {
                // 優先使用 DeliveryMonth
                if (!string.IsNullOrEmpty(DeliveryMonth) && DeliveryMonth.Length == 6)
                {
                    var year = int.Parse(DeliveryMonth[..4]);
                    var month = int.Parse(DeliveryMonth[4..6]);
                    return GetSettlementDate(year, month);
                }

                // 從 Symbol 解析
                if (Symbol.Length >= 10)
                {
                    var datePart = Symbol[4..10];
                    if (datePart.All(char.IsDigit) && datePart.Length == 6)
                    {
                        var year = int.Parse(datePart[..4]);
                        var month = int.Parse(datePart[4..6]);
                        return GetSettlementDate(year, month);
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        // 📋 結算日摘要
        public string GetSettlementSummary()
        {
            try
            {
                if (ProductType != "Futures" && ProductType != "Options")
                    return "非衍生性商品";

                if (IsContinuousContract)
                    return "連續合約 (無到期日)";

                var settlementDate = GetContractSettlementDate();
                if (settlementDate == null)
                    return "無法取得結算日資訊";

                var days = DaysToSettlement;
                var status = IsExpired ? "已到期" : IsNearExpiry ? "即將到期" : "正常";

                return $"結算日: {settlementDate:yyyy/MM/dd} (週三) 13:30\n" +
                       $"剩餘天數: {Math.Max(0, days)} 天";
            }
            catch
            {
                return "結算日資訊解析錯誤";
            }
        }

        // 詳細資訊摘要
        public string DetailSummary
        {
            get
            {
                var summary = new System.Text.StringBuilder();

                summary.AppendLine($"商品類型: {ContractTypeDisplay}");
                summary.AppendLine($"交易所: {Exchange}");

                // 連續合約特殊資訊
                if (IsContinuousContract && !string.IsNullOrEmpty(ActualContractCode))
                {
                    summary.AppendLine($"實際合約: {ActualContractCode}");
                }

                if (LimitUp.HasValue && LimitDown.HasValue)
                {
                    summary.AppendLine($"漲停: {LimitUp:F2} / 跌停: {LimitDown:F2}");
                }

                if (Reference.HasValue)
                {
                    summary.AppendLine($"參考價: {Reference:F2}");
                }

                // 期貨通用資訊（不區分個股期貨）
                if (ProductType == "Futures")
                {
                    var monthDisplay = ContractMonthDisplay;
                    if (!string.IsNullOrEmpty(monthDisplay))
                        summary.AppendLine($"合約月份: {monthDisplay}");

                    // 結算資訊
                    if (!IsContinuousContract)
                    {
                        summary.AppendLine($"距離結算: {Math.Max(0, DaysToSettlement)} 天");
                    }
                }

                // 選擇權特殊資訊
                if (ProductType == "Options")
                {
                    if (StrikePrice.HasValue)
                        summary.AppendLine($"履約價: {StrikePrice:F2}");
                    if (!string.IsNullOrEmpty(OptionRight))
                        summary.AppendLine($"權利類型: {OptionRight}");
                    if (!string.IsNullOrEmpty(UnderlyingKind))
                        summary.AppendLine($"標的種類: {UnderlyingKind}");
                    var monthDisplay = ContractMonthDisplay;
                    if (!string.IsNullOrEmpty(monthDisplay))
                        summary.AppendLine($"到期月份: {monthDisplay}");

                    // 結算資訊
                    summary.AppendLine($"距離結算: {Math.Max(0, DaysToSettlement)} 天");
                }

                // 股票特殊資訊
                if (ProductType == "Stocks")
                {
                    if (DayTrade.HasValue)
                        summary.AppendLine($"當沖: {(DayTrade.Value ? "可" : "不可")}");
                    if (MarginTradingBalance.HasValue)
                        summary.AppendLine($"融資餘額: {MarginTradingBalance:N0}");
                    if (ShortSellingBalance.HasValue)
                        summary.AppendLine($"融券餘額: {ShortSellingBalance:N0}");
                }

                // 通用資訊
                if (!string.IsNullOrEmpty(UpdateDate))
                    summary.AppendLine($"更新日期: {UpdateDate}");

                return summary.ToString().Trim();
            }
        }

        // 建構函式 - 從 ContractInfo 建立
        public ContractSearchResult() : base()
        {
        }

        // 從 ContractInfo 建立，包含新屬性
        public ContractSearchResult(ContractInfo contractInfo) : base()
        {
            if (contractInfo != null)
            {
                // 複製所有基礎屬性
                ProductType = contractInfo.ProductType;
                Symbol = contractInfo.Symbol;
                Code = contractInfo.Code;
                Name = contractInfo.Name;
                Exchange = contractInfo.Exchange;
                Category = contractInfo.Category;
                AnalyzedAt = contractInfo.AnalyzedAt;
                AllProperties = contractInfo.AllProperties;
                SecurityType = contractInfo.SecurityType;
                UpdateDate = contractInfo.UpdateDate;

                // 價格相關
                LimitUp = contractInfo.LimitUp;
                LimitDown = contractInfo.LimitDown;
                Reference = contractInfo.Reference;

                // 股票特有
                MarginTradingBalance = contractInfo.MarginTradingBalance;
                ShortSellingBalance = contractInfo.ShortSellingBalance;
                DayTrade = contractInfo.DayTrade;

                // 期貨特有，包含連續合約屬性
                TargetCode = contractInfo.TargetCode;
                IsContinuousContract = contractInfo.IsContinuousContract;
                ActualContractCode = contractInfo.ActualContractCode;

                // 選擇權特有
                DeliveryMonth = contractInfo.DeliveryMonth;
                StrikePrice = contractInfo.StrikePrice;
                OptionRight = contractInfo.OptionRight;
                UnderlyingKind = contractInfo.UnderlyingKind;
            }
        }

        // 從 Symbol 解析月份資訊 - 標記為靜態
        private static string ExtractMonthFromSymbol(string symbol)
        {
            try
            {
                if (string.IsNullOrEmpty(symbol)) return "";

                // R1/R2 格式
                if (symbol.Contains("R1")) return "近月(R1)";
                if (symbol.Contains("R2")) return "次月(R2)";

                // YYYYMM 格式
                if (symbol.Length >= 10)
                {
                    var datePart = symbol[4..10];
                    if (datePart.All(char.IsDigit) && datePart.Length == 6)
                    {
                        var year = datePart[..4];
                        var month = datePart[4..6];
                        return $"{year}/{month}";
                    }
                }

                return "";
            }
            catch
            {
                return "";
            }
        }

        #region INotifyPropertyChanged Implementation

        // PropertyChanged 事件宣告，避免與基底類別衝突
        public new event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        // 覆寫基底類別的 OnPropertyChanged 方法
        protected override void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            // 觸發自己的事件
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));

            // 也觸發基底類別的事件
            base.OnPropertyChanged(propertyName);
        }

        #endregion
    }
}