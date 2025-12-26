using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Sinopac.Shioaji;
using WpfApp5.Services;
using WpfApp5.Services.Common;
using WpfApp5.Views;

namespace WpfApp5.ViewModels
{
    /// <summary>
    /// MainWindow 的 ViewModel - 負責主視窗的所有業務邏輯
    /// </summary>
    public partial class MainWindowViewModel : ObservableObject, IDisposable
    {
        #region 私有欄位與服務

        private readonly LogService _logService;
        private readonly ShioajiService _shioajiService;
        private readonly WindowManagerService _windowManagerService;
        private readonly MarketService _marketService;
        private bool _disposed = false;

        #endregion

        #region 可觀察屬性

        [ObservableProperty]
        private string _apiKey = "";

        [ObservableProperty]
        private string _secretKey = "";

        [ObservableProperty]
        private string _certPath = "";

        [ObservableProperty]
        private string _certPassword = "";

        [ObservableProperty]
        private string _personId = "";

        [ObservableProperty]
        private bool _simulationMode = false;

        [ObservableProperty]
        private bool _isLoggedIn = false;

        [ObservableProperty]
        private string _connectionStatus = "未連線";

        [ObservableProperty]
        private string _loginButtonText = "登入";

        [ObservableProperty]
        private bool _isLoginInProgress = false;

        [ObservableProperty]
        private ObservableCollection<AccountInfo> _accounts = [];

        [ObservableProperty]
        private AccountInfo? _selectedAccount;

        [ObservableProperty]
        private string _systemLogs = "";

        [ObservableProperty]
        private bool _autoScrollLogs = true;

        [ObservableProperty]
        private string _windowStats = "";

        #endregion

        #region 建構函數

