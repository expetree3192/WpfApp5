using System;

namespace WpfApp5.Services
{
    /// <summary>
    /// 商品類別
    /// </summary>
    public enum ProductCategory
    {
        Stocks,    // 股票
        Futures,   // 期貨
        Options,   // 選擇權
        Indexs,    // 指數
        Unknown    // 未知
    }

    /// <summary>
    /// 商品代碼分類器 - 專注於快速分類
    /// </summary>
    public static class ProductCodeClassifier
    {
        /// <summary>
        /// 快速識別商品類別
        /// </summary>
        /// <param name="code">商品代碼</param>
        /// <returns>商品類別</returns>
        public static ProductCategory ClassifyProductCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return ProductCategory.Unknown;

            code = code.Trim().ToUpper();
            int letterCount = CountLetters(code);   // 計算英文字母數量

            // 規則1: 選擇權 - 長度>=11 且 結尾是C或P
            if (code.Length >= 11 && (code.EndsWith('C') || code.EndsWith('P')))
            {
                return ProductCategory.Options;
            }

            // 規則2: 指數 - 3位純數字
            if (code.Length == 3 && IsAllDigits(code))
            {
                return ProductCategory.Indexs;
            }

            // 規則3: 期貨 - (包含3個英文字母 且 長度在5-10之間) or 只有3個英文字母
            if ((letterCount == 3 && code.Length >= 5 && code.Length <= 10) ||
                (letterCount == 3 && IsAllLetters(code)))
            {
                return ProductCategory.Futures;
            }

            // 規則4: 股票 - 包含英文字母但不超過3個，或純數字
            if (letterCount <= 3)
            {
                return ProductCategory.Stocks;
            }

            // 超過3個英文字母的情況
            return ProductCategory.Unknown;
        }

        /// <summary>
        /// 取得商品類別對應的ProductType字串
        /// </summary>
        /// <param name="category">商品類別</param>
        /// <returns>ProductType字串</returns>
        public static string GetProductTypeString(ProductCategory category)
        {
            return category switch
            {
                ProductCategory.Stocks => "Stocks",
                ProductCategory.Futures => "Futures",
                ProductCategory.Options => "Options",
                ProductCategory.Indexs => "Indexs",
                ProductCategory.Unknown => "",
                _ => ""
            };
        }

        /// <summary>
        /// 檢查商品代碼與選擇的ProductType是否匹配
        /// </summary>
        /// <param name="code">商品代碼</param>
        /// <param name="selectedProductType">選擇的ProductType (Stocks/Futures/Options/Indexs)</param>
        /// <returns>是否匹配</returns>
        public static bool IsProductTypeMatch(string code, string selectedProductType)
        {
            var detectedCategory = ClassifyProductCode(code);
            var expectedProductType = GetProductTypeString(detectedCategory);

            return string.Equals(expectedProductType, selectedProductType, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 取得建議的ProductType
        /// </summary>
        /// <param name="code">商品代碼</param>
        /// <returns>建議的ProductType字串</returns>
        public static string GetSuggestedProductType(string code)
        {
            var category = ClassifyProductCode(code);
            return GetProductTypeString(category);
        }

        /// <summary>
        /// 檢查商品類型是否匹配 - 用於ContractSearchWindow
        /// </summary>
        /// <param name="selectedProductType">使用者選擇的商品類型</param>
        /// <param name="symbol">輸入的商品代號</param>
        /// <returns>檢查結果：null表示匹配，否則返回建議的商品類型</returns>
        public static ProductCategory? CheckProductTypeMatch(string selectedProductType, string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return null;

            var detectedCategory = ClassifyProductCode(symbol);

            // 如果無法分類，不提供建議
            if (detectedCategory == ProductCategory.Unknown)
                return null;

            // 檢查是否匹配
            if (IsProductTypeMatch(symbol, selectedProductType))
                return null; // 匹配，無需建議

            // 不匹配，返回建議的商品類型
            return detectedCategory;
        }

        #region 輔助方法
        /// <summary>
        /// 檢查字串是否包含字母
        /// </summary>
        private static bool HasLetter(string text)
        {
            foreach (char c in text)
            {
                if (char.IsLetter(c))
                    return true;
            }
            return false;
        }

        // 輔助方法：檢查是否全為字母
        private static bool IsAllLetters(string code)
        {
            foreach (char c in code)
            {
                if (!char.IsLetter(c))
                    return false;
            }
            return true;
        }
        // 輔助方法：計算字母數量
        private static int CountLetters(string code)
        {
            int count = 0;
            foreach (char c in code)
            {
                if (char.IsLetter(c))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 檢查字串是否全為數字
        /// </summary>
        private static bool IsAllDigits(string text)
        {
            foreach (char c in text)
            {
                if (!char.IsDigit(c))
                    return false;
            }
            return true;
        }
        #endregion
    }
}
