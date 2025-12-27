using Sinopac.Shioaji;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WpfApp5.Models;
using WpfApp5.Models.MarketData;
using WpfApp5.Services;
using WpfApp5.Services.Common;
using WpfApp5.ViewModels;

namespace WpfApp5.Services
{
    /// <summary>
    /// 市場資料服務 - 處理即時行情訂閱與資料轉換 (重構版)
    /// 充分運用 ShioajiService 的全域靜態 API 方法
    /// </summary>
    public class MarketService : IDisposable
    {
        #region 單例模式
        private static MarketService? _instance;
        private static readonly object _lockInstance = new();

        public static MarketService Instance
        {
            get
            {
                if (_instance is null)
                {
                    lock (_lockInstance)
                    {
                        _instance ??= new MarketService();
                    }
                }
                return _instance!;
            }
        }

        private MarketService()
        {
            _logService = LogService.Instance;
            _contractQueryService = new ContractQueryService();
            _contractAnalyzer = new ContractAnalyzer();
            _subscriptionManager = new SubscriptionManager(_logService);
            InitializeService();
        }

        /// <summary>
        /// 重置單例實例 (僅供測試使用)
        /// </summary>
        internal static void ResetInstance()
        {
            lock (_lockInstance)
            {
                _instance?.Dispose();
                _instance = null;
            }
        }
        #endregion

        #region 欄位與屬性
        private readonly LogService _logService;
        private bool _isQuoteCallbackSet = false;
        private bool _disposed = false;
        private readonly ContractQueryService _contractQueryService;
        private readonly ContractAnalyzer _contractAnalyzer;
        private readonly SubscriptionManager _subscriptionManager;
        private ContractInfo? _currentContractInfo;

        // 獲取當前合約資訊
        public ContractInfo? GetCurrentContractInfo() => _currentContractInfo;

        public interface IWindowIdentifier
        {
            string WindowId { get; }
        }

        // 訂閱統計 - 透過 SubscriptionManager 獲取
        public int SubscribedCount => _subscriptionManager.GetAllUniqueSubscriptions().Count;

        // 取得訂閱管理器
        public SubscriptionManager SubscriptionManager => _subscriptionManager;
        #endregion

        #region 事件定義
        public event Action<ContractInfo>? ContractInfoReceived;
        public event Action<string, Exchange, dynamic>? RawDataReceived;
        public event Action<ContractInfo, string>? OrderBookInitializationRequested;

        // 股票專用事件
        public event Action<STKTickData>? STK_TickReceived;
        public event Action<STKBidAskData>? STK_BidAskReceived;
        public event Action<STKQuoteData>? STK_QuoteReceived;

        // 期權專用事件
        public event Action<FOPTickData>? FOP_TickReceived;
        public event Action<FOPBidAskData>? FOP_BidAskReceived;
        #endregion

