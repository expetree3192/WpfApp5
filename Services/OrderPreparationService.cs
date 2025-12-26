using System;
using System.Diagnostics.Contracts;
using System.Linq;
using Sinopac.Shioaji;
using WpfApp5.Services.Common;
using SJAction = Sinopac.Shioaji.Action;

namespace WpfApp5.Services
{
    // 下單參數準備服務 - 即時準備版本
    public class OrderPreparationService
    {
        #region 預先準備的參數

        private IContract? _preparedContract;
        private Account? _preparedAccount;
        private readonly string _windowId;

        // 預先準備好的 Order 物件（隨 UI 變化即時更新）
        private IOrder? _preparedBuyOrder;   // 買單
        private IOrder? _preparedSellOrder;  // 賣單

        // 通用參數
        private decimal _preparedPrice = 0;
        private int _preparedQuantity = 1;
        private OrderType _preparedOrderType = OrderType.ROD;

        // 證券專屬參數
        private StockPriceType _preparedStockPriceType = StockPriceType.LMT;
        private StockOrderCond _preparedOrderCond = StockOrderCond.Cash;
        private StockOrderLot _preparedOrderLot = StockOrderLot.Common;
        private DayTradeShort _preparedDayTradeShort = DayTradeShort.No;

        // 期貨專屬參數
        private FuturePriceType _preparedFuturePriceType = FuturePriceType.LMT;
        private OCType _preparedOcType = OCType.Auto;

        #endregion

        #region 建構函式

        public OrderPreparationService(string windowId)
        {
            if (string.IsNullOrWhiteSpace(windowId))
            {
                throw new ArgumentException("WindowId 不可為空", nameof(windowId));
            }

            _windowId = windowId;
        }

        #endregion

        #region 更新參數的方法（每次更新都會重建 Order）

        // 更新合約（當訂閱商品時呼叫）
        public void UpdateContract(IContract? contract)
        {
            _preparedContract = contract;
            RebuildOrders();  // 立即重建 Order
        }

        // 更新帳戶
        public void UpdateAccount(Account? account)
        {
            _preparedAccount = account;
            RebuildOrders();  // 立即重建 Order
        }

        // 更新價格
        public void UpdatePrice(decimal price)
        {
            _preparedPrice = price;
            RebuildOrders();  // 立即重建 Order
        }

        // 更新數量
        public void UpdateQuantity(int quantity)
        {
            _preparedQuantity = quantity;
            RebuildOrders();  // 立即重建 Order
        }

        // 更新價格類型（LMT/MKT/MKP）
        public void UpdatePriceType(string priceType)
        {
            if (_preparedContract is Stock)
            {
                _preparedStockPriceType = priceType.ToUpper() switch
                {
                    "LMT" => StockPriceType.LMT,
                    "MKT" => StockPriceType.MKT,
                    "MKP" => StockPriceType.MKT,
                    _ => StockPriceType.LMT
                };
            }
            else if (_preparedContract is Future || _preparedContract is Option || _preparedContract is Sinopac.Shioaji.Index)
            {
                _preparedFuturePriceType = priceType.ToUpper() switch
                {
                    "LMT" => FuturePriceType.LMT,
                    "MKT" => FuturePriceType.MKT,
                    "MKP" => FuturePriceType.MKP,
                    _ => FuturePriceType.LMT
                };
            }

            RebuildOrders();  // 立即重建 Order
        }

        // 更新委託類型（ROD/IOC/FOK）
        public void UpdateOrderType(string orderType)
        {
            _preparedOrderType = orderType.ToUpper() switch
            {
                "ROD" => OrderType.ROD,
                "IOC" => OrderType.IOC,
                "FOK" => OrderType.FOK,
                _ => OrderType.ROD
            };

            RebuildOrders();  // 立即重建 Order
        }

        // 更新證券委託條件
        public void UpdateOrderCond(StockOrderCond orderCond)
        {
            _preparedOrderCond = orderCond;
            RebuildOrders();  // 立即重建 Order
        }

        // 更新證券下單類型
        public void UpdateOrderLot(StockOrderLot orderLot)
        {
            _preparedOrderLot = orderLot;
            RebuildOrders();  // 立即重建 Order
        }

