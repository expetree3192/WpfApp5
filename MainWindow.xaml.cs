using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using WpfApp5.ViewModels;

namespace WpfApp5
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯 - MVVM 版本
    /// 只保留必要的 UI 相關邏輯，所有業務邏輯都移到 ViewModel
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainWindowViewModel? _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            InitializeViewModel();
            SetupPasswordBoxBindings();
            SetupRichTextBoxBinding();
        }

        private void InitializeViewModel()
        {
            try
            {
                _viewModel = new MainWindowViewModel();
                DataContext = _viewModel;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化 ViewModel 失敗：{ex.Message}", "初始化錯誤",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        /// <summary>
        /// 設定 PasswordBox 的資料繫結
        /// 由於 PasswordBox.Password 不是依賴屬性，無法直接繫結，需要手動處理
        /// </summary>
        private void SetupPasswordBoxBindings()
        {
            try
            {
                // SecretKey PasswordBox 繫結
                if (SecretKeyBox != null && _viewModel != null)
                {
                    SecretKeyBox.Password = _viewModel.SecretKey;
                    SecretKeyBox.PasswordChanged += (s, e) =>
                    {
                        _viewModel?.SecretKey = SecretKeyBox.Password;
                    };

                    // 監聽 ViewModel 的 SecretKey 變更
                    _viewModel.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(MainWindowViewModel.SecretKey) &&
                            SecretKeyBox.Password != _viewModel.SecretKey)
                        {
                            SecretKeyBox.Password = _viewModel.SecretKey;
                        }
                    };
                }

                // CertPassword PasswordBox 繫結
                if (CertPasswordBox != null && _viewModel != null)
                {
                    CertPasswordBox.Password = _viewModel.CertPassword;
                    CertPasswordBox.PasswordChanged += (s, e) =>
                    {
                        _viewModel?.CertPassword = CertPasswordBox.Password;
                    };

                    // 監聽 ViewModel 的 CertPassword 變更
                    _viewModel.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(MainWindowViewModel.CertPassword) &&
                            CertPasswordBox.Password != _viewModel.CertPassword)
                        {
                            CertPasswordBox.Password = _viewModel.CertPassword;
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"設定 PasswordBox 繫結失敗：{ex.Message}");
            }
        }

        /// <summary>
        /// 🎨 設定 RichTextBox 的彩色日誌顯示
        /// </summary>
        private void SetupRichTextBoxBinding()
        {
            try
            {
                if (LogRichTextBox != null && _viewModel != null)
                {
                    // 初始化 RichTextBox 文檔
                    LogRichTextBox.Document = new FlowDocument();

                    // 監聽 ViewModel 的日誌變更
                    _viewModel.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(MainWindowViewModel.SystemLogs))
                        {
                            UpdateRichTextBoxContent(_viewModel.SystemLogs);
                        }
                    };

                    // 初始化顯示
                    UpdateRichTextBoxContent(_viewModel.SystemLogs);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"設定 RichTextBox 繫結失敗：{ex.Message}");
            }
        }

        // 🎨 更新 RichTextBox 的彩色內容
        private void UpdateRichTextBoxContent(string logText)
        {
            try
            {
                if (LogRichTextBox == null) return;

                Dispatcher.Invoke(() =>
                {
                    var document = new FlowDocument();

                    if (!string.IsNullOrEmpty(logText))
                    {
                        var lines = logText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            var paragraph = new Paragraph();
                            var run = new Run(line.Trim());

                            // 🎨 根據日誌級別和內容設定顏色
                            if (line.Contains("ERR") || line.Contains('❌'))
                            {
                                run.Foreground = Brushes.Red;
                                run.FontWeight = FontWeights.Bold;
                            }
                            else if (line.Contains("WARNING") || line.Contains("⚠️"))
                            {
                                run.Foreground = Brushes.Orange;
                                run.FontWeight = FontWeights.Bold;
                            }
                            else if (line.Contains("INFO") || line.Contains('✅'))
                            {
                                run.Foreground = Brushes.LightGreen;
                            }
                            else if (line.Contains("DEBUG") || line.Contains("🔧"))
                            {
                                run.Foreground = Brushes.Gray;
                            }
                            else if (line.Contains("🚀"))
                            {
                                run.Foreground = Brushes.Cyan;
                                run.FontWeight = FontWeights.Bold;
                            }
                            else if (line.Contains("LOGIN") || line.Contains("登入"))
                            {
                                run.Foreground = Brushes.Yellow;
                                run.FontWeight = FontWeights.Bold;
                            }
                            else if (line.Contains("LOGOUT") || line.Contains("登出"))
                            {
                                run.Foreground = Brushes.Pink;
                                run.FontWeight = FontWeights.Bold;
                            }
                            else
                            {
                                run.Foreground = Brushes.White;
                            }

                            paragraph.Inlines.Add(run);
                            paragraph.Margin = new Thickness(0, 1, 0, 1);
                            document.Blocks.Add(paragraph);
                        }
                    }

                    LogRichTextBox.Document = document;

                    // 🔄 自動滾動到底部（如果啟用）
                    if (_viewModel?.AutoScrollLogs == true)
                    {
                        LogRichTextBox.ScrollToEnd();
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新 RichTextBox 內容失敗：{ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // 釋放 ViewModel 資源
                _viewModel?.Dispose();
                _viewModel = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow 關閉時發生錯誤：{ex.Message}");
            }

            base.OnClosed(e);
        }

        // 處理視窗關閉事件 - 確保正確清理資源
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (_viewModel?.IsLoggedIn == true)
                {
                    try
                    {
                        _viewModel.ForceLogoutCommand.Execute(null);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"關閉時登出失敗：{ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"處理視窗關閉事件時發生錯誤：{ex.Message}");
            }

            base.OnClosing(e);
        }
    }
}