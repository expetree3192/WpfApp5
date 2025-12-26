using Sinopac.Shioaji;
using System;
using System.Collections.Generic;
using System.Linq;
using WpfApp5.Models;
using WpfApp5.Models.MarketData;
using WpfApp5.Services.Common;
using WpfApp5.ViewModels;

namespace WpfApp5.Services
{
    public class SubscriptionManager(LogService logService)
    {
        private readonly LogService _logService = logService;

        // 存儲訂閱信息的集合
        private readonly List<SubscriptionInfo> _subscriptions = [];

        // 映射字典：從實際代碼到訂閱信息的映射
        private readonly Dictionary<string, List<SubscriptionInfo>> _actualCodeToSubscriptions = [];

        // 映射字典：從視窗ID到訂閱信息的映射
        private readonly Dictionary<string, List<SubscriptionInfo>> _windowIdToSubscriptions = [];

        public class SubscriptionInfo
        {
            // 合約對象
            public required IContract Contract { get; set; }

            // 核心參數
            public required string ActualCode { get; set; }
            public required string WindowId { get; set; }
            public QuoteType QuoteType { get; set; }
            public bool IsOddLot { get; set; }

            // 原始訂閱參數
            public required string ProductType { get; set; }
            public required string Exchange { get; set; }
            public required string Symbol { get; set; }

            // 合約屬性
            public required string ActualExchange { get; set; }
            public required string SecurityType { get; set; }
            public decimal LimitUp { get; set; }
            public decimal LimitDown { get; set; }
            public decimal Reference { get; set; }

            // 用於訂閱管理的唯一鍵
            public string SubscriptionKey => $"{ActualCode}.{QuoteType}" + (IsOddLot ? ".ODD" : "");
        }

