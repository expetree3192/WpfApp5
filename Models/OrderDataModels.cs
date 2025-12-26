using System;
using System.Text;
using System.Runtime.CompilerServices;
using Sinopac.Shioaji;
using WpfApp5.Utils;
using WpfApp5.Services;
using System.Collections.Generic;

namespace WpfApp5.Models
{
    #region 🚀 統一的委託成交回報資料結構

    // 統一的委託回報資料結構 - 直接 dynamic 存取
    public class UnifiedOrderReport
    {
        // ═══════════════════════════════════════
        // Operation 操作資訊 (證券+期貨共用)
        // ═══════════════════════════════════════
        public string OpType { get; set; } = string.Empty;
        public string OpCode { get; set; } = string.Empty;
        public string OpMsg { get; set; } = string.Empty;

        // ═══════════════════════════════════════
        // Order 委託資訊 (證券+期貨共用)
        // ═══════════════════════════════════════
        public string Id { get; set; } = string.Empty;
        public string Seqno { get; set; } = string.Empty;
        public string Ordno { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public long Price { get; set; } = 0L;
        public long Quantity { get; set; } = 0L;
        public string OrderType { get; set; } = string.Empty;
        public string PriceType { get; set; } = string.Empty;

        // ═══════════════════════════════════════
        // Account 帳號資訊 (證券+期貨共用)
        // ═══════════════════════════════════════
        public string AccountType { get; set; } = string.Empty;
        public string PersonId { get; set; } = string.Empty;
        public string BrokerId { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public bool Signed { get; set; }

        // ═══════════════════════════════════════
        // Status 狀態資訊 (證券+期貨共用)
        // ═══════════════════════════════════════
        public long ExchangeTs { get; set; } = 0L;
        public long ModifiedPrice { get; set; } = 0L;
        public long CancelQuantity { get; set; } = 0L;
        public long OrderQuantity { get; set; } = 0L;
        public string WebId { get; set; } = string.Empty;
        public string FormattedExchangeTs => _formattedExchangeTs ??= DataTypeConverter.FormatUnixTimestamp(ExchangeTs);

        // ═══════════════════════════════════════
        // Contract 合約資訊 (證券+期貨共用)
        // ═══════════════════════════════════════
        public string SecurityType { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string FullCode { get; set; } = string.Empty;
        public string ActuralCode { get; set; } = string.Empty;
        public string Exchange { get; set; } = string.Empty;

        // 證券專用欄位
        public string Symbol { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public string OrderCond { get; set; } = string.Empty;
        public string OrderLot { get; set; } = string.Empty;
        public string CustomField { get; set; } = string.Empty;

        // 期貨專用欄位
        public string DeliveryMonth { get; set; } = string.Empty;
        public string DeliveryDate { get; set; } = string.Empty;
        public long StrikePrice { get; set; } = 0L;
        public string OptionRight { get; set; } = string.Empty;
        public string MarketType { get; set; } = string.Empty;
        public string OcType { get; set; } = string.Empty;
        public string Subaccount { get; set; } = string.Empty;
        public bool Combo { get; set; }

        // ═══════════════════════════════════════
        // 🚀 快取計算屬性（避免重複計算）
        // ═══════════════════════════════════════
        private bool? _isSuccess;
        private bool? _isBuy;
        private bool? _isSell;
        private bool? _isStock;
        private bool? _isFutures;
        private bool? _isOption;
        private bool? _isNewOrder;
        private bool? _isCancel;
        private bool? _isModify;
        private string? _formattedExchangeTs;

        public bool IsSuccess => _isSuccess ??= (OpCode == "00");
        public bool IsBuy => _isBuy ??= Action.Equals("Buy", StringComparison.OrdinalIgnoreCase);
        public bool IsSell => _isSell ??= Action.Equals("Sell", StringComparison.OrdinalIgnoreCase);
        public bool IsStock => _isStock ??= (SecurityType == "STK");
        public bool IsFutures => _isFutures ??= (SecurityType == "FUT");
        public bool IsOption => _isOption ??= (SecurityType == "OPT");
        public bool IsNewOrder => _isNewOrder ??= (OpType == "New");
        public bool IsCancel => _isCancel ??= (OpType == "Cancel");
        public bool IsModify => _isModify ??= (OpType == "UpdatePrice" || OpType == "UpdateQty");

        // 🚀 便利屬性：提供 double 和 int 版本（需要時才轉換）
        public double PriceAsDouble => Price;
        public int QuantityAsInt => (int)Quantity;
        public double ModifiedPriceAsDouble => ModifiedPrice;
        public int CancelQuantityAsInt => (int)CancelQuantity;
        public int OrderQuantityAsInt => (int)OrderQuantity;
        public double StrikePriceAsDouble => StrikePrice;

        /// <summary>
        /// 🚀 重置快取（當關鍵屬性變更時調用）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResetCache()
        {
            _isSuccess = null;
            _isBuy = null;
            _isSell = null;
            _isStock = null;
            _isFutures = null;
            _isOption = null;
            _isNewOrder = null;
            _isCancel = null;
            _isModify = null;
            _formattedExchangeTs = null;
        }

        public override string ToString()
        {
            return $"[{SecurityType}] {Action} {Quantity} @ {Price} | {OpType}({OpCode}) | {Code}";
        }
    }

    /// <summary>
    /// 統一的成交回報資料結構 - 終極高效能版（直接 dynamic 存取，零開銷）
    /// </summary>
    public class UnifiedDealReport
    {
        // ═══════════════════════════════════════
        // 基本成交資訊 (證券+期貨共用)
        // ═══════════════════════════════════════
        public string TradeId { get; set; } = string.Empty;
        public string Seqno { get; set; } = string.Empty;
        public string Ordno { get; set; } = string.Empty;
        public string ExchangeSeq { get; set; } = string.Empty;
        public string BrokerId { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string FullCode { get; set; } = string.Empty;
        public string ActuralCode { get; set; } = string.Empty;
        public long Price { get; set; } = 0L;
        public long Quantity { get; set; } = 0L;
        public string WebId { get; set; } = string.Empty;
        public double Ts { get; set; } = 0.0;
        public string FormattedTs => _formattedTs ??= DataTypeConverter.FormatUnixTimestamp(Ts);

        // ═══════════════════════════════════════
        // 證券專用欄位
        // ═══════════════════════════════════════
        public string OrderCond { get; set; } = string.Empty;
        public string OrderLot { get; set; } = string.Empty;
        public string CustomField { get; set; } = string.Empty;

        // ═══════════════════════════════════════
        // 期貨專用欄位
        // ═══════════════════════════════════════
        public string Subaccount { get; set; } = string.Empty;
        public string SecurityType { get; set; } = string.Empty;
        public string DeliveryMonth { get; set; } = string.Empty;
        public long StrikePrice { get; set; } = 0L;
        public string OptionRight { get; set; } = string.Empty;
        public string MarketType { get; set; } = string.Empty;
        public bool Combo { get; set; }

        // ═══════════════════════════════════════
        // 🚀 快取計算屬性（避免重複計算）
        // ═══════════════════════════════════════
        private bool? _isBuy;
        private bool? _isSell;
        private bool? _isStock;
        private bool? _isFutures;
        private bool? _isOption;
        private long? _tsAsLong;
        private string? _formattedTs;

        public bool IsBuy => _isBuy ??= Action.Equals("Buy", StringComparison.OrdinalIgnoreCase);
        public bool IsSell => _isSell ??= Action.Equals("Sell", StringComparison.OrdinalIgnoreCase);
        public bool IsStock => _isStock ??= (string.IsNullOrEmpty(SecurityType) || SecurityType == "STK");
        public bool IsFutures => _isFutures ??= (SecurityType == "FUT");
        public bool IsOption => _isOption ??= (SecurityType == "OPT");

        // 🚀 便利屬性：提供 double 和 int 版本（需要時才轉換）
        public double PriceAsDouble => Price;
        public int QuantityAsInt => (int)Quantity;
        public double StrikePriceAsDouble => StrikePrice;

        // 🚀 時間戳記轉換（只在需要時才轉換）
        public long TsAsLong => _tsAsLong ??= Convert.ToInt64(Ts);

        /// <summary>
        /// 🚀 重置快取（當關鍵屬性變更時調用）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResetCache()
        {
            _isBuy = null;
            _isSell = null;
            _isStock = null;
            _isFutures = null;
            _isOption = null;
            _tsAsLong = null;
            _formattedTs = null;
        }

        public override string ToString()
        {
            var type = IsStock ? "STK" : SecurityType;
            return $"[{type}] {Action} {Code} @ {Price} * {Quantity}";
        }
    }

    #endregion

    #region 🚀 委託統計更新事件資料結構（終極高效能版）

    /// <summary>
    /// 委託統計更新事件參數 - 終極高效能版（直接 dynamic 存取，零開銷）
    /// </summary>
    public class OrderStatsUpdateEventArgs
    {
        public OrderState OrderState { get; set; }
        public List<string> WindowIds { get; set; } = [];
        public string ContractCode { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string OpType { get; set; } = string.Empty;
        public string OpCode { get; set; } = string.Empty;
        public long Quantity { get; set; } = 0L;
        public long CancelQuantity { get; set; } = 0L;
        public long Price { get; set; } = 0L;
        public string Seqno { get; set; } = string.Empty;
        public string Ordno { get; set; } = string.Empty;
        public string CustomField { get; set; } = string.Empty;

        // 🚀 快取計算屬性
        private bool? _isSuccess;
        private bool? _isBuy;
        private bool? _isSell;
        private bool? _isOrderReport;
        private bool? _isDealReport;

        public bool IsSuccess => _isSuccess ??= (OpCode == "00");
        public bool IsBuy => _isBuy ??= Action.Equals("Buy", StringComparison.OrdinalIgnoreCase);
        public bool IsSell => _isSell ??= Action.Equals("Sell", StringComparison.OrdinalIgnoreCase);
        public bool IsOrderReport => _isOrderReport ??= (OrderState == OrderState.StockOrder || OrderState == OrderState.FuturesOrder);
        public bool IsDealReport => _isDealReport ??= (OrderState == OrderState.StockDeal || OrderState == OrderState.FuturesDeal);

        // 🚀 便利屬性：提供 double 和 int 版本（需要時才轉換）
        public double PriceAsDouble => Price;
        public int QuantityAsInt => (int)Quantity;
        public int CancelQuantityAsInt => (int)CancelQuantity;

        /// <summary>
        /// 🚀 從 orderData 建立事件參數（終極高效能版 - 直接 dynamic 存取）
        /// </summary>
        public static OrderStatsUpdateEventArgs? CreateFromOrderData(
            OrderState orderState,
            dynamic orderData,
            List<string> windowIds,
            string? seqno = null,
            string? ordno = null,
            string? customField = null)
        {
            try
            {
                var args = new OrderStatsUpdateEventArgs
                {
                    OrderState = orderState,
                    WindowIds = windowIds,
                    Seqno = seqno ?? string.Empty,
                    Ordno = ordno ?? string.Empty,
                    CustomField = customField ?? string.Empty
                };

                // 🚀 根據回報類型提取資料（直接 dynamic 存取）
                switch (orderState)
                {
                    case OrderState.StockOrder:
                    case OrderState.FuturesOrder:
                        // 委託回報 - 直接存取
                        var orderReport = UltimatePerformanceConverter.ConvertOrderReport(orderData);
                        args.Action = orderReport.Action;
                        args.OpType = orderReport.OpType;
                        args.OpCode = orderReport.OpCode;
                        args.Quantity = orderReport.Quantity;
                        args.CancelQuantity = orderReport.CancelQuantity;
                        args.Price = orderReport.Price;
                        args.ContractCode = orderReport.ActuralCode;
                        break;

                    case OrderState.StockDeal:
                    case OrderState.FuturesDeal:
                        // 成交回報 - 直接存取
                        var dealReport = UltimatePerformanceConverter.ConvertDealReport(orderData);
                        args.Action = dealReport.Action;
                        args.OpType = "Deal";
                        args.OpCode = "00";
                        args.Quantity = dealReport.Quantity;
                        args.Price = dealReport.Price;
                        args.ContractCode = dealReport.ActuralCode;
                        break;

                    default:
                        return null;
                }

                return args;
            }
            catch (Exception ex)
            {
                LogService.Instance?.LogError(ex, "建立委託統計更新事件參數失敗", "OrderStatsUpdateEventArgs");
                return null;
            }
        }

        public override string ToString()
        {
            return $"[{OrderState}] {Action} {ContractCode} @ {Price}*{Quantity} | {OpType}({OpCode}) | 視窗數:{WindowIds.Count}";
        }
    }

    #endregion

    #region 🚀 終極高效能轉換器（直接 dynamic 存取，零開銷）

    /// <summary>
    /// 🚀 終極高效能轉換器 - 直接 dynamic 存取，零開銷
    /// </summary>
    public static class UltimatePerformanceConverter
    {
        /// <summary>
        /// 🚀 轉換委託回報資料（終極高效能版 - 直接 dynamic 存取）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UnifiedOrderReport ConvertOrderReport(dynamic orderData)
        {
            var result = new UnifiedOrderReport();

            try
            {
                var operation = orderData.operation;
                var order = orderData.order;
                var status = orderData.status;
                var contract = orderData.contract;

                // Operation 操作資訊 - 🚀 直接存取
                result.OpType = operation.op_type ?? string.Empty;
                result.OpCode = operation.op_code ?? string.Empty;
                result.OpMsg = operation.op_msg ?? string.Empty;

                // Order 委託資訊 - 🚀 直接存取
                result.Id = order.id ?? string.Empty;
                result.Seqno = order.seqno ?? string.Empty;
                result.Ordno = order.ordno ?? string.Empty;
                result.Action = order.action ?? string.Empty;
                result.Price = order.price ?? 0L;
                result.Quantity = order.quantity ?? 0L;
                result.OrderType = order.order_type ?? string.Empty;
                result.PriceType = order.price_type ?? string.Empty;
                result.CustomField = order.custom_field ?? string.Empty;
                result.OrderCond = order.order_cond ?? string.Empty;
                result.OrderLot = order.order_lot ?? string.Empty;
                result.MarketType = order.market_type ?? string.Empty;
                result.OcType = order.oc_type ?? string.Empty;
                result.Subaccount = order.subaccount ?? string.Empty;
                result.Combo = order.combo ?? false;

                // 帳號資訊 - 🚀 直接存取
                var account = order.account;
                result.AccountType = account.account_type ?? string.Empty;
                result.PersonId = account.person_id ?? string.Empty;
                result.BrokerId = account.broker_id ?? string.Empty;
                result.AccountId = account.account_id ?? string.Empty;
                result.Signed = account.signed ?? false;

                // Status 狀態資訊 - 🚀 直接存取
                result.ExchangeTs = status.exchange_ts ?? 0L;
                result.ModifiedPrice = status.modified_price ?? 0L;
                result.CancelQuantity = status.cancel_quantity ?? 0L;
                result.OrderQuantity = status.order_quantity ?? 0L;
                result.WebId = status.web_id ?? string.Empty;

                // Contract 合約資訊 - 🚀 直接存取
                result.SecurityType = contract.security_type ?? string.Empty;
                result.Code = contract.code ?? string.Empty;
                result.FullCode = contract.full_code ?? string.Empty;
                result.Exchange = contract.exchange ?? string.Empty;
                result.Symbol = contract.symbol ?? string.Empty;
                result.Name = contract.name ?? string.Empty;
                result.Currency = contract.currency ?? string.Empty;
                result.DeliveryMonth = contract.delivery_month ?? string.Empty;
                result.DeliveryDate = contract.delivery_date ?? string.Empty;
                result.StrikePrice = contract.strike_price ?? 0L;
                result.OptionRight = contract.option_right ?? string.Empty;

                // ActualCode 計算
                result.ActuralCode = CalculateActualCode(result.Code, result.SecurityType, result.DeliveryMonth, result.FullCode);

                return result;
            }
            catch (Exception ex)
            {
                LogService.Instance?.LogError(ex, "終極高效能委託回報轉換失敗", "UltimatePerformanceConverter");
                return new UnifiedOrderReport();
            }
        }

        // 轉換成交回報資料（直接 dynamic 存取）
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UnifiedDealReport ConvertDealReport(dynamic orderData)
        {
            var result = new UnifiedDealReport();

            try
            {
                // 直接存取
                result.TradeId = orderData.trade_id ?? string.Empty;
                result.Seqno = orderData.seqno ?? string.Empty;
                result.Ordno = orderData.ordno ?? string.Empty;
                result.ExchangeSeq = orderData.exchange_seq ?? string.Empty;
                result.BrokerId = orderData.broker_id ?? string.Empty;
                result.AccountId = orderData.account_id ?? string.Empty;
                result.Action = orderData.action ?? string.Empty;
                result.Code = orderData.code ?? string.Empty;
                result.FullCode = orderData.full_code ?? string.Empty;
                result.Price = orderData.price ?? 0L;
                result.Quantity = orderData.quantity ?? 0L;
                result.WebId = orderData.web_id ?? string.Empty;
                result.Ts = orderData.ts ?? 0.0;

                // 證券專用欄位 - 直接存取
                result.OrderCond = orderData.order_cond ?? string.Empty;
                result.OrderLot = orderData.order_lot ?? string.Empty;
                result.CustomField = orderData.custom_field ?? string.Empty;

                // 期貨專用欄位 - 直接存取
                result.Subaccount = orderData.subaccount ?? string.Empty;
                result.SecurityType = orderData.security_type ?? string.Empty;
                result.DeliveryMonth = orderData.delivery_month ?? string.Empty;
                result.StrikePrice = orderData.strike_price ?? 0L;
                result.OptionRight = orderData.option_right ?? string.Empty;
                result.MarketType = orderData.market_type ?? string.Empty;
                result.Combo = orderData.combo ?? false;

                // ActualCode 計算
                result.ActuralCode = CalculateActualCode(result.Code, result.SecurityType, result.DeliveryMonth, result.FullCode);

                return result;
            }
            catch (Exception ex)
            {
                LogService.Instance?.LogError(ex, "終極高效能成交回報轉換失敗", "UltimatePerformanceConverter");
                return new UnifiedDealReport();
            }
        }

        // ActualCode 計算
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string CalculateActualCode(string code, string securityType, string deliveryMonth, string fullCode)
        {
            // 優先使用 FullCode，如果為空則使用 Code
            return !string.IsNullOrEmpty(fullCode) ? fullCode : code ?? string.Empty;
        }
    }

    #endregion

    #region 委託回報資料打包類別

    // 委託回報資料打包類別（使用轉換器 + 快取機制）
    public class OrderDataInfo
    {
        public OrderState? State { get; set; }
        public dynamic? OrderData { get; set; }
        public string FormattedText { get; set; }
        public bool IsError { get; set; }
        public string? ErrorType { get; set; }
        public string? ErrorMessage { get; set; }

        // 快取轉換後的統一資料結構（避免重複轉換）
        private UnifiedOrderReport? _orderReportCache;
        private UnifiedDealReport? _dealReportCache;

        private OrderDataInfo(OrderState? orderState, dynamic? orderData, string formattedText,
            bool isError = false, string? errorType = null, string? errorMessage = null)
        {
            State = orderState;
            OrderData = orderData;
            FormattedText = formattedText;
            IsError = isError;
            ErrorType = errorType;
            ErrorMessage = errorMessage;
        }

        // 一行指令:打印完整資料(委託回報或錯誤訊息) - 使用 LogService
        public void PrintToLog(string source, LogDisplayTarget target = LogDisplayTarget.Default)
        {
            if (IsError)
            {
                LogService.Instance?.LogError(new Exception(ErrorMessage ?? "未知錯誤"), FormattedText, source, target);
            }
            else
            {
                LogService.Instance?.LogInfo(FormattedText, source, target);
            }
        }

        // 靜態方法：從 orderData 建立 OrderDataInfo（正常回報）
        public static OrderDataInfo Create(OrderState orderState, dynamic orderData)
        {
            var formattedText = FormatOrderData(orderState, orderData);
            return new OrderDataInfo(orderState, orderData, formattedText, isError: false);
        }

        // 靜態方法：建立錯誤訊息的 OrderDataInfo
        public static OrderDataInfo CreateError(string errorType, string errorMessage)
        {
            var formattedText = FormatError(errorType, errorMessage);
            return new OrderDataInfo(null, null, formattedText, isError: true, errorType: errorType, errorMessage: errorMessage);
        }

        // 取得統一委託回報資料（快取機制 - 只轉換一次，使用轉換器）
        public UnifiedOrderReport GetOrderReport()
        {
            if (_orderReportCache == null && OrderData != null)
            {
                _orderReportCache = UltimatePerformanceConverter.ConvertOrderReport(OrderData);
            }
            return _orderReportCache ?? new UnifiedOrderReport();
        }

        // 取得統一成交回報資料（快取機制 - 只轉換一次，使用轉換器）
        public UnifiedDealReport GetDealReport()
        {
            if (_dealReportCache == null && OrderData != null)
            {
                _dealReportCache = UltimatePerformanceConverter.ConvertDealReport(OrderData);
            }
            return _dealReportCache ?? new UnifiedDealReport();
        }

        // 格式化錯誤訊息
        private static string FormatError(string errorType, string errorMessage)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine($"❌ [委託錯誤] {errorType}");
            sb.AppendLine($"  └─ 錯誤訊息: {errorMessage}");
            sb.AppendLine("═══════════════════════════════════════");
            return sb.ToString();
        }

        // 格式化 orderData 為字串（使用轉換器）
        private static string FormatOrderData(OrderState orderState, dynamic orderData)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"[委託回報] ==================== {orderState} ====================");

                switch (orderState)
                {
                    case OrderState.StockOrder:
                    case OrderState.FuturesOrder:
                        FormatOrderReport(sb, orderData, orderState);
                        break;

                    case OrderState.StockDeal:
                    case OrderState.FuturesDeal:
                        FormatDealReport(sb, orderData, orderState);
                        break;
                }

                sb.AppendLine("[委託回報] ========================================================");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"[委託回報] ⚠️ 格式化 orderData 失敗: {ex.Message}";
            }
        }

        // 格式化委託回報（一次轉換 + 直接顯示）
        private static void FormatOrderReport(StringBuilder sb, dynamic orderData, OrderState orderState)
        {
            try
            {
                // 一次轉換，使用轉換器
                var data = UltimatePerformanceConverter.ConvertOrderReport(orderData);
                var reportType = orderState == OrderState.StockOrder ? "證券" : "期貨";

                sb.AppendLine($"【委託回報 - {reportType}】");

                // Operation 資訊
                sb.AppendLine("┌─ [Operation 操作資訊]");
                sb.AppendLine($"│  ├─ op_type: {data.OpType}");
                sb.AppendLine($"│  ├─ op_code: {data.OpCode}");
                sb.AppendLine($"│  └─ op_msg: {data.OpMsg}");

                // Order 委託資訊
                sb.AppendLine("├─ [Order 委託資訊]");
                sb.AppendLine($"│  ├─ id: {data.Id}");
                sb.AppendLine($"│  ├─ seqno: {data.Seqno}");
                sb.AppendLine($"│  ├─ ordno: {data.Ordno}");
                sb.AppendLine($"│  ├─ action: {data.Action}");
                sb.AppendLine($"│  ├─ price: {data.Price}");
                sb.AppendLine($"│  ├─ quantity: {data.Quantity}");
                sb.AppendLine($"│  ├─ order_type: {data.OrderType}");
                sb.AppendLine($"│  ├─ price_type: {data.PriceType}");

                // 證券專用欄位
                if (data.IsStock)
                {
                    sb.AppendLine($"│  ├─ order_cond: {data.OrderCond}");
                    sb.AppendLine($"│  ├─ order_lot: {data.OrderLot}");
                    sb.AppendLine($"│  └─ custom_field: {data.CustomField}");
                }
                // 期貨專用欄位
                else
                {
                    sb.AppendLine($"│  ├─ market_type: {data.MarketType}");
                    sb.AppendLine($"│  ├─ oc_type: {data.OcType}");
                    sb.AppendLine($"│  ├─ subaccount: {data.Subaccount}");
                    sb.AppendLine($"│  └─ combo: {data.Combo}");
                }

                // Account 帳戶資訊
                sb.AppendLine("├─ [Account 帳戶資訊]");
                sb.AppendLine($"│  ├─ account_type: {data.AccountType}");
                sb.AppendLine($"│  ├─ person_id: {data.PersonId}");
                sb.AppendLine($"│  ├─ broker_id: {data.BrokerId}");
                sb.AppendLine($"│  ├─ account_id: {data.AccountId}");
                sb.AppendLine($"│  └─ signed: {data.Signed}");

                // Status 狀態資訊
                sb.AppendLine("├─ [Status 狀態資訊]");
                sb.AppendLine($"│  ├─ exchange_ts: {data.ExchangeTs} ({data.FormattedExchangeTs})");
                sb.AppendLine($"│  ├─ modified_price: {data.ModifiedPrice}");
                sb.AppendLine($"│  ├─ cancel_quantity: {data.CancelQuantity}");
                sb.AppendLine($"│  ├─ order_quantity: {data.OrderQuantity}");
                sb.AppendLine($"│  └─ web_id: {data.WebId}");

                // Contract 合約資訊
                sb.AppendLine("└─ [Contract 合約資訊]");
                sb.AppendLine($"   ├─ security_type: {data.SecurityType}");
                sb.AppendLine($"   ├─ exchange: {data.Exchange}");
                sb.AppendLine($"   ├─ code: {data.Code}");
                sb.AppendLine($"   ├─ full_code: {data.FullCode}");

                if (data.IsStock)
                {
                    sb.AppendLine($"   ├─ symbol: {data.Symbol}");
                    sb.AppendLine($"   ├─ name: {data.Name}");
                    sb.AppendLine($"   └─ currency: {data.Currency}");
                }
                else
                {
                    sb.AppendLine($"   ├─ delivery_month: {data.DeliveryMonth}");
                    sb.AppendLine($"   ├─ delivery_date: {data.DeliveryDate}");
                    sb.AppendLine($"   ├─ strike_price: {data.StrikePrice}");
                    sb.AppendLine($"   └─ option_right: {data.OptionRight}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  └─ ⚠️ 格式化委託回報失敗: {ex.Message}");
            }
        }

        // 格式化成交回報（一次轉換 + 直接顯示）
        private static void FormatDealReport(StringBuilder sb, dynamic orderData, OrderState orderState)
        {
            try
            {
                // 一次轉換，使用轉換器
                var data = UltimatePerformanceConverter.ConvertDealReport(orderData);
                var reportType = orderState == OrderState.StockDeal ? "證券" : "期貨";

                sb.AppendLine($"【成交回報 - {reportType}】");
                sb.AppendLine($"  ├─ trade_id: {data.TradeId}");
                sb.AppendLine($"  ├─ seqno: {data.Seqno}");
                sb.AppendLine($"  ├─ ordno: {data.Ordno}");
                sb.AppendLine($"  ├─ exchange_seq: {data.ExchangeSeq}");
                sb.AppendLine($"  ├─ broker_id: {data.BrokerId}");
                sb.AppendLine($"  ├─ account_id: {data.AccountId}");
                sb.AppendLine($"  ├─ action: {data.Action}");
                sb.AppendLine($"  ├─ code: {data.Code}");
                sb.AppendLine($"  ├─ full_code: {data.FullCode}");
                sb.AppendLine($"  ├─ price: {data.Price}");
                sb.AppendLine($"  ├─ quantity: {data.Quantity}");
                sb.AppendLine($"  ├─ web_id: {data.WebId}");

                // 證券專用欄位
                if (data.IsStock)
                {
                    sb.AppendLine($"  ├─ order_cond: {data.OrderCond}");
                    sb.AppendLine($"  ├─ order_lot: {data.OrderLot}");
                    sb.AppendLine($"  └─ custom_field: {data.CustomField}");
                }
                // 期貨專用欄位
                else
                {
                    sb.AppendLine($"  ├─ subaccount: {data.Subaccount}");
                    sb.AppendLine($"  ├─ security_type: {data.SecurityType}");
                    sb.AppendLine($"  ├─ delivery_month: {data.DeliveryMonth}");
                    sb.AppendLine($"  ├─ strike_price: {data.StrikePrice}");
                    sb.AppendLine($"  ├─ option_right: {data.OptionRight}");
                    sb.AppendLine($"  ├─ market_type: {data.MarketType}");
                    sb.AppendLine($"  └─ combo: {data.Combo}");
                }

                sb.AppendLine($"  └─ ts: {data.Ts} ({data.FormattedTs})");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  └─ ⚠️ 格式化成交回報失敗: {ex.Message}");
            }
        }
    }

    #endregion
}
