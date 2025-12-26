using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Sinopac.Shioaji;
using WpfApp5.Services.Common;
using WpfApp5.Utils;
using WpfApp5.Models;
using SJAction = Sinopac.Shioaji.Action;

namespace WpfApp5.Services
{
    public class OrderService
    {
        #region 單例模式

        private static OrderService? _instance;
        private static readonly object _lockInstance = new();

        public static OrderService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockInstance)
                    {
                        _instance ??= new OrderService();
                    }
                }
                return _instance;
            }
        }

        private OrderService()
        {
            InitializeService();
        }

        #endregion

        #region 核心狀態與事件

        // 靜態追蹤字典 - 簡化版本，保留 customField 用於股票下單追蹤
        private static readonly ConcurrentDictionary<string, string> _customFieldToWindowId = new();

        // UpdateStatus 限流控制
        private static readonly SemaphoreSlim _updateStatusSemaphore = new(1, 1);
        private static readonly DateTime _lastUpdateStatusTime = DateTime.MinValue;
        private static readonly object _updateStatusLock = new();
        private const int UPDATE_STATUS_COOLDOWN_MS = 1000; // 冷卻時間

        // 靜態事件
        public static event Action<Trade>? StaticOrderStatusUpdated;
        public static event Action<OrderDataInfo>? StaticGlobalOrderCallback;
        public static event Action<string, OrderDataInfo>? StaticWindowOrderCallback;
        public static event Action<OrderStatsUpdateEventArgs>? OrderStatsUpdateRequested;

        // 實例事件
        public event Action<Trade>? OrderStatusUpdated;
        public event Action<OrderDataInfo>? GlobalOrderCallback;
        public event Action<string, OrderDataInfo>? WindowOrderCallback;

        private void InitializeService()
        {
            LogInfo("[服務初始化] OrderService 已啟動");
        }

        #endregion

        #region 核心交易函數方法

        //直接使用 Shioaji API，完成後自動 UpdateStatus
        public static async Task<ServiceResult<Trade>> PlaceOrderAsync(IContract contract, IOrder order, string windowId, bool useNonBlocking = true, int timeoutMs = 5000, bool autoUpdateStatus = true)
        {
            try
            {
                // 設定追蹤資訊
                var customField = order.custom_field ?? GenerateCustomField();
                order.custom_field = customField;
                _customFieldToWindowId[customField] = windowId;

                LogInfo($"[高效能下單] 合約: {contract.code}, 動作: {order.action}, 價格: {order.price}, 數量: {order.quantity}");

                Trade trade;
                if (useNonBlocking)
                {
                    // 非阻塞模式
                    trade = await Task.Run(() => ShioajiService.PlaceOrder(contract, order, timeout: 0, cb: null));
                }
                else
                {
                    // 阻塞模式
                    trade = await Task.Run(() => ShioajiService.PlaceOrder(contract, order, timeout: timeoutMs, cb: null));
                }

                if (trade == null)
                {
                    return ServiceResult<Trade>.Failure("下單失敗，未收到回應");
                }

                LogInfo($"[高效能下單] 下單成功: {trade.order?.id}");

                // 自動執行 UpdateStatus
                if (autoUpdateStatus)
                {
                    _ = Task.Run(async () =>
                    {
                        await SmartUpdateStatusAsync("單筆下單", trade.order?.account);
                    });
                }

                return ServiceResult<Trade>.Success(trade, "下單成功");
            }
            catch (Exception ex)
            {
                LogError(ex, "[高效能下單] 下單失敗");
                return ServiceResult<Trade>.Failure($"下單失敗: {ex.Message}");
            }
        }

        // 🎯 重構：使用統一查詢的 GetCancellableOrdersAsync
        public static async Task<ServiceResult<List<Trade>>> GetCancellableOrdersAsync(string? contractFilter = null, string? actionFilter = null)
        {
            try
            {
                var unifiedResult = await GetUnifiedOrderDataAsync(contractFilter, actionFilter, forceUpdateStatus: true);

                if (!unifiedResult.IsSuccess || unifiedResult.Data == null)
                {
                    return ServiceResult<List<Trade>>.Failure(unifiedResult.Message);
                }

                return ServiceResult<List<Trade>>.Success(unifiedResult.Data.CancellableOrders, $"找到 {unifiedResult.Data.CancellableOrders.Count} 筆可刪除委託");
            }
            catch (Exception ex)
            {
                LogError(ex, "[智能篩選] 取得可刪除委託失敗");
                return ServiceResult<List<Trade>>.Failure($"取得可刪除委託失敗: {ex.Message}");
            }
        }

        // 單筆刪單 - 直接使用 Trade 物件(可單獨使用（設定 autoUpdateStatus = true）)
        public static async Task<ServiceResult<Trade>> CancelOrderAsync(Trade trade, bool useNonBlocking = false, int timeoutMs = 5000, bool autoUpdateStatus = false) // 單筆刪單預設不自動更新，由批量刪單統一更新
        {
            if (trade == null)
            {
                return ServiceResult<Trade>.Failure("Trade 物件不能為空");
            }

            try
            {
                LogInfo($"[高效能刪單] 開始刪除委託: {trade.order?.ordno}");

                Trade cancelledTrade;
                if (useNonBlocking)
                {
                    // 非阻塞模式
                    cancelledTrade = await Task.Run(() => ShioajiService.CancelOrder(trade, timeout: 0, cb: null));
                }
                else
                {
                    // 阻塞模式 - 更可靠
                    cancelledTrade = await Task.Run(() => ShioajiService.CancelOrder(trade, timeout: timeoutMs, cb: null));
                }

                if (cancelledTrade == null)
                {
                    return ServiceResult<Trade>.Failure("刪單失敗，未收到回應");
                }

                // 觸發狀態更新事件
                StaticOrderStatusUpdated?.Invoke(cancelledTrade);

                LogInfo($"[高效能刪單] 刪單成功: {trade.order?.ordno}");

                // 自動執行 UpdateStatus (僅在單獨呼叫時)
                if (autoUpdateStatus)
                {
                    _ = Task.Run(async () =>
                    {
                        await SmartUpdateStatusAsync("單筆刪單", trade.order?.account);
                    });
                }

                return ServiceResult<Trade>.Success(cancelledTrade, "刪單成功");
            }
            catch (Exception ex)
            {
                LogError(ex, $"[高效能刪單] 刪單失敗:ordno: {trade.order?.ordno}");
                return ServiceResult<Trade>.Failure($"刪單失敗: {ex.Message}");
            }
        }

        // 並行批量刪單 - 使用 Parallel.ForEach
        public static async Task<ServiceResult<BatchCancelResult>> CancelOrdersBatchAsync(List<Trade> trades, int maxDegreeOfParallelism = 5, bool autoUpdateStatus = true)
        {
            if (trades == null || trades.Count == 0)
            {
                return ServiceResult<BatchCancelResult>.Success(new BatchCancelResult { TotalCount = 0 }, "沒有需要刪除的委託");
            }

            LogInfo($"[修正並行刪單] 🚀 開始並行刪除 {trades.Count} 筆委託 (並行度: {maxDegreeOfParallelism})");

            // 🚀 使用 ConcurrentBag 收集結果（線程安全）
            var results = new ConcurrentBag<CancelResult>();
            var successCount = 0;
            var failCount = 0;

            // 🚀 配置並行選項
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism
            };

            // 🚀 使用 Parallel.ForEach 進行高效並行處理
            await Task.Run(() =>
            {
                try
                {
                    Parallel.ForEach(
                        trades.Select((trade, index) => new { Trade = trade, Index = index }),
                        parallelOptions,
                        item =>
                        {
                            var trade = item.Trade;
                            var index = item.Index;

                            try
                            {
                                LogInfo($"[修正並行刪單] 📝 處理第 {index + 1}/{trades.Count} 筆: {trade.order?.ordno}");

                                // 🛠️ 修正：使用阻塞模式，確保能捕捉到真實的錯誤，且使用較短的 timeout 來平衡速度和準確性
                                Trade? cancelledTrade = null;
                                string errorMessage = string.Empty;
                                bool isSuccess = false;

                                try
                                {
                                    // 使用阻塞模式，但設定較短的 timeout (3秒)
                                    cancelledTrade = ShioajiService.CancelOrder(trade, timeout: 3000, cb: null);
                                    isSuccess = cancelledTrade != null;
                                    if (!isSuccess)
                                    {
                                        errorMessage = "刪單失敗，未收到回應";
                                    }
                                }
                                catch (Exception cancelEx)
                                {
                                    isSuccess = false;
                                    errorMessage = cancelEx.Message;
                                    LogWarning($"[修正並行刪單] ⚠️ 第 {index + 1} 筆刪單 API 錯誤: {trade.order?.ordno} - {cancelEx.Message}");
                                }

                                var cancelResult = new CancelResult
                                {
                                    OrderId = trade.order?.id ?? string.Empty,
                                    OrderNumber = trade.order?.ordno ?? string.Empty,
                                    Success = isSuccess,
                                    Message = isSuccess ? "刪單成功" : errorMessage,
                                    ContractCode = GetContractCode(trade)
                                };

                                results.Add(cancelResult);

                                if (isSuccess)
                                {
                                    Interlocked.Increment(ref successCount);
                                    LogInfo($"[修正並行刪單] ✅ 第 {index + 1} 筆刪單成功: {trade.order?.ordno}");

                                    // 觸發狀態更新事件
                                    if (cancelledTrade != null)
                                    {
                                        StaticOrderStatusUpdated?.Invoke(cancelledTrade);
                                    }
                                }
                                else
                                {
                                    Interlocked.Increment(ref failCount);
                                    LogWarning($"[修正並行刪單] ❌ 第 {index + 1} 筆刪單失敗: {trade.order?.ordno} - {errorMessage}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Interlocked.Increment(ref failCount);
                                var cancelResult = new CancelResult
                                {
                                    OrderId = trade.order?.id ?? string.Empty,
                                    OrderNumber = trade.order?.ordno ?? string.Empty,
                                    Success = false,
                                    Message = $"例外錯誤: {ex.Message}",
                                    ContractCode = GetContractCode(trade)
                                };
                                results.Add(cancelResult);

                                LogError(ex, $"[修正並行刪單] ❌ 第 {index + 1} 筆刪單例外: {trade.order?.ordno}");
                            }
                        });
                }
                catch (Exception ex)
                {
                    LogError(ex, "[修正並行刪單] Parallel.ForEach 執行失敗");
                }
            });

            var batchResult = new BatchCancelResult
            {
                TotalCount = trades.Count,
                SuccessCount = successCount,
                FailCount = failCount,
                Results = [.. results]
            };

            LogInfo($"[修正並行刪單] 🏁 完成！總計: {trades.Count} 筆，成功: {successCount} 筆，失敗: {failCount} 筆");

            // 🚀 整批次刪單後執行全帳號 UpdateStatus
            if (autoUpdateStatus)
            {
                LogInfo("[修正並行刪單] 🔄 整批次刪單(後)執行全帳號 UpdateStatus...");
                try
                {
                    await ShioajiService.UpdateAllAccountStatusAsync().ConfigureAwait(false);
                    await Task.Delay(50).ConfigureAwait(false);    // 短暫等待讓API狀態穩定
                    LogInfo("[修正並行刪單] ✅ 整批次刪單(後) UpdateStatus 完成");
                }
                catch (Exception ex)
                {
                    LogWarning($"[修正並行刪單] ⚠️ 整批次刪單(後) UpdateStatus 失敗: {ex.Message}");
                }
            }

            return ServiceResult<BatchCancelResult>.Success(batchResult, $"修正並行批量刪單完成: 成功 {successCount} 筆，失敗 {failCount} 筆");
        }


        // 合約刪單 - 一鍵刪除指定合約的所有委託，完成後自動 UpdateStatus
        public static async Task<ServiceResult<BatchCancelResult>> CancelContractOrdersAsync(string contractCode, string? actionFilter = null, bool autoUpdateStatus = true, int maxDegreeOfParallelism = 5)
        {
            try
            {
                LogInfo($"[合約刪單] 開始刪除合約 {contractCode} 的{actionFilter ?? "所有"}委託單");

                // 取得可刪除的委託
                var cancellableResult = await GetCancellableOrdersAsync(contractCode, actionFilter);    //  GetCancellableOrdersAsync函數會先執行await ShioajiService.UpdateAllAccountStatusAsync();
                if (!cancellableResult.IsSuccess)
                {
                    return ServiceResult<BatchCancelResult>.Failure(cancellableResult.Message);
                }

                if (cancellableResult.Data == null || cancellableResult.Data.Count == 0)
                {
                    LogInfo($"[合約刪單] ℹ️ 沒有找到合約 {contractCode} 的可刪除{actionFilter ?? ""}委託");
                    return ServiceResult<BatchCancelResult>.Success(new BatchCancelResult { TotalCount = 0 }, $"沒有找到合約 {contractCode} 的可刪除{actionFilter ?? ""}委託");
                }

                LogInfo($"[合約刪單] 📋 找到 {cancellableResult.Data.Count} 筆可刪除的{actionFilter ?? ""}委託");

                // 🚀 使用並行批量刪單
                return await CancelOrdersBatchAsync(cancellableResult.Data, maxDegreeOfParallelism, autoUpdateStatus);  //  整批次刪單後執行全帳號 UpdateStatus
            }
            catch (Exception ex)
            {
                LogError(ex, $"[合約刪單] 刪除合約 {contractCode} 委託失敗");
                return ServiceResult<BatchCancelResult>.Failure($"刪除合約委託失敗: {ex.Message}");
            }
        }

        // 一鍵清倉 - 刪除所有可刪除委託，完成後自動 UpdateStatus
        public static async Task<ServiceResult<BatchCancelResult>> CancelAllOrdersAsync(bool autoUpdateStatus = true, int maxDegreeOfParallelism = 5)
        {
            try
            {
                LogInfo("[一鍵清倉] 🧹 開始刪除所有可刪除委託");

                var cancellableResult = await GetCancellableOrdersAsync();  //  GetCancellableOrdersAsync函數會先執行await ShioajiService.UpdateAllAccountStatusAsync();
                if (!cancellableResult.IsSuccess)
                {
                    return ServiceResult<BatchCancelResult>.Failure(cancellableResult.Message);
                }

                if (cancellableResult.Data == null || cancellableResult.Data.Count == 0)
                {
                    LogInfo("[一鍵清倉] ℹ️ 沒有找到可刪除的委託");
                    return ServiceResult<BatchCancelResult>.Success(new BatchCancelResult { TotalCount = 0 }, "沒有找到可刪除的委託");
                }

                LogInfo($"[一鍵清倉] 📋 找到 {cancellableResult.Data.Count} 筆可刪除委託");

                // 使用並行批量刪單
                return await CancelOrdersBatchAsync(cancellableResult.Data, maxDegreeOfParallelism, autoUpdateStatus);  //  整批次刪單後 會執行全帳號 UpdateStatus
            }
            catch (Exception ex)
            {
                LogError(ex, "[一鍵清倉] 刪除所有委託失敗");
                return ServiceResult<BatchCancelResult>.Failure($"一鍵清倉失敗: {ex.Message}");
            }
        }

        #endregion

        #region UpdateStatus 機制

        // UpdateStatus - 保留並發控制
        private static async Task SmartUpdateStatusAsync(string operation, Account? account = null)
        {
            if (account == null)
            {
                LogWarning($"[智能更新] {operation} - 帳戶為空，跳過狀態更新");
                return;
            }

            try
            {
                // 🔒 使用信號量確保同時只有一個 UpdateStatus 在執行
                if (!await _updateStatusSemaphore.WaitAsync(100))
                {
                    LogWarning($"[智能更新] {operation} - UpdateStatus 正在執行中，跳過此次更新");
                    return;
                }

                try
                {
                    LogInfo($"[智能更新] {operation} - 開始執行 UpdateStatus...");

                    // 在背景執行緒中執行 UpdateStatus
                    await Task.Run(() =>
                    {
                        try
                        {
                            ShioajiService.UpdateAccountStatus(account, timeout: 3000);
                            LogInfo($"[智能更新] {operation} - UpdateStatus 執行成功");
                        }
                        catch (Exception ex)
                        {
                            LogWarning($"[智能更新] {operation} - UpdateStatus 執行失敗: {ex.Message}");
                        }
                    });
                }
                finally
                {
                    _updateStatusSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                LogError(ex, $"[智能更新] {operation} - 智能更新異常");
            }
        }

        // 手動觸發 UpdateStatus (提供給外部使用)
        public static async Task ManualUpdateStatusAsync(Account account, string source = "手動觸發")
        {
            await SmartUpdateStatusAsync(source, account);
        }

        #endregion
        #region 🎯 統一委託資料查詢方法
        /// <summary>
        /// 🚀 統一的委託資料查詢方法 - 一次 ListTrades 滿足所有需求
        /// </summary>
        /// <param name="contractFilter">合約篩選（可選）</param>
        /// <param name="actionFilter">買賣方向篩選（可選）</param>
        /// <param name="forceUpdateStatus">是否強制執行 UpdateStatus</param>
        /// <returns>統一的委託查詢結果</returns>
        public static async Task<ServiceResult<UnifiedOrderQueryResult>> GetUnifiedOrderDataAsync(
            string? contractFilter = null,
            string? actionFilter = null,
            bool forceUpdateStatus = true)
        {
            try
            {
                LogInfo($"[統一查詢] 🔄 開始統一委託查詢 - 合約:{contractFilter ?? "全部"}, 方向:{actionFilter ?? "全部"}, 強制更新:{forceUpdateStatus}");

                // 🚀 只在需要時執行 UpdateStatus
                if (forceUpdateStatus)
                {
                    LogInfo("[統一查詢] 🔄 執行 UpdateAllAccountStatus 確保資料最新...");
                    await ShioajiService.UpdateAllAccountStatusAsync();
                    LogInfo("[統一查詢] 📡 UpdateAllAccountStatus 執行完成");
                }

                // 一次性取得所有委託資料
                var allTrades = ShioajiService.ListTrades();

                // 初始化結果容器
                var result = new UnifiedOrderQueryResult
                {
                    QueryTime = DateTime.Now,
                    ContractFilter = contractFilter,
                    ActionFilter = actionFilter
                };

                // 去重和統計變數
                var seenOrders = new HashSet<string>();
                var duplicateCount = 0;
                var nullOrdnoCount = 0;

                // 一次遍歷trade，同時處理多項需求(收集可刪除委託（用於刪單）、按價格分組統計（用於 OrderBook 顯示）、 統計資料（用於統計顯示）)
                foreach (Trade trade in allTrades.Cast<Trade>())
                {
                    try
                    {
                        var ordno = trade.order?.ordno?.ToString();
                        if (string.IsNullOrEmpty(ordno))
                        {
                            nullOrdnoCount++;
                            continue;
                        }

                        // 去重檢查
                        if (!seenOrders.Add(ordno))
                        {
                            duplicateCount++;
                            continue;
                        }

                        // 1. 檢查狀態是否可刪除
                        if (!IsOrderCancellable(trade))
                            continue;

                        // 2. 提取基本資訊
                        var tradeContractCode = GetContractCode(trade);
                        var action = trade.order?.action?.ToString();
                        var price = (decimal)(trade.order?.price ?? 0.0);
                        var totalQuantity = Convert.ToInt32(trade.order?.quantity ?? 0);
                        var status = trade.status?.status;

                        if (price <= 0 || totalQuantity <= 0) continue;

                        // 3. 合約篩選
                        bool matchContract = string.IsNullOrEmpty(contractFilter) ||
                                           string.Equals(tradeContractCode, contractFilter, StringComparison.OrdinalIgnoreCase);

                        // 4. 買賣方向篩選
                        bool matchAction = string.IsNullOrEmpty(actionFilter) ||
                                         string.Equals(action, actionFilter, StringComparison.OrdinalIgnoreCase);

                        // 5. 同時處理多種需求

                        // A. 收集可刪除委託（用於刪單）
                        if (matchContract && matchAction)
                        {
                            result.CancellableOrders.Add(trade);
                        }

                        // B. 按價格分組統計（用於 OrderBook 顯示）
                        if (matchContract) // 詳細掛單只需要合約匹配
                        {
                            if (!result.PriceOrderDetails.TryGetValue(price, out PriceOrderDetails? details))
                            {
                                details = new PriceOrderDetails { Price = price };
                                result.PriceOrderDetails[price] = details;
                            }

                            // 判斷已成交數量（簡化處理）
                            int filledQuantity = 0; // 暫時設為 0，實際需要透過成交回報追蹤
                            int pendingQuantity = totalQuantity;

                            if (action?.Equals("Buy", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                details.BuyPendingQuantity += pendingQuantity;
                                details.BuyFilledQuantity += filledQuantity;

                                // C. 統計資料（用於統計顯示）
                                result.Stats.PendingBuyOrderCount++;
                                result.Stats.PendingBuyQuantity += totalQuantity;
                                result.Stats.TotalBuyValue += (double)price * totalQuantity;
                            }
                            else if (action?.Equals("Sell", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                details.SellPendingQuantity += pendingQuantity;
                                details.SellFilledQuantity += filledQuantity;

                                // C. 統計資料（用於統計顯示）
                                result.Stats.PendingSellOrderCount++;
                                result.Stats.PendingSellQuantity += totalQuantity;
                                result.Stats.TotalSellValue += (double)price * totalQuantity;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWarning($"[統一查詢] 處理委託時發生錯誤: {ex.Message}");
                    }
                }

                // 完成統計計算
                result.Stats.ContractCode = contractFilter ?? "全部";
                result.Stats.TotalPendingOrderCount = result.Stats.PendingBuyOrderCount + result.Stats.PendingSellOrderCount;
                result.Stats.TotalPendingQuantity = result.Stats.PendingBuyQuantity + result.Stats.PendingSellQuantity;

                // 計算平均價格
                if (result.Stats.PendingBuyQuantity > 0)
                {
                    result.Stats.PendingBuyPrice = result.Stats.TotalBuyValue / result.Stats.PendingBuyQuantity;
                }

                if (result.Stats.PendingSellQuantity > 0)
                {
                    result.Stats.PendingSellPrice = result.Stats.TotalSellValue / result.Stats.PendingSellQuantity;
                }

                // 記錄統計摘要
                var summaryMessage = $"[統一查詢] 總數: {allTrades.Count}, 可刪除: {result.CancellableOrders.Count}, " +
                                   $"價格組: {result.PriceOrderDetails.Count}, 去重後: {seenOrders.Count}";
                if (nullOrdnoCount > 0) summaryMessage += $", 無ordno: {nullOrdnoCount}";
                if (duplicateCount > 0) summaryMessage += $", 重複: {duplicateCount}";

                //  LogInfo(summaryMessage);
                //  LogInfo($"[統一查詢] ✅ 統計完成 - 買單: {result.Stats.PendingBuyOrderCount}筆({result.Stats.PendingBuyQuantity}), " + $"賣單: {result.Stats.PendingSellOrderCount}筆({result.Stats.PendingSellQuantity})");

                return ServiceResult<UnifiedOrderQueryResult>.Success(result, "統一委託查詢完成");
            }
            catch (Exception ex)
            {
                LogError(ex, "[統一查詢] 統一委託查詢失敗");
                return ServiceResult<UnifiedOrderQueryResult>.Failure($"統一委託查詢失敗: {ex.Message}");
            }
        }

        // 統一委託查詢結果
        public class UnifiedOrderQueryResult
        {
            public DateTime QueryTime { get; set; } = DateTime.Now;
            public string? ContractFilter { get; set; }
            public string? ActionFilter { get; set; }

            // A. 可刪除委託列表（用於刪單）
            public List<Trade> CancellableOrders { get; set; } = [];

            // B. 按價格分組的詳細資訊（用於 OrderBook 顯示）
            public Dictionary<decimal, PriceOrderDetails> PriceOrderDetails { get; set; } = [];

            // C. 統計資訊（用於統計顯示）
            public ContractPendingOrderStats Stats { get; set; } = new();
        }

        #endregion
        #region 掛單統計查詢方法

        // 🎯 重構：使用統一查詢的 GetContractPendingOrderStatsSync
        public static ServiceResult<ContractPendingOrderStats> GetContractPendingOrderStatsSync(string contractCode)
        {
            try
            {
                if (string.IsNullOrEmpty(contractCode))
                {
                    return ServiceResult<ContractPendingOrderStats>.Failure("合約代碼不可為空");
                }

                // 🎯 使用統一查詢
                var unifiedResult = GetUnifiedOrderDataAsync(contractCode, actionFilter: null, forceUpdateStatus: false).Result;

                if (!unifiedResult.IsSuccess || unifiedResult.Data == null)
                {
                    return ServiceResult<ContractPendingOrderStats>.Failure(unifiedResult.Message);
                }

                var stats = unifiedResult.Data.Stats;
                stats.UpdateTime = DateTime.Now;

                LogInfo($"[掛單統計] ✅ {contractCode} 統計完成 - 買單: {stats.PendingBuyOrderCount}筆({stats.PendingBuyQuantity}), " +
                        $"賣單: {stats.PendingSellOrderCount}筆({stats.PendingSellQuantity})");

                return ServiceResult<ContractPendingOrderStats>.Success(stats, "掛單統計完成");
            }
            catch (Exception ex)
            {
                LogError(ex, $"[掛單統計] 查詢合約 {contractCode} 掛單統計失敗");
                return ServiceResult<ContractPendingOrderStats>.Failure($"查詢掛單統計失敗: {ex.Message}");
            }
        }

        #endregion

        #region 🔧 支援類別 - 合約掛單統計

        // 合約掛單統計資訊
        public class ContractPendingOrderStats
        {
            public string ContractCode { get; set; } = string.Empty;

            // 筆數統計
            public long PendingBuyOrderCount { get; set; } = 0;     // 掛單中的買單筆數
            public long PendingSellOrderCount { get; set; } = 0;    // 掛單中的賣單筆數
            public long TotalPendingOrderCount { get; set; } = 0;   // 總掛單筆數

            // 數量統計
            public long PendingBuyQuantity { get; set; } = 0;       // 掛單中的買單總量
            public long PendingSellQuantity { get; set; } = 0;      // 掛單中的賣單總量
            public long TotalPendingQuantity { get; set; } = 0;     // 總掛單數量
            // 平均價格統計
            public double PendingBuyPrice { get; set; } = 0.0;      // 掛買單平均價格
            public double PendingSellPrice { get; set; } = 0.0;     // 掛賣單平均價格
            // 用於計算的內部欄位
            internal double TotalBuyValue { get; set; } = 0.0;   // 買單總價值（內部使用）
            internal double TotalSellValue { get; set; } = 0.0;  // 賣單總價值（內部使用）
            public DateTime UpdateTime { get; set; } = DateTime.Now;

            public override string ToString()
            {
                return $"{ContractCode} 掛單統計 - 買單: {PendingBuyOrderCount}筆({PendingBuyQuantity}), " +
                       $"賣單: {PendingSellOrderCount}筆({PendingSellQuantity}), " +
                       $"總計: {TotalPendingOrderCount}筆({TotalPendingQuantity})";
            }
        }

        #endregion

        #region 詳細掛單查詢方法

        // 🎯 重構：使用統一查詢的 GetContractDetailedPendingOrdersSync
        public static ServiceResult<Dictionary<decimal, PriceOrderDetails>> GetContractDetailedPendingOrdersSync(string contractCode, bool forceUpdateStatus = false)
        {
            try
            {
                if (string.IsNullOrEmpty(contractCode))
                {
                    return ServiceResult<Dictionary<decimal, PriceOrderDetails>>.Failure("合約代碼不可為空");
                }

                // 如果需要強制更新，則同步等待
                if (forceUpdateStatus)
                {
                    LogInfo($"[詳細掛單查詢] 🔄 強制更新狀態中...");
                    // 同步等待 UpdateStatus 完成
                    var updateTask = ShioajiService.UpdateAllAccountStatusAsync();
                    updateTask.Wait(); // 🔑 同步等待完成

                    LogInfo($"[詳細掛單查詢] ✅ 狀態更新完成");
                }

                // 使用統一查詢，但不再次更新狀態
                var unifiedResult = GetUnifiedOrderDataAsync(contractCode, actionFilter: null, forceUpdateStatus: false).Result;

                if (!unifiedResult.IsSuccess || unifiedResult.Data == null)
                {
                    return ServiceResult<Dictionary<decimal, PriceOrderDetails>>.Failure(unifiedResult.Message);
                }

                LogInfo($"[詳細掛單查詢] ✅ {contractCode} 詳細查詢完成 - 找到 {unifiedResult.Data.PriceOrderDetails.Count} 個價格的掛單");

                return ServiceResult<Dictionary<decimal, PriceOrderDetails>>.Success(unifiedResult.Data.PriceOrderDetails, "詳細掛單查詢完成");
            }
            catch (Exception ex)
            {
                LogError(ex, $"[詳細掛單查詢] 查詢合約 {contractCode} 詳細掛單失敗");
                return ServiceResult<Dictionary<decimal, PriceOrderDetails>>.Failure($"詳細掛單查詢失敗: {ex.Message}");
            }
        }

        // 價格掛單詳細資訊 - 支援類別 - 價格行掛單資訊
        public class PriceOrderDetails
        {
            public decimal Price { get; set; }
            public int BuyPendingQuantity { get; set; } = 0;    // 買單未成交數量
            public int BuyFilledQuantity { get; set; } = 0;     // 買單已成交數量
            public int SellPendingQuantity { get; set; } = 0;   // 賣單未成交數量
            public int SellFilledQuantity { get; set; } = 0;    // 賣單已成交數量

            public int BuyTotalQuantity => BuyPendingQuantity + BuyFilledQuantity;
            public int SellTotalQuantity => SellPendingQuantity + SellFilledQuantity;

            public override string ToString()
            {
                return $"價格 {Price}: 買單 {BuyPendingQuantity}({BuyTotalQuantity}), 賣單 {SellPendingQuantity}({SellTotalQuantity})";
            }
        }

        #endregion

        #region 輔助方法

        // 判斷委託是否可刪除
        private static bool IsOrderCancellable(Trade trade)
        {
            if (trade?.status?.status == null)
                return false;

            var status = trade.status.status;
            var cancellableStatuses = new[] { "PendingSubmit", "PreSubmitted", "Submitted", "PartFilled" };

            return cancellableStatuses.Contains(status);
        }

        // 取得合約代碼
        private static string GetContractCode(Trade trade)
        {
            try
            {
                var contract = trade?.contract;
                if (contract == null) return string.Empty;

                // 嘗試多種方式取得合約代碼
                if (contract is Stock stock) return stock.code ?? string.Empty;
                if (contract is Future future) return future.code ?? string.Empty;
                if (contract is Option option) return option.code ?? string.Empty;

                // 動態存取
                return contract.code?.ToString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        // 產生自訂欄位(api限制只允許輸入大小寫英文字母及數字，且長度最長為6個字元)
        public static string GenerateCustomField()
        {
            var random = new Random();
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            return new string([.. Enumerable.Range(0, 6).Select(_ => chars[random.Next(chars.Length)])]);
        }

        // 取得委託狀態文字
        public static string GetOrderStatusText(string? status)
        {
            return status switch
            {
                "Inactive" => "傳送中",
                "PendingSubmit" => "待送出",
                "PreSubmitted" => "預約單",
                "Submitted" => "已送出",
                "Failed" => "失敗",
                "Cancelled" => "已刪除",
                "Filled" => "完全成交",
                "PartFilled" => "部分成交",
                null => "未知狀態",
                _ => status
            };
        }

        #endregion
        #region 委託回報處理 - 基於合約代碼統一查找

        // 處理委託回報
        public void HandleOrderCallback(OrderState orderState, dynamic orderData)
        {
            try
            {
                // 提取關鍵資訊 - 修正解構語法錯誤
                string? seqno = null;
                string? ordno = null;
                string? customField = null;

                ExtractKeyFields(orderState, orderData, out seqno, out ordno, out customField);

                // 🚀 新架構：統一使用合約代碼查找視窗
                var targetWindowIds = FindTargetWindowsByContract(orderData);

                // 發布統計更新事件
                PublishOrderStatsUpdate(orderState, orderData, targetWindowIds, seqno, ordno, customField);

                // 觸發全域回調
                var orderDataInfo = OrderDataInfo.Create(orderState, orderData);
                StaticGlobalOrderCallback?.Invoke(orderDataInfo);
                GlobalOrderCallback?.Invoke(orderDataInfo);

                // 觸發視窗回調
                foreach (var windowId in targetWindowIds)
                {
                    StaticWindowOrderCallback?.Invoke(windowId, orderDataInfo);
                    WindowOrderCallback?.Invoke(windowId, orderDataInfo);
                }

                LogInfo($"[委託回報] 處理完成: {orderState}, ordno: {ordno}, 目標視窗: {targetWindowIds.Count}個");
            }
            catch (Exception ex)
            {
                LogError(ex, "[委託回報] 處理失敗");
            }
        }

        // 提取關鍵欄位
        private static void ExtractKeyFields(OrderState orderState, dynamic orderData,
            out string? seqno, out string? ordno, out string? customField)
        {
            seqno = null;
            ordno = null;
            customField = null;

            try
            {
                switch (orderState)
                {
                    case OrderState.StockOrder:
                    case OrderState.FuturesOrder:
                        seqno = orderData.order?.seqno?.ToString();
                        ordno = orderData.order?.ordno?.ToString();
                        customField = orderData.order?.custom_field?.ToString();
                        break;

                    case OrderState.StockDeal:
                    case OrderState.FuturesDeal:
                        seqno = orderData.seqno?.ToString();
                        ordno = orderData.ordno?.ToString();
                        customField = orderData.custom_field?.ToString();
                        break;
                }
            }
            catch (Exception ex)
            {
                LogWarning($"[提取欄位] 提取關鍵欄位失敗: {ex.Message}");
            }
        }

        // 基於合約代碼統一查找目標視窗
        private static List<string> FindTargetWindowsByContract(dynamic orderData)
        {
            var windowIds = new List<string>();

            try
            {
                var contract = orderData.contract;
                if (contract == null)
                {
                    LogWarning("[視窗查找] 合約資訊為空");
                    return windowIds;
                }

                // 合約代碼提取邏輯
                string? contractCode = ExtractContractCode(contract);

                if (string.IsNullOrEmpty(contractCode))
                {
                    LogWarning("[視窗查找] 無法提取合約代碼");
                    return windowIds;
                }

                LogInfo($"[視窗查找] 提取到合約代碼: {contractCode}");

                // 使用合約代碼查找所有相關視窗
                var foundWindowIds = MarketService.Instance.SubscriptionManager.FindAllWindowIdsByCode(contractCode);

                if (foundWindowIds.Count > 0)
                {
                    windowIds.AddRange(foundWindowIds);
                    LogInfo($"[視窗查找] 找到 {foundWindowIds.Count} 個視窗: {string.Join(", ", foundWindowIds)}");
                }
                else
                {
                    LogWarning($"[視窗查找] 找不到合約 {contractCode} 對應的視窗");
                }
            }
            catch (Exception ex)
            {
                LogWarning($"[視窗查找] 查找視窗失敗: {ex.Message}");
            }

            return windowIds;
        }

        // 合約代碼提取邏輯
        private static string? ExtractContractCode(dynamic contract)
        {
            try
            {
                // 1. 優先使用 full_code (期貨/選擇權)
                string? fullCode = contract.full_code?.ToString();
                if (!string.IsNullOrEmpty(fullCode))
                {
                    return fullCode;
                }

                // 2. 使用 code (股票/指數)
                string? code = contract.code?.ToString();
                if (!string.IsNullOrEmpty(code))
                {
                    return code;
                }

                // 3. 使用 symbol (備用)
                string? symbol = contract.symbol?.ToString();
                if (!string.IsNullOrEmpty(symbol))
                {
                    return symbol;
                }

                return null;
            }
            catch (Exception ex)
            {
                LogWarning($"[合約代碼提取] 提取失敗: {ex.Message}");
                return null;
            }
        }

        // 發布委託統計更新事件
        private static void PublishOrderStatsUpdate(OrderState orderState, dynamic orderData,
            List<string> targetWindowIds, string? seqno, string? ordno, string? customField)
        {
            try
            {
                if (OrderStatsUpdateRequested == null || targetWindowIds.Count == 0)
                    return;

                var eventArgs = OrderStatsUpdateEventArgs.CreateFromOrderData(
                    orderState, orderData, targetWindowIds, seqno, ordno, customField);

                if (eventArgs != null)
                {
                    Application.Current?.Dispatcher?.BeginInvoke(() =>
                    {
                        try
                        {
                            OrderStatsUpdateRequested.Invoke(eventArgs);
                        }
                        catch (Exception ex)
                        {
                            LogWarning($"[統計更新] 事件處理失敗: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                LogError(ex, "[統計更新] 發布事件失敗");
            }
        }

        #endregion

        #region 🔧 支援類別

        public class CancelResult
        {
            public string OrderId { get; set; } = string.Empty;
            public string OrderNumber { get; set; } = string.Empty;
            public string ContractCode { get; set; } = string.Empty;
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        public class BatchCancelResult
        {
            public int TotalCount { get; set; }
            public int SuccessCount { get; set; }
            public int FailCount { get; set; }
            public List<CancelResult> Results { get; set; } = [];
        }

        #endregion

        #region 🔧 日誌與清理

        private static void LogInfo(string message) => LogService.Instance?.LogInfo(message, "OrderService", LogDisplayTarget.MainWindow);
        private static void LogWarning(string message) => LogService.Instance?.LogWarning(message, "OrderService", LogDisplayTarget.MainWindow);
        private static void LogError(Exception ex, string message) => LogService.Instance?.LogError(ex, message, "OrderService", LogDisplayTarget.MainWindow);

        // 清理靜態資源
        public static void ClearStaticTracking()
        {
            _customFieldToWindowId.Clear();
            LogInfo("[靜態清理] 已清理追蹤字典");
        }

        // 重置服務實例
        internal static void ResetInstance()
        {
            lock (_lockInstance)
            {
                _instance = null;
                ClearStaticTracking();
                LogInfo("[服務重置] OrderService 已重置");
            }
        }

        #endregion
    }
}