        // 更新當沖設定
        public void UpdateDayTrade(bool isDayTradeEnabled)
        {
            if (_preparedContract is Stock)
            {
                _preparedDayTradeShort = isDayTradeEnabled ? DayTradeShort.Yes : DayTradeShort.No;
            }
            else if (_preparedContract is Future || _preparedContract is Option)
            {
                _preparedOcType = isDayTradeEnabled ? OCType.DayTrade : OCType.Auto;
            }

            RebuildOrders();  // 立即重建 Order
        }

        // 更新期貨倉位類型
        public void UpdateOcType(string ocType)
        {
            _preparedOcType = ocType.ToUpper() switch
            {
                "AUTO" => OCType.Auto,
                "NEW" => OCType.New,
                "COVER" => OCType.Cover,
                "DAYTRADE" => OCType.DayTrade,
                _ => OCType.Auto
            };

            RebuildOrders();  // 立即重建 Order
        }

        #endregion

        #region 共用函數

        // 驗證必要參數
        private string? ValidateParameters()
        {
            if (_preparedContract == null)
            {
                return "合約未設定";
            }

            if (_preparedAccount == null)
            {
                return "帳戶未設定";
            }

            return null; // null 表示驗證成功
        }

        // 建立證券委託單
        private StockOrder CreateStockOrder(SJAction action, double price, StockPriceType priceType, OrderType orderType, string customField)
        {
            return new StockOrder()
            {
                price = price,
                quantity = _preparedQuantity,  // API需要int
                action = action,
                price_type = priceType,
                order_type = orderType,
                order_lot = _preparedOrderLot,
                order_cond = _preparedOrderCond,
                daytrade_short = _preparedDayTradeShort,
                account = _preparedAccount,
                custom_field = customField
            };
        }

        // 建立期貨選擇權委託單
        private FutOptOrder CreateFutOptOrder(SJAction action, double price, FuturePriceType priceType, OrderType orderType, string customField)
        {
            return new FutOptOrder()
            {
                price = price,
                quantity = _preparedQuantity,  // API需要int
                action = action,
                price_type = priceType,
                order_type = orderType,
                octype = _preparedOcType,
                account = _preparedAccount,
                custom_field = customField
            };
        }

        // 建立 OrderPackage
        private OrderPackage CreateOrderPackage(IOrder order, SJAction action, string trackingId, string priceTypeStr)
        {
            return new OrderPackage
            {
                Contract = _preparedContract!,
                Order = order,
                Action = action.ToString(),
                SecurityType = _preparedContract!.security_type ?? "UNKNOWN",
                TrackingId = trackingId,
                WindowId = _windowId,
                CustomField = order.custom_field,
                PriceType = priceTypeStr
            };
        }

        // Order 建立邏輯
        private ServiceResult<OrderPackage> CreateOrderInternal(
            SJAction action,
            double price,
            string priceTypeStr,
            StockPriceType? stockPriceType = null,
            FuturePriceType? futurePriceType = null,
            OrderType? orderType = null,
            string? trackingPrefix = null)
        {
            // 1- 驗證參數
            var validationError = ValidateParameters();
            if (validationError != null)
            {
                return ServiceResult<OrderPackage>.Failure(validationError);
            }

            try
            {
                // 2- 產生 custom_field 和 tracking_id
                string customField = OrderService.GenerateCustomField();
                string prefix = trackingPrefix ?? action.ToString();
                string trackingId = $"{_windowId}_{prefix}_{_preparedContract!.code}_{DateTime.Now:HH:mm:ss.fff}";

                // 3- 使用預設值
                var finalOrderType = orderType ?? _preparedOrderType;

                // 4- 根據商品類型建立 Order
                IOrder order;
                if (_preparedContract is Stock)
                {
                    var finalStockPriceType = stockPriceType ?? _preparedStockPriceType;
                    order = CreateStockOrder(action, price, finalStockPriceType, finalOrderType, customField);
                }
                else if (_preparedContract is Future || _preparedContract is Option)
                {
                    var finalFuturePriceType = futurePriceType ?? _preparedFuturePriceType;
                    order = CreateFutOptOrder(action, price, finalFuturePriceType, finalOrderType, customField);
                }
                else
                {
                    return ServiceResult<OrderPackage>.Failure($"不支援的商品類型: {_preparedContract.GetType().Name}");
                }

                // 5- 建立 OrderPackage
                var package = CreateOrderPackage(order, action, trackingId, priceTypeStr);

                return ServiceResult<OrderPackage>.Success(
                    package,
                    $"{priceTypeStr} 單已建立: {action} {_preparedContract.code} @ {price} ({priceTypeStr}) (custom_field: {customField})"
                );
            }
            catch (Exception ex)
            {
                return ServiceResult<OrderPackage>.Failure($"建立 Order 失敗: {ex.Message}");
            }
        }

