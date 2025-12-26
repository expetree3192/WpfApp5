using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Sinopac.Shioaji;
using WpfApp5.Services.Common;

namespace WpfApp5.Services
{
    // Shioaji 服務 - 配合重構後的 OrderService
    public partial class ShioajiService : IDisposable
    {
        #region 單例模式

        private static ShioajiService? _instance;
        private static readonly object _lockInstance = new();

        public static ShioajiService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockInstance)
                    {
                        _instance ??= new ShioajiService();
                    }
                }
                return _instance;
            }
        }

        private ShioajiService()
        {
            InitializeCore();
        }

        #endregion

        #region 核心屬性與實例

        private Shioaji? _api;
        private List<Account> _accounts = [];
        private bool _isLoggedIn = false;
        private bool _isConnected = false;
        private readonly object _lockCore = new();
        private readonly LogService _logService = LogService.Instance;

        // API 實例
        public Shioaji? Api => _api;

        // 實例屬性
        public bool IsLoggedIn => _isLoggedIn;
        public bool IsConnected => _isConnected;

        // 帳戶清單
        public List<Account> Accounts => [.. _accounts];

        #endregion

        #region 全域靜態 API 存取器

        // 全域 API 實例
        public static Shioaji? API => Instance._api;

        // 全域登入狀態
        public static bool IsApiLoggedIn => Instance._isLoggedIn;

        // 全域股票帳戶 - 直接從 API 取得
        public static Account? StockAccount => Instance._api?.StockAccount;

        // 全域期貨帳戶 - 直接從 API 取得
        public static Account? FutureAccount => Instance._api?.FutureAccount;

        // 全域合約 - 直接從 API 取得
        public static Contracts? Contracts => Instance._api?.Contracts;

        // 取得 API 實例 - 供 OrderService 使用
        public static Shioaji GetApi()
        {
            return EnsureApiAvailable();
        }

        #endregion

        #region 私有輔助方法

        // 確保 API 可用
        private static Shioaji EnsureApiAvailable()
        {
            var api = Instance._api;
            if (api == null)
            {
                var error = "API 尚未初始化，請先登入";
                Instance._logService.LogError(error, "ShioajiService", LogDisplayTarget.DebugOutput | LogDisplayTarget.MainWindow);
                throw new InvalidOperationException(error);
            }
            return api;
        }

        // 安全執行 API 操作
        private static T SafeExecute<T>(Func<Shioaji, T> operation, string operationName)
        {
            try
            {
                var api = EnsureApiAvailable();
                return operation(api);
            }
            catch (Exception ex)
            {
                Instance._logService.LogError(ex, $"{operationName} 失敗", "ShioajiService", LogDisplayTarget.DebugOutput | LogDisplayTarget.MainWindow);
                throw;
            }
        }

        // 安全執行 API 操作 (無返回值) - 簡化版本
        private static void SafeExecute(Action<Shioaji> operation, string operationName)
        {
            try
            {
                var api = EnsureApiAvailable();
                operation(api);
            }
            catch (Exception ex)
            {
                Instance._logService.LogError(ex, $"{operationName} 失敗", "ShioajiService", LogDisplayTarget.DebugOutput | LogDisplayTarget.MainWindow);
                throw;
            }
        }

        #endregion

        #region 全域靜態交易方法

        // 全域下單
        public static Trade PlaceOrder(IContract contract, IOrder order, int timeout = 5000, Action<Trade>? cb = null)
        {
            return SafeExecute(api => api.PlaceOrder(contract, order, timeout, cb), "PlaceOrder");
        }

        // 全域取消訂單 - 預設超時時間為 5000 ms
        public static Trade CancelOrder(Trade trade, int timeout = 5000, Action<Trade>? cb = null)
        {
            return SafeExecute(api => api.CancelOrder(trade, timeout, cb), "CancelOrder");
        }

        // 全域修改訂單 - 預設超時時間為 5000 ms
        public static Trade UpdateOrder(Trade trade, double price = 0.0, int quantity = 0, int timeout = 5000, Action<Trade>? cb = null)
        {
            return SafeExecute(api => api.UpdateOrder(trade, price, quantity, timeout, cb), "UpdateOrder");
        }

        // 全域更新狀態
        public static void UpdateStatus(Account? account = null, Trade? trade = null, int timeout = 5000, Action<Trade>? cb = null)
        {
            SafeExecute(api => api.UpdateStatus(account, trade, timeout, cb), "UpdateStatus");
        }

        // 更新特定帳戶狀態
        public static void UpdateAccountStatus(Account account, int timeout = 5000, Action<Trade>? cb = null)
        {
            SafeExecute(api => api.UpdateStatus(account: account, trade: null, timeout: timeout, cb: cb), "UpdateAccountStatus");
        }

        // 更新所有帳戶狀態
        public static async Task UpdateAllAccountStatusAsync()
        {
            var api = EnsureApiAvailable();
            var tasks = new List<Task>();

            Instance._logService.LogInfo("🔄 開始更新所有帳戶狀態...", "ShioajiService");

            if (api.StockAccount?.signed == true)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        var startTime = DateTime.Now;
                        api.UpdateStatus(api.StockAccount, timeout: 5000);
                        var elapsed = DateTime.Now - startTime;
                        Instance._logService.LogInfo($"✅ 股票帳戶狀態更新完成 (耗時: {elapsed.TotalMilliseconds}ms)", "ShioajiService");
                    }
                    catch (Exception ex)
                    {
                        Instance._logService.LogWarning($"❌ 更新股票帳戶狀態失敗: {ex.Message}", "ShioajiService");
                        throw; // 重新拋出異常，讓 Task.WhenAll 能捕獲
                    }
                }));
            }

            if (api.FutureAccount?.signed == true)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        var startTime = DateTime.Now;
                        api.UpdateStatus(api.FutureAccount, timeout: 5000);
                        var elapsed = DateTime.Now - startTime;
                        Instance._logService.LogInfo($"✅ 期貨帳戶狀態更新完成 (耗時: {elapsed.TotalMilliseconds}ms)", "ShioajiService");
                    }
                    catch (Exception ex)
                    {
                        Instance._logService.LogWarning($"❌ 更新期貨帳戶狀態失敗: {ex.Message}", "ShioajiService");
                        throw; // 重新拋出異常，讓 Task.WhenAll 能捕獲
                    }
                }));
            }

            try
            {
                var overallStartTime = DateTime.Now;
                await Task.WhenAll(tasks).ConfigureAwait(false);
                var overallElapsed = DateTime.Now - overallStartTime;
                Instance._logService.LogInfo($"🎉 所有帳戶狀態更新完成 (總耗時: {overallElapsed.TotalMilliseconds}ms)", "ShioajiService");
            }
            catch (Exception ex)
            {
                Instance._logService.LogError(ex, "❌ 更新帳戶狀態部分失敗", "ShioajiService");
                throw; // 重新拋出異常，讓調用方知道更新失敗
            }
        }

        // 更新特定交易狀態
        public static void UpdateTradeStatus(Trade trade, int timeout = 5000, Action<Trade>? cb = null)
        {
            SafeExecute(api => api.UpdateStatus(account: null, trade: trade, timeout: timeout, cb: cb), "UpdateTradeStatus");
        }

        // 全域取得交易清單
        public static SJList ListTrades()
        {
            return SafeExecute(api => api.ListTrades(), "ListTrades");
        }

        #endregion

        #region 全域帳戶-靜態方法

        public static SJList ListAccounts()
        {
            return SafeExecute(api => api.ListAccounts(), "ListAccounts");
        }

        public static AccountBalance AccountBalance()
        {
            return SafeExecute(api => api.AccountBalance(), "取得帳戶餘額");
        }

        public static dynamic ListStockPositions(Unit unit = Unit.Common)
        {
            return SafeExecute(api => api.ListPositions(account: api.StockAccount, unit: unit), "ListStockPositions");
        }

        public static dynamic ListFuturePositions(Unit unit = Unit.Common)
        {
            return SafeExecute(api => api.ListPositions(account: api.FutureAccount, unit: unit), "ListFuturePositions");
        }

        public static dynamic ListPositions(Account account, Unit unit = Unit.Common)
        {
            return SafeExecute(api => api.ListPositions(account: account, unit: unit), "ListPositions");
        }

        public static dynamic ListPositions(Unit unit = Unit.Common)
        {
            return SafeExecute(api => api.ListPositions(unit: unit), "ListPositions");
        }

        public static dynamic ListPositionDetail(Account? account = null, int detail_id = 0)
        {
            return SafeExecute(api => api.ListPositionDetail(account, detail_id), "ListPositionDetail");
        }

        public static dynamic ListProfitLoss(string begin_date, string end_date, Account? account = null, Unit unit = Unit.Common)
        {
            return SafeExecute(api => api.ListProfitLoss(begin_date, end_date, account, unit), "ListProfitLoss");
        }

        public static dynamic ListProfitLossDetail(Account? account = null, int detail_id = 0, Unit unit = Unit.Common)
        {
            return SafeExecute(api => api.ListProfitLossDetail(account, detail_id, unit), "ListProfitLossDetail");
        }

        public static ProfitLossSummaryTotal ListProfitLossSummary(string begin_date, string end_date, Account? account = null)
        {
            return SafeExecute(api => api.ListProfitLossSummary(begin_date, end_date, account), "ProfitLossSummaryTotal");
        }

        #endregion


        #region 全域靜態行情方法

        public static Ticks Ticks(IContract contract, string date = "", TicksQueryType query_type = TicksQueryType.AllDay,
            string time_start = "", string time_end = "", int last_cnt = 0)
        {
            return SafeExecute(api => api.Ticks(contract, date, query_type, time_start, time_end, last_cnt), "取得 Tick 資料");
        }

        public static Kbars Kbars(IContract contract, string start, string end)
        {
            return SafeExecute(api => api.Kbars(contract, start, end), "取得 K 線資料");
        }

        public static SJList Snapshots(List<IContract> contracts)
        {
            return SafeExecute(api => api.Snapshots(contracts), "Snapshots");
        }

        public static DailyQuotes DailyQuotes(string date = "", bool exclude = true)
        {
            return SafeExecute(api => api.DailyQuotes(date, exclude), "DailyQuotes");
        }

        #endregion

        #region 全域靜態訂閱方法

        public static void Subscribe(IContract contract, QuoteType quoteType = QuoteType.tick, bool intradayOdd = false, QuoteVersion version = QuoteVersion.v1)
        {
            SafeExecute(api => api.Subscribe(contract, quoteType, intradayOdd, version), "Subscribe");
        }

        public static void UnSubscribe(IContract contract, QuoteType quoteType = QuoteType.tick, bool intradayOdd = false, QuoteVersion version = QuoteVersion.v1)
        {
            SafeExecute(api => api.UnSubscribe(contract, quoteType, intradayOdd, version), "UnSubscribe");
        }

        public static void SetQuoteCallback_v1(Action<Exchange, dynamic> callBack)
        {
            SafeExecute(api => api.SetQuoteCallback_v1(callBack), "設定報價回調");
        }

        public static void SetEventCallback(Action<int, int, string, string> callBack)
        {
            SafeExecute(api => api.SetEventCallback(callBack), "設定事件回調");
        }

        public static void SetOrderCallback(Action<OrderState, dynamic> callBack)
        {
            SafeExecute(api => api.SetOrderCallback(callBack), "設定訂單回調");
        }

        #endregion

        #region 全域靜態其他方法

        public static bool LogoutApi()
        {
            return SafeExecute(api => api.Logout(), "登出");
        }

        public static UsageStatus Usage()
        {
            return SafeExecute(api => api.Usage(), "取得使用狀態");
        }

        public static SJList Scanners(ScannerType scannerType, bool ascending = true, string date = "", int count = 100)
        {
            return SafeExecute(api => api.Scanners(scannerType, ascending, date, count), "取得掃描器資料");
        }

        public static SJList Settlements()
        {
            return SafeExecute(api => api.Settlements(), "取得結算資料");
        }

        public static Margin Margin()
        {
            return SafeExecute(api => api.Margin(), "取得保證金");
        }

        public static SJList CreditEnquire(List<Stock> contracts)
        {
            return SafeExecute(api => api.CreditEnquire(contracts), "信用查詢");
        }

        public static SJList ShortStockSources(List<Stock> contracts)
        {
            return SafeExecute(api => api.ShortStockSources(contracts), "券源查詢");
        }

        #endregion

        #region 核心初始化與連線管理

        private void InitializeCore()
        {
            _logService.LogInfo("ShioajiService 初始化開始", "ShioajiService", LogDisplayTarget.DebugOutput | LogDisplayTarget.MainWindow);
            _accounts = [];
            _isLoggedIn = false;
            _isConnected = false;
            _logService.LogInfo("ShioajiService 初始化完成", "ShioajiService", LogDisplayTarget.DebugOutput | LogDisplayTarget.MainWindow);
        }

        // 設定 API 實例
        public ServiceResult SetApi(Shioaji api, ObservableCollection<Account>? accounts = null)
        {
            try
            {
                lock (_lockCore)
                {
                    _api = api ?? throw new ArgumentNullException(nameof(api));
                    _accounts = accounts?.ToList() ?? [];
                    _isLoggedIn = true;
                    _isConnected = true;

                    // 清除快取，強制重新載入
                    ClearAllCache();

                    _logService.LogInfo($"API 設定成功，帳戶數量: {_accounts.Count}", "ShioajiService", LogDisplayTarget.DebugOutput | LogDisplayTarget.MainWindow);

                    // 記錄帳戶資訊
                    var stockAccount = api.StockAccount;
                    if (stockAccount?.account_id != null)
                    {
                        _logService.LogInfo($"股票帳戶: {stockAccount.account_id}", "ShioajiService", LogDisplayTarget.DebugOutput | LogDisplayTarget.MainWindow);
                    }

                    var futureAccount = api.FutureAccount;
                    if (futureAccount?.account_id != null)
                    {
                        _logService.LogInfo($"期貨帳戶: {futureAccount.account_id}", "ShioajiService", LogDisplayTarget.DebugOutput | LogDisplayTarget.MainWindow);
                    }
                }

                return ServiceResult.Success("API 設定成功");
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "設定 API 失敗", "ShioajiService", LogDisplayTarget.DebugOutput | LogDisplayTarget.MainWindow);
                return ServiceResult.Failure($"設定 API 失敗: {ex.Message}");
            }
        }

        // 清理資源
        public ServiceResult Clear()
        {
            try
            {
                lock (_lockCore)
                {
                    _logService.LogInfo("開始清理 ShioajiService 資源", "ShioajiService", LogDisplayTarget.DebugOutput | LogDisplayTarget.MainWindow);

                    _api = null;
                    _accounts.Clear();
                    _isLoggedIn = false;
                    _isConnected = false;

                    ClearAllCache();    // 清除所有快取
                    _logService.LogInfo("ShioajiService 資源清理完成", "ShioajiService", LogDisplayTarget.DebugOutput | LogDisplayTarget.MainWindow);
                }

                return ServiceResult.Success("資源清理完成");
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "清理資源失敗", "ShioajiService", LogDisplayTarget.DebugOutput | LogDisplayTarget.MainWindow);
                return ServiceResult.Failure($"清理資源失敗: {ex.Message}");
            }
        }

        #endregion

        #region 快取管理

        partial void ClearContractsCache();
        partial void ClearQuoteCache();
        partial void ClearTradingCache();

        private void ClearAllCache()
        {
            _logService.LogDebug("清除所有快取", "ShioajiService", LogDisplayTarget.DebugOutput);
            ClearContractsCache();
            ClearQuoteCache();
            ClearTradingCache();
        }

        #endregion

        #region 相容性支援

        public static SubscriptionStats GetSubscriptionStats()
        {
            return new SubscriptionStats();
        }

        #endregion

        #region 錯誤處理輔助方法

        private ServiceResult<T> HandleApiError<T>(Exception ex, string operation)
        {
            _logService.LogError(ex, $"{operation}失敗", "ShioajiService", LogDisplayTarget.DebugOutput | LogDisplayTarget.MainWindow);
            return ServiceResult<T>.Failure($"HandleApiError<T>_{operation}失敗: {ex.Message}");
        }

        private ServiceResult HandleApiError(Exception ex, string operation)
        {
            _logService.LogError(ex, $"{operation}失敗", "ShioajiService", LogDisplayTarget.DebugOutput | LogDisplayTarget.MainWindow);
            return ServiceResult.Failure($"HandleApiError_{operation}失敗: {ex.Message}");
        }

        #endregion

        #region 資源釋放

        private bool _disposed = false;

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
                    Clear();
                }
                _disposed = true;
            }
        }

        ~ShioajiService()
        {
            Dispose(false);
        }

        #endregion
    }
}