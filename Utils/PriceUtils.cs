using System;

namespace WpfApp5.Utils
{
    /// <summary>
    /// 價格相關的通用工具類
    /// ✅ 根據完整 TickSize 規則總結重寫
    /// </summary>
    public static class PriceUtils
    {
        /// <summary>
        /// 根據價格計算Tick間距的通用方法
        /// </summary>
        /// <param name="price">參考價格</param>
        /// <returns>對應的Tick間距</returns>
        public static decimal CalculatePriceTick(decimal price)
        {
            if (price >= 10000) return 1m;      // ≥ 10000: TickSize = 1
            if (price >= 1000) return 5m;       // 1000 ~ 9995: TickSize = 5
            if (price >= 500) return 1m;        // 500 ~ 999: TickSize = 1
            if (price >= 100) return 0.5m;      // 100 ~ 499.5: TickSize = 0.5
            if (price >= 50) return 0.1m;       // 50 ~ 99.9: TickSize = 0.1
            if (price >= 10) return 0.05m;      // 10 ~ 49.95: TickSize = 0.05
            return 0.01m;                       // < 10: TickSize = 0.01
        }

        /// <summary>
        /// ✅ 取得下一個有效價格（向下）
        /// 正確處理 TickSize 變化的臨界點
        /// </summary>
        /// <param name="currentPrice">當前價格</param>
        /// <returns>下一個有效價格</returns>
        public static decimal GetNextPriceDown(decimal currentPrice)
        {
            // 取得當前價格的 TickSize
            decimal currentTick = CalculatePriceTick(currentPrice);

            // 計算下一個價格（先嘗試用當前 TickSize）
            decimal nextPrice = currentPrice - currentTick;

            // ✅ 檢查是否跨越臨界點
            decimal nextTick = CalculatePriceTick(nextPrice);

            if (nextTick != currentTick)
            {
                // ✅ 跨越臨界點：需要找到正確的下一個價格
                // 例如：從 1000 (TickSize=5) 往下，應該是 999 (TickSize=1)
                //       從 50 (TickSize=0.1) 往下，應該是 49.95 (TickSize=0.05)

                // 找到臨界點
                decimal boundary = GetLowerBoundary(currentPrice);

                // 下一個價格應該是：臨界點 - 新的 TickSize
                nextPrice = boundary - nextTick;
            }

            return nextPrice;
        }

        /// <summary>
        /// ✅ 取得上一個有效價格（向上）
        /// 正確處理 TickSize 變化的臨界點
        /// </summary>
        /// <param name="currentPrice">當前價格</param>
        /// <returns>上一個有效價格</returns>
        public static decimal GetNextPriceUp(decimal currentPrice)
        {
            // 取得當前價格的 TickSize
            decimal currentTick = CalculatePriceTick(currentPrice);

            // 計算上一個價格（先嘗試用當前 TickSize）
            decimal nextPrice = currentPrice + currentTick;

            // ✅ 檢查是否跨越臨界點
            decimal nextTick = CalculatePriceTick(nextPrice);

            if (nextTick != currentTick)
            {
                // ✅ 跨越臨界點：需要找到正確的上一個價格
                // 例如：從 999 (TickSize=1) 往上，應該是 1000 (TickSize=5)
                //       從 49.95 (TickSize=0.05) 往上，應該是 50 (TickSize=0.1)

                // 找到臨界點（上邊界）
                decimal boundary = GetUpperBoundary(currentPrice);

                // 上一個價格就是臨界點本身
                nextPrice = boundary;
            }

            return nextPrice;
        }

        /// <summary>
        /// ✅ 取得當前價格區間的下邊界（臨界點）
        /// </summary>
        /// <param name="price">參考價格</param>
        /// <returns>下邊界價格</returns>
        private static decimal GetLowerBoundary(decimal price)
        {
            if (price >= 10000) return 10000m;
            if (price >= 1000) return 1000m;
            if (price >= 500) return 500m;
            if (price >= 100) return 100m;
            if (price >= 50) return 50m;
            if (price >= 10) return 10m;
            return 0m;
        }

        /// <summary>
        /// ✅ 取得當前價格區間的上邊界（臨界點）
        /// </summary>
        /// <param name="price">參考價格</param>
        /// <returns>上邊界價格</returns>
        private static decimal GetUpperBoundary(decimal price)
        {
            if (price < 10) return 10m;
            if (price < 50) return 50m;
            if (price < 100) return 100m;
            if (price < 500) return 500m;
            if (price < 1000) return 1000m;
            if (price < 10000) return 10000m;
            return decimal.MaxValue;
        }

        /// <summary>
        /// ✅ 將價格調整到最接近的有效價格（四捨五入）
        /// 確保價格符合當前 TickSize 的規則
        /// </summary>
        /// <param name="price">原始價格</param>
        /// <returns>調整後的有效價格</returns>
        public static decimal RoundToValidPrice(decimal price)
        {
            decimal tick = CalculatePriceTick(price);
            decimal boundary = GetLowerBoundary(price);

            // ✅ 以臨界點為基準進行四捨五入
            // 例如：1003 → boundary=1000, offset=3, tick=5 → roundedOffset=5 → 結果=1005
            // 例如：50.03 → boundary=50, offset=0.03, tick=0.1 → roundedOffset=0 → 結果=50.0
            decimal offset = price - boundary;
            decimal roundedOffset = Math.Round(offset / tick, MidpointRounding.AwayFromZero) * tick;

            return boundary + roundedOffset;
        }

        /// <summary>
        /// 驗證價格是否為有效的交易價格
        /// </summary>
        /// <param name="price">價格</param>
        /// <returns>是否為有效價格</returns>
        public static bool IsValidPrice(decimal price)
        {
            decimal tick = CalculatePriceTick(price);
            decimal rounded = RoundToValidPrice(price);
            return Math.Abs(price - rounded) < 0.0001m;
        }

        /// <summary>
        /// 根據價格和Tick間距格式化價格顯示
        /// </summary>
        /// <param name="price">價格</param>
        /// <param name="tickSize">Tick間距</param>
        /// <returns>格式化後的價格字串</returns>
        public static string FormatPrice(decimal price, decimal tickSize)
        {
            if (tickSize >= 1) return price.ToString("F0");
            if (tickSize >= 0.1m) return price.ToString("F1");
            if (tickSize >= 0.01m) return price.ToString("F2");
            return price.ToString("F3");
        }

        /// <summary>
        /// 根據價格和商品類型格式化價格顯示
        /// </summary>
        /// <param name="price">價格</param>
        /// <param name="securityType">商品類型</param>
        /// <returns>格式化後的價格字串</returns>
        public static string FormatPrice(decimal price, string securityType)
        {
            if (securityType == "STK" || securityType == "FUT" || securityType == "OPT")
            {
                decimal tick = CalculatePriceTick(price);
                return FormatPrice(price, tick);
            }
            else if (securityType == "IND")
            {
                return price.ToString("F1");
            }
            return price.ToString("F2");
        }
    }
}
