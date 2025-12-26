using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace WpfApp5.Services
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }

    // 🆕 日誌顯示目標枚舉
    [Flags]
    public enum LogDisplayTarget
    {
        None = 0,
        DebugOutput = 1,       // 顯示在 Visual Studio 輸出視窗 (Debug.WriteLine)
        SourceWindow = 2,      // 顯示在來源視窗
        MainWindow = 4,        // 顯示在 MainWindow
        QuoteWindow = 5,       // 修正: 原本是 3，改為唯一值 5 (DebugOutput | SourceWindow | MainWindow)
        AllWindows = 8,        // 顯示在所有視窗
        Default = DebugOutput | SourceWindow  // 預設：Debug輸出 + 來源視窗 (值為 3)
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public LogDisplayTarget DisplayTarget { get; set; } = LogDisplayTarget.Default;

        // 統一格式: [時間戳][等級] [來源] 訊息
        public string FormattedMessage => $"[{Timestamp:HH:mm:ss.fff}][{GetLevelShortName()}] [{Source}] {Message}";

        private string GetLevelShortName()
        {
            return Level switch
            {
                LogLevel.Debug => "DBG",
                LogLevel.Info => "INF",
                LogLevel.Warning => "WRN",
                LogLevel.Error => "ERR",
                LogLevel.Fatal => "FTL",
                _ => "UNK"
            };
        }

        // 🆕 檢查是否應該顯示在指定目標
        public bool ShouldDisplayIn(LogDisplayTarget target)
        {
            return DisplayTarget.HasFlag(target);
        }
    }

    public class LogService : INotifyPropertyChanged, IDisposable
    {
        private static readonly Lazy<LogService> _instance = new(() => new LogService());
        public static LogService Instance => _instance.Value;

        private Logger _logger = null!;
        private readonly ConcurrentQueue<LogEntry> _logQueue = new();
        private readonly object _lockObject = new();
        private bool _disposed = false;

        // 不同的日誌集合，用於不同的 UI 顯示
        public ObservableCollection<LogEntry> AllLogs { get; } = [];
        public ObservableCollection<LogEntry> MainWindowLogs { get; } = [];
        public ObservableCollection<LogEntry> ContractLogs { get; } = [];
        public ObservableCollection<LogEntry> QuoteLogs { get; } = [];

        // 字串格式的日誌，用於綁定到 TextBox 或 TextBlock
        private string _allLogsText = string.Empty;
        private string _mainWindowLogsText = string.Empty;
        private string _contractLogsText = string.Empty;
        private string _quoteLogsText = string.Empty;

        public string AllLogsText
        {
            get => _allLogsText;
            private set
            {
                _allLogsText = value;
                OnPropertyChanged(nameof(AllLogsText));
            }
        }

        public string MainWindowLogsText
        {
            get => _mainWindowLogsText;
            private set
            {
                _mainWindowLogsText = value;
                OnPropertyChanged(nameof(MainWindowLogsText));
            }
        }

        public string ContractLogsText
        {
            get => _contractLogsText;
            private set
            {
                _contractLogsText = value;
                OnPropertyChanged(nameof(ContractLogsText));
            }
        }

        public string QuoteLogsText
        {
            get => _quoteLogsText;
            private set
            {
                _quoteLogsText = value;
                OnPropertyChanged(nameof(QuoteLogsText));
            }
        }

        // 設定選項
        public int MaxLogEntries { get; set; } = 1000;
        public bool EnableFileLogging { get; set; } = true;
        public bool EnableDebugOutput { get; set; } = true;  // 🔧 改名為 EnableDebugOutput

        private LogService()
        {
            InitializeSerilog();
        }

        private void InitializeSerilog()
        {
            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Debug();

            // 🔧 只保留檔案輸出，移除 Console 輸出
            if (EnableFileLogging)
            {
                string outputTemplate = "[{Timestamp:HH:mm:ss.fff}][{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

                loggerConfig.WriteTo.File(
                    path: "logs/app-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    outputTemplate: outputTemplate
                );
            }

            _logger = loggerConfig.CreateLogger();
            Log.Logger = _logger;
        }

        // 🆕 增強的日誌記錄方法 - 支援顯示目標
        public void LogDebug(string message, string source = "General", LogDisplayTarget displayTarget = LogDisplayTarget.Default)
        {
            WriteLog(LogLevel.Debug, message, source, displayTarget);
        }

        public void LogInfo(string message, string source = "General", LogDisplayTarget displayTarget = LogDisplayTarget.Default)
        {
            WriteLog(LogLevel.Info, message, source, displayTarget);
        }

        public void LogWarning(string message, string source = "General", LogDisplayTarget displayTarget = LogDisplayTarget.Default)
        {
            WriteLog(LogLevel.Warning, message, source, displayTarget);
        }

        public void LogError(string message, string source = "General", LogDisplayTarget displayTarget = LogDisplayTarget.Default)
        {
            WriteLog(LogLevel.Error, message, source, displayTarget);
        }

        /// <summary>
        /// 記錄錯誤日誌（支援 nullable Exception）
        /// </summary>
        /// <param name="ex">異常對象（可為 null）</param>
        /// <param name="message">錯誤訊息</param>
        /// <param name="source">來源標識</param>
        /// <param name="displayTarget">顯示目標</param>
        public void LogError(Exception? ex, string message = "", string source = "General", LogDisplayTarget displayTarget = LogDisplayTarget.Default)
        {
            // ✅ 處理 null 情況
            if (ex == null)
            {
                var logMessage = string.IsNullOrEmpty(message) ? "發生未知錯誤（無 Exception 詳細資訊）" : message;
                WriteLog(LogLevel.Error, logMessage, source, displayTarget);
                return;
            }

            var fullMessage = string.IsNullOrEmpty(message) ? ex.Message : $"{message}: {ex.Message}";
            WriteLog(LogLevel.Error, fullMessage, source, displayTarget);

            // Exception 的詳細記錄到檔案
            if (EnableFileLogging)
            {
                _logger.ForContext("SourceContext", source).Error(ex, "{Message}", fullMessage);
            }
        }

        public void LogFatal(string message, string source = "General", LogDisplayTarget displayTarget = LogDisplayTarget.Default)
        {
            WriteLog(LogLevel.Fatal, message, source, displayTarget);
        }

        // 🆕 便利方法：記錄到 MainWindow（從其他視窗）
        public void LogToMainWindow(LogLevel level, string message, string source)
        {
            var displayTarget = LogDisplayTarget.DebugOutput | LogDisplayTarget.MainWindow;
            WriteLog(level, message, source, displayTarget);
        }

        // 🆕 便利方法：記錄到所有地方
        public void LogToAll(LogLevel level, string message, string source)
        {
            var displayTarget = LogDisplayTarget.DebugOutput | LogDisplayTarget.SourceWindow | LogDisplayTarget.MainWindow;
            WriteLog(level, message, source, displayTarget);
        }

        private void WriteLog(LogLevel level, string message, string source, LogDisplayTarget displayTarget)
        {
            if (_disposed) return;

            var logEntry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message,
                Source = source,
                DisplayTarget = displayTarget
            };

            // 🔧 輸出到 Visual Studio 輸出視窗 (Debug Output)
            if (EnableDebugOutput && displayTarget.HasFlag(LogDisplayTarget.DebugOutput))
            {
                Debug.WriteLine(logEntry.FormattedMessage);
            }

            // 🔧 輸出到 Serilog 檔案
            if (EnableFileLogging)
            {
                var logContext = _logger.ForContext("SourceContext", source);

                switch (level)
                {
                    case LogLevel.Debug:
                        logContext.Debug("{Message}", message);
                        break;
                    case LogLevel.Info:
                        logContext.Information("{Message}", message);
                        break;
                    case LogLevel.Warning:
                        logContext.Warning("{Message}", message);
                        break;
                    case LogLevel.Error:
                        logContext.Error("{Message}", message);
                        break;
                    case LogLevel.Fatal:
                        logContext.Fatal("{Message}", message);
                        break;
                }
            }

            // 加入到內部佇列
            _logQueue.Enqueue(logEntry);

            // 更新 UI (在 UI 執行緒中)
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                UpdateLogCollections(logEntry);
            });
        }

        private void UpdateLogCollections(LogEntry logEntry)
        {
            if (_disposed) return;

            lock (_lockObject)
            {
                AllLogs.Add(logEntry);  // 總是加入到 AllLogs
                TrimCollection(AllLogs);

                // 根據 DisplayTarget 決定要加入哪些集合

                // MainWindow 集合：來源是 MainWindow 或 DisplayTarget 包含 MainWindow
                if (logEntry.Source.Equals("MainWindow", StringComparison.OrdinalIgnoreCase) ||
                    logEntry.Source.Equals("Main", StringComparison.OrdinalIgnoreCase) ||
                    logEntry.DisplayTarget.HasFlag(LogDisplayTarget.MainWindow))
                {
                    MainWindowLogs.Add(logEntry);
                    TrimCollection(MainWindowLogs);
                }

                // Contract 集合：來源是 Contract 或 DisplayTarget 包含 SourceWindow 且來源是 Contract
                if ((logEntry.Source.Equals("Contract", StringComparison.OrdinalIgnoreCase) ||
                     logEntry.Source.Equals("ContractSearch", StringComparison.OrdinalIgnoreCase)) &&
                    logEntry.DisplayTarget.HasFlag(LogDisplayTarget.SourceWindow))
                {
                    ContractLogs.Add(logEntry);
                    TrimCollection(ContractLogs);
                }

                // Quote 集合 - 包含 QuoteWindow 和 QuoteViewModel 的日誌
                if ((logEntry.Source.Equals("Quote", StringComparison.OrdinalIgnoreCase) ||
                     logEntry.Source.Equals("QuoteWindow", StringComparison.OrdinalIgnoreCase) ||
                     logEntry.Source.Equals("QuoteViewModel", StringComparison.OrdinalIgnoreCase)) &&
                    logEntry.DisplayTarget.HasFlag(LogDisplayTarget.SourceWindow))
                {
                    QuoteLogs.Add(logEntry);
                    TrimCollection(QuoteLogs);
                }

                UpdateLogTexts();   // 更新文字格式
                OnPropertyChanged(nameof(MainWindowLogs));  // 觸發 MainWindowLogs 更新事件
            }
        }

        private void TrimCollection(ObservableCollection<LogEntry> collection)
        {
            while (collection.Count > MaxLogEntries)
            {
                collection.RemoveAt(0);
            }
        }

        private void UpdateLogTexts()
        {
            if (_disposed) return;

            AllLogsText = string.Join("\n", AllLogs.Select(log => log.FormattedMessage));
            MainWindowLogsText = string.Join("\n", MainWindowLogs.Select(log => log.FormattedMessage));
            ContractLogsText = string.Join("\n", ContractLogs.Select(log => log.FormattedMessage));
            QuoteLogsText = string.Join("\n", QuoteLogs.Select(log => log.FormattedMessage));
        }

        // 清除特定來源的日誌
        public void ClearLogs(string source = "")
        {
            if (_disposed) return;

            Application.Current?.Dispatcher.Invoke(() =>
            {
                lock (_lockObject)
                {
                    if (string.IsNullOrEmpty(source))
                    {
                        AllLogs.Clear();
                        MainWindowLogs.Clear();
                        ContractLogs.Clear();
                        QuoteLogs.Clear();
                    }
                    else
                    {
                        switch (source.ToLower())
                        {
                            case "mainwindow":
                            case "main":
                                // 🔧 只清除真正來源是 MainWindow 的日誌
                                var itemsToRemove = MainWindowLogs
                                    .Where(log => log.Source.Equals("MainWindow", StringComparison.OrdinalIgnoreCase) ||
                                                 log.Source.Equals("Main", StringComparison.OrdinalIgnoreCase))
                                    .ToList();
                                foreach (var item in itemsToRemove)
                                {
                                    MainWindowLogs.Remove(item);
                                }
                                break;
                            case "contract":
                            case "contractsearch":
                                ContractLogs.Clear();
                                break;
                            case "quote":
                            case "quotewindow":
                                QuoteLogs.Clear();
                                break;
                        }
                    }
                    UpdateLogTexts();
                }
            });
        }

        // 取得特定等級的日誌
        public IEnumerable<LogEntry> GetLogsByLevel(LogLevel level)
        {
            return AllLogs.Where(log => log.Level == level);
        }

        // 取得特定來源的日誌
        public IEnumerable<LogEntry> GetLogsBySource(string source)
        {
            return AllLogs.Where(log => log.Source.Equals(source, StringComparison.OrdinalIgnoreCase));
        }

        // 匯出日誌到檔案
        public void ExportLogs(string filePath, string source = "")
        {
            if (_disposed) return;

            try
            {
                var logsToExport = string.IsNullOrEmpty(source)
                    ? AllLogs
                    : GetLogsBySource(source);

                var logText = string.Join("\n", logsToExport.Select(log => log.FormattedMessage));
                System.IO.File.WriteAllText(filePath, logText);

                LogInfo($"日誌已匯出到: {filePath}", "LogService");
            }
            catch (Exception ex)
            {
                LogError(ex, "匯出日誌失敗", "LogService");
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // 🔧 設定方法，控制 Debug 輸出
        public void SetDebugOutput(bool enable)
        {
            if (EnableDebugOutput != enable)
            {
                EnableDebugOutput = enable;
                LogInfo($"Visual Studio 輸出已{(enable ? "啟用" : "停用")}", "LogService");
            }
        }

        // 🔧 取得 Debug 輸出狀態
        public string GetDebugStatus()
        {
            return EnableDebugOutput ? "Debug輸出: 啟用" : "Debug輸出: 停用";
        }

        // 正確實作 IDisposable
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
                        _logger?.Dispose();
                        Log.CloseAndFlush();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"LogService Dispose 錯誤: {ex.Message}");
                    }
                }

                _disposed = true;
            }
        }

        ~LogService()
        {
            Dispose(false);
        }
    }
}