// ShioajiService.Login.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sinopac.Shioaji;
using WpfApp5.Services.Common;

namespace WpfApp5.Services
{
    // Shioaji 服務 - 登入模組
    public partial class ShioajiService
    {
        #region 登入參數
        // 登入參數
        public class LoginParameters
        {
            public string ApiKey { get; set; } = string.Empty;
            public string SecretKey { get; set; } = string.Empty;
            public string CertPath { get; set; } = string.Empty;
            public string CertPassword { get; set; } = string.Empty;
            public string PersonId { get; set; } = string.Empty;
            public bool SimulationMode { get; set; } = false;
            public int ReceiveWindow { get; set; } = 50000;
        }

        // 登入結果
        public class LoginResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = string.Empty;
            public List<Account> Accounts { get; set; } = [];
            public string EnvironmentName { get; set; } = string.Empty;
        }
        #endregion

        #region 登入方法

        // 登入 API (非同步)
        public async Task<ServiceResult<LoginResult>> LoginAsync(LoginParameters parameters)
        {
            return await Task.Run(() => Login(parameters));
        }

        // 登入 API (同步)
        public ServiceResult<LoginResult> Login(LoginParameters parameters)
        {
            try
            {
                lock (_lockCore)
                {
                    // 檢查是否已登入
                    if (_isLoggedIn)
                    {
                        return ServiceResult<LoginResult>.Failure("已經登入，請先登出");
                    }

                    // 驗證參數
                    var validationResult = ValidateLoginParameters(parameters);
                    if (!validationResult.IsSuccess)
                    {
                        return ServiceResult<LoginResult>.Failure(validationResult.Message);
                    }

                    // 建立 Shioaji 實例
                    _api = new Shioaji(simulation: parameters.SimulationMode);

                    // 執行登入
                    var loginResponse = _api.Login(parameters.ApiKey, parameters.SecretKey, parameters.ReceiveWindow);

                    if (loginResponse == null)
                    {
                        return ServiceResult<LoginResult>.Failure("登入失敗，未收到回應");
                    }

                    LogService.Instance.LogInfo("🎉 登入成功！", "ShioajiService", LogDisplayTarget.MainWindow);

                    // 啟用憑證 (只在非模擬環境下)
                    if (!parameters.SimulationMode)
                    {
                        var caResult = ActivateCertificate(parameters.CertPath, parameters.CertPassword, parameters.PersonId);
                        if (!caResult.IsSuccess)
                        {
                            // 憑證啟用失敗，登出並返回錯誤
                            _api.Logout();
                            _api = null;
                            return ServiceResult<LoginResult>.Failure(caResult.Message);
                        }
                    }

                    // 設定委託及成交回報 (在登入後設置)
                    _api.SetOrderCallback((orderState, orderData) =>
                    {
                        OrderService.Instance.HandleOrderCallback(orderState, orderData);   // 委託給 OrderService 處理
                    });

                    // 設定事件回調
                    _api.SetEventCallback((respCode, eventCode, eventInfo, eventStatus) =>
                    {
                        HandleEventCallback(respCode, eventCode, eventInfo, eventStatus);
                    });

                    LogService.Instance.LogInfo("🔗 已設置SetOrderCallback & SetEventCallback", "ShioajiService", LogDisplayTarget.MainWindow);

                    // 收集帳戶資訊
                    _accounts.Clear();
                    if (_api.StockAccount != null)
                    {
                        _accounts.Add(_api.StockAccount);
                    }
                    if (_api.FutureAccount != null)
                    {
                        _accounts.Add(_api.FutureAccount);
                    }

                    // 更新狀態
                    _isLoggedIn = true;
                    _isConnected = true;

                    LogService.Instance.LogInfo($"✅ 登入完成，可用帳戶數：{_accounts.Count}", "ShioajiService", LogDisplayTarget.MainWindow);

                    // 🚀 新增：登入完成後立即執行 UpdateAllAccountStatusAsync 並等待完成
                    try
                    {
                        LogService.Instance.LogInfo("🔄 登入完成後開始初始化帳戶狀態...", "ShioajiService", LogDisplayTarget.MainWindow);

                        var updateStartTime = DateTime.Now;

                        // 🔑 同步等待 UpdateAllAccountStatusAsync 完成
                        var updateTask = UpdateAllAccountStatusAsync();
                        updateTask.Wait(); // 同步等待完成

                        var updateElapsed = DateTime.Now - updateStartTime;
                        LogService.Instance.LogInfo($"✅ 登入後帳戶狀態初始化完成 (耗時: {updateElapsed.TotalMilliseconds:F0}ms)", "ShioajiService", LogDisplayTarget.MainWindow);
                    }
                    catch (Exception updateEx)
                    {
                        // 🔍 記錄警告但不影響登入結果，因為登入本身已經成功
                        LogService.Instance.LogWarning($"⚠️ 登入後帳戶狀態初始化失敗: {updateEx.Message}", "ShioajiService", LogDisplayTarget.MainWindow);
                        LogService.Instance.LogWarning("💡 這不會影響登入狀態，但可能需要手動重新整理帳戶資訊", "ShioajiService", LogDisplayTarget.MainWindow);
                    }

                    // 建立登入結果
                    var result = new LoginResult
                    {
                        Success = true,
                        Message = "登入成功",
                        Accounts = [.. _accounts],
                        EnvironmentName = parameters.SimulationMode ? "模擬環境" : "正式環境"
                    };

                    return ServiceResult<LoginResult>.Success(result, "登入成功");
                }
            }
            catch (Exception ex)
            {
                return HandleApiError<LoginResult>(ex, "登入");
            }
        }

