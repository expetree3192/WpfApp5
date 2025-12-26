// 新建 WindowManagerService.cs 在 Services 資料夾下
using Sinopac.Shioaji;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace WpfApp5.Services
{
    /// <summary>
    /// 視窗管理服務 - 處理應用程式中所有視窗的註冊與查詢
    /// </summary>
    public class WindowManagerService
    {
        #region 單例模式
        private static WindowManagerService? _instance;
        private static readonly object _lockInstance = new();

        public static WindowManagerService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockInstance)
                    {
                        _instance ??= new WindowManagerService();
                    }
                }
                return _instance;
            }
        }

        private WindowManagerService()
        {
            _logService = LogService.Instance;
            InitializeService();
        }
        #endregion

        #region Private Fields
        private readonly LogService _logService;
        private readonly Dictionary<string, Window> _openWindows = [];        
        private readonly Dictionary<string, Dictionary<QuoteType, HashSet<string>>> _contractSubscriptions = [];    // 將合約訂閱映射改為包含報價類型的多層映射
        private readonly Dictionary<string, string> _windowTitles = [];  // windowId -> title
        private readonly Dictionary<string, string> _windowDefaultTitles = [];  // windowId -> defaultTitle
        private int _nextWindowId = 1;
        #endregion

        #region 初始化
        private void InitializeService()
        {
            _logService.LogInfo("WindowManagerService 已初始化", "WindowManagerService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
        }
        #endregion

        #region 公開方法
        // 產生新的視窗 ID
        public string GenerateWindowId(string windowType)
        {
            string windowId = $"{windowType}_{_nextWindowId:D3}_{DateTime.Now:HHmmss}";
            _nextWindowId++;
            return windowId;
        }

        // 註冊開啟的視窗
        public void RegisterWindow(string windowId, Window window)
        {
            _openWindows[windowId] = window;
            
            _windowDefaultTitles[windowId] = window.Title;  // 記錄視窗的預設標題
            // 當視窗關閉時自動移除註冊
            window.Closed += (s, e) => {
                _openWindows.Remove(windowId);
                _windowTitles.Remove(windowId);  // 清理標題記錄
                _windowDefaultTitles.Remove(windowId);  // 清理預設標題記錄
                _logService.LogInfo($"視窗已關閉: {windowId}", "WindowManagerService");
            };

            _logService.LogInfo($"已註冊視窗: {windowId}", "WindowManagerService");
        }

        // 取得特定類型的所有開啟視窗
        public IEnumerable<Window> GetWindowsByType(string windowType)
        {
            return _openWindows
                .Where(kv => kv.Key.StartsWith(windowType + "_"))
                .Select(kv => kv.Value);
        }

        // 取得所有開啟的報價視窗
        public IEnumerable<Views.QuoteWindow> GetQuoteWindows()
        {
            return GetWindowsByType("Quote").OfType<Views.QuoteWindow>();
        }
        // 獲取特定視窗 ID 的視窗
        public Window? GetWindowById(string windowId)
        {
            if (_openWindows.TryGetValue(windowId, out var window))
            {
                return window;
            }
            return null;
        }

        // 檢查視窗 ID 是否存在
        public bool WindowExists(string windowId)
        {
            return _openWindows.ContainsKey(windowId);
        }
        // 取得視窗統計資訊
        public string GetWindowStats()
        {
            var stats = _openWindows.GroupBy(w => w.Key.Split('_')[0])
                                   .Select(g => $"{g.Key}: {g.Count()}")
                                   .ToArray();
            return string.Join(", ", stats);
        }

        // 關閉特定類型的所有視窗
        public int CloseWindowsByType(string windowType)
        {
            var windowsToClose = GetWindowsByType(windowType).ToList();
            int closedCount = 0;

            foreach (var window in windowsToClose)
            {
                try
                {
                    window.Close();
                    closedCount++;
                }
                catch (Exception ex)
                {
                    _logService.LogError(ex, $"關閉視窗失敗: {window}", "WindowManagerService");
                }
            }

            return closedCount;
        }

        // 關閉所有視窗
        public int CloseAllWindows()
        {
            var windowsToClose = _openWindows.Values.ToList();
            int closedCount = 0;

            foreach (var window in windowsToClose)
            {
                try
                {
                    window.Close();
                    closedCount++;
                }
                catch (Exception ex)
                {
                    _logService.LogError(ex, $"關閉視窗失敗: {window}", "WindowManagerService");
                }
            }

            return closedCount;
        }

        #region 🔥 視窗標題管理功能

        // 更新視窗標題為合約代碼
        public bool UpdateWindowTitleWithContract(string windowId, string contractCode, WindowTitleFormat titleFormat = WindowTitleFormat.ContractOnly, string contractName = "")
        {
            try
            {
                if (!_openWindows.TryGetValue(windowId, out var window))
                {
                    _logService.LogWarning($"找不到視窗 ID: {windowId}", "WindowManagerService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return false;
                }

                // 根據格式生成標題
                string newTitle = GenerateTitle(windowId, contractCode, titleFormat, contractName);

                // 在 UI 線程中更新標題
                Application.Current.Dispatcher.Invoke(() =>
                {
                    window.Title = newTitle;
                });

                // 記錄當前標題
                _windowTitles[windowId] = newTitle;

                _logService.LogInfo($"視窗 {windowId} 標題已更新為: {newTitle}", "WindowManagerService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"更新視窗 {windowId} 標題失敗: {ex.Message}", "WindowManagerService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                return false;
            }
        }

        // 重置視窗標題為預設標題
        public bool ResetWindowTitle(string windowId)
        {
            try
            {
                if (!_openWindows.TryGetValue(windowId, out var window))
                {
                    _logService.LogWarning($"找不到視窗 ID: {windowId}", "WindowManagerService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                    return false;
                }

                // 取得預設標題，如果沒有則使用通用預設標題
                string defaultTitle = _windowDefaultTitles.TryGetValue(windowId, out var title) ? title : "報價視窗";

                // 在 UI 線程中重置標題
                Application.Current.Dispatcher.Invoke(() =>
                {
                    window.Title = defaultTitle;
                });

                // 移除當前標題記錄
                _windowTitles.Remove(windowId);

                _logService.LogInfo($"視窗 {windowId} 標題已重置為: {defaultTitle}",
                    "WindowManagerService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"重置視窗 {windowId} 標題失敗: {ex.Message}",
                    "WindowManagerService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);
                return false;
            }
        }

        // 批量更新多個視窗的標題
        public int BatchUpdateWindowTitles(Dictionary<string, string> updates, WindowTitleFormat titleFormat = WindowTitleFormat.ContractOnly)
        {
            int successCount = 0;

            foreach (var update in updates)
            {
                if (UpdateWindowTitleWithContract(update.Key, update.Value, titleFormat))
                {
                    successCount++;
                }
            }

            _logService.LogInfo($"批量更新視窗標題完成，成功: {successCount}/{updates.Count}",
                "WindowManagerService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

            return successCount;
        }

        // 重置所有視窗標題
        public int ResetAllWindowTitles()
        {
            int successCount = 0;
            var windowIds = _openWindows.Keys.ToList();

            foreach (var windowId in windowIds)
            {
                if (ResetWindowTitle(windowId))
                {
                    successCount++;
                }
            }

            _logService.LogInfo($"批量重置視窗標題完成，成功: {successCount}/{windowIds.Count}",
                "WindowManagerService", LogDisplayTarget.SourceWindow | LogDisplayTarget.DebugOutput);

            return successCount;
        }

        // 取得視窗當前標題
        public string? GetWindowTitle(string windowId)
        {
            if (_openWindows.TryGetValue(windowId, out var window))
            {
                return window.Title;
            }
            return null;
        }

        // 檢查視窗標題是否包含合約代碼
        public bool WindowTitleContainsContract(string windowId, string contractCode)
        {
            var title = GetWindowTitle(windowId);
            return !string.IsNullOrEmpty(title) && title.Contains(contractCode);
        }

        // 根據格式生成標題
        private string GenerateTitle(string windowId, string contractCode, WindowTitleFormat format, string contractName = "")
        {
            string defaultTitle = _windowDefaultTitles.TryGetValue(windowId, out var title) ? title : "報價視窗";

            return format switch
            {
                WindowTitleFormat.ContractOnly => contractCode,
                WindowTitleFormat.DefaultWithContract => $"{defaultTitle} - {contractCode}",
                WindowTitleFormat.ContractWithTimestamp => $"{contractCode} - {DateTime.Now:HH:mm:ss}",
                WindowTitleFormat.FullFormat => $"{defaultTitle} - {contractCode} - {DateTime.Now:HH:mm:ss}",
                WindowTitleFormat.WindowIdWithContract => $"[{windowId}] {contractCode}",
                WindowTitleFormat.ContractWithName => string.IsNullOrEmpty(contractName) ? contractCode : $"{contractCode} - {contractName}",
                _ => contractCode
            };
        }

        #endregion

        // 註冊視窗對特定合約和報價類型的訂閱
        public void SubscribeToContract(string windowId, string contractCode, QuoteType quoteType)
        {
            // 使用 TryGetValue 替代 ContainsKey + 索引器存取
            if (!_contractSubscriptions.TryGetValue(contractCode, out var quoteTypeDict))
            {
                quoteTypeDict = [];
                _contractSubscriptions[contractCode] = quoteTypeDict;
            }

            // 使用 TryGetValue 檢查報價類型是否存在
            if (!quoteTypeDict.TryGetValue(quoteType, out var windowIds))
            {
                windowIds = [];
                quoteTypeDict[quoteType] = windowIds;
            }

            // 添加視窗 ID 到訂閱集合
            windowIds.Add(windowId);
            _logService.LogDebug($"視窗 {windowId} 已訂閱合約 {contractCode} 的 {quoteType} 報價", "WindowManagerService");
        }

        // 取消視窗對特定合約和報價類型的訂閱
        public void UnsubscribeFromContract(string windowId, string contractCode, QuoteType quoteType)
        {
            // 使用 TryGetValue 替代 ContainsKey + 索引器存取
            if (_contractSubscriptions.TryGetValue(contractCode, out var quoteTypeDict) && quoteTypeDict.TryGetValue(quoteType, out var windowIds))
            {
                windowIds.Remove(windowId);

                // 清理空集合
                if (windowIds.Count == 0)
                {
                    quoteTypeDict.Remove(quoteType);

                    // 如果合約沒有任何報價類型的訂閱，移除整個合約
                    if (quoteTypeDict.Count == 0)
                    {
                        _contractSubscriptions.Remove(contractCode);
                    }
                }

                _logService.LogDebug($"視窗 {windowId} 已取消訂閱合約 {contractCode} 的 {quoteType} 報價", "WindowManagerService");
            }
        }

        // 取得訂閱特定合約特定報價類型的所有視窗 ID 集合
        public IEnumerable<string> GetWindowsSubscribedToContract(string contractCode, QuoteType quoteType)
        {
            if (_contractSubscriptions.TryGetValue(contractCode, out var quoteTypes) &&
                quoteTypes.TryGetValue(quoteType, out var windowIds))
            {
                return windowIds;
            }

            return [];
        }

        // 檢查視窗是否訂閱了特定合約的特定報價類型
        public bool IsWindowSubscribedToContract(string windowId, string contractCode, QuoteType quoteType)
        {
            if (_contractSubscriptions.TryGetValue(contractCode, out var quoteTypes) &&
                quoteTypes.TryGetValue(quoteType, out var windowIds))
            {
                return windowIds.Contains(windowId);
            }

            return false;
        }

        // 清除視窗的所有訂閱
        public void ClearWindowSubscriptions(string windowId)
        {
            // 找出視窗訂閱的所有合約和報價類型
            var contractsToUpdate = new List<(string contractCode, QuoteType quoteType)>();

            foreach (var contractEntry in _contractSubscriptions)
            {
                foreach (var quoteTypeEntry in contractEntry.Value)
                {
                    if (quoteTypeEntry.Value.Contains(windowId))
                    {
                        contractsToUpdate.Add((contractEntry.Key, quoteTypeEntry.Key));
                    }
                }
            }

            // 移除訂閱
            foreach (var (contractCode, quoteType) in contractsToUpdate)
            {
                UnsubscribeFromContract(windowId, contractCode, quoteType);
            }

            _logService.LogDebug($"已清除視窗 {windowId} 的所有訂閱", "WindowManagerService");
        }
        #endregion

    }

    // 視窗標題格式枚舉
    public enum WindowTitleFormat
    {
        ContractOnly,   /// <summary>只顯示合約代碼</summary>
        DefaultWithContract,    /// <summary>預設標題 + 合約代碼</summary>
        ContractWithTimestamp,  /// <summary>合約代碼 + 時間戳</summary>
        FullFormat, /// <summary>完整格式：預設標題 + 合約代碼 + 時間戳</summary>
        WindowIdWithContract,    /// <summary>視窗 ID + 合約代碼</summary>
        ContractWithName    /// <summary>合約代碼 - 合約名稱</summary>
    }
}
