using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using WpfApp5.Models;

namespace WpfApp5.Views
{
    /// <summary>
    /// 📊 合約詳細資訊視窗
    /// </summary>
    public partial class ContractDetailsWindow : Window
    {
        private readonly List<ContractSearchResult> _contracts;

        public ContractDetailsWindow(List<ContractSearchResult> contracts)
        {
            InitializeComponent();
            _contracts = contracts ?? [];

            InitializeWindow();
        }

        private void InitializeWindow()
        {
            ContractCountTextBlock.Text = $"(共 {_contracts.Count} 個合約)";

            // 為每個合約建立 TabItem
            foreach (var contract in _contracts)
            {
                var tabItem = CreateContractTabItem(contract);
                ContractTabControl.Items.Add(tabItem);
            }

            // 預設選擇第一個 Tab
            if (ContractTabControl.Items.Count > 0)
            {
                ContractTabControl.SelectedIndex = 0;
            }
        }

        private TabItem CreateContractTabItem(ContractSearchResult contract)
        {
            var tabItem = new TabItem
            {
                Header = $"{contract.Symbol} - {contract.Name}",
                Content = CreateContractDetailPanel(contract)
            };

            return tabItem;
        }

        private ScrollViewer CreateContractDetailPanel(ContractSearchResult contract)
        {
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            var mainPanel = new StackPanel { Margin = new Thickness(10) };

            // 基本資訊
            mainPanel.Children.Add(CreateGroupBox("📋 基本資訊", CreateBasicInfoPanel(contract)));

            // 價格資訊
            mainPanel.Children.Add(CreateGroupBox("💰 價格資訊", CreatePriceInfoPanel(contract)));

            // 根據商品類型顯示特殊資訊
            switch (contract.ProductType)
            {
                case "Futures":
                    mainPanel.Children.Add(CreateGroupBox("📊 期貨資訊", CreateFutureInfoPanel(contract)));
                    break;

                case "Options":
                    mainPanel.Children.Add(CreateGroupBox("🎯 選擇權資訊", CreateOptionInfoPanel(contract)));
                    break;

                case "Stocks":
                    mainPanel.Children.Add(CreateGroupBox("📊 股票資訊", CreateStockInfoPanel(contract)));
                    break;

                case "Indexs":
                    mainPanel.Children.Add(CreateGroupBox("📈 指數資訊", CreateIndexInfoPanel(contract)));
                    break;
            }

            // 查詢資訊
            mainPanel.Children.Add(CreateGroupBox("🔍 查詢資訊", CreateQueryInfoPanel(contract)));

            scrollViewer.Content = mainPanel;
            return scrollViewer;
        }

        private static GroupBox CreateGroupBox(string header, Panel content)
        {
            return new GroupBox
            {
                Header = header,
                Content = content,
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(10)
            };
        }

        private Grid CreateBasicInfoPanel(ContractSearchResult contract)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var rowIndex = 0;

            AddInfoRow(grid, "商品代號:", contract.Symbol, rowIndex++);
            AddInfoRow(grid, "商品名稱:", contract.Name ?? "N/A", rowIndex++);
            AddInfoRow(grid, "交易所:", contract.Exchange, rowIndex++);
            AddInfoRow(grid, "商品類型:", contract.ProductType, rowIndex++);

            // 🔧 修正: 直接處理 DateTime 類型 (檢查是否為 default 值)
            var analyzedAtText = contract.AnalyzedAt == default
                ? "N/A"
                : contract.AnalyzedAt.ToString("yyyy-MM-dd HH:mm:ss");
            AddInfoRow(grid, "分析時間:", analyzedAtText, rowIndex++);

            return grid;
        }

        private Grid CreatePriceInfoPanel(ContractSearchResult contract)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var rowIndex = 0;

            AddInfoRow(grid, "參考價:", contract.Reference?.ToString("F2") ?? "N/A", rowIndex++);
            AddInfoRow(grid, "漲停價:", contract.LimitUp?.ToString("F2") ?? "N/A", rowIndex++, "#E74C3C");
            AddInfoRow(grid, "跌停價:", contract.LimitDown?.ToString("F2") ?? "N/A", rowIndex++, "#27AE60");