        public MainWindowViewModel()
        {
            // 初始化服務
            _logService = LogService.Instance;
            _shioajiService = ShioajiService.Instance;
            _windowManagerService = WindowManagerService.Instance;
            _marketService = MarketService.Instance;

            // 載入預設設定
            LoadDefaultSettings();

            // 訂閱服務事件
            SubscribeToServiceEvents();

            // 初始化日誌顯示
            InitializeLogDisplay();

            // 初始化狀態
            UpdateConnectionStatus();
            UpdateWindowStats();

            _logService.LogInfo("MainWindowViewModel 已初始化", "MainWindowViewModel", LogDisplayTarget.MainWindow);

            // 如果設定了自動登入，則執行自動登入
            if (ShouldAutoLogin())
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000); // 延遲1秒確保UI完全載入
                    await AutoLoginAsync();
                });
            }
        }

        #endregion

        #region 初始化方法

        private void LoadDefaultSettings()
        {
            try
            {
                ApiKey = ConfigurationManager.AppSettings["DefaultAPIKey"] ?? "";
                SecretKey = ConfigurationManager.AppSettings["DefaultSecretKey"] ?? "";
                CertPath = ConfigurationManager.AppSettings["DefaultCertPath"] ?? "";
                CertPassword = ConfigurationManager.AppSettings["DefaultCertPassword"] ?? "";
                PersonId = ConfigurationManager.AppSettings["DefaultPersonID"] ?? "";
                SimulationMode = bool.Parse(ConfigurationManager.AppSettings["SimulationMode"] ?? "false");

                _logService.LogInfo("已載入預設登入設定", "MainWindowViewModel", LogDisplayTarget.MainWindow);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "載入預設設定失敗", "MainWindowViewModel", LogDisplayTarget.MainWindow);
            }
        }

        private void SubscribeToServiceEvents()
        {
            // 訂閱日誌服務的屬性變更事件
            _logService.PropertyChanged += OnLogServicePropertyChanged;

            _logService.LogDebug("已訂閱服務事件", "MainWindowViewModel", LogDisplayTarget.MainWindow);
        }

        private void InitializeLogDisplay()
        {
            // 綁定到 LogService 的 MainWindowLogsText
            SystemLogs = _logService.MainWindowLogsText;
        }

        private bool ShouldAutoLogin()
        {
            try
            {
                string autoLoginSetting = ConfigurationManager.AppSettings["AutoLogin"] ?? "false";
                bool shouldAutoLogin = bool.Parse(autoLoginSetting);

                if (shouldAutoLogin)
                {
                    _logService.LogInfo("檢測到自動登入設定，將執行自動登入", "MainWindowViewModel", LogDisplayTarget.MainWindow);
                }

                return shouldAutoLogin;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region 登入/登出命令

        // 登入/登出命令 - 根據當前狀態決定執行登入還是登出
        [RelayCommand]
        private async Task ToggleLoginAsync()
        {
            if (IsLoggedIn)
            {
                await LogoutAsync();    // 當前已登入，執行登出
            }
            else
            {
                await LoginAsync(); // 當前未登入，執行登入
            }
        }

        // 登入命令 - 現在改為私有方法，只能透過 ToggleLoginAsync 調用
        private async Task LoginAsync()
        {
            if (IsLoginInProgress) return;

            try
            {
                IsLoginInProgress = true;
                LoginButtonText = "登入中...";

                _logService.LogInfo("開始執行登入程序", "MainWindowViewModel", LogDisplayTarget.MainWindow);

                // 驗證輸入
                if (!ValidateLoginInputs())
                {
                    return;
                }

                // 準備登入參數
                var loginParams = new ShioajiService.LoginParameters
                {
                    ApiKey = ApiKey,
                    SecretKey = SecretKey,
                    CertPath = CertPath,
                    CertPassword = CertPassword,
                    PersonId = PersonId,
                    SimulationMode = SimulationMode,
                    ReceiveWindow = int.Parse(ConfigurationManager.AppSettings["ReceiveWindow"] ?? "50000")
                };

                // 執行登入
                var result = await _shioajiService.LoginAsync(loginParams);

                if (result.IsSuccess && result.Data != null)
                {
                    // 登入成功
                    IsLoggedIn = true;
                    LoginButtonText = "登出";

                    // 更新帳戶清單
                    UpdateAccountsList(result.Data.Accounts);

                    // 更新連線狀態
                    UpdateConnectionStatus();

                    _logService.LogInfo($"✅ 登入成功_環境：{result.Data.EnvironmentName}_帳戶數量：{result.Data.Accounts.Count}", "MainWindowViewModel", LogDisplayTarget.MainWindow);
                }
                else
                {
                    // 登入失敗
                    _logService.LogError($"❌ 登入失敗：{result.Message}", "MainWindowViewModel", LogDisplayTarget.MainWindow);
                    MessageBox.Show($"登入失敗：{result.Message}", "登入錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "登入過程發生異常", "MainWindowViewModel", LogDisplayTarget.MainWindow);
                MessageBox.Show($"登入過程發生異常：{ex.Message}", "登入錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoginInProgress = false;
                if (!IsLoggedIn)
                {
                    LoginButtonText = "登入";
                }
            }
        }

        // 登出命令 - 現在改為私有方法，只能透過 ToggleLoginAsync 調用
        private async Task LogoutAsync()
        {
            if (IsLoginInProgress) return;

            try
            {
                IsLoginInProgress = true;
                LoginButtonText = "登出中...";

                _logService.LogInfo("開始執行登出程序", "MainWindowViewModel", LogDisplayTarget.MainWindow);

                // 將同步操作包裝在 Task.Run 中，避免阻塞 UI 線程
                await Task.Run(() =>
                {
                    CloseAllChildWindows(); // 關閉所有子視窗
                });

                // 將同步的 Logout 操作包裝在 Task.Run 中
                var result = await Task.Run(() => _shioajiService.Logout());

                if (result.IsSuccess)
                {
                    // 登出成功 - 在 UI 線程中更新 UI
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        IsLoggedIn = false;
                        LoginButtonText = "登入";

                        // 清空帳戶清單
                        Accounts.Clear();
                        SelectedAccount = null;

                        UpdateConnectionStatus();   // 更新連線狀態
                    });

                    _logService.LogInfo("✅ 登出成功", "MainWindowViewModel", LogDisplayTarget.MainWindow);
                }
                else
                {
                    _logService.LogError($"❌ 登出失敗：{result.Message}", "MainWindowViewModel", LogDisplayTarget.MainWindow);

                    // 在 UI 線程中顯示錯誤訊息
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show($"登出失敗：{result.Message}", "登出錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "登出過程發生異常", "MainWindowViewModel", LogDisplayTarget.MainWindow);

                // 在 UI 線程中顯示異常訊息
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"登出過程發生異常：{ex.Message}", "登出錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                // 確保在 UI 線程中重置狀態
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    IsLoginInProgress = false;
                    if (IsLoggedIn)
                    {
                        LoginButtonText = "登出";
                    }
                    else
                    {
                        LoginButtonText = "登入";
                    }
                });
            }
        }

        // 提供給外部調用的登入方法（例如自動登入）
        [RelayCommand]
        private async Task ForceLoginAsync()
        {
            if (!IsLoggedIn)
            {
                await LoginAsync();
            }
        }

        // 提供給外部調用的登出方法（例如視窗關閉時）
        [RelayCommand]
        private async Task ForceLogoutAsync()
        {
            if (IsLoggedIn)
            {
                await LogoutAsync();
            }
        }

        #endregion

        #region 自動登入

        private async Task AutoLoginAsync()
        {
            try
            {
                _logService.LogInfo("🚀 開始自動登入程序", "MainWindowViewModel", LogDisplayTarget.MainWindow);

                // 在 UI 線程中執行登入
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await ForceLoginAsync();
                });
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "自動登入失敗", "MainWindowViewModel", LogDisplayTarget.MainWindow);
            }
        }

        private bool ValidateLoginInputs()
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                MessageBox.Show("請輸入 API Key", "輸入錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (string.IsNullOrWhiteSpace(SecretKey))
            {
                MessageBox.Show("請輸入 Secret Key", "輸入錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!SimulationMode)
            {
                if (string.IsNullOrWhiteSpace(CertPath))
                {
                    MessageBox.Show("正式環境需要憑證路徑", "輸入錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(CertPassword))
                {
                    MessageBox.Show("正式環境需要憑證密碼", "輸入錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(PersonId))
                {
                    MessageBox.Show("正式環境需要身分證字號", "輸入錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region 其他命令
        [RelayCommand]
        private void ShowAllUniqueSubscribedContracts()
        {
            try
            {
                // 獲取所有唯一訂閱的合約
                var subscriptionManager = _marketService.SubscriptionManager;
                var allSubscriptions = subscriptionManager.GetAllUniqueSubscriptions();

                if (allSubscriptions.Count == 0)
                {
                    _logService.LogInfo("[訂閱] 目前沒有已訂閱的合約", "MainWindowViewModel", LogDisplayTarget.MainWindow);
                    return;
                }

                _logService.LogInfo($"[訂閱] 已訂閱合約列表 (共 {allSubscriptions.Count} 個):", "MainWindowViewModel", LogDisplayTarget.MainWindow);

                // 按商品代碼分組顯示
                var groupedByCode = allSubscriptions
                    .GroupBy(s => s.ActualCode)
                    .OrderBy(g => g.Key);

                foreach (var contractGroup in groupedByCode)
                {
                    string contractCode = contractGroup.Key;
                    var subscriptions = contractGroup.ToList();

                    _logService.LogInfo($"📈 {contractCode}:", "MainWindowViewModel", LogDisplayTarget.MainWindow);

                    foreach (var subscription in subscriptions.OrderBy(s => s.QuoteType))
                    {
                        string oddLotText = subscription.IsOddLot ? " [零股]" : " [整股]";
                        _logService.LogInfo($"    - {subscription.QuoteType}{oddLotText}", "MainWindowViewModel", LogDisplayTarget.MainWindow);
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "[錯誤] 顯示已訂閱合約列表失敗", "MainWindowViewModel", LogDisplayTarget.MainWindow);
            }
        }
        #endregion

        #region 視窗管理命令

        [RelayCommand]
        private async Task OpenQuoteWindow()
        {
            try
            {
                if (!IsLoggedIn)
                {
                    MessageBox.Show("請先登入後再開啟報價視窗", "需要登入", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                _logService.LogInfo("🚀 開始創建報價視窗...", "MainWindowViewModel", LogDisplayTarget.MainWindow);

                string windowId = _windowManagerService.GenerateWindowId("Quote");  // 生成視窗ID

                // 創建報價視窗
                var quoteWindow = new QuoteWindow
                {
                    WindowId = windowId // ✅ 設定 WindowId，讓 QuoteWindow 內部創建正確的 ViewModel
                };

                // 註冊視窗
                _windowManagerService.RegisterWindow(windowId, quoteWindow);

                _logService.LogInfo($"📋 報價視窗已創建，視窗ID: {windowId}", "MainWindowViewModel", LogDisplayTarget.MainWindow);

                // 🔧 關鍵修復：等待視窗完全載入後再顯示
                quoteWindow.Show(); // 顯示視窗

                // 🔧 新增：等待視窗完全初始化
                await WaitForWindowInitialization(quoteWindow, windowId);

                UpdateWindowStats();    // 更新視窗統計

                _logService.LogInfo($"✅ 報價視窗已完全載入：{windowId}", "MainWindowViewModel", LogDisplayTarget.MainWindow);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "開啟報價視窗失敗", "MainWindowViewModel", LogDisplayTarget.MainWindow);
                MessageBox.Show($"開啟報價視窗失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 🔧 新增：等待視窗初始化完成的方法
        private async Task WaitForWindowInitialization(QuoteWindow quoteWindow, string windowId)
        {
            const int maxWaitTime = 5000; // 最大等待5秒
            const int checkInterval = 100; // 每100ms檢查一次
            int totalWaited = 0;

            _logService.LogDebug($"⏳ 等待視窗 {windowId} 初始化完成...", "MainWindowViewModel", LogDisplayTarget.MainWindow);

            while (totalWaited < maxWaitTime)
            {
                // 檢查視窗是否已完全初始化
                if (IsWindowFullyInitialized(quoteWindow))
                {
                    _logService.LogDebug($"✅ 視窗 {windowId} 初始化完成，耗時: {totalWaited}ms", "MainWindowViewModel", LogDisplayTarget.MainWindow);
                    return;
                }

                await Task.Delay(checkInterval);
                totalWaited += checkInterval;
            }

            _logService.LogWarning($"⚠️ 視窗 {windowId} 初始化超時 ({maxWaitTime}ms)", "MainWindowViewModel", LogDisplayTarget.MainWindow);
        }

        // 🔧 新增：檢查視窗是否完全初始化
        private static bool IsWindowFullyInitialized(QuoteWindow quoteWindow)
        {
            try
            {
                // 檢查視窗是否已載入
                if (!quoteWindow.IsLoaded)
                    return false;

                // 檢查 DataContext 是否已設置
                if (quoteWindow.DataContext is not QuoteViewModel viewModel)
                    return false;

                // 檢查 ViewModel 的關鍵屬性是否已初始化
                if (string.IsNullOrEmpty(viewModel.WindowId))
                    return false;

                // 檢查 OrderBookViewModel 是否已初始化
                if (viewModel.OrderBookViewModel == null)
                    return false;

                /*
                // 檢查帳戶是否已載入
                if (viewModel.Accounts == null || viewModel.Accounts.Count == 0)
                    return false;
                */
                return true;
            }
            catch
            {
                return false;
            }
        }

        [RelayCommand]
        private void OpenContractSearchWindow()
        {
            try
            {
                if (!IsLoggedIn)
                {
                    MessageBox.Show("請先登入後再開啟合約搜尋視窗", "需要登入", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 生成視窗ID
                string windowId = _windowManagerService.GenerateWindowId("ContractSearch");

                // 創建合約搜尋視窗
                var contractSearchWindow = new ContractSearchWindow
                {
                    WindowId = windowId,
                    Owner = Application.Current.MainWindow // 設定父視窗
                };

                // 註冊視窗到管理服務
                _windowManagerService.RegisterWindow(windowId, contractSearchWindow);

                // 訂閱合約選擇事件（如果需要的話）
                contractSearchWindow.ContractSelected += OnContractSelected;

                // 顯示視窗
                contractSearchWindow.Show();

                // 更新視窗統計
                UpdateWindowStats();

                _logService.LogInfo($"已開啟合約搜尋視窗：{windowId}", "MainWindowViewModel", LogDisplayTarget.MainWindow);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "開啟合約搜尋視窗失敗", "MainWindowViewModel", LogDisplayTarget.MainWindow);
                MessageBox.Show($"開啟合約搜尋視窗失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 處理合約選擇事件
        private void OnContractSelected(object? sender, ContractSelectedEventArgs e)
        {
            try
            {
                _logService.LogInfo($"收到合約選擇事件，共 {e.SelectedContracts.Count} 個合約", "MainWindowViewModel", LogDisplayTarget.MainWindow);

                // 這裡可以處理選中的合約，例如：
                // - 創建快速按鈕
                // - 加入到監控清單
                // - 其他業務邏輯

                foreach (var contract in e.SelectedContracts)
                {
                    _logService.LogDebug($"選中合約：{contract.Symbol} - {contract.Name}", "MainWindowViewModel", LogDisplayTarget.MainWindow);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "處理合約選擇事件失敗", "MainWindowViewModel", LogDisplayTarget.MainWindow);
            }
        }

        [RelayCommand]
        private void CloseAllQuoteWindows()
        {
            try
            {
                int closedCount = _windowManagerService.CloseWindowsByType("Quote");
                UpdateWindowStats();

                _logService.LogInfo($"已關閉 {closedCount} 個報價視窗", "MainWindowViewModel", LogDisplayTarget.MainWindow);

                if (closedCount > 0)
                {
                    MessageBox.Show($"已關閉 {closedCount} 個報價視窗", "關閉完成", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "關閉報價視窗失敗", "MainWindowViewModel", LogDisplayTarget.MainWindow);
                MessageBox.Show($"關閉報價視窗失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void CloseAllWindows()
        {
            try
            {
                int closedCount = _windowManagerService.CloseAllWindows();
                UpdateWindowStats();

                _logService.LogInfo($"已關閉所有子視窗，共 {closedCount} 個", "MainWindowViewModel", LogDisplayTarget.MainWindow);

                if (closedCount > 0)
                {
                    MessageBox.Show($"已關閉所有子視窗，共 {closedCount} 個", "關閉完成", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "關閉所有視窗失敗", "MainWindowViewModel", LogDisplayTarget.MainWindow);
                MessageBox.Show($"關閉所有視窗失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region 日誌管理命令

        [RelayCommand]
        private void ClearLogs()
        {
            try
            {
                _logService.ClearLogs("MainWindow");
                _logService.LogInfo("已清除主視窗日誌", "MainWindowViewModel", LogDisplayTarget.MainWindow);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "清除日誌失敗", "MainWindowViewModel", LogDisplayTarget.MainWindow);
            }
        }

        [RelayCommand]
        private void ExportLogs()
        {
            try
            {
                string fileName = $"MainWindow_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);

                _logService.ExportLogs(filePath, "MainWindow");

                MessageBox.Show($"日誌已匯出至桌面：{fileName}", "匯出完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "匯出日誌失敗", "MainWindowViewModel", LogDisplayTarget.MainWindow);
                MessageBox.Show($"匯出日誌失敗：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void ToggleDebugOutput()
        {
            try
            {
                bool currentStatus = _logService.EnableDebugOutput;
                _logService.SetDebugOutput(!currentStatus);

                string statusText = _logService.EnableDebugOutput ? "已啟用" : "已停用";
                _logService.LogInfo($"Debug 輸出{statusText}", "MainWindowViewModel", LogDisplayTarget.MainWindow);
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "切換 Debug 輸出失敗", "MainWindowViewModel", LogDisplayTarget.MainWindow);
            }
        }

        #endregion

        #region 系統管理命令

        [RelayCommand]
        private void RefreshConnectionStatus()
        {
            UpdateConnectionStatus();
            UpdateWindowStats();
            _logService.LogInfo("已重新整理系統狀態", "MainWindowViewModel", LogDisplayTarget.MainWindow);
        }

        #endregion

        #region 私有輔助方法

        private void UpdateAccountsList(System.Collections.Generic.List<Account> accounts)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Accounts.Clear();

                    foreach (var account in accounts)
                    {
                        var accountInfo = new AccountInfo
                        {
                            Account = account,
                            DisplayName = $"{account.account_type}-{account.account_id}",
                            AccountType = account.account_type.ToString(),
                            AccountId = account.account_id,
                            IsSigned = account.signed
                        };

                        Accounts.Add(accountInfo);
                    }

                    // 自動選擇第一個帳戶
                    if (Accounts.Count > 0)
                    {
                        SelectedAccount = Accounts[0];
                    }
                });
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "更新帳戶清單失敗", "MainWindowViewModel", LogDisplayTarget.MainWindow);
            }
        }

        private void UpdateConnectionStatus()
        {
            try
            {
                var result = _shioajiService.GetConnectionStatus();
                ConnectionStatus = result.IsSuccess ? result.Data! : "連線狀態未知";

                // 同步登入狀態
                IsLoggedIn = _shioajiService.IsLoggedIn;
                LoginButtonText = IsLoggedIn ? "登出" : "登入";
            }
            catch (Exception ex)
            {
                ConnectionStatus = "取得狀態失敗";
                _logService.LogError(ex, "更新連線狀態失敗", "MainWindowViewModel", LogDisplayTarget.MainWindow);
            }
        }

        private void UpdateWindowStats()
        {
            try
            {
                WindowStats = _windowManagerService.GetWindowStats();
            }
            catch (Exception ex)
            {
                WindowStats = "統計資料錯誤";
                _logService.LogError(ex, "更新視窗統計失敗", "MainWindowViewModel", LogDisplayTarget.MainWindow);
            }
        }

        private void CloseAllChildWindows()
        {
            try
            {
                _windowManagerService.CloseAllWindows();
                UpdateWindowStats();
            }
            catch (Exception ex)
            {
                _logService.LogError(ex, "關閉子視窗失敗", "MainWindowViewModel", LogDisplayTarget.MainWindow);
            }
        }

        private void OnLogServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LogService.MainWindowLogsText))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    SystemLogs = _logService.MainWindowLogsText;
                });
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
                        // 取消事件訂閱
                        _logService.PropertyChanged -= OnLogServicePropertyChanged;

                        // 關閉所有子視窗
                        CloseAllChildWindows();

                        _logService.LogInfo("MainWindowViewModel 已釋放資源", "MainWindowViewModel", LogDisplayTarget.MainWindow);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"MainWindowViewModel Dispose 錯誤: {ex.Message}");
                    }
                }
                _disposed = true;
            }
        }

        ~MainWindowViewModel()
        {
            Dispose(false);
        }

        #endregion
    }

    #region 輔助類別

    /// <summary>
    /// 帳戶資訊包裝類別
    /// </summary>
    public class AccountInfo
    {
        public Account Account { get; set; } = null!;
        public string DisplayName { get; set; } = "";
        public string AccountType { get; set; } = "";
        public string AccountId { get; set; } = "";  // 加入預設值
        public bool IsSigned { get; set; }

        public override string ToString() => DisplayName;
    }

    #endregion
}