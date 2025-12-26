using System;
using System.Windows;
using System.Windows.Threading;
using WpfApp5.Services;

namespace WpfApp5
{
    // Interaction logic for App.xaml
    public partial class App : Application
    {
        #region 全域服務

        // 全域時間服務
        public static GlobalTimeService TimeService { get; private set; } = null!;

        #endregion

        protected override void OnStartup(StartupEventArgs e)
        {
            InitializeGlobalServices(); // 🔥 初始化全域時間服務

            // 處理未捕獲的例外（靜默處理 RuntimeBinder 錯誤）
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            base.OnStartup(e);
        }

        // 初始化全域服務
        private void InitializeGlobalServices()
        {
            // 創建並初始化時間服務
            TimeService = new GlobalTimeService();
            TimeService.Initialize();

            // 註冊全域時間事件
            RegisterGlobalTimeEvents();

            System.Diagnostics.Debug.WriteLine("[App] 全域服務初始化完成");
        }

        // 註冊全域時間事件
        private void RegisterGlobalTimeEvents()
        {
            // 時間更新事件
            TimeService.TimeUpdated += OnGlobalTimeUpdated;

            // 交易時間狀態變化事件
            TimeService.TradingTimeStatusChanged += OnTradingTimeStatusChanged;
        }

        // 全域時間更新處理
        private void OnGlobalTimeUpdated(DateTime currentTime)
        {
            // 應用程式級別的定時任務

            // 1. 每5分鐘執行自動保存
            if (currentTime.Second == 0 && currentTime.Minute % 5 == 0)
            {
                //  PerformAutoSave();
            }

            // 2. 每分鐘檢查系統狀態
            if (currentTime.Second == 0)
            {
                //  CheckSystemStatus();
            }

        }

        // 交易時間狀態變化處理
        private void OnTradingTimeStatusChanged(bool isTradingTime)
        {
            if (isTradingTime)
            {
                System.Diagnostics.Debug.WriteLine($"[App] 🟢 進入交易時間 - {TimeService.TimeOnly}");
                // 可以在這裡啟動交易相關服務
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[App] 🔴 離開交易時間 - {TimeService.TimeOnly}");
                // 可以在這裡暫停或調整交易相關服務
            }
        }

        #region 應用程式級別任務

        private static void PerformAutoSave()
        {
            System.Diagnostics.Debug.WriteLine($"[App] 💾 執行自動保存 - {TimeService.TimeOnly}");
            // 實作自動保存邏輯
        }

        private static void CheckSystemStatus()
        {
            System.Diagnostics.Debug.WriteLine($"[App] 🔍 系統狀態檢查 - {TimeService.TimeOnly}");
            // 實作系統狀態檢查邏輯
        }

        private static void CheckMemoryUsage()
        {
            var memoryUsage = GC.GetTotalMemory(false);
            if (memoryUsage > 100 * 1024 * 1024) // 超過100MB
            {
                System.Diagnostics.Debug.WriteLine($"[App] ⚠️ 記憶體使用量: {memoryUsage / 1024 / 1024:F1}MB - {TimeService.TimeOnly}");
            }
        }

        #endregion

        protected override void OnExit(ExitEventArgs e)
        {
            // 🔥 清理全域服務
            TimeService?.Dispose();

            System.Diagnostics.Debug.WriteLine("[App] 應用程式正在退出，已清理全域服務");

            base.OnExit(e);
        }

        #region 例外處理

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
            {
                // 靜默處理 RuntimeBinder 錯誤，不顯示任何訊息
                System.Diagnostics.Debug.WriteLine($"[已忽略] RuntimeBinder錯誤: {e.ExceptionObject}");
            }
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (e.Exception is Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
            {
                // 標記為已處理，避免程式崩潰，不顯示任何訊息
                e.Handled = true;
                System.Diagnostics.Debug.WriteLine($"[已忽略] UI RuntimeBinder錯誤: {e.Exception.Message}");
            }
        }

        #endregion
    }
}
