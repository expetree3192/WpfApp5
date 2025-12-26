using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using WpfApp5.Models;
using WpfApp5.Services;

namespace WpfApp5.Services
{
    // 🔍 合約查詢服務 - 優化版本 (移除重複檢查)
    public class ContractQueryService
    {
        private readonly LogService _logService;
        private readonly ContractAnalyzer _contractAnalyzer;
        private const string LOG_SOURCE = "ContractSearch";

        // 建構函式 - 使用 LogService 單例
        public ContractQueryService()
        {
            _logService = LogService.Instance;
            _contractAnalyzer = new ContractAnalyzer();

            _logService.LogInfo("ContractQueryService 已初始化", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
        }

        // 🌟 完全通用的商品查詢架構 - 基於 API 動態發現
        public async Task<List<ContractSearchResult>> QueryUniversalContractsWithApiDiscovery(string productType, string exchange = "", string symbol = "")
        {
            var results = new List<ContractSearchResult>();

            try
            {
                _logService.LogInfo("=== 🌟 開始_完全通用商品查詢架構 (API動態發現) ===", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                // 🎯 使用第一層查詢方法取得合約群組，包含所有必要的檢查
                var contractGroup = GetContractByOneParameter(productType);
                if (contractGroup == null)
                {
                    _logService.LogError($"無法取得 {productType} 合約群組", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return results;
                }

                _logService.LogInfo($"📊 開始查詢{productType}商品類別架構下的內容...", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                // 🔍 動態發現該商品類型的所有可用交易所
                var availableExchanges = DiscoverAvailableExchanges(contractGroup, productType);

                if (availableExchanges is not null && availableExchanges.Count > 0)
                {
                    _logService.LogInfo($"📊 確認 {productType} 商品類別有可用交易所，開始執行通用查詢策略", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                    // 🎯 執行通用查詢策略
                    await ExecuteUniversalQueryStrategies(contractGroup, productType, symbol, exchange, availableExchanges, results);
                }
                else
                {
                    _logService.LogWarning($"📊 無法發現 {productType} 的可用交易所", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                }

                _logService.LogInfo($"=== ✅ 查詢完成，找到 {results.Count} 個合約 ===", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                _logService.LogInfo("=== 🌟 結束_完全通用商品查詢架構 (API動態發現) ===", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "通用查詢失敗", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }

            return results;
        }

        #region 🌟 通用合約查詢函數 - 分層設計

        /// <summary>
        /// 🎯 第一層查詢方法：取得商品類型對應的合約群組
        /// 輸入: productType (商品類型)
        /// 輸出: 合約群組物件 (api.Contracts.{productType})
        /// </summary>
        /// <param name="productType">商品類型 (Stocks/Futures/Options/Indexs)</param>
        /// <returns>合約群組物件，失敗時返回 null</returns>
        public dynamic? GetContractByOneParameter(string productType)
        {
            var apiPath = $"api.Contracts.{productType}";
            try
            {
                _logService.LogDebug($"🔍 第一層查詢方法 - 取得商品類型: {productType}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                // 統一的登入檢查 - 只在這裡檢查一次
                if (!ShioajiService.Instance.IsLoggedIn)
                {
                    _logService.LogError("API 尚未登入，無法查詢合約群組", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return null;
                }

                var api = ShioajiService.Instance.Api;
                if (api?.Contracts == null)
                {
                    _logService.LogError("無法取得 API Contracts 物件", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return null;
                }

                if (string.IsNullOrEmpty(productType))
                {
                    _logService.LogError($"❌ 路徑 {apiPath} 查詢失敗: 商品類型不能為空", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return null;
                }

                var contractGroup = productType switch
                {
                    "Stocks" => api.Contracts.Stocks,
                    "Futures" => api.Contracts.Futures,
                    "Options" => api.Contracts.Options,
                    "Indexs" => api.Contracts.Indexs,
                    _ => null
                };
                if (contractGroup != null)
                {
                    _logService.LogInfo($"✅ 路徑 {apiPath} 存取成功", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return contractGroup;
                }
                else
                {
                    _logService.LogError($"❌ 路徑 {apiPath} 存取失敗", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    _logService.LogInfo($"💡 錯誤分析: 商品類型 '{productType}' 在 api.Contracts 中不存在", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    _logService.LogInfo($"💡 常見類型: Stocks (股票)、Futures (期貨)、Options (選擇權)、Indexs (指數)", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "第一層查詢方法失敗", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                return null;
            }
        }

        /// <summary>
        /// 🎯 第二層查詢方法：取得交易所群組
        /// 輸入: productType, exchange (商品類型, 交易所)
        /// 輸出: 交易所群組物件 (api.Contracts.{productType}.{exchange})
        /// </summary>
        /// <param name="productType">商品類型 (Stocks/Futures/Options/Indexs)</param>
        /// <param name="exchange">交易所代碼 (TSE/OTC/TXF/TXO等)</param>
        /// <returns>交易所群組物件，失敗時返回 null</returns>
        public dynamic? GetContractByTwoParameter(string productType, string exchange)
        {
            var apiPath = $"api.Contracts.{productType}.{exchange}";
            try
            {
                _logService.LogDebug($"🔍 第二層查詢方法 - 商品類型: {productType}, 交易所: {exchange}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                if (string.IsNullOrEmpty(exchange))
                {
                    _logService.LogError($"❌ 路徑 {apiPath} 查詢失敗: 交易所代碼不能為空", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return null;
                }

                // 使用第一層查詢方法，避免重複檢查
                var contractGroup = GetContractByOneParameter(productType);
                if (contractGroup == null)
                {
                    return null;
                }

                // 取得第二層查詢方法
                var exchangeGroup = contractGroup[exchange];
                if (exchangeGroup != null)
                {
                    _logService.LogInfo($"✅ 路徑 {apiPath} 存取成功", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return exchangeGroup;
                }
                else
                {
                    _logService.LogError($"❌ 路徑 {apiPath} 存取失敗", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    _logService.LogInfo($"💡 錯誤分析: 交易所 '{exchange}' 在 {productType} 中不存在", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"路徑 {apiPath} 查詢失敗", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                return null;
            }
        }

        /// <summary>
        /// 🎯 第三層查詢方法：取得特定合約
        /// 輸入: productType, exchange, symbol (商品類型, 交易所, 商品代號)
        /// 輸出: 合約物件 (api.Contracts.{productType}.{exchange}.{symbol})
        /// </summary>
        /// <param name="productType">商品類型 (Stocks/Futures/Options/Indexs)</param>
        /// <param name="exchange">交易所代碼 (TSE/OTC/TXF/TXO等)</param>
        /// <param name="symbol">商品代號 (2330/TXF202509/TXO20250924400C等)</param>
        /// <returns>合約物件，失敗時返回 null</returns>
        public dynamic? GetContractByThreeParameter(string productType, string exchange, string symbol)
        {
            var apiPath = $"api.Contracts.{productType}.{exchange}.{symbol}";
            try
            {
                _logService.LogDebug($"🔍 第三層查詢方法 - 商品類型: {productType}, 交易所: {exchange}, 商品代號: {symbol}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                if (string.IsNullOrEmpty(symbol))
                {
                    _logService.LogError($"❌ 路徑 {apiPath} 查詢失敗: 商品代號不能為空", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return null;
                }

                // 使用第二層查詢方法，避免重複檢查
                var exchangeGroup = GetContractByTwoParameter(productType, exchange);
                if (exchangeGroup == null)
                {
                    return null;
                }

                // 取得第三層
                var contract = exchangeGroup[symbol];
                if (contract != null)
                {
                    _logService.LogInfo($"✅ 路徑 {apiPath} 存取成功", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return contract;
                }
                else
                {
                    _logService.LogError($"❌ 路徑 {apiPath} 存取失敗", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    _logService.LogInfo($"💡 錯誤分析: 商品代號 '{symbol}' 在 {productType}.{exchange} 中不存在", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"路徑 {apiPath} 查詢失敗", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                return null;
            }
        }
        #endregion

        // 🔍 動態發現商品類型的所有可用交易所
        private List<string>? DiscoverAvailableExchanges(dynamic contractGroup, string productType)
        {
            try
            {
                _logService.LogDebug($"📊 🔍 動態發現 {productType} 的可用交易所...", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                var groupType = contractGroup?.GetType();
                var keysProperty = groupType?.GetProperty("Keys");

                if (keysProperty is not null)
                {
                    var keys = keysProperty.GetValue(contractGroup) as System.Collections.IEnumerable;
                    if (keys is not null)
                    {
                        var exchanges = new List<string>();
                        foreach (var key in keys)
                        {
                            var keyStr = key?.ToString();
                            if (!string.IsNullOrEmpty(keyStr))
                            {
                                exchanges.Add(keyStr);
                            }
                        }

                        if (exchanges.Count > 0)
                        {
                            _logService.LogInfo($"📊 ✅ 發現 {exchanges.Count} 個交易所: {string.Join(", ", exchanges)}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                            return exchanges;
                        }
                    }
                }

                _logService.LogWarning($"📊 ❌ 未發現任何交易所", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                return null;
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "交易所發現失敗", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                return null;
            }
        }

        // 🎯 執行通用查詢策略 - 整合商品代碼分類檢查
        private async Task ExecuteUniversalQueryStrategies(dynamic contractGroup, string productType, string symbol,
            string exchange, List<string> availableExchanges, List<ContractSearchResult> results)
        {
            try
            {
                // 🎯 策略1: 精確查詢 (有交易所 + 有商品代號)
                if (!string.IsNullOrEmpty(exchange) && !string.IsNullOrEmpty(symbol))
                {
                    var apiPath1 = $"api.Contracts.{productType}[\"{exchange}\"][\"{symbol}\"]";
                    _logService.LogInfo($"📊 策略1 - 精確查詢: {apiPath1}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                    if (TryExchangeSymbolQuery(contractGroup, exchange, symbol, apiPath1, productType, results))
                    {
                        return; // 找到就結束
                    }
                }

                // 🎯 策略2: 群組直接查詢 (有商品代號，無論是否有交易所) - 加入商品代碼分類檢查
                if (!string.IsNullOrEmpty(symbol))
                {
                    // 🆕 商品代碼分類檢查 - 加入使用者確認機制
                    bool shouldContinue = CheckProductCodeClassification(symbol, productType);

                    if (!shouldContinue)
                    {
                        _logService.LogInfo($"📊 策略2 - 使用者選擇停止查詢", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                        return; // 使用者選擇停止，直接結束查詢
                    }

                    var apiPath2 = $"api.Contracts.{productType}[\"{symbol}\"]";
                    _logService.LogInfo($"📊 策略2 - 群組直接查詢: {apiPath2}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                    if (await TryGroupDirectQuery(contractGroup, symbol, apiPath2, productType, results))
                    {
                        return; // 找到就結束
                    }
                }

                // 🎯 策略3: 全交易所搜尋 (只有商品代號)
                if (!string.IsNullOrEmpty(symbol) && string.IsNullOrEmpty(exchange))
                {
                    _logService.LogInfo($"📊 策略3 - 全交易所搜尋商品: {symbol}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    symbol = symbol.Trim().ToUpper();

                    // 選擇權判斷：13-16字元且以C或P結尾
                    if (symbol.Length >= 13 && symbol.Length <= 16 &&
                        (symbol.EndsWith('C') || symbol.EndsWith('P')))
                    {
                        productType = "Options";    // 設定 productType 為 "Options"
                        _logService.LogInfo($"📊 偵測到symbol輸入為選擇權商品代號，已自動設定商品類型為: {productType}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    }
                    else
                    {
                        _logService.LogDebug($"symbol不符合選擇權的商品代碼", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    }

                    foreach (var ex in availableExchanges)
                    {
                        var apiPath3 = $"api.Contracts.{productType}[\"{ex}\"][\"{symbol}\"]";
                        _logService.LogDebug($"📊 嘗試交易所 {ex}: {apiPath3}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                        if (TryExchangeSymbolQuery(contractGroup, ex, symbol, apiPath3, productType, results))
                        {
                            return; // 找到就結束
                        }
                    }

                    _logService.LogWarning($"📊 在所有交易所都找不到 {symbol}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                }

                // 🎯 策略4: 交易所資訊查詢 (有交易所 + 無商品代號)
                if (!string.IsNullOrEmpty(exchange) && string.IsNullOrEmpty(symbol))
                {
                    var apiPath4 = $"api.Contracts.{productType}[\"{exchange}\"]";
                    _logService.LogInfo($"📊 策略4 - 交易所資訊查詢: {apiPath4}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                    // 🚨 修改：先檢查數量再決定是否處理
                    var exchangeGroup = contractGroup[exchange];
                    if (exchangeGroup != null)
                    {
                        int itemCount = GetObjectItemCount(exchangeGroup);

                        if (itemCount > 500)
                        {
                            _logService.LogWarning($"⚠️ {apiPath4} 包含 {itemCount} 個項目，數量過大 (>500)", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                            _logService.LogInfo($"📁 因數量過大，將內容儲存到 txt 檔案，不進行合約解析", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                            await SaveLargeObjectToFile(exchangeGroup, apiPath4, itemCount);

                            // 🛑 不進行合約解析，直接返回空結果
                            return;
                        }
                        else
                        {
                            // 數量合理，正常處理
                            await TryExchangeInfoQuery(contractGroup, exchange, apiPath4, productType, results);
                        }
                    }
                }

                // 🎯 策略5: 顯示所有交易所資訊 (無交易所 + 無商品代號)
                if (string.IsNullOrEmpty(exchange) && string.IsNullOrEmpty(symbol))
                {
                    _logService.LogInfo($"📊 策略5 - 顯示前3個 {productType} 交易所資訊", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                    foreach (var ex in availableExchanges.Take(3)) // 限制顯示前3個避免過多
                    {
                        var apiPath5 = $"api.Contracts.{productType}[\"{ex}\"]";
                        _logService.LogDebug($"📊 查詢交易所 {ex}: {apiPath5}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                        await TryExchangeInfoQuery(contractGroup, ex, apiPath5, productType, results);
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "通用查詢策略執行失敗", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
        }

        /// <summary>
        /// 🆕 商品代碼分類檢查 - 在策略2執行前檢查商品代碼與商品類型是否匹配
        /// </summary>
        /// <param name="symbol">商品代號</param>
        /// <param name="selectedProductType">使用者選擇的商品類型</param>
        /// <returns>true: 繼續執行查詢, false: 停止查詢</returns>
        private bool CheckProductCodeClassification(string symbol, string selectedProductType)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(symbol) || string.IsNullOrWhiteSpace(selectedProductType))
                {
                    _logService.LogInfo($"輸入有誤：請檢查 商品代號、商品類別", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return false; // 參數無效時->停止
                }

                _logService.LogDebug($"🔍 開始檢查商品代碼分類 - 代號: {symbol}, 選擇類型: {selectedProductType}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                // 使用 ProductCodeClassifier 檢查商品類型匹配
                var suggestedCategory = ProductCodeClassifier.CheckProductTypeMatch(selectedProductType, symbol);

                if (suggestedCategory.HasValue)
                {
                    var suggestedProductType = ProductCodeClassifier.GetProductTypeString(suggestedCategory.Value);

                    // 記錄警告訊息
                    _logService.LogWarning($"⚠️ 商品代碼類型不匹配！", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    _logService.LogWarning($"   商品代號: {symbol}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    _logService.LogWarning($"   目前選擇: {selectedProductType}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    _logService.LogWarning($"   建議修改商品類別為: {suggestedProductType}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                    // 🆕 顯示確認訊息框
                    var message = $"⚠️ 商品代碼類型可能不匹配！\n\n" +
                                 $"商品代號: {symbol}\n" +
                                 $"目前選擇的商品類型: {selectedProductType}\n" +
                                 $"建議的商品類型: {suggestedProductType}\n\n" +
                                 $"是否要繼續使用目前選擇的 '{selectedProductType}' 進行查詢？\n\n" +
                                 $"• 按「是」：繼續查詢\n" +
                                 $"• 按「否」：停止查詢，請重新選擇正確的商品類型";

                    var result = System.Windows.MessageBox.Show(
                        message,
                        "商品代碼類型確認",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning,
                        System.Windows.MessageBoxResult.No // 預設選擇「否」，更安全
                    );

                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        _logService.LogInfo($"✅ 使用者確認繼續使用 {selectedProductType} 進行查詢", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                        return true; // 使用者選擇繼續
                    }
                    else
                    {
                        _logService.LogInfo($"❌ 使用者選擇停止查詢，建議修改商品類型為 {suggestedProductType}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                        return false; // 使用者選擇停止
                    }
                }
                else
                {
                    _logService.LogDebug($"✅ 商品代碼分類檢查通過 - {symbol} 符合 {selectedProductType} 類型", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return true; // 檢查通過，繼續執行
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "商品代碼分類檢查失敗", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                return true; // 發生錯誤時繼續執行，避免阻塞
            }
        }

        /// <summary>
        /// 🔧 處理群組直接查詢結果
        /// </summary>
        private void ProcessGroupResult(dynamic result, string productType, string symbol, string apiPath,
            List<ContractSearchResult> results)
        {
            try
            {
                _logService.LogDebug($"📊 🔧 處理群組查詢結果: {apiPath}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                if (result == null)
                {
                    _logService.LogDebug($"📊 🔧 群組查詢結果為 null", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return;
                }

                var resultType = result.GetType();
                _logService.LogDebug($"📊 🔧 結果類型: {resultType.Name}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                // 檢查是否有 Keys 屬性 (表示是群組)
                var keysProperty = resultType.GetProperty("Keys");
                if (keysProperty is not null)
                {
                    _logService.LogDebug($"📊 🔧 檢測到群組結構，處理為交易所資訊", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    ProcessExchangeInfo(result, symbol, apiPath, productType, results);
                }
                else
                {
                    _logService.LogDebug($"📊 🔧 檢測到單一合約，直接分析", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    var contractInfo = _contractAnalyzer.AnalyzeContract(result, productType, symbol, apiPath);
                    if (contractInfo is not null)
                    {
                        var searchResult = new ContractSearchResult(contractInfo);
                        results.Add(searchResult);
                        _logService.LogInfo($"📊 ✅ 加入合約: {searchResult.DisplayName}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "處理群組查詢結果失敗", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
        }

        /// <summary>
        /// 🎯 策略1: 交易所+商品代號查詢
        /// </summary>
        private bool TryExchangeSymbolQuery(dynamic contractGroup, string exchange, string symbol, string apiPath,
            string productType, List<ContractSearchResult> results)
        {
            try
            {
                var exchangeGroup = contractGroup[exchange];
                if (exchangeGroup is not null)
                {
                    var contract = exchangeGroup[symbol];
                    if (contract is not null)
                    {
                        _logService.LogInfo($"📊 ✅ 精確查詢成功路徑: {apiPath}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                        _logService.LogDebug($"📊 ✅ {apiPath}路徑內容:\n {contract}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                        var contractInfo = _contractAnalyzer.AnalyzeContract(contract, productType, symbol, apiPath);
                        if (contractInfo is not null)
                        {
                            var searchResult = new ContractSearchResult(contractInfo);
                            results.Add(searchResult);
                            _logService.LogInfo($"📊 ✅ 加入合約: {searchResult.DisplayName}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                            return true;
                        }
                    }
                    else
                    {
                        _logService.LogWarning($"📊 ❌ 精確查詢失敗: {apiPath} -> 商品代號 {symbol} 不存在", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    }
                }
                else
                {
                    _logService.LogWarning($"📊 ❌ 精確查詢失敗: {apiPath} -> 交易所 {exchange} 不存在", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"精確查詢異常: {apiPath}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
            return false;
        }

        /// <summary>
        /// 🎯 策略2: 群組直接查詢
        /// </summary>
        private async Task<bool> TryGroupDirectQuery(dynamic contractGroup, string symbol, string apiPath, string productType,
    List<ContractSearchResult> results)
        {
            try
            {
                var result = contractGroup[symbol];
                if (result is not null)
                {
                    _logService.LogInfo($"📊 ✅ 群組直接查詢成功路徑: {apiPath} -> 找到群組或交易所", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                    // 🆕 檢查數量並決定輸出方式
                    await HandleLargeObjectOutput(result, apiPath);

                    ProcessGroupResult(result, productType, symbol, apiPath, results);
                    return true;
                }
                else
                {
                    _logService.LogWarning($"📊 ❌ 群組直接查詢失敗: {apiPath} -> {symbol} 不是群組或交易所", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"群組直接查詢異常: {apiPath}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
            return false;
        }

        /// <summary>
        /// 🎯 策略4: 交易所資訊查詢
        /// </summary>
        private async Task TryExchangeInfoQuery(dynamic contractGroup, string exchange, string apiPath, string productType, List<ContractSearchResult> results)
        {
            try
            {
                var exchangeGroup = contractGroup[exchange];
                if (exchangeGroup is not null)
                {
                    _logService.LogInfo($"📊 ✅ 交易所資訊查詢成功路徑: {apiPath}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                    // 🆕 檢查數量並決定輸出方式
                    await HandleLargeObjectOutput(exchangeGroup, apiPath);

                    ProcessExchangeInfo(exchangeGroup, exchange, apiPath, productType, results);
                }
                else
                {
                    _logService.LogWarning($"📊 ❌ 交易所資訊查詢失敗: {apiPath} -> 交易所 {exchange} 不存在", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"交易所資訊查詢異常: {apiPath}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
        }
        /// <summary>
        /// 🆕 處理大物件輸出 - 檢查數量，決定是輸出到日誌還是儲存到檔案
        /// </summary>
        /// <param name="dataObject">要輸出的物件</param>
        /// <param name="apiPath">API 路徑</param>
        private async Task HandleLargeObjectOutput(dynamic dataObject, string apiPath)
        {
            try
            {
                // 🔍 檢查物件數量
                int itemCount = GetObjectItemCount(dataObject);

                if (itemCount > 500)
                {
                    // 🚨 數量過大，儲存到檔案
                    _logService.LogWarning($"⚠️ {apiPath} 包含 {itemCount} 個項目，數量過大 (>500)", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    _logService.LogInfo($"📁 因數量過大，將內容儲存到 txt 檔案而非顯示在日誌中", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                    await SaveLargeObjectToFile(dataObject, apiPath, itemCount);
                }
                else
                {
                    // ✅ 數量適中，正常輸出到日誌
                    _logService.LogDebug($"📊 ✅ {apiPath}路徑內容:\n {dataObject}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"處理大物件輸出失敗: {apiPath}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                // 🔄 發生錯誤時，嘗試正常輸出（但可能會有問題）
                try
                {
                    _logService.LogDebug($"📊 ✅ {apiPath}路徑內容:\n {dataObject}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                }
                catch (Exception logEx)
                {
                    _logService.LogError(logEx, $"正常日誌輸出也失敗: {apiPath}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                }
            }
        }

        /// <summary>
        /// 🔍 取得物件項目數量
        /// </summary>
        /// <param name="dataObject">要檢查的物件</param>
        /// <returns>項目數量，無法取得時返回 0</returns>
        private int GetObjectItemCount(dynamic dataObject)
        {
            try
            {
                if (dataObject == null) return 0;

                var objectType = dataObject.GetType();
                var keysProperty = objectType.GetProperty("Keys");

                if (keysProperty is not null)
                {
                    var keys = keysProperty.GetValue(dataObject) as System.Collections.IEnumerable;
                    if (keys is not null)
                    {
                        int count = 0;
                        foreach (var _ in keys)
                        {
                            count++;
                        }
                        return count;
                    }
                }

                // 如果沒有 Keys 屬性，嘗試其他方法
                if (dataObject is System.Collections.ICollection collection)
                {
                    return collection.Count;
                }

                return 0; // 無法確定數量時返回 0（視為小物件）
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "取得物件項目數量失敗", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                return 0;
            }
        }

        /// <summary>
        /// 💾 將大物件內容儲存到 txt 檔案
        /// </summary>
        /// <param name="dataObject">要儲存的物件</param>
        /// <param name="apiPath">API 路徑</param>
        /// <param name="itemCount">項目數量</param>
        private async Task SaveLargeObjectToFile(dynamic dataObject, string apiPath, int itemCount)
        {
            try
            {
                // 🗂️ 建立檔案名稱
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var safeApiPath = apiPath.Replace("[", "").Replace("]", "").Replace("\"", "").Replace(".", "_");
                var fileName = $"LargeObjectOutput_{safeApiPath}_{timestamp}.txt";
                var filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);

                // 📝 準備檔案內容
                var content = new StringBuilder();
                content.AppendLine($"=== 大物件輸出檔案 ===");
                content.AppendLine($"產生時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                content.AppendLine($"API 路徑: {apiPath}");
                content.AppendLine($"項目數量: {itemCount}");
                content.AppendLine($"物件類型: {dataObject?.GetType()?.Name ?? "Unknown"}");
                content.AppendLine();
                content.AppendLine("=== 物件內容 ===");
                content.AppendLine(dataObject?.ToString() ?? "null");

                // 💾 非同步寫入檔案
                await File.WriteAllTextAsync(filePath, content.ToString(), Encoding.UTF8);

                // 📢 通知使用者
                _logService.LogInfo($"✅ 大物件內容已儲存到檔案: {fileName}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                _logService.LogInfo($"📂 檔案位置: {filePath}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"儲存大物件到檔案失敗: {apiPath}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                // 🔄 儲存失敗時，嘗試簡化輸出到日誌
                try
                {
                    _logService.LogWarning($"📊 因儲存檔案失敗，改為簡化日誌輸出: {apiPath} (包含 {itemCount} 個項目)", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    _logService.LogDebug($"📊 物件類型: {dataObject?.GetType()?.Name ?? "Unknown"}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                }
                catch (Exception logEx)
                {
                    _logService.LogError(logEx, "簡化日誌輸出也失敗", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                }
            }
        }
        /// <summary>
        /// 🔧 處理交易所資訊
        /// </summary>
        private void ProcessExchangeInfo(dynamic exchangeGroup, string exchange, string apiPath, string productType,
            List<ContractSearchResult> results)
        {
            try
            {
                _logService.LogDebug($"📊 🔧 處理交易所資訊: {exchange}", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                var groupType = exchangeGroup?.GetType();
                var keysProperty = groupType?.GetProperty("Keys");

                if (keysProperty is not null)
                {
                    var keys = keysProperty.GetValue(exchangeGroup) as System.Collections.IEnumerable;
                    if (keys is not null)
                    {
                        var symbolList = new List<string>();
                        foreach (var key in keys)
                        {
                            if (key?.ToString() is { } keyStr && !string.IsNullOrEmpty(keyStr))
                            {
                                symbolList.Add(keyStr);
                            }
                        }

                        _logService.LogDebug($"📊 🔧 發現 {symbolList.Count} 個商品代號", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                        foreach (var symbol in symbolList)
                        {
                            try
                            {
                                // 安全地取得合約，避免 null 參考警告
                                var contract = exchangeGroup?[symbol];
                                if (contract is not null)
                                {
                                    var contractInfo = _contractAnalyzer.AnalyzeContract(contract, productType, symbol, $"{apiPath}[\"{symbol}\"]");
                                    if (contractInfo is not null)
                                    {
                                        var searchResult = new ContractSearchResult(contractInfo);
                                        results.Add(searchResult);
                                    }
                                }
                                else
                                {
                                    _logService.LogDebug($"📊 🔧 商品 {symbol} 合約為 null，跳過處理", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logService.LogError(ex, $"處理商品 {symbol} 失敗", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                            }
                        }
                    }
                    else
                    {
                        _logService.LogWarning($"📊 🔧 無法取得交易所 {exchange} 的 Keys 集合", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    }
                }
                else
                {
                    _logService.LogWarning($"📊 🔧 無法取得交易所 {exchange} 的商品清單 - Keys 屬性不存在", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "處理交易所資訊失敗", LOG_SOURCE, LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
        }
    }
}