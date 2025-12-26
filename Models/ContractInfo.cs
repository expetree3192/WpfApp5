using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfApp5.Models
{
    // 合約資訊模型 - 基於商品類型差異分析優化，加入 INotifyPropertyChanged 支援
    public class ContractInfo : INotifyPropertyChanged
    {
        #region Private Fields
        private string _productType = "";
        private string _symbol = "";
        private string _code = "";
        private string _name = "";
        private string _exchange = "";
        private string _category = "";
        private DateTime _analyzedAt;
        private string _allProperties = "";
        private string? _securityType;
        private string? _updateDate;
        private decimal? _limitUp;
        private decimal? _limitDown;
        private decimal? _reference;
        private int? _marginTradingBalance;
        private int? _shortSellingBalance;
        private bool? _dayTrade;
        private string? _targetCode;
        private bool _isContinuousContract = false;
        private string? _actualContractCode;
        private string? _deliveryMonth;
        private decimal? _strikePrice;
        private string? _optionRight;
        private string? _underlyingKind;
        #endregion

        #region Public Properties
        // 基礎屬性 (所有商品都有)
        public string ProductType
        {
            get => _productType;
            set => SetProperty(ref _productType, value);
        }

        public string Symbol
        {
            get => _symbol;
            set => SetProperty(ref _symbol, value);
        }

        public string Code
        {
            get => _code;
            set => SetProperty(ref _code, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Exchange
        {
            get => _exchange;
            set => SetProperty(ref _exchange, value);
        }

        public string Category
        {
            get => _category;
            set => SetProperty(ref _category, value);
        }

        public DateTime AnalyzedAt
        {
            get => _analyzedAt;
            set => SetProperty(ref _analyzedAt, value);
        }

        public string AllProperties
        {
            get => _allProperties;
            set => SetProperty(ref _allProperties, value);
        }

        public string? SecurityType
        {
            get => _securityType;
            set => SetProperty(ref _securityType, value);
        }

        public string? UpdateDate
        {
            get => _updateDate;
            set => SetProperty(ref _updateDate, value);
        }

        // 價格相關屬性 (股票、期貨、選擇權有)
        public decimal? LimitUp
        {
            get => _limitUp;
            set => SetProperty(ref _limitUp, value);
        }

        public decimal? LimitDown
        {
            get => _limitDown;
            set => SetProperty(ref _limitDown, value);
        }

        public decimal? Reference
        {
            get => _reference;
            set => SetProperty(ref _reference, value);
        }

        // 股票特有屬性
        public int? MarginTradingBalance
        {
            get => _marginTradingBalance;
            set => SetProperty(ref _marginTradingBalance, value);
        }

        public int? ShortSellingBalance
        {
            get => _shortSellingBalance;
            set => SetProperty(ref _shortSellingBalance, value);
        }

        public bool? DayTrade
        {
            get => _dayTrade;
            set => SetProperty(ref _dayTrade, value);
        }

        // 期貨特有屬性
        public string? TargetCode
        {
            get => _targetCode;
            set => SetProperty(ref _targetCode, value);
        }

        public bool IsContinuousContract
        {
            get => _isContinuousContract;
            set => SetProperty(ref _isContinuousContract, value);
        }

        public string? ActualContractCode
        {
            get => _actualContractCode;
            set => SetProperty(ref _actualContractCode, value);
        }

        // 選擇權特有屬性
        public string? DeliveryMonth
        {
            get => _deliveryMonth;
            set => SetProperty(ref _deliveryMonth, value);
        }

        public decimal? StrikePrice
        {
            get => _strikePrice;
            set => SetProperty(ref _strikePrice, value);
        }

        public string? OptionRight
        {
            get => _optionRight;
            set => SetProperty(ref _optionRight, value);
        }

        public string? UnderlyingKind
        {
            get => _underlyingKind;
            set => SetProperty(ref _underlyingKind, value);
        }
        #endregion

        #region Display Properties
        // 顯示用屬性，考慮連續合約
        public string DisplayName => !string.IsNullOrEmpty(Name) ? $"{Symbol} - {Name}" : Symbol;

        public string PriceRangeText => LimitUp.HasValue && LimitDown.HasValue
            ? $"漲停: {LimitUp:F2}, 跌停: {LimitDown:F2}"
            : "無價格限制";

        // 連續合約顯示資訊
        public string ContractTypeDisplay
        {
            get
            {
                if (IsContinuousContract)
                {
                    var contractType = Symbol.Contains("R1") ? "近月" : Symbol.Contains("R2") ? "次月" : "連續";
                    return $"{contractType}合約 (實際: {ActualContractCode})";
                }
                return ProductType switch
                {
                    "Stocks" => "股票",
                    "Futures" => "期貨",
                    "Options" => "選擇權",
                    "Indexs" => "指數",
                    _ => ProductType
                };
            }
        }

        // 詳細合約資訊
        public string DetailedInfo
        {
            get
            {
                var info = new System.Text.StringBuilder();
                info.AppendLine($"商品類型: {ContractTypeDisplay}");
                info.AppendLine($"交易所: {Exchange}");

                if (!string.IsNullOrEmpty(SecurityType))
                    info.AppendLine($"證券類型: {SecurityType}");

                if (!string.IsNullOrEmpty(Category))
                    info.AppendLine($"類別: {Category}");

                if (!string.IsNullOrEmpty(UpdateDate))
                    info.AppendLine($"更新日期: {UpdateDate}");

                if (LimitUp.HasValue && LimitDown.HasValue && Reference.HasValue)
                {
                    info.AppendLine($"參考價: {Reference:F2}");
                    info.AppendLine($"漲停價: {LimitUp:F2}");
                    info.AppendLine($"跌停價: {LimitDown:F2}");
                }

                // 連續期貨特殊資訊
                if (IsContinuousContract && !string.IsNullOrEmpty(ActualContractCode))
                {
                    info.AppendLine($"實際合約: {ActualContractCode}");
                }

                // 選擇權特殊資訊
                if (ProductType == "Options")
                {
                    if (!string.IsNullOrEmpty(DeliveryMonth))
                        info.AppendLine($"到期月份: {DeliveryMonth}");
                    if (StrikePrice.HasValue)
                        info.AppendLine($"履約價: {StrikePrice:F2}");
                    if (!string.IsNullOrEmpty(OptionRight))
                        info.AppendLine($"權利類型: {OptionRight}");
                    if (!string.IsNullOrEmpty(UnderlyingKind))
                        info.AppendLine($"標的種類: {UnderlyingKind}");
                }

                // 股票特殊資訊
                if (ProductType == "Stocks")
                {
                    if (DayTrade.HasValue)
                        info.AppendLine($"當沖: {(DayTrade.Value ? "可" : "不可")}");
                    if (MarginTradingBalance.HasValue)
                        info.AppendLine($"融資餘額: {MarginTradingBalance:N0}");
                    if (ShortSellingBalance.HasValue)
                        info.AppendLine($"融券餘額: {ShortSellingBalance:N0}");
                }

                return info.ToString().Trim();
            }
        }
        #endregion

        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}