        #endregion

        #region 核心：重建 Order 物件

        // 重建 Order 物件（每次參數變動時呼叫）
        private void RebuildOrders()
        {
            if (_preparedContract == null || _preparedAccount == null)
            {
                _preparedBuyOrder = null;
                _preparedSellOrder = null;
                return;
            }

            try
            {
                // 動態產生 custom_field（每次重建都產生新的 6 字元）
                string customFieldBuy = OrderService.GenerateCustomField();
                string customFieldSell = OrderService.GenerateCustomField();

                if (_preparedContract is Stock)
                {
                    _preparedBuyOrder = CreateStockOrder(SJAction.Buy, (double)_preparedPrice, _preparedStockPriceType, _preparedOrderType, customFieldBuy);
                    _preparedSellOrder = CreateStockOrder(SJAction.Sell, (double)_preparedPrice, _preparedStockPriceType, _preparedOrderType, customFieldSell);
                }
                else if (_preparedContract is Future || _preparedContract is Option)
                {
                    _preparedBuyOrder = CreateFutOptOrder(SJAction.Buy, (double)_preparedPrice, _preparedFuturePriceType, _preparedOrderType, customFieldBuy);
                    _preparedSellOrder = CreateFutOptOrder(SJAction.Sell, (double)_preparedPrice, _preparedFuturePriceType, _preparedOrderType, customFieldSell);
                }
                else
                {
                    _preparedBuyOrder = null;
                    _preparedSellOrder = null;
                }
            }
            catch
            {
                _preparedBuyOrder = null;
                _preparedSellOrder = null;
            }
        }

        #endregion

        #region 快速取得 Order 物件（極速版本）

        // 取得買單（直接回傳預先準備好的 Order）
        public ServiceResult<OrderPackage> GetBuyOrder(decimal? overridePrice = null, string? priceType = null)
        {
            return GetOrder(SJAction.Buy, overridePrice, priceType);
        }

        // 取得賣單（直接回傳預先準備好的 Order）
        public ServiceResult<OrderPackage> GetSellOrder(decimal? overridePrice = null, string? priceType = null)
        {
            return GetOrder(SJAction.Sell, overridePrice, priceType);
        }