            return grid;
        }

        private Grid CreateStockFutureInfoPanel(ContractSearchResult contract)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var rowIndex = 0;

            AddInfoRow(grid, "合約月份:", contract.ContractMonthDisplay, rowIndex++);
            AddInfoRow(grid, "標的代號:", contract.TargetCode ?? "N/A", rowIndex++);
            AddInfoRow(grid, "標的名稱:", contract.Name ?? "N/A", rowIndex++);

            return grid;
        }

        private Grid CreateFutureInfoPanel(ContractSearchResult contract)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var rowIndex = 0;

            AddInfoRow(grid, "到期月份:", contract.DeliveryMonth ?? "N/A", rowIndex++);
            // 移除不存在的屬性
            AddInfoRow(grid, "合約規格:", "標準合約", rowIndex++);
            AddInfoRow(grid, "最小跳動點:", "依交易所規定", rowIndex++);

            return grid;
        }

        private Grid CreateOptionInfoPanel(ContractSearchResult contract)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var rowIndex = 0;

            AddInfoRow(grid, "履約價:", contract.StrikePrice?.ToString("F2") ?? "N/A", rowIndex++);
            AddInfoRow(grid, "權利類型:", contract.OptionRight ?? "N/A", rowIndex++);
            AddInfoRow(grid, "到期月份:", contract.DeliveryMonth ?? "N/A", rowIndex++);
            AddInfoRow(grid, "標的種類:", contract.UnderlyingKind ?? "N/A", rowIndex++);

            return grid;
        }

        private Grid CreateStockInfoPanel(ContractSearchResult contract)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var rowIndex = 0;

            // 修正 bool? 類型的處理
            AddInfoRow(grid, "當沖:", contract.DayTrade?.ToString() ?? "N/A", rowIndex++);
            AddInfoRow(grid, "融資餘額:", contract.MarginTradingBalance?.ToString("N0") ?? "N/A", rowIndex++);
            AddInfoRow(grid, "融券餘額:", contract.ShortSellingBalance?.ToString("N0") ?? "N/A", rowIndex++);

            return grid;
        }

        private Grid CreateIndexInfoPanel(ContractSearchResult contract)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var rowIndex = 0;

            AddInfoRow(grid, "指數值:", contract.Reference?.ToString("F2") ?? "N/A", rowIndex++);
            AddInfoRow(grid, "計算方式:", "加權平均", rowIndex++);

            return grid;
        }

        private Grid CreateQueryInfoPanel(ContractSearchResult contract)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var rowIndex = 0;

            // 🔧 修正: 直接處理 DateTime 類型 (檢查是否為 default 值)
            var queryTimeText = contract.QueryTime == default
                ? "N/A"
                : contract.QueryTime.ToString("yyyy-MM-dd HH:mm:ss");
            AddInfoRow(grid, "查詢時間:", queryTimeText, rowIndex++);
            AddInfoRow(grid, "查詢關鍵字:", contract.QueryKeyword ?? "N/A", rowIndex++);

            return grid;
        }

        private void AddInfoRow(Grid grid, string label, string value, int row, string valueColor = "#2C3E50")
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var labelBlock = new TextBlock
            {
                Text = label,
                Style = (Style)FindResource("LabelStyle")
            };
            Grid.SetRow(labelBlock, row);
            Grid.SetColumn(labelBlock, 0);
            grid.Children.Add(labelBlock);

            var valueBlock = new TextBlock
            {
                Text = value,
                Style = (Style)FindResource("ValueStyle"),
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(valueColor))
            };
            Grid.SetRow(valueBlock, row);
            Grid.SetColumn(valueBlock, 1);
            grid.Children.Add(valueBlock);
        }

        #region 按鈕事件處理

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var exportData = GenerateExportData();

                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "文字檔案 (*.txt)|*.txt|所有檔案 (*.*)|*.*",
                    DefaultExt = "txt",
                    FileName = $"合約詳細資訊_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    System.IO.File.WriteAllText(saveDialog.FileName, exportData, Encoding.UTF8);
                    MessageBox.Show($"資料已匯出至：{saveDialog.FileName}", "匯出成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"匯出失敗：{ex.Message}", "錯誤",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyAllButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var exportData = GenerateExportData();
                Clipboard.SetText(exportData);
                MessageBox.Show("資料已複製到剪貼簿", "複製成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"複製失敗：{ex.Message}", "錯誤",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        #endregion

        #region 輔助方法

        private string GenerateExportData()
        {
            var export = new StringBuilder();
            export.AppendLine("=== 合約詳細資訊匯出 ===");
            export.AppendLine($"匯出時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            export.AppendLine($"合約數量: {_contracts.Count}");
            export.AppendLine();

            foreach (var contract in _contracts)
            {
                export.AppendLine($"=== {contract.Symbol} - {contract.Name} ===");
                export.AppendLine();

                // 基本資訊
                export.AppendLine("【基本資訊】");
                export.AppendLine($"商品代號: {contract.Symbol}");
                export.AppendLine($"商品名稱: {contract.Name ?? "N/A"}");
                export.AppendLine($"交易所: {contract.Exchange}");
                export.AppendLine($"商品類型: {contract.ProductType}");

                // 🔧 修正: 直接處理 DateTime 類型 (檢查是否為 default 值)
                var analyzedAtText = contract.AnalyzedAt == default
                    ? "N/A"
                    : contract.AnalyzedAt.ToString("yyyy-MM-dd HH:mm:ss");
                export.AppendLine($"分析時間: {analyzedAtText}");
                export.AppendLine();

                // 價格資訊
                export.AppendLine("【價格資訊】");
                export.AppendLine($"參考價: {contract.Reference?.ToString("F2") ?? "N/A"}");
                export.AppendLine($"漲停價: {contract.LimitUp?.ToString("F2") ?? "N/A"}");
                export.AppendLine($"跌停價: {contract.LimitDown?.ToString("F2") ?? "N/A"}");
                export.AppendLine();

                // 特殊資訊
                switch (contract.ProductType)
                {
                    case "Futures":
                        export.AppendLine("【期貨資訊】");
                        export.AppendLine($"合約月份: {contract.ContractMonthDisplay}");
                        export.AppendLine($"標的代號: {contract.TargetCode ?? "N/A"}");
                        export.AppendLine($"標的代號: {contract.ActualContractCode ?? "N/A"}");
                        export.AppendLine($"到期月份: {contract.DeliveryMonth ?? "N/A"}");
                        break;

                    case "Options":
                        export.AppendLine("【選擇權資訊】");
                        export.AppendLine($"履約價: {contract.StrikePrice?.ToString("F2") ?? "N/A"}");
                        export.AppendLine($"權利類型: {contract.OptionRight ?? "N/A"}");
                        export.AppendLine($"到期月份: {contract.DeliveryMonth ?? "N/A"}");
                        export.AppendLine($"標的種類: {contract.UnderlyingKind ?? "N/A"}");
                        break;

                    case "Stocks":
                        export.AppendLine("【股票資訊】");
                        export.AppendLine($"當沖: {contract.DayTrade?.ToString() ?? "N/A"}");
                        export.AppendLine($"融資餘額: {contract.MarginTradingBalance?.ToString("N0") ?? "N/A"}");
                        export.AppendLine($"融券餘額: {contract.ShortSellingBalance?.ToString("N0") ?? "N/A"}");
                        break;
                }

                export.AppendLine();

                // 查詢資訊
                export.AppendLine("【查詢資訊】");

                // 🔧 修正: 直接處理 DateTime 類型 (檢查是否為 default 值)
                var queryTimeText = contract.QueryTime == default
                    ? "N/A"
                    : contract.QueryTime.ToString("yyyy-MM-dd HH:mm:ss");
                export.AppendLine($"查詢時間: {queryTimeText}");
                export.AppendLine($"查詢關鍵字: {contract.QueryKeyword ?? "N/A"}");
                export.AppendLine();
                export.AppendLine("".PadRight(50, '-'));
                export.AppendLine();
            }

            return export.ToString();
        }

        #endregion
    }
}