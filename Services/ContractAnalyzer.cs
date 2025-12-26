using Sinopac.Shioaji;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using WpfApp5.Models;
using WpfApp5.Services;
using WpfApp5.Utils;

namespace WpfApp5.Services
{
    // 🔍 合約分析器 - 整合 LogService 版本
    public class ContractAnalyzer
    {
        private readonly LogService _logService;
        private const string LOG_SOURCE = "ContractSearch";

        // 建構函式 - 使用 LogService 單例
        public ContractAnalyzer()
        {
            _logService = LogService.Instance;
        }

        /// <summary>
        /// 獲取合約的基本屬性和價格屬性
        /// </summary>
        /// <param name="contract">合約物件</param>
        /// <param name="productType">商品類型 (Stocks/Futures/Options)</param>
        /// <returns>包含合約基本資訊的字典</returns>
        public Dictionary<string, object> GetContractBasicInfo(dynamic contract, string productType)
        {
            try
            {
                if (contract == null)
                {
                    _logService.LogError(new ArgumentNullException(nameof(contract)), "合約物件為空", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    return [];
                }

                var contractInfo = new Dictionary<string, object>();

                // 獲取基本屬性 (所有商品類型共有)
                try { contractInfo["security_type"] = contract.security_type?.ToString() ?? ""; } catch { contractInfo["security_type"] = ""; }
                try { contractInfo["code"] = contract.code?.ToString() ?? ""; } catch { contractInfo["code"] = ""; }
                try { contractInfo["symbol"] = contract.symbol?.ToString() ?? ""; } catch { contractInfo["symbol"] = ""; }
                try { contractInfo["exchange"] = contract.exchange?.ToString() ?? ""; } catch { contractInfo["exchange"] = ""; }
                try { contractInfo["name"] = contract.name?.ToString() ?? ""; } catch { contractInfo["name"] = ""; }
                try { contractInfo["target_code"] = contract.target_code?.ToString() ?? ""; } catch { contractInfo["target_code"] = ""; }

                // 創建錯誤處理委派
                void errorLogger(Exception ex, string msg) =>
                    _logService.LogError(ex, msg, LOG_SOURCE, LogDisplayTarget.SourceWindow);

                // 獲取價格屬性 (注意不同商品類型的數據類型不同)
                switch (productType)
                {
                    case "Stocks":
                        // 股票價格屬性 (double 類型)
                        try
                        {
                            var limitUp = DataTypeConverter.TryConvertToDecimal(contract.limit_up, typeof(double), (Action<Exception, string>)errorLogger);
                            contractInfo["limit_up"] = limitUp ?? 0m;
                        }
                        catch (Exception ex)
                        {
                            _logService.LogError(ex, "獲取股票漲停價失敗", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                            contractInfo["limit_up"] = 0m;
                        }

                        try
                        {
                            var limitDown = DataTypeConverter.TryConvertToDecimal(contract.limit_down, typeof(double), (Action<Exception, string>)errorLogger);
                            contractInfo["limit_down"] = limitDown ?? 0m;
                        }
                        catch (Exception ex)
                        {
                            _logService.LogError(ex, "獲取股票跌停價失敗", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                            contractInfo["limit_down"] = 0m;
                        }

                        try
                        {
                            var reference = DataTypeConverter.TryConvertToDecimal(contract.reference, typeof(double), (Action<Exception, string>)errorLogger);
                            contractInfo["reference"] = reference ?? 0m;
                        }
                        catch (Exception ex)
                        {
                            _logService.LogError(ex, "獲取股票參考價失敗", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                            contractInfo["reference"] = 0m;
                        }
                        break;

                    case "Futures":
                    case "Options":
                        // 期貨和選擇權價格屬性 (float 類型)
                        try
                        {
                            var limitUp = DataTypeConverter.TryConvertToDecimal(contract.limit_up, typeof(float), (Action<Exception, string>)errorLogger);
                            contractInfo["limit_up"] = limitUp ?? 0m;
                        }
                        catch (Exception ex)
                        {
                            _logService.LogError(ex, $"獲取{productType}漲停價失敗", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                            contractInfo["limit_up"] = 0m;
                        }

                        try
                        {
                            var limitDown = DataTypeConverter.TryConvertToDecimal(contract.limit_down, typeof(float), (Action<Exception, string>)errorLogger);
                            contractInfo["limit_down"] = limitDown ?? 0m;
                        }
                        catch (Exception ex)
                        {
                            _logService.LogError(ex, $"獲取{productType}跌停價失敗", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                            contractInfo["limit_down"] = 0m;
                        }

                        try
                        {
                            var reference = DataTypeConverter.TryConvertToDecimal(contract.reference, typeof(float), (Action<Exception, string>)errorLogger);
                            contractInfo["reference"] = reference ?? 0m;
                        }
                        catch (Exception ex)
                        {
                            _logService.LogError(ex, $"獲取{productType}參考價失敗", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                            contractInfo["reference"] = 0m;
                        }
                        break;
                }

                // 記錄成功信息
                _logService.LogDebug($"✅ 成功獲取 {productType} 合約 {contractInfo["symbol"]} 的基本資訊", LOG_SOURCE, LogDisplayTarget.SourceWindow);

                return contractInfo;
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"獲取合約基本資訊失敗: {ex.Message}", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                return [];
            }
        }

        /// <summary>
        /// 獲取合約的所有屬性
        /// </summary>
        /// <param name="contract">合約物件</param>
        /// <returns>包含合約所有屬性的字典</returns>
        public Dictionary<string, object> GetContractAllProperties(dynamic contract)
        {
            try
            {
                if (contract == null)
                {
                    _logService.LogError(new ArgumentNullException(nameof(contract)), "合約物件為空", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    return [];
                }

                var properties = new Dictionary<string, object>();
                var contractType = contract.GetType();
                var propertyInfos = contractType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (var property in propertyInfos)
                {
                    try
                    {
                        var value = property.GetValue(contract);
                        properties[property.Name] = value ?? "";
                    }
                    catch (Exception ex)
                    {
                        _logService.LogError(ex, $"獲取屬性 {property.Name} 失敗: {ex.Message}", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                        properties[property.Name] = "";
                    }
                }

                _logService.LogDebug($"✅ 成功獲取合約的所有屬性，共 {properties.Count} 個", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                return properties;
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"獲取合約所有屬性失敗: {ex.Message}", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                return [];
            }
        }

        public static string GetActualContractCode(IContract contract)
        {
            try
            {
                var targetCode = contract.target_code;  // 嘗試訪問 target_code
                string? targetCodeStr = targetCode?.ToString(); // 使用 null 條件運算子和 null 合併運算子來避免警告

                // 檢查 target_code 是否有值
                if (!string.IsNullOrEmpty(targetCodeStr))
                {
                    return targetCodeStr;
                }
            }
            catch
            {
                // 如果訪問 target_code 失敗，忽略錯誤
                // 這意味著 target_code 不存在或無法訪問
            }
            return contract.code;   // 使用 code 作為默認值
        }
        // 統一合約分析方法 - 基於商品類型差異優化
        public ContractInfo? AnalyzeContract(dynamic contract, string productType, string symbol, string apiPath)
        {
            try
            {
                if (contract == null) return null;

                var contractInfo = new ContractInfo
                {
                    ProductType = productType,
                    Symbol = symbol,
                    AnalyzedAt = DateTime.Now
                };

                var contractType = contract.GetType();
                var properties = contractType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                var allPropertiesInfo = new StringBuilder();
                allPropertiesInfo.AppendLine($"=== {productType} 合約資訊 ===");
                allPropertiesInfo.AppendLine($"API路徑: {apiPath}");
                allPropertiesInfo.AppendLine($"商品代號: {symbol}");
                allPropertiesInfo.AppendLine($"物件類型: {contractType.Name}");
                allPropertiesInfo.AppendLine("");

                _logService.LogDebug($"=== 🔍 {symbol} 屬性分析 ({productType}) ===", LOG_SOURCE, LogDisplayTarget.SourceWindow);

                foreach (var property in properties)
                {
                    try
                    {
                        var value = property.GetValue(contract);
                        var valueStr = value?.ToString() ?? "null";
                        var propertyType = property.PropertyType;
                        var typeName = DataTypeConverter.GetFriendlyTypeName(propertyType); // 使用 DataTypeConverter

                        var typeInfo = $"{property.Name} ({typeName}) = {valueStr}";
                        allPropertiesInfo.AppendLine(typeInfo);

                        // 基於商品類型差異進行屬性映射
                        MapPropertyByProductType(contractInfo, property.Name, value, propertyType, productType);
                    }
                    catch (Exception ex)
                    {
                        var errorInfo = $"{property.Name}: [錯誤] {ex.Message}";
                        allPropertiesInfo.AppendLine(errorInfo);
                        _logService.LogError(ex, $"屬性錯誤: {errorInfo}", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    }
                }

                contractInfo.AllProperties = allPropertiesInfo.ToString();

                // 處理連續期貨合約的 target_code 優先級
                if (!string.IsNullOrEmpty(contractInfo.TargetCode))
                {
                    // 合約：target_code 優先級 > code
                    _logService.LogDebug($"🔄 合約檢測: Symbol={contractInfo.Symbol}, Code={contractInfo.Code}, TargetCode={contractInfo.TargetCode}", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                }

                _logService.LogDebug($"=== ✅ {symbol} 分析完成 ===", LOG_SOURCE, LogDisplayTarget.SourceWindow);

                return contractInfo;
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "分析合約失敗", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                return null;
            }
        }

        // 基於商品類型差異的屬性映射 - 修正版
        private void MapPropertyByProductType(ContractInfo contractInfo, string propertyName, object? value, Type propertyType, string productType)
        {
            if (value == null) return;

            var lowerName = propertyName.ToLowerInvariant();
            var valueStr = value.ToString() ?? "";

            // 🔧 通用屬性映射 (所有商品都有)
            switch (lowerName)
            {
                case "code":
                    contractInfo.Code = valueStr;
                    _logService.LogDebug($"✅ 商品代碼: {valueStr}", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    break;
                case "symbol":
                    contractInfo.Symbol = valueStr;
                    _logService.LogDebug($"✅ 商品代號: {valueStr}", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    break;
                case "name":
                    contractInfo.Name = valueStr;
                    _logService.LogDebug($"✅ 商品名稱: {valueStr}", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    break;
                case "exchange":
                    contractInfo.Exchange = valueStr;
                    _logService.LogDebug($"✅ 交易所: {valueStr}", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    break;
                case "category":
                    contractInfo.Category = valueStr;
                    _logService.LogDebug($"✅ 商品類別: {valueStr}", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    break;
                case "update_date":
                    contractInfo.UpdateDate = valueStr;
                    _logService.LogDebug($"✅ 更新日期: {valueStr}", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    break;
                case "security_type":
                    contractInfo.SecurityType = valueStr;
                    _logService.LogDebug($"✅ 證券類型: {valueStr}", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    break;
            }

            // 🎯 基於商品類型差異的特殊屬性映射
            switch (productType)
            {
                case "Stocks":
                    MapStockSpecificProperties(contractInfo, lowerName, value, propertyType);
                    break;
                case "Futures":
                    MapFutureSpecificProperties(contractInfo, lowerName, value, propertyType);
                    break;
                case "Options":
                    MapOptionSpecificProperties(contractInfo, lowerName, value, propertyType);
                    break;
                case "Indexs":
                    // 指數最簡單，只需要基礎屬性
                    break;
            }
        }

        /// <summary>
        /// 🏢 股票特有屬性映射 - double類型價格，融資融券資訊
        /// </summary>
        private void MapStockSpecificProperties(ContractInfo contractInfo, string lowerName, object value, Type propertyType)
        {
            // 創建日誌記錄委派
            void logError(Exception ex, string message) =>
                _logService.LogError(ex, message, LOG_SOURCE, LogDisplayTarget.SourceWindow);

            switch (lowerName)
            {
                case "limitup":
                case "limit_up":
                    var limitUp = DataTypeConverter.TryConvertToDecimal(value, propertyType, logError);
                    if (limitUp.HasValue)
                    {
                        contractInfo.LimitUp = limitUp.Value;
                        _logService.LogDebug($"✅ 股票漲停價: {limitUp.Value} (double -> decimal)", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    }
                    break;
                case "limitdown":
                case "limit_down":
                    var limitDown = DataTypeConverter.TryConvertToDecimal(value, propertyType, logError);
                    if (limitDown.HasValue)
                    {
                        contractInfo.LimitDown = limitDown.Value;
                        _logService.LogDebug($"✅ 股票跌停價: {limitDown.Value} (double -> decimal)", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    }
                    break;
                case "reference":
                    var reference = DataTypeConverter.TryConvertToDecimal(value, propertyType, logError);
                    if (reference.HasValue)
                    {
                        contractInfo.Reference = reference.Value;
                        _logService.LogDebug($"✅ 股票參考價: {reference.Value} (double -> decimal)", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    }
                    break;
                case "margin_trading_balance":
                    var marginBalance = DataTypeConverter.TryConvertToInt(value, propertyType, logError);
                    if (marginBalance.HasValue)
                    {
                        contractInfo.MarginTradingBalance = marginBalance.Value;
                        _logService.LogDebug($"✅ 融資餘額: {marginBalance.Value}", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    }
                    break;
                case "short_selling_balance":
                    var shortBalance = DataTypeConverter.TryConvertToInt(value, propertyType, logError);
                    if (shortBalance.HasValue)
                    {
                        contractInfo.ShortSellingBalance = shortBalance.Value;
                        _logService.LogDebug($"✅ 融券餘額: {shortBalance.Value}", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    }
                    break;
                case "daytrade":
                case "day_trade":
                    var dayTrade = DataTypeConverter.TryConvertToBool(value, propertyType, logError);
                    if (dayTrade.HasValue)
                    {
                        contractInfo.DayTrade = dayTrade.Value;
                        _logService.LogDebug($"✅ 當沖標記: {dayTrade.Value}", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    }
                    break;
            }
        }

        /// <summary>
        /// 📈 期貨特有屬性映射 - float類型價格
        /// </summary>
        private void MapFutureSpecificProperties(ContractInfo contractInfo, string lowerName, object value, Type propertyType)
        {
            // 創建日誌記錄委派
            void logError(Exception ex, string message) =>
                _logService.LogError(ex, message, LOG_SOURCE, LogDisplayTarget.SourceWindow);

            switch (lowerName)
            {
                case "limitup":
                case "limit_up":
                    var limitUp = DataTypeConverter.TryConvertToDecimal(value, propertyType, logError);
                    if (limitUp.HasValue)
                    {
                        contractInfo.LimitUp = limitUp.Value;
                        _logService.LogDebug($"✅ 期貨漲停價: {limitUp.Value} (float -> decimal)", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    }
                    break;
                case "limitdown":
                case "limit_down":
                    var limitDown = DataTypeConverter.TryConvertToDecimal(value, propertyType, logError);
                    if (limitDown.HasValue)
                    {
                        contractInfo.LimitDown = limitDown.Value;
                        _logService.LogDebug($"✅ 期貨跌停價: {limitDown.Value} (float -> decimal)", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    }
                    break;
                case "reference":
                    var reference = DataTypeConverter.TryConvertToDecimal(value, propertyType, logError);
                    if (reference.HasValue)
                    {
                        contractInfo.Reference = reference.Value;
                        _logService.LogDebug($"✅ 期貨參考價: {reference.Value} (float -> decimal)", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    }
                    break;
                case "target_code":
                    contractInfo.TargetCode = value.ToString();
                    _logService.LogDebug($"✅ 期貨目標代碼: {value}", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    break;
            }
        }

        /// <summary>
        /// 🎲 選擇權特有屬性映射 - float類型價格，履約價、權利類型等
        /// </summary>
        private void MapOptionSpecificProperties(ContractInfo contractInfo, string lowerName, object value, Type propertyType)
        {
            // 創建日誌記錄委派
            void logError(Exception ex, string message) =>
                _logService.LogError(ex, message, LOG_SOURCE, LogDisplayTarget.SourceWindow);

            switch (lowerName)
            {
                case "limitup":
                case "limit_up":
                    var limitUp = DataTypeConverter.TryConvertToDecimal(value, propertyType, logError);
                    if (limitUp.HasValue)
                    {
                        contractInfo.LimitUp = limitUp.Value;
                        _logService.LogDebug($"✅ 選擇權漲停價: {limitUp.Value} (float -> decimal)", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    }
                    break;
                case "limitdown":
                case "limit_down":
                    var limitDown = DataTypeConverter.TryConvertToDecimal(value, propertyType, logError);
                    if (limitDown.HasValue)
                    {
                        contractInfo.LimitDown = limitDown.Value;
                        _logService.LogDebug($"✅ 選擇權跌停價: {limitDown.Value} (float -> decimal)", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    }
                    break;
                case "reference":
                    var reference = DataTypeConverter.TryConvertToDecimal(value, propertyType, logError);
                    if (reference.HasValue)
                    {
                        contractInfo.Reference = reference.Value;
                        _logService.LogDebug($"✅ 選擇權參考價: {reference.Value} (float -> decimal)", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    }
                    break;
                case "delivery_month":
                    contractInfo.DeliveryMonth = value.ToString();
                    _logService.LogDebug($"✅ 到期月份: {value}", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    break;
                case "strike_price":
                    var strikePrice = DataTypeConverter.TryConvertToDecimal(value, propertyType, logError);
                    if (strikePrice.HasValue)
                    {
                        contractInfo.StrikePrice = strikePrice.Value;
                        _logService.LogDebug($"✅ 履約價格: {strikePrice.Value}", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    }
                    break;
                case "option_right":
                    contractInfo.OptionRight = value.ToString();
                    _logService.LogDebug($"✅ 權利類型: {value}", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    break;
                case "underlying_kind":
                    contractInfo.UnderlyingKind = value.ToString();
                    _logService.LogDebug($"✅ 標的種類: {value}", LOG_SOURCE, LogDisplayTarget.SourceWindow);
                    break;
            }
        }
    }


    // 🔍 商品類型驗證器 - 基於商品類型差異分析
    public class ProductTypeValidator
    {
        private readonly Dictionary<string, ProductTypeInfo> _productTypeInfos;

        public ProductTypeValidator()
        {
            _productTypeInfos = new Dictionary<string, ProductTypeInfo>
            {
                ["Stocks"] = new ProductTypeInfo
                {
                    ProductType = "Stocks",
                    PriceType = "double",
                    HasPriceLimit = true,
                    ValidExchanges = ["TSE", "OTC", "OES"],
                    SpecialProperties = ["margin_trading_balance", "short_selling_balance", "day_trade"],
                    Examples = ["2330", "2454", "3293", "4194", "073152"]
                },
                ["Futures"] = new ProductTypeInfo
                {
                    ProductType = "Futures",
                    PriceType = "float",
                    HasPriceLimit = true,
                    ValidExchanges = ["TAIFEX"],
                    SpecialProperties = ["target_code", "continuous_contract"],
                    Examples = ["TXFR1", "TXFR2", "CDFR1", "TXF202509", "CDF202509"]
                },
                ["Options"] = new ProductTypeInfo
                {
                    ProductType = "Options",
                    PriceType = "float",
                    HasPriceLimit = true,
                    ValidExchanges = ["TAIFEX"],
                    SpecialProperties = ["delivery_month", "strike_price", "option_right", "underlying_kind"],
                    Examples = ["TXO20250924600C", "CDO202512140000P"]
                },
                ["Indexs"] = new ProductTypeInfo
                {
                    ProductType = "Indexs",
                    PriceType = "none",
                    HasPriceLimit = false,
                    ValidExchanges = ["TSE", "OTC", "TAIFEX"],
                    SpecialProperties = [],
                    Examples = ["001", "TAIWANVIX"]
                }
            };
        }

        public ProductTypeInfo? GetProductTypeInfo(string productType)
        {
            return _productTypeInfos.TryGetValue(productType, out var info) ? info : null;
        }

        public bool IsValidProductType(string productType)
        {
            return _productTypeInfos.ContainsKey(productType);
        }

        public List<string> GetAllProductTypes()
        {
            return [.. _productTypeInfos.Keys];
        }

        public List<string> GetValidExchanges(string productType)
        {
            return GetProductTypeInfo(productType)?.ValidExchanges ?? [];
        }
    }

    // 🔍 商品類型資訊
    public class ProductTypeInfo
    {
        public string ProductType { get; set; } = "";
        public string PriceType { get; set; } = "";
        public bool HasPriceLimit { get; set; }
        public List<string> ValidExchanges { get; set; } = [];
        public List<string> SpecialProperties { get; set; } = [];
        public List<string> Examples { get; set; } = [];
    }
}