        // 核心：取得 Order 物件
        private ServiceResult<OrderPackage> GetOrder(SJAction orderAction, decimal? overridePrice = null, string? priceType = null)
        {
            try
            {
                // 1- 驗證必要參數
                var validationError = ValidateParameters();
                if (validationError != null)
                {
                    return ServiceResult<OrderPackage>.Failure(validationError);
                }

                // 2- 選擇對應的 Order
                IOrder? order = orderAction == SJAction.Buy ? _preparedBuyOrder : _preparedSellOrder;

                if (order == null)
                {
                    return ServiceResult<OrderPackage>.Failure("Order 尚未準備好");
                }

                // 3- 如果有覆寫價格，更新 Order 的價格
                if (overridePrice.HasValue)
                {
                    order.price = (double)overridePrice.Value;
                }

                // 4- 如果有覆寫價格類型，更新 Order 的價格類型
                string finalPriceType = "";
                if (!string.IsNullOrWhiteSpace(priceType))
                {
                    if (order is StockOrder stockOrder)
                    {
                        stockOrder.price_type = priceType.ToUpper() switch
                        {
                            "LMT" => StockPriceType.LMT,
                            "MKT" => StockPriceType.MKT,
                            "MKP" => StockPriceType.MKT,    //  注意：C# API 中 沒有StockPriceType.MKP ，所以要使用 StockPriceType.MKT
                            _ => stockOrder.price_type
                        };
                        finalPriceType = stockOrder.price_type.ToString();
                    }
                    else if (order is FutOptOrder futOptOrder)
                    {
                        futOptOrder.price_type = priceType.ToUpper() switch
                        {
                            "LMT" => FuturePriceType.LMT,
                            "MKT" => FuturePriceType.MKT,
                            "MKP" => FuturePriceType.MKP,
                            _ => futOptOrder.price_type
                        };
                        finalPriceType = futOptOrder.price_type.ToString();
                    }
                }
                else
                {
                    // 沒有覆寫，使用原本的價格類型
                    if (order is StockOrder stockOrder)
                    {
                        finalPriceType = stockOrder.price_type.ToString();
                    }
                    else if (order is FutOptOrder futOptOrder)
                    {
                        finalPriceType = futOptOrder.price_type.ToString();
                    }
                }

                // 5- 產生追蹤 ID 和封裝
                string trackingId = $"{_windowId}_{orderAction}_{_preparedContract!.code}_{DateTime.Now:HH:mm:ss.fff}";
                var package = CreateOrderPackage(order, orderAction, trackingId, finalPriceType);

                return ServiceResult<OrderPackage>.Success(
                    package,
                    $"Order 已建立: {orderAction} {_preparedContract.code} @ {order.price} ({finalPriceType}) (custom_field: {order.custom_field})"
                );
            }
            catch (Exception ex)
            {
                return ServiceResult<OrderPackage>.Failure($"取得 Order 失敗: {ex.Message}");
            }
        }

        #endregion

        #region 市價單專用方法

        // 取得市價買單
        public ServiceResult<OrderPackage> GetMarketBuyOrder()
        {
            return CreateOrderInternal(
                action: SJAction.Buy,
                price: 0,
                priceTypeStr: "MKT",
                stockPriceType: StockPriceType.MKT,
                futurePriceType: FuturePriceType.MKT,
                orderType: OrderType.IOC,
                trackingPrefix: "MKT"
            );
        }

        // 取得市價賣單
        public ServiceResult<OrderPackage> GetMarketSellOrder()
        {
            return CreateOrderInternal(
                action: SJAction.Sell,
                price: 0,
                priceTypeStr: "MKT",
                stockPriceType: StockPriceType.MKT,
                futurePriceType: FuturePriceType.MKT,
                orderType: OrderType.IOC,
                trackingPrefix: "MKT"
            );
        }

        // 取得範圍市價買單（MKP）
        public ServiceResult<OrderPackage> GetMarketRangeBuyOrder()
        {
            return CreateOrderInternal(
                action: SJAction.Buy,
                price: (double)_preparedPrice,
                priceTypeStr: "MKP",
                stockPriceType: StockPriceType.MKT, // 證券的 MKP 使用 MKT
                futurePriceType: FuturePriceType.MKP,
                orderType: OrderType.IOC,
                trackingPrefix: "MKP"
            );
        }

        // 取得範圍市價賣單（MKP）
        public ServiceResult<OrderPackage> GetMarketRangeSellOrder()
        {
            return CreateOrderInternal(
                action: SJAction.Sell,
                price: (double)_preparedPrice,
                priceTypeStr: "MKP",
                stockPriceType: StockPriceType.MKT, // 證券的 MKP 使用 MKT
                futurePriceType: FuturePriceType.MKP,
                orderType: OrderType.IOC,
                trackingPrefix: "MKP"
            );
        }

        #endregion

        #region 內部類別：OrderPackage

        public class OrderPackage
        {
            public IContract Contract { get; set; } = null!;
            public IOrder Order { get; set; } = null!;
            public string Action { get; set; } = "";
            public string SecurityType { get; set; } = "";
            public string TrackingId { get; set; } = "";
            public string WindowId { get; set; } = "";
            public string CustomField { get; set; } = "";
            public string PriceType { get; set; } = "";
        }

        #endregion
    }
}