        // 驗證登入參數
        private static ServiceResult ValidateLoginParameters(LoginParameters parameters)
        {
            if (string.IsNullOrEmpty(parameters.ApiKey))
            {
                return ServiceResult.Failure("API Key 不可為空");
            }

            if (string.IsNullOrEmpty(parameters.SecretKey))
            {
                return ServiceResult.Failure("Secret Key 不可為空");
            }

            if (!parameters.SimulationMode)
            {
                if (string.IsNullOrEmpty(parameters.CertPath))
                {
                    return ServiceResult.Failure("憑證路徑不可為空");
                }

                if (string.IsNullOrEmpty(parameters.CertPassword))
                {
                    return ServiceResult.Failure("憑證密碼不可為空");
                }

                if (string.IsNullOrEmpty(parameters.PersonId))
                {
                    return ServiceResult.Failure("身分證字號不可為空");
                }
            }

            return ServiceResult.Success("參數驗證通過");
        }

        #endregion

        #region 憑證管理

        // 啟用憑證
        private ServiceResult ActivateCertificate(string certPath, string certPassword, string personId)
        {
            try
            {
                if (_api == null)
                {
                    return ServiceResult.Failure("API 尚未初始化");
                }

                LogService.Instance.LogInfo("🔐 正在啟用憑證...", "ShioajiService", LogDisplayTarget.MainWindow);

                bool caActivated = _api.ca_activate(certPath, certPassword, personId);

                if (!caActivated)
                {
                    return ServiceResult.Failure("CA憑證啟用失敗");
                }

                LogService.Instance.LogInfo("🔐 CA憑證啟用成功", "ShioajiService", LogDisplayTarget.MainWindow);
                return ServiceResult.Success("憑證啟用成功");
            }
            catch (Exception ex)
            {
                // 檢查是否為重複啟用
                if (ex.Message.Contains("same key has already been added"))
                {
                    LogService.Instance.LogInfo("🔐 CA憑證已啟用 (重複啟用)", "ShioajiService", LogDisplayTarget.MainWindow);
                    return ServiceResult.Success("憑證已啟用");
                }

                return HandleApiError(ex, "憑證啟用");
            }
        }

        #endregion

        #region 登出方法

        // 登出 API
        public ServiceResult Logout()
        {
            try
            {
                lock (_lockCore)
                {
                    if (!_isLoggedIn || _api == null)
                    {
                        return ServiceResult.Failure("尚未登入");
                    }

                    LogService.Instance.LogInfo("🔄 執行登出", "ShioajiService", LogDisplayTarget.MainWindow);

                    // 執行登出
                    bool logoutResult = _api.Logout();

                    if (!logoutResult)
                    {
                        LogService.Instance.LogWarning("⚠️ 登出回應為 false", "ShioajiService", LogDisplayTarget.MainWindow);
                    }

                    // 清理資源
                    _api.Dispose();
                    _api = null;
                    _accounts.Clear();
                    _isLoggedIn = false;
                    _isConnected = false;

                    // 清除所有快取
                    ClearAllCache();

                    LogService.Instance.LogInfo("✅ 登出成功", "ShioajiService", LogDisplayTarget.MainWindow);

                    return ServiceResult.Success("登出成功");
                }
            }
            catch (Exception ex)
            {
                // 即使發生錯誤也要清理資源
                try
                {
                    _api?.Dispose();
                    _api = null;
                    _accounts.Clear();
                    _isLoggedIn = false;
                    _isConnected = false;
                    ClearAllCache();
                }
                catch
                {
                    // 忽略清理過程中的錯誤
                }

                return HandleApiError(ex, "登出");
            }
        }

        #endregion

        #region 連線狀態管理

        // 取得連線狀態
        public ServiceResult<string> GetConnectionStatus()
        {
            try
            {
                lock (_lockCore)
                {
                    if (!_isConnected)
                    {
                        return ServiceResult<string>.Success("未連線", "API 尚未連線");
                    }

                    if (!_isLoggedIn)
                    {
                        return ServiceResult<string>.Success("已連線但未登入", "API 已連線但尚未登入");
                    }

                    var accountCount = _accounts.Count;
                    var stockAccount = _api?.StockAccount != null ? "有" : "無";
                    var futureAccount = _api?.FutureAccount != null ? "有" : "無";
                    var status = $"已登入 (帳戶數: {accountCount}, 股票帳戶: {stockAccount}, 期貨帳戶: {futureAccount})";

                    return ServiceResult<string>.Success(status, "連線狀態正常");
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "取得連線狀態失敗", "ShioajiService", LogDisplayTarget.DebugOutput | LogDisplayTarget.MainWindow);
                return ServiceResult<string>.Failure($"取得連線狀態失敗: {ex.Message}");
            }
        }