        // 添加訂閱
        public bool AddSubscription(
            IContract contract,
            string actualCode,
            string windowId,
            string productType,
            string exchange,
            string symbol,
            string actualExchange,
            string securityType,
            decimal limitUp,
            decimal limitDown,
            decimal reference,
            QuoteType quoteType,
            bool isOddLot)
        {
            try
            {
                // 檢查是否已訂閱
                if (_subscriptions.Any(s =>
                    s.ActualCode == actualCode &&
                    s.WindowId == windowId &&
                    s.QuoteType == quoteType &&
                    s.IsOddLot == isOddLot))
                {
                    _logService.LogWarning($"視窗 {windowId} 已訂閱合約: {actualCode}.{quoteType}{(isOddLot ? ".ODD" : "")}",
                        "SubscriptionManager", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return false;
                }

                var subscriptionInfo = new SubscriptionInfo
                {
                    Contract = contract,
                    ActualCode = actualCode,
                    WindowId = windowId,
                    ProductType = productType,
                    Exchange = exchange,
                    Symbol = symbol,
                    ActualExchange = actualExchange,
                    SecurityType = securityType,
                    LimitUp = limitUp,
                    LimitDown = limitDown,
                    Reference = reference,
                    QuoteType = quoteType,
                    IsOddLot = isOddLot
                };

                // 添加到訂閱列表
                _subscriptions.Add(subscriptionInfo);

                // 添加到實際代碼映射
                if (!_actualCodeToSubscriptions.TryGetValue(actualCode, out var codeSubscriptions))
                {
                    codeSubscriptions = [];
                    _actualCodeToSubscriptions[actualCode] = codeSubscriptions;
                }
                codeSubscriptions.Add(subscriptionInfo);

                // 添加到視窗ID映射
                if (!_windowIdToSubscriptions.TryGetValue(windowId, out var windowSubscriptions))
                {
                    windowSubscriptions = [];
                    _windowIdToSubscriptions[windowId] = windowSubscriptions;
                }
                windowSubscriptions.Add(subscriptionInfo);

                _logService.LogInfo($"視窗 {windowId} 已訂閱合約: {actualCode}.{quoteType}{(isOddLot ? ".ODD" : "")}, 原始參數: {productType}.{exchange}.{symbol}",
                    "SubscriptionManager", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"添加訂閱失敗: {ex.Message}",
                    "SubscriptionManager", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                return false;
            }
        }

        // 移除訂閱
        public bool RemoveSubscription(string actualCode, string windowId, QuoteType quoteType, bool isOddLot)
        {
            try
            {
                // 找到匹配的訂閱
                var subscription = _subscriptions.FirstOrDefault(s =>
                    s.ActualCode == actualCode &&
                    s.WindowId == windowId &&
                    s.QuoteType == quoteType &&
                    s.IsOddLot == isOddLot);

                if (subscription == null)
                {
                    _logService.LogWarning($"找不到視窗 {windowId} 的訂閱: {actualCode}.{quoteType}{(isOddLot ? ".ODD" : "")}",
                        "SubscriptionManager", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return false;
                }

                // 從實際代碼映射中移除
                if (_actualCodeToSubscriptions.TryGetValue(actualCode, out var codeSubscriptions))
                {
                    codeSubscriptions.Remove(subscription);
                    if (codeSubscriptions.Count == 0)
                    {
                        _actualCodeToSubscriptions.Remove(actualCode);
                    }
                }

                // 從視窗ID映射中移除
                if (_windowIdToSubscriptions.TryGetValue(windowId, out var windowSubscriptions))
                {
                    windowSubscriptions.Remove(subscription);
                    if (windowSubscriptions.Count == 0)
                    {
                        _windowIdToSubscriptions.Remove(windowId);
                    }
                }

                // 從訂閱列表中移除
                _subscriptions.Remove(subscription);

                _logService.LogInfo($"視窗 {windowId} 已取消訂閱: {actualCode}.{quoteType}{(isOddLot ? ".ODD" : "")}, 原始參數: {subscription.ProductType}.{subscription.Exchange}.{subscription.Symbol}",
                    "SubscriptionManager", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"移除訂閱失敗: {ex.Message}",
                    "SubscriptionManager", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                return false;
            }
        }

        // 檢查視窗是否有任何訂閱
        public bool HasWindowAnySubscriptions(string windowId)
        {
            try
            {
                return _windowIdToSubscriptions.ContainsKey(windowId) && _windowIdToSubscriptions[windowId].Count > 0;
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"檢查視窗 {windowId} 訂閱狀態失敗: {ex.Message}",
                    "SubscriptionManager", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                return false;
            }
        }

        // 檢查合約是否已被任何視窗訂閱
        public bool IsContractSubscribed(string actualCode, QuoteType quoteType, bool isOddLot)
        {
            return _subscriptions.Any(s =>
                s.ActualCode == actualCode &&
                s.QuoteType == quoteType &&
                s.IsOddLot == isOddLot);
        }

        // 檢查視窗是否已訂閱特定合約
        public bool IsWindowSubscribed(string actualCode, string windowId, QuoteType quoteType, bool isOddLot)
        {
            return _subscriptions.Any(s =>
                s.ActualCode == actualCode &&
                s.WindowId == windowId &&
                s.QuoteType == quoteType &&
                s.IsOddLot == isOddLot);
        }

        // 檢查是否有其他視窗訂閱了相同的合約
        public bool HasOtherWindowSubscriptions(string actualCode, string windowId, QuoteType quoteType, bool isOddLot)
        {
            return _subscriptions.Any(s =>
                s.ActualCode == actualCode &&
                s.WindowId != windowId &&
                s.QuoteType == quoteType &&
                s.IsOddLot == isOddLot);
        }
        
        // 獲取特定視窗的所有訂閱
        public List<SubscriptionInfo> GetWindowSubscriptions(string windowId)
        {
            if (_windowIdToSubscriptions.TryGetValue(windowId, out var subscriptions))
            {
                return [.. subscriptions];
            }
            return [];
        }

        /// <summary>
        /// 獲取視窗當前訂閱的主要合約（通常一個視窗只訂閱一個商品）
        /// </summary>
        /// <param name="windowId">視窗ID</param>
        /// <param name="actualCode">合約代碼（可選，如果視窗訂閱多個商品時使用）</param>
        /// <returns>IContract 或 null</returns>
        public IContract? GetWindowCurrentContract(string windowId, string? actualCode = null)
        {
            try
            {
                if (_windowIdToSubscriptions.TryGetValue(windowId, out var subscriptions) && subscriptions.Count > 0)
                {
                    // 如果指定了合約代碼，查找匹配的訂閱
                    if (!string.IsNullOrEmpty(actualCode))
                    {
                        var subscription = subscriptions.FirstOrDefault(s => s.ActualCode == actualCode);
                        if (subscription != null)
                        {
                            _logService.LogDebug($"視窗 {windowId} 指定合約: {actualCode}", "SubscriptionManager", LogDisplayTarget.DebugOutput);
                            return subscription.Contract;
                        }

                        _logService.LogWarning($"視窗 {windowId} 找不到合約: {actualCode}", "SubscriptionManager", LogDisplayTarget.DebugOutput);
                        return null;
                    }

                    // 如果沒有指定合約代碼，返回第一個訂閱的合約
                    var contract = subscriptions[0].Contract;
                    _logService.LogDebug($"視窗 {windowId} 當前合約: {subscriptions[0].ActualCode}", "SubscriptionManager", LogDisplayTarget.DebugOutput);
                    return contract;
                }

                _logService.LogWarning($"視窗 {windowId} 沒有任何訂閱", "SubscriptionManager", LogDisplayTarget.DebugOutput);
                return null;
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"獲取視窗 {windowId} 當前合約失敗: {ex.Message}", "SubscriptionManager", LogDisplayTarget.DebugOutput);
                return null;
            }
        }

        /// <summary>
        /// 獲取視窗當前訂閱的完整資訊（包含合約、價格、交易所等）
        /// </summary>
        /// <param name="windowId">視窗ID</param>
        /// <param name="actualCode">合約代碼（可選，如果視窗訂閱多個商品時使用）</param>
        /// <returns>SubscriptionInfo 或 null</returns>
        public SubscriptionInfo? GetWindowCurrentSubscriptionInfo(string windowId, string? actualCode = null)
        {
            try
            {
                if (_windowIdToSubscriptions.TryGetValue(windowId, out var subscriptions) && subscriptions.Count > 0)
                {
                    // 如果指定了合約代碼，查找匹配的訂閱
                    if (!string.IsNullOrEmpty(actualCode))
                    {
                        var subscription = subscriptions.FirstOrDefault(s => s.ActualCode == actualCode);
                        if (subscription != null)
                        {
                            _logService.LogDebug($"視窗 {windowId} 指定訂閱: {actualCode} ({subscription.ProductType}.{subscription.Exchange}.{subscription.Symbol})",
                                "SubscriptionManager", LogDisplayTarget.DebugOutput);
                            return subscription;
                        }

                        _logService.LogWarning($"視窗 {windowId} 找不到訂閱: {actualCode}",
                            "SubscriptionManager", LogDisplayTarget.DebugOutput);
                        return null;
                    }

                    // 如果沒有指定合約代碼，返回第一個訂閱的完整資訊
                    var info = subscriptions[0];
                    _logService.LogDebug($"視窗 {windowId} 當前訂閱: {info.ActualCode} ({info.ProductType}.{info.Exchange}.{info.Symbol})",
                        "SubscriptionManager", LogDisplayTarget.DebugOutput);
                    return info;
                }

                _logService.LogWarning($"視窗 {windowId} 沒有任何訂閱",
                    "SubscriptionManager", LogDisplayTarget.DebugOutput);
                return null;
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"獲取視窗 {windowId} 當前訂閱資訊失敗: {ex.Message}",
                    "SubscriptionManager", LogDisplayTarget.DebugOutput);
                return null;
            }
        }
        /// <summary>
        /// 根據合約代碼查找所有訂閱該商品的 WindowIds
        /// </summary>
        /// <param name="actualCode">合約代碼（例如：2890, TXFK5）</param>
        /// <returns>WindowId 列表</returns>
        public List<string> FindAllWindowIdsByCode(string actualCode)
        {
            try
            {
                if (string.IsNullOrEmpty(actualCode))
                {
                    _logService.LogWarning($"合約代碼為空，無法查找 WindowIds", "SubscriptionManager", LogDisplayTarget.DebugOutput);
                    return [];
                }

                // 從 actualCode 映射中查找訂閱
                if (_actualCodeToSubscriptions.TryGetValue(actualCode, out var subscriptions))
                {
                    // 去重後返回所有 WindowIds
                    var windowIds = subscriptions
                        .Select(s => s.WindowId)
                        .Distinct()
                        .ToList();

                    _logService.LogDebug($"找到合約 {actualCode} 對應的 {windowIds.Count} 個視窗: {string.Join(", ", windowIds)}", "SubscriptionManager", LogDisplayTarget.DebugOutput);

                    return windowIds;
                }

                _logService.LogWarning($"找不到合約 {actualCode} 的訂閱視窗", "SubscriptionManager", LogDisplayTarget.DebugOutput);

                return [];
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"查找合約 {actualCode} 的 WindowIds 失敗: {ex.Message}",
                    "SubscriptionManager", LogDisplayTarget.DebugOutput);
                return [];
            }
        }
        // 獲取訂閱合約
        public IContract? GetContractByActualCode(string actualCode)
        {
            if (_actualCodeToSubscriptions.TryGetValue(actualCode, out var subscriptions) && subscriptions.Count > 0)
            {
                return subscriptions[0].Contract;
            }
            return null;
        }

        // 獲取所有唯一訂閱（按 actualCode + quoteType + isOddLot 組合）
        public List<SubscriptionInfo> GetAllUniqueSubscriptions()
        {
            try
            {
                // 使用字典來確保唯一性
                var uniqueSubscriptions = new Dictionary<string, SubscriptionInfo>();

                foreach (var subscription in _subscriptions)
                {
                    var key = subscription.SubscriptionKey;
                    if (!uniqueSubscriptions.ContainsKey(key))
                    {
                        uniqueSubscriptions[key] = subscription;
                    }
                }

                return [.. uniqueSubscriptions.Values];
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"獲取唯一訂閱失敗: {ex.Message}", "SubscriptionManager", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                return [];
            }
        }

        // 清空所有訂閱記錄
        public void ClearAllSubscriptions()
        {
            try
            {
                int count = _subscriptions.Count;

                // 清空所有集合
                _subscriptions.Clear();
                _actualCodeToSubscriptions.Clear();
                _windowIdToSubscriptions.Clear();

                _logService.LogInfo($"已清空所有訂閱記錄，共 {count} 條",
                    "SubscriptionManager", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"清空訂閱記錄失敗: {ex.Message}",
                    "SubscriptionManager", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
        }

        // 清理視窗的所有訂閱 (優化版本)
        public void CleanupWindowSubscriptions(string windowId)
        {
            try
            {
                if (_windowIdToSubscriptions.TryGetValue(windowId, out var subscriptions))
                {
                    // 複製訂閱列表以避免集合修改異常
                    var subscriptionsCopy = subscriptions.ToList();

                    // 從主訂閱列表和代碼映射中移除
                    foreach (var subscription in subscriptionsCopy)
                    {
                        _subscriptions.Remove(subscription);

                        // 從代碼映射中移除
                        if (_actualCodeToSubscriptions.TryGetValue(subscription.ActualCode, out var codeSubscriptions))
                        {
                            codeSubscriptions.Remove(subscription);
                            if (codeSubscriptions.Count == 0)
                            {
                                _actualCodeToSubscriptions.Remove(subscription.ActualCode);
                            }
                        }
                    }

                    // 從視窗ID映射中移除
                    _windowIdToSubscriptions.Remove(windowId);

                    _logService.LogInfo($"已清理視窗 {windowId} 的所有訂閱，共 {subscriptionsCopy.Count} 條",
                        "SubscriptionManager", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"清理視窗 {windowId} 的訂閱失敗: {ex.Message}",
                    "SubscriptionManager", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
            }
        }

        // 獲取所有訂閱的合約代碼
        public List<string> GetAllSubscribedCodes()
        {
            return [.. _actualCodeToSubscriptions.Keys];
        }
    }
}