        #region 初始化與設定
        private void InitializeService()
        {
            _logService.LogInfo("MarketService (重構版) 已初始化", "MarketService",
                LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
        }

        /// <summary>
        /// 設定報價回調 - 使用 ShioajiService 的全域靜態方法
        /// </summary>
        public ServiceResult SetupQuoteCallback()
        {
            try
            {
                // 檢查 API 是否可用
                if (!ShioajiService.IsApiLoggedIn)
                {
                    return ServiceResult.Failure("API 尚未登入");
                }

                if (!_isQuoteCallbackSet)
                {
                    // 使用 ShioajiService 的全域靜態方法設定回調
                    ShioajiService.SetQuoteCallback_v1(OnQuoteCallback);
                    _isQuoteCallbackSet = true;
                    _logService.LogInfo("報價回調已設定 (使用全域靜態方法)", "MarketService",
                        LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                }

                return ServiceResult.Success("報價回調設定成功");
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "設定報價回調失敗", "MarketService",
                    LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                return ServiceResult.Failure($"設定報價回調失敗: {ex.Message}");
            }
        }
        #endregion

        #region 合約查詢
        public ContractInfo? QueryContractInfo(string productType, string exchange, string symbol)
        {
            try
            {
                _logService.LogInfo($"[查詢] 正在查詢 {productType} 商品類別合約 {symbol} 的詳細資訊...",
                    "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                // 使用已初始化的 ContractQueryService 查詢合約
                var contract = _contractQueryService.GetContractByThreeParameter(productType, exchange, symbol);

                if (contract == null)
                {
                    _logService.LogWarning($"[錯誤] 找不到 {productType} 合約 {symbol}",
                        "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return null;
                }

                // 使用 ContractAnalyzer 分析合約
                var apiPath = $"api.Contracts.{productType}[\"{exchange}\"][\"{symbol}\"]";
                var contractInfo = _contractAnalyzer.AnalyzeContract(contract, productType, symbol, apiPath);

                if (contractInfo == null)
                {
                    _logService.LogWarning($"[錯誤] 分析 {productType} 合約 {symbol} 失敗",
                        "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return null;
                }

                _currentContractInfo = contractInfo;
                ContractInfoReceived?.Invoke(contractInfo);

                _logService.LogInfo($"[成功] 已獲取 {contractInfo.DisplayName} 的詳細資訊",
                    "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                return contractInfo;
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "查詢合約詳細資訊失敗", "MarketService",
                    LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                return null;
            }
        }
        #endregion

        #region Update OrderBookViewModel
        public void UpdateOrderBookViewModel(ContractInfo contractInfo, string windowId)
        {
            try
            {
                _logService.LogDebug($"準備更新 OrderBookViewModel: {contractInfo.Symbol} (視窗ID: {windowId})",
                    "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                if (string.IsNullOrEmpty(windowId))
                {
                    _logService.LogWarning("未提供 WindowId，無法更新 OrderBookViewModel",
                        "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return;
                }

                if (OrderBookInitializationRequested == null)
                {
                    _logService.LogWarning($"OrderBookInitializationRequested 事件沒有訂閱者",
                        "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return;
                }

                _logService.LogDebug($"觸發 OrderBookInitializationRequested 事件: {contractInfo.Symbol} (視窗ID: {windowId})",
                    "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                OrderBookInitializationRequested?.Invoke(contractInfo, windowId);

                _logService.LogDebug($"已觸發 OrderBookInitializationRequested 事件: {contractInfo.Symbol} (視窗ID: {windowId})",
                    "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"更新 OrderBookViewModel 時發生錯誤: {ex.Message}",
                    "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
        }
        #endregion

        #region 合約代碼獲取
        public string GetActualCode(string productType, string exchange, string symbol)
        {
            var contract = _contractQueryService.GetContractByThreeParameter(productType, exchange, symbol)
                ?? throw new InvalidOperationException($"找不到合約: {productType}.{exchange}.{symbol}");
            return ContractAnalyzer.GetActualContractCode(contract);
        }
        #endregion

        #region 訂閱管理 - 重構版本
        /// <summary>
        /// 通用訂閱商品函數 - 使用 ShioajiService 全域靜態方法
        /// </summary>
        public ServiceResult SubscribeProduct(string productType, string exchange, string symbol, string windowId,
            QuoteType quoteType = QuoteType.tick, bool intradayOdd = false)
        {
            try
            {
                _logService.LogDebug($"視窗{windowId}開始訂閱商品 - 類型: {productType}, 交易所: {exchange}, 代號: {symbol}, 報價類型: {quoteType}, 零股: {intradayOdd}",
                    "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                // 檢查 API 是否可用
                if (!ShioajiService.IsApiLoggedIn)
                {
                    return ServiceResult.Failure("API 尚未登入");
                }

                // 確保回調已設定
                var callbackResult = SetupQuoteCallback();
                if (!callbackResult.IsSuccess)
                {
                    return callbackResult;
                }

                // 動作1: 取得合約與合約的基本屬性資料
                var contractDataResult = GetContractData(productType, exchange, symbol);
                if (!contractDataResult.IsSuccess)
                {
                    return ServiceResult.Failure(contractDataResult.Message);
                }
                var contractData = contractDataResult.Data!;

                // 動作2: 檢查視窗目前訂閱的所有合約代碼
                var checkResult = CheckWindowSubscriptions(windowId, contractData.ActualCode, quoteType, intradayOdd);
                if (!checkResult.IsSuccess)
                {
                    return ServiceResult.Failure(checkResult.Message);
                }
                var check = checkResult.Data!;

                // 如果視窗已訂閱了其他合約代碼，需要先取消這些訂閱
                if (check.NeedUnsubscribeOthers)
                {
                    _logService.LogInfo($"視窗 {windowId} 已訂閱了其他合約代碼，將先取消這些訂閱", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    UnsubscribeAllForWindow(windowId);
                }

                // 動作3: 進行訂閱操作與記錄
                var subscribeResult = PerformSubscription(contractData, windowId, productType, exchange, symbol, quoteType, intradayOdd, check.IsAlreadySubscribedByOthers);

                if (!subscribeResult.IsSuccess)
                {
                    return subscribeResult;
                }

                // 動作4: 只有在訂閱新合約時才初始化 OrderBookViewModel
                if (check.IsNewContract)
                {
                    _logService.LogInfo($"IsNewContract_執行InitializeOrderBookViewModel", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    InitializeOrderBookViewModel(windowId, contractData.Contract);
                }

                return subscribeResult;
            }
            catch (Exception ex)
            {
                var errorMsg = $"訂閱失敗 {productType}.{exchange}.{symbol}: {ex.Message}";
                _logService.LogError(ex, errorMsg, "MarketService",
                    LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                return ServiceResult.Failure(errorMsg);
            }
        }

        // 🚀 智能訂閱 - 根據商品類型自動訂閱對應的報價類型 (使用全域靜態方法)
        public async Task<ServiceResult> SmartSubscribeProduct(string productType, string exchange, string symbol, string windowId, bool intradayOdd = false)
        {
            try
            {
                _logService.LogInfo($"🚀 開始智能訂閱 - 視窗: {windowId}, 類型: {productType}, 交易所: {exchange}, 代號: {symbol}, 零股: {intradayOdd}",
                    "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                // 檢查 API 是否可用
                if (!ShioajiService.IsApiLoggedIn)
                {
                    return ServiceResult.Failure("API 尚未登入");
                }

                // 確保回調已設定
                var callbackResult = SetupQuoteCallback();
                if (!callbackResult.IsSuccess)
                {
                    return callbackResult;
                }

                // 動作1: 取得合約與合約的基本屬性資料
                var contractDataResult = GetContractData(productType, exchange, symbol);
                if (!contractDataResult.IsSuccess)
                {
                    return ServiceResult.Failure(contractDataResult.Message);
                }
                var contractData = contractDataResult.Data!;

                // 根據商品類型決定訂閱策略
                List<QuoteType> quoteTypesToSubscribe = productType.ToUpper() switch
                {
                    "STOCKS" => [QuoteType.quote],
                    "FUTURES" or "OPTIONS" => [QuoteType.bidask, QuoteType.tick],
                    _ => throw new ArgumentException($"不支援的商品類型: {productType}")
                };

                _logService.LogInfo($"📊 {productType} 商品 - 訂閱 {string.Join(" + ", quoteTypesToSubscribe)}", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                // 🔧 修復：檢查視窗目前的訂閱狀況（在開始訂閱前）
                var windowSubscriptions = _subscriptionManager.GetWindowSubscriptions(windowId);
                var currentSubscribedCodes = windowSubscriptions
                    .Select(s => s.ActualCode)
                    .Distinct()
                    .ToList();

                bool needUnsubscribeOthers = currentSubscribedCodes.Count > 0 && !currentSubscribedCodes.Contains(contractData.ActualCode);
                bool isCompletelyNewContract = !currentSubscribedCodes.Contains(contractData.ActualCode);

                // 如果視窗已訂閱了其他合約代碼，需要先取消這些訂閱
                if (needUnsubscribeOthers)
                {
                    _logService.LogInfo($"視窗 {windowId} 已訂閱了其他合約代碼，將先取消這些訂閱", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    UnsubscribeAllForWindow(windowId);
                }

                // 執行訂閱
                var results = new List<ServiceResult>();
                var successMessages = new List<string>();
                var failureMessages = new List<string>();
                bool hasAnySuccess = false;

                foreach (var quoteType in quoteTypesToSubscribe)
                {
                    _logService.LogInfo($"[詳細追蹤] 🔄 開始處理 {quoteType}",
                        "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                    // 動作2: 檢查此特定報價類型的訂閱狀況
                    var checkResult = CheckWindowSubscriptions(windowId, contractData.ActualCode, quoteType, intradayOdd);

                    // 如果已訂閱，跳過
                    if (!checkResult.IsSuccess && checkResult.Message.Contains("已訂閱"))
                    {
                        _logService.LogWarning($"跳過已訂閱的報價類型: {quoteType}",
                            "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                        failureMessages.Add($"⚠️ {quoteType}: 已訂閱");
                        continue;
                    }

                    var check = checkResult.Data!;

                    // 動作3: 進行訂閱操作與記錄
                    var subscribeResult = PerformSubscription(contractData, windowId, productType, exchange, symbol, quoteType, intradayOdd, check.IsAlreadySubscribedByOthers);

                    results.Add(subscribeResult);

                    if (subscribeResult.IsSuccess)
                    {
                        successMessages.Add($"✅ {quoteType}");
                        hasAnySuccess = true;
                        await Task.Delay(50); // 延遲讓 API 內部狀態穩定
                    }
                    else
                    {
                        failureMessages.Add($"❌ {quoteType}: {subscribeResult.Message}");
                    }
                }

                // 🔧 修復：只要有任何訂閱成功且是新合約，就初始化 OrderBookViewModel
                if (hasAnySuccess && isCompletelyNewContract)
                {
                    _logService.LogInfo($"hasAnySuccess + isCompletelyNewContract = 執行InitializeOrderBookViewModel", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    InitializeOrderBookViewModel(windowId, contractData.Contract);
                }

                // 統計結果
                int successCount = results.Count(r => r.IsSuccess);
                int totalCount = results.Count;

                // 組合結果訊息
                var summaryMessage = $"智能訂閱完成 ({successCount}/{totalCount})";
                if (successMessages.Count > 0)
                {
                    summaryMessage += $"\n成功: {string.Join(", ", successMessages)}";
                }
                if (failureMessages.Count > 0)
                {
                    summaryMessage += $"\n失敗: {string.Join(", ", failureMessages)}";
                }

                // 記錄結果
                if (successCount == totalCount)
                {
                    _logService.LogInfo($"✅ {summaryMessage}", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return ServiceResult.Success(summaryMessage);
                }
                else if (successCount > 0)
                {
                    _logService.LogWarning($"⚠️ {summaryMessage}", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return ServiceResult.Success(summaryMessage);
                }
                else
                {
                    _logService.LogError(new Exception(summaryMessage), "❌ 智能訂閱全部失敗", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return ServiceResult.Failure(summaryMessage);
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"智能訂閱失敗: {ex.Message}";
                _logService.LogError(ex, errorMsg, "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                return ServiceResult.Failure(errorMsg);
            }
        }

        // 取消視窗對特定合約的訂閱 - 使用 ShioajiService 全域靜態方法
        public ServiceResult UnsubscribeProduct(string actualCode, string windowId, QuoteType quoteType, bool intradayOdd)
        {
            try
            {
                _logService.LogDebug($"視窗{windowId}開始取消訂閱 - 真實合約代碼: {actualCode}, 報價類型: {quoteType}, 零股: {intradayOdd}",
                    "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                // 檢查 API 是否可用
                if (!ShioajiService.IsApiLoggedIn)
                {
                    return ServiceResult.Failure("API 尚未登入");
                }

                // 檢查此視窗是否已訂閱此合約
                if (!_subscriptionManager.IsWindowSubscribed(actualCode, windowId, quoteType, intradayOdd))
                {
                    _logService.LogWarning($"視窗 {windowId} 未訂閱合約: {actualCode}.{quoteType}{(intradayOdd ? ".ODD" : "")}",
                        "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return ServiceResult.Failure($"此視窗未訂閱合約: {actualCode}");
                }

                // 移除視窗訂閱
                _subscriptionManager.RemoveSubscription(actualCode, windowId, quoteType, intradayOdd);

                // 檢查是否有其他視窗仍在訂閱此合約
                if (_subscriptionManager.HasOtherWindowSubscriptions(actualCode, windowId, quoteType, intradayOdd))
                {
                    var message = $"視窗 {windowId} 已從合約 {actualCode}.{quoteType}{(intradayOdd ? ".ODD" : "")} 的訂閱列表中移除，但其他視窗仍在訂閱";
                    _logService.LogInfo(message, "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return ServiceResult.Success(message);
                }

                // 若沒有其他視窗訂閱，則執行取消全域訂閱 - 使用 ShioajiService 全域靜態方法
                var contract = _subscriptionManager.GetContractByActualCode(actualCode);
                if (contract != null)
                {
                    ShioajiService.UnSubscribe(contract, quoteType, intradayOdd, QuoteVersion.v1);
                    _logService.LogInfo($"已取消全域訂閱: {actualCode}.{quoteType}{(intradayOdd ? ".ODD" : "")}", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                }
                else
                {
                    _logService.LogWarning($"找不到合約以取消訂閱: {actualCode}", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                }

                // 檢查視窗是否還有其他訂閱，如果沒有則重置 OrderBookViewModel
                if (!_subscriptionManager.HasWindowAnySubscriptions(windowId))
                {
                    ResetWindowOrderBookViewModel(windowId);
                }

                var successMessage = $"視窗 {windowId} 已取消訂閱 {actualCode} ({quoteType})" + (intradayOdd ? " [零股]" : " [整股]");
                _logService.LogInfo(successMessage, "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                return ServiceResult.Success(successMessage);
            }
            catch (Exception ex)
            {
                var errorMsg = $"取消訂閱失敗 {actualCode}: {ex.Message}";
                _logService.LogError(ex, errorMsg, "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                return ServiceResult.Failure(errorMsg);
            }
        }

        // 取消指定視窗的所有訂閱 - 使用 ShioajiService 全域靜態方法
        public ServiceResult UnsubscribeAllForWindow(string windowId)
        {
            try
            {
                _logService.LogInfo($"開始取消視窗 {windowId} 的所有訂閱並重置狀態...", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                ResetWindowOrderBookViewModel(windowId);    // 1. 執行本地 UI 重置 (觸發我們改好的數據清除模式)

                var windowSubscriptions = _subscriptionManager.GetWindowSubscriptions(windowId);    // 2. 獲取視窗的所有訂閱紀錄並進行本地移除
                if (windowSubscriptions.Count == 0)
                {
                    return ServiceResult.Success("此視窗目前沒有任何本地訂閱紀錄");
                }

                int totalUnsubscribed = 0;
                foreach (var subscription in windowSubscriptions.ToList())
                {
                    // 移除本地管理器中的紀錄
                    _subscriptionManager.RemoveSubscription(subscription.ActualCode, windowId, subscription.QuoteType, subscription.IsOddLot);

                    // 只有當「沒有其他視窗」在使用此合約時，才嘗試呼叫 API 退訂
                    if (!_subscriptionManager.HasOtherWindowSubscriptions(subscription.ActualCode, windowId, subscription.QuoteType, subscription.IsOddLot))
                    {
                        if (subscription.Contract != null)
                        {
                            // 這裡 ShioajiService 內部會自行處理 Api 是否為 null 的情況
                            ShioajiService.UnSubscribe(subscription.Contract, subscription.QuoteType, subscription.IsOddLot, QuoteVersion.v1);
                            _logService.LogInfo($"已執行 API 退訂: {subscription.ActualCode}", "MarketService");
                        }
                    }
                    totalUnsubscribed++;
                }

                return ServiceResult.Success($"視窗 {windowId} 已成功完成本地資源清理並取消 {totalUnsubscribed} 個訂閱");
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"取消視窗 {windowId} 的訂閱時發生異常", "MarketService");
                return ServiceResult.Failure($"清理失敗: {ex.Message}");
            }
        }

        // 取消所有訂閱並清理相關資源 - 使用 ShioajiService 全域靜態方法
        public ServiceResult UnsubscribeAll()
        {
            try
            {
                _logService.LogInfo("開始取消所有訂閱...", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                // 檢查 API 是否可用
                if (!ShioajiService.IsApiLoggedIn)
                {
                    return ServiceResult.Failure("API 尚未登入");
                }

                // 獲取所有訂閱的唯一組合
                var uniqueSubscriptions = _subscriptionManager.GetAllUniqueSubscriptions();
                int totalUnsubscribed = 0;

                // 取消每個唯一訂閱 - 使用 ShioajiService 全域靜態方法
                foreach (var subscription in uniqueSubscriptions)
                {
                    try
                    {
                        if (subscription.Contract != null)
                        {
                            ShioajiService.UnSubscribe(subscription.Contract, subscription.QuoteType, subscription.IsOddLot, QuoteVersion.v1);
                            totalUnsubscribed++;

                            _logService.LogInfo($"已取消全域訂閱: {subscription.ActualCode}.{subscription.QuoteType}{(subscription.IsOddLot ? ".ODD" : "")}",
                                "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                        }
                        else
                        {
                            _logService.LogWarning($"找不到合約以取消訂閱: {subscription.ActualCode}", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logService.LogError(ex, $"取消訂閱 {subscription.ActualCode} 時發生錯誤: {ex.Message}", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    }
                }

                // 清空訂閱管理器
                _subscriptionManager.ClearAllSubscriptions();

                var successMessage = $"已成功取消所有訂閱，共 {totalUnsubscribed} 個合約";
                _logService.LogInfo(successMessage, "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                return ServiceResult.Success(successMessage);
            }
            catch (Exception ex)
            {
                var errorMsg = $"取消所有訂閱失敗: {ex.Message}";
                _logService.LogError(ex, errorMsg, "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                return ServiceResult.Failure(errorMsg);
            }
        }

        // 處理視窗關閉時的清理
        public void CleanupWindowSubscriptions(string windowId)
        {
            _logService.LogInfo($"清理視窗 {windowId} 的所有訂閱", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            // 實際清理邏輯可以調用 UnsubscribeAllForWindow
            UnsubscribeAllForWindow(windowId);
        }

        // 檢查是否已訂閱 (透過 SubscriptionManager)
        public bool IsSubscribed(string actualCode, QuoteType quoteType, bool intradayOdd)
        {
            return _subscriptionManager.IsContractSubscribed(actualCode, quoteType, intradayOdd);
        }
        #endregion

        #region 訂閱管理 - 輔助方法
        // 動作1: 取得合約與合約的基本屬性資料
        private ServiceResult<ContractData> GetContractData(string productType, string exchange, string symbol)
        {
            try
            {
                // 使用 ContractQueryService 取得合約
                var contract = _contractQueryService.GetContractByThreeParameter(productType, exchange, symbol);
                if (contract == null)
                {
                    return ServiceResult<ContractData>.Failure($"找不到合約: {productType}.{exchange}.{symbol}");
                }

                // 從合約中提取關鍵屬性
                string actualCode = ContractAnalyzer.GetActualContractCode(contract);
                // 使用 dynamic 轉換來安全訪問屬性
                dynamic dynamicContract = contract;
                decimal limitUp = Convert.ToDecimal(dynamicContract.limit_up);
                decimal limitDown = Convert.ToDecimal(dynamicContract.limit_down);
                decimal reference = Convert.ToDecimal(dynamicContract.reference);
                string securityType = contract.security_type;
                string actualExchange = contract.exchange;
                string name = contract.name;

                _logService.LogInfo($"真實合約代碼={actualCode}, 漲停={limitUp}, 跌停={limitDown}, 參考價={reference}, 類型={securityType}, 交易所={actualExchange}, name={name}",
                    "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                var contractData = new ContractData
                {
                    Contract = contract,
                    ActualCode = actualCode,
                    Name = name,
                    LimitUp = limitUp,
                    LimitDown = limitDown,
                    Reference = reference,
                    SecurityType = securityType,
                    ActualExchange = actualExchange
                };

                return ServiceResult<ContractData>.Success(contractData, "成功取得合約資料");
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "取得合約資料失敗", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                return ServiceResult<ContractData>.Failure($"取得合約資料失敗: {ex.Message}");
            }
        }

        // 動作2: 檢查視窗目前訂閱的所有合約代碼
        private ServiceResult<WindowSubscriptionCheck> CheckWindowSubscriptions(string windowId, string actualCode, QuoteType quoteType, bool intradayOdd)
        {
            try
            {
                // 檢查視窗目前訂閱的所有合約代碼
                var windowSubscriptions = _subscriptionManager.GetWindowSubscriptions(windowId);
                var currentSubscribedCodes = windowSubscriptions
                    .Select(s => s.ActualCode)
                    .Distinct()
                    .ToList();

                bool isNewContract = !currentSubscribedCodes.Contains(actualCode);
                bool needUnsubscribeOthers = currentSubscribedCodes.Count > 0 && !currentSubscribedCodes.Contains(actualCode);

                // 檢查此視窗是否已訂閱此合約的此報價類型
                if (_subscriptionManager.IsWindowSubscribed(actualCode, windowId, quoteType, intradayOdd))
                {
                    return ServiceResult<WindowSubscriptionCheck>.Failure($"視窗 {windowId} 已訂閱合約: {actualCode}.{quoteType}{(intradayOdd ? ".ODD" : "")}");
                }

                // 檢查是否已有其他視窗訂閱此合約的此報價類型
                bool isAlreadySubscribedByOthers = _subscriptionManager.IsContractSubscribed(actualCode, quoteType, intradayOdd);

                var checkResult = new WindowSubscriptionCheck
                {
                    IsNewContract = isNewContract,
                    NeedUnsubscribeOthers = needUnsubscribeOthers,
                    IsAlreadySubscribedByOthers = isAlreadySubscribedByOthers,
                    CurrentSubscribedCodes = currentSubscribedCodes
                };

                return ServiceResult<WindowSubscriptionCheck>.Success(checkResult, "檢查完成");
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "檢查視窗訂閱狀態失敗", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                return ServiceResult<WindowSubscriptionCheck>.Failure($"檢查視窗訂閱狀態失敗: {ex.Message}");
            }
        }

        // 動作3: 進行訂閱操作與記錄 - 使用 ShioajiService 全域靜態方法
        private ServiceResult PerformSubscription(ContractData contractData, string windowId, string productType, string exchange, string symbol, QuoteType quoteType, bool intradayOdd, bool isAlreadySubscribedByOthers)
        {
            try
            {
                // 如果已有其他視窗訂閱，只需添加此視窗的訂閱記錄
                if (isAlreadySubscribedByOthers)
                {
                    _subscriptionManager.AddSubscription(contractData.Contract, contractData.ActualCode, windowId, productType, exchange, symbol, contractData.ActualExchange, contractData.SecurityType, contractData.LimitUp, contractData.LimitDown, contractData.Reference, quoteType, intradayOdd);

                    var message = $"視窗 {windowId} 已添加到合約 {contractData.ActualCode}.{quoteType}{(intradayOdd ? ".ODD" : "")} 的訂閱列表";
                    _logService.LogInfo(message, "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return ServiceResult.Success(message);
                }

                // 🔧 修復：加強訂閱前的診斷
                _logService.LogInfo($"[訂閱診斷] 準備訂閱: 合約={contractData.ActualCode}, 類型={quoteType}, 零股={intradayOdd}, SecurityType={contractData.SecurityType}, Exchange={contractData.ActualExchange}",
                    "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                // 檢查合約是否有效
                if (contractData.Contract == null)
                {
                    return ServiceResult.Failure("合約物件為 null");
                }

                // 檢查 API 狀態
                if (!ShioajiService.IsApiLoggedIn)
                {
                    return ServiceResult.Failure("API 未登入");
                }

                // 全域未訂閱，進行訂閱操作
                try
                {
                    ShioajiService.Subscribe(contractData.Contract, quoteType, intradayOdd, QuoteVersion.v1);
                    _logService.LogInfo($"[訂閱成功] ShioajiService.Subscribe 呼叫成功: {contractData.ActualCode}.{quoteType}", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                }
                catch (Exception subscribeEx)
                {
                    _logService.LogError(subscribeEx, $"[訂閱失敗] ShioajiService.Subscribe 呼叫失敗: {contractData.ActualCode}.{quoteType}", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return ServiceResult.Failure($"執行訂閱操作失敗: {subscribeEx.Message}");
                }

                // 記錄視窗訂閱
                _subscriptionManager.AddSubscription(
                    contractData.Contract,
                    contractData.ActualCode,
                    windowId,
                    productType,
                    exchange,
                    symbol,
                    contractData.ActualExchange,
                    contractData.SecurityType,
                    contractData.LimitUp,
                    contractData.LimitDown,
                    contractData.Reference,
                    quoteType,
                    intradayOdd
                );

                var successMessage = $"視窗 {windowId} 已訂閱 {contractData.ActualCode} ({quoteType})" + (intradayOdd ? " [零股]" : " [整股]");
                _logService.LogInfo(successMessage, "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                return ServiceResult.Success(successMessage);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"執行訂閱操作失敗: 合約={contractData.ActualCode}, 類型={quoteType}", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                return ServiceResult.Failure($"執行訂閱操作失敗: {ex.Message}");
            }
        }

        // 內部資料類別：合約資料
        private class ContractData
        {
            public required IContract Contract { get; set; }
            public required string ActualCode { get; set; }
            public required string Name { get; set; }
            public decimal LimitUp { get; set; }
            public decimal LimitDown { get; set; }
            public decimal Reference { get; set; }
            public required string SecurityType { get; set; }
            public required string ActualExchange { get; set; }
        }

        // 內部資料類別：視窗訂閱檢查結果
        private class WindowSubscriptionCheck
        {
            public bool IsNewContract { get; set; }
            public bool NeedUnsubscribeOthers { get; set; }
            public bool IsAlreadySubscribedByOthers { get; set; }
            public List<string> CurrentSubscribedCodes { get; set; } = [];
        }
        #endregion

        #region 回調處理
        // 報價回調處理
        private void OnQuoteCallback(Exchange exchange, dynamic data)
        {
            try
            {
                var dataTypeName = data.GetType().Name;

                // 根據資料類型名稱處理資料
                switch (dataTypeName)
                {
                    // 股票相關資料類型
                    case "BidAskSTKv1":
                        ProcessSTKBidAsk(exchange, data);
                        break;
                    case "TickSTKv1":
                        ProcessSTKTick(exchange, data);
                        break;
                    case "QuoteSTKv1":
                        ProcessSTKQuote(exchange, data);
                        break;

                    // 期貨選擇權相關資料類型
                    case "BidAskFOPv1":
                        ProcessFOPBidAsk(exchange, data);
                        break;
                    case "TickFOPv1":
                        ProcessFOPTick(exchange, data);
                        break;

                    default:
                        _logService.LogWarning($"未支援的資料類型: {dataTypeName} (Exchange: {exchange})",
                            "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "處理報價回調失敗", "MarketService",
                    LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
        }
        #endregion

        #region 股票資料處理
        // 處理股票 BidAsk 資料
        private void ProcessSTKBidAsk(Exchange exchange, dynamic data)
        {
            try
            {
                var isOddLot = data.intraday_odd;
                var STKbidAskData = new STKBidAskData(data, isOddLot);
                STK_BidAskReceived?.Invoke(STKbidAskData);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "處理股票 BidAsk 資料失敗", "MarketService", LogDisplayTarget.SourceWindow);
            }
        }

        // 處理股票 Tick 資料
        private void ProcessSTKTick(Exchange exchange, dynamic data)
        {
            try
            {
                string contractCode = "";
                try { contractCode = data.code; } catch { _logService.LogWarning("無法獲取股票代碼", "MarketService"); }

                if (string.IsNullOrEmpty(contractCode))
                {
                    _logService.LogWarning("有收到的股票 Tick 數據，但沒有成功取得合約代碼", "MarketService");
                    return;
                }

                var isOddLot = data.intraday_odd;
                var STKtickData = new STKTickData(data, isOddLot);
                STK_TickReceived?.Invoke(STKtickData);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "處理股票 Tick 資料失敗", "MarketService", LogDisplayTarget.SourceWindow);
            }
        }

        // 處理股票 Quote 資料
        private void ProcessSTKQuote(Exchange exchange, dynamic data)
        {
            try
            {
                var STKquoteData = new STKQuoteData(data);
                STK_QuoteReceived?.Invoke(STKquoteData);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "處理股票 Quote 資料失敗", "MarketService", LogDisplayTarget.SourceWindow);
            }
        }
        #endregion

        #region 期貨選擇權資料處理
        // 處理期貨 BidAsk 資料
        private void ProcessFOPBidAsk(Exchange exchange, dynamic data)
        {
            try
            {
                var FOPbidAskData = new FOPBidAskData(data);
                FOP_BidAskReceived?.Invoke(FOPbidAskData);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "處理FOP BidAsk 資料失敗", "MarketService", LogDisplayTarget.SourceWindow);
            }
        }

        // 處理期貨 Tick 資料
        private void ProcessFOPTick(Exchange exchange, dynamic data)
        {
            try
            {
                var FOPtickData = new FOPTickData(data);
                FOP_TickReceived?.Invoke(FOPtickData);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "處理FOP_Tick 資料失敗", "MarketService", LogDisplayTarget.SourceWindow);
            }
        }
        #endregion

        #region 輔助方法
        // 初始化 OrderBookViewModel
        private void InitializeOrderBookViewModel(string windowId, IContract contract)
        {
            try
            {
                // 從合約中提取關鍵屬性
                string actualCode = ContractAnalyzer.GetActualContractCode(contract);
                dynamic dynamicContract = contract;
                decimal limitUp = Convert.ToDecimal(dynamicContract.limit_up);
                decimal limitDown = Convert.ToDecimal(dynamicContract.limit_down);
                decimal reference = Convert.ToDecimal(dynamicContract.reference);
                string securityType = contract.security_type;
                string name = dynamicContract.name;

                _logService.LogInfo($"[OrderBook初始化] 📋 合約資訊: actualCode={actualCode}, limitUp={limitUp}, limitDown={limitDown}, reference={reference}",
                    "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                // 建立合約資訊
                var contractInfo = new ContractInfo
                {
                    Code = actualCode,
                    Symbol = actualCode,
                    Name = name,
                    SecurityType = securityType,
                    LimitUp = limitUp,        // ✅ BaseViewModel 需要這個
                    LimitDown = limitDown,    // ✅ BaseViewModel 需要這個
                    Reference = reference,    // ✅ BaseViewModel 需要這個
                };

                _logService.LogInfo($"[OrderBook初始化] 📊 建立 ContractInfo: Code={contractInfo.Code}, Symbol={contractInfo.Symbol}", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                // 觸發事件
                try
                {
                    OrderBookInitializationRequested?.Invoke(contractInfo, windowId);
                    _logService.LogInfo($"[OrderBook初始化] ✅ OrderBookInitializationRequested 事件觸發完成", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                }
                catch (Exception eventEx)
                {
                    _logService.LogError(eventEx, $"[OrderBook初始化] ❌ 觸發 OrderBookInitializationRequested 事件時發生錯誤", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                }

                // 獲取當前活動視窗並找到 QuoteViewModel
                var activeWindow = Application.Current.Windows.OfType<Window>()
                    .FirstOrDefault(w => (w.DataContext as QuoteViewModel)?.WindowId == windowId);

                if (activeWindow != null && activeWindow.DataContext is QuoteViewModel quoteViewModel)
                {
                    quoteViewModel.CurrentSubscribedCode = actualCode;

                    // 通過 QuoteViewModel 獲取 OrderBookViewModel
                    if (quoteViewModel.OrderBookViewModel != null)
                    {
                        quoteViewModel.OrderBookViewModel.CurrentSubscribedCode = actualCode;

                        // 在 UI 線程上執行初始化
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            quoteViewModel.OrderBookViewModel.InitializeOrderBook(limitUp, limitDown, reference, securityType);
                            quoteViewModel.OrderBookViewModel.Code = actualCode;
                        });

                        // 使用 WindowManagerService 更新視窗標題
                        var titleUpdateSuccess = WindowManagerService.Instance.UpdateWindowTitleWithContract(windowId, actualCode, WindowTitleFormat.ContractWithName, name);

                        if (titleUpdateSuccess)
                        {
                            _logService.LogInfo($"視窗 {windowId} 標題已更新為: {actualCode} - {name}", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                        }

                        _logService.LogInfo($"視窗{windowId}已初始化 OrderBookViewModel，訂閱商品檔_真實合約代碼: {actualCode}", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    }
                    else
                    {
                        _logService.LogWarning("QuoteViewModel.OrderBookViewModel 為 null", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    }
                }
                else
                {
                    _logService.LogWarning($"無法獲取視窗 {windowId} 的 QuoteViewModel 實例", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"初始化 OrderBookViewModel 失敗: {ex.Message}", "MarketService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
        }

        // 重置指定視窗的 OrderBookViewModel
        private void ResetWindowOrderBookViewModel(string windowId)
        {
            try
            {
                _logService.LogDebug($"[重置觸發] 準備重置視窗 {windowId} 的 OrderBook 數據", "MarketService");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (WindowManagerService.Instance.GetWindowById(windowId) is Views.QuoteWindow window &&
                        window.DataContext is QuoteViewModel quoteViewModel)
                    {
                        // 1. 清空商品代碼
                        quoteViewModel.CurrentSubscribedCode = "";

                        // 2. 執行我們剛改好的「不重建實例」的重置函數
                        quoteViewModel.ResetOrderBookViewModel();

                        // 3. 重置視窗標題
                        WindowManagerService.Instance.ResetWindowTitle(windowId);
                    }
                });
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"重置視窗 {windowId} 的 OrderBookViewModel 失敗", "MarketService");
            }
        }
        #endregion

        #region 資源釋放
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        // 取消所有訂閱
                        UnsubscribeAll();

                        // 清除事件訂閱
                        STK_TickReceived = null;
                        STK_BidAskReceived = null;
                        STK_QuoteReceived = null;
                        FOP_TickReceived = null;
                        FOP_BidAskReceived = null;
                        RawDataReceived = null;
                        ContractInfoReceived = null;
                        OrderBookInitializationRequested = null;

                        _logService.LogInfo("MarketService (重構版) 已釋放資源", "MarketService");
                    }
                    catch (Exception ex)
                    {
                        _logService.LogError(ex, "MarketService 釋放資源時發生錯誤", "MarketService");
                    }
                }

                _disposed = true;
            }
        }

        ~MarketService()
        {
            Dispose(false);
        }
        #endregion
    }
}