        #endregion

        #region 帳戶管理

        // 取得帳戶清單 (相容性方法)
        public List<Account> GetAccounts()
        {
            lock (_lockCore)
            {
                return [.. _accounts];
            }
        }

        // 取得指定帳戶
        public ServiceResult<Account> GetAccount(string accountId)
        {
            try
            {
                lock (_lockCore)
                {
                    var account = _accounts.FirstOrDefault(a => a.account_id == accountId);
                    if (account == null)
                    {
                        return ServiceResult<Account>.Failure($"找不到帳戶: {accountId}");
                    }
                    return ServiceResult<Account>.Success(account, $"找到帳戶: {accountId}");
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"取得帳戶失敗: {accountId}", "ShioajiService", LogDisplayTarget.DebugOutput | LogDisplayTarget.MainWindow);
                return ServiceResult<Account>.Failure($"取得帳戶失敗: {ex.Message}");
            }
        }

        // 取得股票帳戶
        public ServiceResult<Account> GetStockAccount()
        {
            try
            {
                lock (_lockCore)
                {
                    var stockAccount = _api?.StockAccount;
                    if (stockAccount == null)
                    {
                        return ServiceResult<Account>.Failure("股票帳戶不可用");
                    }
                    return ServiceResult<Account>.Success(stockAccount, "取得股票帳戶成功");
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "取得股票帳戶失敗", "ShioajiService", LogDisplayTarget.DebugOutput | LogDisplayTarget.MainWindow);
                return ServiceResult<Account>.Failure($"取得股票帳戶失敗: {ex.Message}");
            }
        }

        // 取得期貨帳戶
        public ServiceResult<Account> GetFutureAccount()
        {
            try
            {
                lock (_lockCore)
                {
                    var futureAccount = _api?.FutureAccount;
                    if (futureAccount == null)
                    {
                        return ServiceResult<Account>.Failure("期貨帳戶不可用");
                    }
                    return ServiceResult<Account>.Success(futureAccount, "取得期貨帳戶成功");
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "取得期貨帳戶失敗", "ShioajiService", LogDisplayTarget.DebugOutput | LogDisplayTarget.MainWindow);
                return ServiceResult<Account>.Failure($"取得期貨帳戶失敗: {ex.Message}");
            }
        }

        // 檢查帳戶可用性
        public ServiceResult<bool> IsAccountAvailable(string accountId)
        {
            try
            {
                lock (_lockCore)
                {
                    var account = _accounts.FirstOrDefault(a => a.account_id == accountId);
                    bool isAvailable = account != null;

                    string message = isAvailable ? $"帳戶 {accountId} 可用" : $"帳戶 {accountId} 不可用";

                    return ServiceResult<bool>.Success(isAvailable, message);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, $"檢查帳戶可用性失敗: {accountId}", "ShioajiService", LogDisplayTarget.DebugOutput | LogDisplayTarget.MainWindow);
                return ServiceResult<bool>.Failure($"檢查帳戶可用性失敗: {ex.Message}");
            }
        }

        // 取得帳戶統計資訊
        public ServiceResult<string> GetAccountStats()
        {
            try
            {
                lock (_lockCore)
                {
                    if (!_isLoggedIn)
                    {
                        return ServiceResult<string>.Success("未登入", "尚未登入");
                    }

                    var totalAccounts = _accounts.Count;
                    var stockAccountCount = _api?.StockAccount != null ? 1 : 0;
                    var futureAccountCount = _api?.FutureAccount != null ? 1 : 0;

                    var stats = $"總帳戶數: {totalAccounts} (股票: {stockAccountCount}, 期貨: {futureAccountCount})";

                    return ServiceResult<string>.Success(stats, "取得帳戶統計成功");
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "取得帳戶統計失敗", "ShioajiService", LogDisplayTarget.DebugOutput | LogDisplayTarget.MainWindow);
                return ServiceResult<string>.Failure($"取得帳戶統計失敗: {ex.Message}");
            }
        }

        #endregion

        #region 事件回調處理

        // 處理事件回調
        private void HandleEventCallback(int respCode, int eventCode, string eventInfo, string eventStatus)
        {
            try
            {
                LogService.Instance.LogInfo($"[事件] Code: {eventCode} | Info: {eventInfo} | Status: {eventStatus}", "ShioajiService", LogDisplayTarget.MainWindow);

                // 可以根據 eventCode 進行不同的處理
                // 參考官方文件的事件代碼表
            }
            catch (Exception ex)
            {
                LogService.Instance.LogError(ex, "[事件] 處理事件回調失敗", "ShioajiService", LogDisplayTarget.MainWindow);
            }
        }

        #endregion
    }
}