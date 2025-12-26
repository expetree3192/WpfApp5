using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace WpfApp5.Utils
{
    #region WPF 值轉換器

    /// <summary>
    /// 將布爾值轉換為Visibility
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 將布爾值轉換為背景顏色
    /// </summary>
    public class BoolToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not bool boolValue || !boolValue)
            {
                return new SolidColorBrush(Colors.Transparent);
            }

            string? param = parameter as string;

            return param switch
            {
                "BidBest" => new SolidColorBrush(Color.FromRgb(80, 30, 30)),
                "BidVolume" => new SolidColorBrush(Color.FromRgb(60, 20, 20)),
                "AskBest" => new SolidColorBrush(Color.FromRgb(30, 80, 30)),
                "AskVolume" => new SolidColorBrush(Color.FromRgb(20, 60, 20)),
                _ => new SolidColorBrush(Colors.Transparent)
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 將布爾值轉換為文字
    /// </summary>
    public class BoolToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string paramString)
            {
                string[] options = paramString.Split('|');
                if (options.Length >= 2)
                {
                    return boolValue ? options[0] : options[1];
                }
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 將布爾值反向轉換為Visibility（true -> Collapsed, false -> Visible）
    /// </summary>
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Collapsed;
            }
            return false;
        }
    }

    /// <summary>
    /// 將布爾值轉換為 GridLength
    /// 用於動態控制 Grid 的行/列寬度
    /// 參數格式: "visibleValue,collapsedValue" 例如: "*,0" 或 "200,0"
    /// </summary>
    public class VisibilityToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isVisible && parameter is string paramString)
            {
                try
                {
                    // 解析參數 "visibleValue,collapsedValue"
                    string[] parts = paramString.Split(',');
                    if (parts.Length == 2)
                    {
                        string visibleValue = parts[0].Trim();
                        string collapsedValue = parts[1].Trim();

                        string targetValue = isVisible ? visibleValue : collapsedValue;

                        // 處理 "*" 星號（比例寬度）
                        if (targetValue == "*")
                        {
                            return new GridLength(1, GridUnitType.Star);
                        }

                        // 處理 "2*" 這樣的比例寬度
                        if (targetValue.EndsWith('*'))
                        {
                            string numPart = targetValue.TrimEnd('*');
                            if (double.TryParse(numPart, out double starValue))
                            {
                                return new GridLength(starValue, GridUnitType.Star);
                            }
                            return new GridLength(1, GridUnitType.Star);
                        }

                        // 處理 "Auto"
                        if (targetValue.Equals("Auto", StringComparison.OrdinalIgnoreCase))
                        {
                            return GridLength.Auto;
                        }

                        // 處理固定數值
                        if (double.TryParse(targetValue, out double pixelValue))
                        {
                            return new GridLength(pixelValue, GridUnitType.Pixel);
                        }
                    }
                }
                catch
                {
                    // 解析失敗，返回默認值
                }
            }

            // 默認返回 0 寬度（隱藏）
            return new GridLength(0, GridUnitType.Pixel);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 將數值轉換為布爾值（大於零為 true）
    /// 用於 XAML 中的條件觸發器，例如：掛單數量 > 0 時改變樣式
    /// </summary>
    public class GreaterThanZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 使用現有的高效能轉換方法
            int intValue = DataTypeConverter.SafeConvertToInt(value);
            return intValue > 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 通用數值比較轉換器
    /// 支援多種比較操作：>, <, >=, <=, ==, !=
    /// 參數格式：">0", ">=10", "==5", "!=0" 等
    /// </summary>
    public class NumberComparisonConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is not string paramString || string.IsNullOrEmpty(paramString))
            {
                return false;
            }

            // 使用高效能轉換方法
            double numValue = DataTypeConverter.SafeConvertToDouble(value);

            // 解析參數（例如：">0", ">=10", "==5"）
            string trimmedParam = paramString.Trim();

            if (trimmedParam.StartsWith(">="))
            {
                if (double.TryParse(trimmedParam[2..], out double threshold))
                    return numValue >= threshold;
            }
            else if (trimmedParam.StartsWith("<="))
            {
                if (double.TryParse(trimmedParam[2..], out double threshold))
                    return numValue <= threshold;
            }
            else if (trimmedParam.StartsWith("!="))
            {
                if (double.TryParse(trimmedParam[2..], out double threshold))
                    return Math.Abs(numValue - threshold) > 0.0001; // 避免浮點數精度問題
            }
            else if (trimmedParam.StartsWith("=="))
            {
                if (double.TryParse(trimmedParam[2..], out double threshold))
                    return Math.Abs(numValue - threshold) <= 0.0001; // 避免浮點數精度問題
            }
            else if (trimmedParam.StartsWith('>'))
            {
                if (double.TryParse(trimmedParam[1..], out double threshold))
                    return numValue > threshold;
            }
            else if (trimmedParam.StartsWith('<'))
            {
                if (double.TryParse(trimmedParam[1..], out double threshold))
                    return numValue < threshold;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    #region 數值顏色轉換器
    // 數值顏色轉換器，根據數值的正負零狀態轉換為對應顏色，>0 = 紅色, =0 = 白色, <0 = 綠色
    public class NumberToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 使用現有的高效能轉換方法
            double numValue = DataTypeConverter.SafeConvertToDouble(value);

            // 處理參數（可選：自定義顏色）
            string? param = parameter as string;

            if (!string.IsNullOrEmpty(param))
            {
                // 支援自定義顏色格式：PositiveColor|ZeroColor|NegativeColor，例如：Red|White|Green 或 #FF0000|#FFFFFF|#00FF00
                string[] colors = param.Split('|');
                if (colors.Length == 3)
                {
                    try
                    {
                        if (numValue > 0)
                            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[0]));
                        else if (numValue == 0)
                            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[1]));
                        else
                            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[2]));
                    }
                    catch
                    {
                        // 如果自定義顏色解析失敗，使用預設顏色
                    }
                }
            }

            // 預設顏色方案
            if (numValue > 0)
            {
                return new SolidColorBrush(Color.FromRgb(255, 68, 68));    // 紅色 #FF4444
            }
            else if (numValue == 0)
            {
                return new SolidColorBrush(Colors.White);                   // 白色
            }
            else
            {
                return new SolidColorBrush(Color.FromRgb(0, 204, 68));     // 綠色 #00CC44
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // 數值文字顏色轉換器（返回 Color 而非 Brush），適用於需要 Color 類型的場景
    public class NumberToTextColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double numValue = DataTypeConverter.SafeConvertToDouble(value);

            if (numValue > 0)
            {
                return Color.FromRgb(255, 68, 68);    // 紅色
            }
            else if (numValue == 0)
            {
                return Colors.White;                   // 白色
            }
            else
            {
                return Color.FromRgb(0, 204, 68);     // 綠色
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


    #endregion
    #endregion

    #region 通用數據轉換工具類

    /// <summary>
    /// 通用數據轉換工具類 - 提供高效能的類型轉換方法
    /// </summary>
    public static class DataTypeConverter
    {
        #region 高效能轉換方法（用於高頻調用場景）

        /// <summary>
        /// 【高效能版本】安全地轉換為 int - 使用 AggressiveInlining 優化
        /// 適用於高頻調用場景（如五檔資料解析）
        /// </summary>
        /// <param name="value">要轉換的值</param>
        /// <returns>轉換後的 int 值，失敗則返回 0</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int SafeConvertToInt(object? value)
        {
            if (value == null) return 0;

            return value switch
            {
                int intValue => intValue,                    // 最常見，放第一
                long longValue => (int)longValue,            // API 可能返回 long
                decimal decValue => (int)decValue,           // 金融數據常見
                double dblValue => (int)dblValue,
                float floatValue => (int)floatValue,
                string strValue when int.TryParse(strValue, out int result) => result,
                _ => ConvertToIntFallback(value)             // 分離複雜邏輯
            };
        }

        /// <summary>
        /// 【高效能版本】安全地轉換為 decimal - 使用 AggressiveInlining 優化
        /// 適用於高頻調用場景（如價格資料解析）
        /// </summary>
        /// <param name="value">要轉換的值</param>
        /// <returns>轉換後的 decimal 值，失敗則返回 0</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal SafeConvertToDecimal(object? value)
        {
            if (value == null) return 0m;

            return value switch
            {
                decimal decimalValue => decimalValue,        // 最常見，放第一
                int intValue => intValue,
                long longValue => longValue,
                double doubleValue => (decimal)doubleValue,
                float floatValue => (decimal)floatValue,
                string strValue when decimal.TryParse(strValue, out decimal result) => result,
                _ => ConvertToDecimalFallback(value)
            };
        }

        /// <summary>
        /// 【高效能版本】安全地轉換為 bool - 使用 AggressiveInlining 優化
        /// 適用於高頻調用場景（如標記位解析）
        /// </summary>
        /// <param name="value">要轉換的值</param>
        /// <returns>轉換後的 bool 值，失敗則返回 false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SafeConvertToBool(object? value)
        {
            if (value == null) return false;

            if (value is bool boolValue)
            {
                return boolValue;
            }

            var strValue = value.ToString()?.ToLowerInvariant();
            return strValue switch
            {
                "yes" or "true" or "1" or "y" => true,
                "no" or "false" or "0" or "n" => false,
                _ => bool.TryParse(strValue, out var result) && result
            };
        }

        /// <summary>
        /// 【高效能版本】安全地轉換為 double - 使用 AggressiveInlining 優化
        /// </summary>
        /// <param name="value">要轉換的值</param>
        /// <returns>轉換後的 double 值，失敗則返回 0</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double SafeConvertToDouble(object? value)
        {
            if (value == null) return 0d;

            return value switch
            {
                double doubleValue => doubleValue,
                float floatValue => floatValue,
                int intValue => intValue,
                long longValue => longValue,
                decimal decimalValue => (double)decimalValue,
                string strValue when double.TryParse(strValue, out double result) => result,
                _ => ConvertToDoubleFallback(value)
            };
        }

        /// <summary>
        /// 【高效能版本】安全地轉換為 long - 使用 AggressiveInlining 優化
        /// </summary>
        /// <param name="value">要轉換的值</param>
        /// <returns>轉換後的 long 值，失敗則返回 0</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long SafeConvertToLong(object? value)
        {
            if (value == null) return 0L;

            return value switch
            {
                long longValue => longValue,
                int intValue => intValue,
                decimal decValue => (long)decValue,
                double dblValue => (long)dblValue,
                float floatValue => (long)floatValue,
                string strValue when long.TryParse(strValue, out long result) => result,
                _ => ConvertToLongFallback(value)
            };
        }

        #endregion

        #region 後備轉換方法（不內聯，避免主路徑膨脹）

        /// <summary>
        /// int 轉換的後備邏輯 - 處理罕見類型
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int ConvertToIntFallback(object value)
        {
            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// decimal 轉換的後備邏輯 - 處理罕見類型
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static decimal ConvertToDecimalFallback(object value)
        {
            try
            {
                return Convert.ToDecimal(value);
            }
            catch
            {
                return 0m;
            }
        }

        /// <summary>
        /// double 轉換的後備邏輯 - 處理罕見類型
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static double ConvertToDoubleFallback(object value)
        {
            try
            {
                return Convert.ToDouble(value);
            }
            catch
            {
                return 0d;
            }
        }

        /// <summary>
        /// long 轉換的後備邏輯 - 處理罕見類型
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long ConvertToLongFallback(object value)
        {
            try
            {
                return Convert.ToInt64(value);
            }
            catch
            {
                return 0L;
            }
        }

        #endregion



        #region 可空類型轉換方法（帶日誌記錄）

        /// <summary>
        /// 嘗試將任意對象轉換為 decimal 類型（可空版本）
        /// </summary>
        /// <param name="value">要轉換的值</param>
        /// <param name="sourceType">源數據類型（用於記錄）</param>
        /// <param name="logAction">可選的日誌記錄函數</param>
        /// <returns>轉換後的 decimal 值，失敗則返回 null</returns>
        public static decimal? TryConvertToDecimal(object? value, Type? sourceType = null, Action<Exception, string>? logAction = null)
        {
            if (value == null) return null;

            try
            {
                return value switch
                {
                    decimal decimalValue => decimalValue,
                    double doubleValue => (decimal)doubleValue,
                    float floatValue => (decimal)floatValue,
                    int intValue => intValue,
                    long longValue => longValue,
                    string strValue => decimal.TryParse(strValue, out var result) ? result : null,
                    _ => decimal.TryParse(value.ToString(), out var result) ? result : null
                };
            }
            catch (Exception ex)
            {
                logAction?.Invoke(ex, $"decimal 轉換失敗: {value} ({sourceType?.Name ?? "Unknown"})");
                return null;
            }
        }

        /// <summary>
        /// 嘗試將任意對象轉換為 bool 類型（可空版本）
        /// </summary>
        /// <param name="value">要轉換的值</param>
        /// <param name="sourceType">源數據類型（用於記錄）</param>
        /// <param name="logAction">可選的日誌記錄函數</param>
        /// <returns>轉換後的 bool 值，失敗則返回 null</returns>
        public static bool? TryConvertToBool(object? value, Type? sourceType = null, Action<Exception, string>? logAction = null)
        {
            if (value == null) return null;

            try
            {
                if (value is bool boolValue)
                {
                    return boolValue;
                }

                var strValue = value.ToString()?.ToLowerInvariant();
                return strValue switch
                {
                    "yes" or "true" or "1" or "y" => true,
                    "no" or "false" or "0" or "n" => false,
                    _ => bool.TryParse(strValue, out var result) ? result : null
                };
            }
            catch (Exception ex)
            {
                logAction?.Invoke(ex, $"bool 轉換失敗: {value} ({sourceType?.Name ?? "Unknown"})");
                return null;
            }
        }

        /// <summary>
        /// 嘗試將任意對象轉換為 int 類型（可空版本）
        /// </summary>
        /// <param name="value">要轉換的值</param>
        /// <param name="sourceType">源數據類型（用於記錄）</param>
        /// <param name="logAction">可選的日誌記錄函數</param>
        /// <returns>轉換後的 int 值，失敗則返回 null</returns>
        public static int? TryConvertToInt(object? value, Type? sourceType = null, Action<Exception, string>? logAction = null)
        {
            if (value == null) return null;

            try
            {
                return value switch
                {
                    int intValue => intValue,
                    long longValue => (int)longValue,
                    double doubleValue => (int)doubleValue,
                    float floatValue => (int)floatValue,
                    decimal decimalValue => (int)decimalValue,
                    string strValue => int.TryParse(strValue, out var result) ? result : null,
                    _ => int.TryParse(value.ToString(), out var result) ? result : null
                };
            }
            catch (Exception ex)
            {
                logAction?.Invoke(ex, $"int 轉換失敗: {value} ({sourceType?.Name ?? "Unknown"})");
                return null;
            }
        }

        /// <summary>
        /// 嘗試將任意對象轉換為 double 類型（可空版本）
        /// </summary>
        /// <param name="value">要轉換的值</param>
        /// <param name="sourceType">源數據類型（用於記錄）</param>
        /// <param name="logAction">可選的日誌記錄函數</param>
        /// <returns>轉換後的 double 值，失敗則返回 null</returns>
        public static double? TryConvertToDouble(object? value, Type? sourceType = null, Action<Exception, string>? logAction = null)
        {
            if (value == null) return null;

            try
            {
                return value switch
                {
                    double doubleValue => doubleValue,
                    float floatValue => floatValue,
                    int intValue => intValue,
                    long longValue => longValue,
                    decimal decimalValue => (double)decimalValue,
                    string strValue => double.TryParse(strValue, out var result) ? result : null,
                    _ => double.TryParse(value.ToString(), out var result) ? result : null
                };
            }
            catch (Exception ex)
            {
                logAction?.Invoke(ex, $"double 轉換失敗: {value} ({sourceType?.Name ?? "Unknown"})");
                return null;
            }
        }

        /// <summary>
        /// 嘗試將任意對象轉換為 DateTime 類型
        /// </summary>
        /// <param name="value">要轉換的值</param>
        /// <param name="sourceType">源數據類型（用於記錄）</param>
        /// <param name="logAction">可選的日誌記錄函數</param>
        /// <returns>轉換後的 DateTime 值，失敗則返回 null</returns>
        public static DateTime? TryConvertToDateTime(object? value, Type? sourceType = null, Action<Exception, string>? logAction = null)
        {
            if (value == null) return null;

            try
            {
                if (value is DateTime dateTime)
                {
                    return dateTime;
                }

                string? strValue = value.ToString();
                if (string.IsNullOrEmpty(strValue)) return null;

                if (DateTime.TryParse(strValue, out var result))
                {
                    return result;
                }

                return null;
            }
            catch (Exception ex)
            {
                logAction?.Invoke(ex, $"DateTime 轉換失敗: {value} ({sourceType?.Name ?? "Unknown"})");
                return null;
            }
        }

        #endregion

        #region ✅ 新增：時間戳轉換方法

        /// <summary>
        /// 將 Unix 時間戳（秒）轉換為可讀時間格式
        /// 支援科學記號格式（例如: 1.7616111E+09）
        /// </summary>
        /// <param name="timestamp">Unix 時間戳（可以是 int, long, double, string 等）</param>
        /// <param name="format">時間格式（預設: "yyyy/MM/dd HH:mm:ss"）</param>
        /// <param name="timeZoneOffsetHours">時區偏移小時數（預設: 8，即 UTC+8）</param>
        /// <returns>格式化後的時間字串，失敗則返回空字串</returns>
        public static string FormatUnixTimestamp(dynamic? timestamp, string format = "yyyy/MM/dd HH:mm:ss", int timeZoneOffsetHours = 8)
        {
            try
            {
                if (timestamp == null)
                    return "";

                // 處理科學記號格式 (例如: 1.7616111E+09)
                if (double.TryParse(timestamp.ToString(), out double unixTimestamp))
                {
                    // 如果是 0 或負數，返回空字串
                    if (unixTimestamp <= 0)
                        return "";

                    // Unix 時間戳轉換為 DateTime
                    var dateTime = DateTimeOffset.FromUnixTimeSeconds((long)unixTimestamp)
                        .ToOffset(TimeSpan.FromHours(timeZoneOffsetHours));

                    return $"{dateTime.ToString(format)} (UTC{(timeZoneOffsetHours >= 0 ? "+" : "")}{timeZoneOffsetHours})";
                }

                return timestamp.ToString() ?? "";
            }
            catch
            {
                return timestamp?.ToString() ?? "";
            }
        }

        /// <summary>
        /// 將 Unix 時間戳（毫秒）轉換為可讀時間格式
        /// </summary>
        /// <param name="timestampMs">Unix 時間戳（毫秒）</param>
        /// <param name="format">時間格式（預設: "yyyy/MM/dd HH:mm:ss.fff"）</param>
        /// <param name="timeZoneOffsetHours">時區偏移小時數（預設: 8，即 UTC+8）</param>
        /// <returns>格式化後的時間字串，失敗則返回空字串</returns>
        public static string FormatUnixTimestampMs(dynamic? timestampMs, string format = "yyyy/MM/dd HH:mm:ss.fff", int timeZoneOffsetHours = 8)
        {
            try
            {
                if (timestampMs == null)
                    return "";

                if (double.TryParse(timestampMs.ToString(), out double unixTimestampMs))
                {
                    if (unixTimestampMs <= 0)
                        return "";

                    var dateTime = DateTimeOffset.FromUnixTimeMilliseconds((long)unixTimestampMs)
                        .ToOffset(TimeSpan.FromHours(timeZoneOffsetHours));

                    return $"{dateTime.ToString(format)} (UTC{(timeZoneOffsetHours >= 0 ? "+" : "")}{timeZoneOffsetHours})";
                }

                return timestampMs.ToString() ?? "";
            }
            catch
            {
                return timestampMs?.ToString() ?? "";
            }
        }

        #endregion
        #region ✅ 新增：期貨合約代碼解析

        /// <summary>
        /// 解析期貨合約代碼（例如：TXF + 202511 → TXFK5）
        /// </summary>
        /// <param name="code">合約代碼（例如：TXF）</param>
        /// <param name="deliveryMonth">交割月份（格式：YYYYMM，例如：202511）</param>
        /// <param name="logAction">可選的日誌記錄函數</param>
        /// <returns>完整合約代碼（例如：TXFK5），失敗則返回原始 code</returns>
        public static string ParseFuturesContractCode(string code, string deliveryMonth, Action<string>? logAction = null)
        {
            try
            {
                if (string.IsNullOrEmpty(code))
                {
                    logAction?.Invoke($"[合約解析] code 為空");
                    return code ?? string.Empty;
                }

                if (string.IsNullOrEmpty(deliveryMonth))
                {
                    logAction?.Invoke($"[合約解析] delivery_month 為空");
                    return code;
                }

                if (deliveryMonth.Length != 6)
                {
                    logAction?.Invoke($"[合約解析] delivery_month 格式錯誤: {deliveryMonth}（應為 6 位數字，例如：202511）");
                    return code;
                }

                // 解析年份和月份
                var year = deliveryMonth[..4];      // 2025
                var month = deliveryMonth[4..];     // 11

                // 月份對應字母（01→A, 02→B, ..., 11→K, 12→L）
                var monthCode = month switch
                {
                    "01" => "A",
                    "02" => "B",
                    "03" => "C",
                    "04" => "D",
                    "05" => "E",
                    "06" => "F",
                    "07" => "G",
                    "08" => "H",
                    "09" => "I",
                    "10" => "J",
                    "11" => "K",
                    "12" => "L",
                    _ => "?"
                };

                // 年份取最後一位（2025 → 5）
                var yearCode = year[^1..];

                // 組合完整合約代碼（例如：TXFK5）
                var fullCode = $"{code}{monthCode}{yearCode}";

                logAction?.Invoke($"[合約解析] 解析成功: {code} + {deliveryMonth} → {fullCode}");

                return fullCode;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"[合約解析] 解析失敗: {ex.Message}");
                return code;
            }
        }

        /// <summary>
        /// 解析期貨合約代碼（重載版本，支援 dynamic 類型）
        /// </summary>
        /// <param name="code">合約代碼（dynamic 類型）</param>
        /// <param name="deliveryMonth">交割月份（dynamic 類型）</param>
        /// <param name="logAction">可選的日誌記錄函數</param>
        /// <returns>完整合約代碼，失敗則返回原始 code 的字串表示</returns>
        public static string ParseFuturesContractCode(dynamic? code, dynamic? deliveryMonth, Action<string>? logAction = null)
        {
            string? codeStr = code?.ToString();
            string? deliveryMonthStr = deliveryMonth?.ToString();

            if (string.IsNullOrEmpty(codeStr))
            {
                return string.Empty;
            }

            if (string.IsNullOrEmpty(deliveryMonthStr))
            {
                return codeStr;
            }

            return ParseFuturesContractCode(codeStr, deliveryMonthStr, logAction);
        }

        /// <summary>
        /// 驗證期貨合約代碼格式是否正確
        /// </summary>
        /// <param name="contractCode">完整合約代碼（例如：TXFK5）</param>
        /// <returns>是否為有效的期貨合約代碼</returns>
        public static bool IsValidFuturesContractCode(string contractCode)
        {
            if (string.IsNullOrEmpty(contractCode) || contractCode.Length < 4)
            {
                return false;
            }

            // 檢查最後兩個字元是否符合 "月份字母 + 年份數字" 格式
            char monthChar = contractCode[^2];
            char yearChar = contractCode[^1];

            bool isValidMonth = monthChar is >= 'A' and <= 'L';
            bool isValidYear = char.IsDigit(yearChar);

            return isValidMonth && isValidYear;
        }

        /// <summary>
        /// 從完整合約代碼反解析出交割月份（例如：TXFK5 → 202511）
        /// </summary>
        /// <param name="contractCode">完整合約代碼（例如：TXFK5）</param>
        /// <param name="baseYear">基準年份（預設：當前年份）</param>
        /// <returns>交割月份（格式：YYYYMM），失敗則返回 null</returns>
        public static string? ReverseParseDeliveryMonth(string contractCode, int? baseYear = null)
        {
            try
            {
                if (string.IsNullOrEmpty(contractCode) || contractCode.Length < 4)
                {
                    return null;
                }

                char monthChar = contractCode[^2];
                char yearChar = contractCode[^1];

                // 解析月份
                int month = monthChar switch
                {
                    'A' => 1,
                    'B' => 2,
                    'C' => 3,
                    'D' => 4,
                    'E' => 5,
                    'F' => 6,
                    'G' => 7,
                    'H' => 8,
                    'I' => 9,
                    'J' => 10,
                    'K' => 11,
                    'L' => 12,
                    _ => 0
                };

                if (month == 0)
                {
                    return null;
                }

                // 解析年份（假設是 2020 年代）
                if (!char.IsDigit(yearChar))
                {
                    return null;
                }

                int currentYear = baseYear ?? DateTime.Now.Year;
                int decade = (currentYear / 10) * 10; // 取得當前年代（例如：2020）
                int year = decade + (yearChar - '0');

                return $"{year:D4}{month:D2}";
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 獲取友好的類型名稱
        /// </summary>
        /// <param name="type">類型</param>
        /// <returns>友好的類型名稱</returns>
        public static string GetFriendlyTypeName(Type type)
        {
            return type switch
            {
                var t when t == typeof(string) => "string",
                var t when t == typeof(int) => "int",
                var t when t == typeof(long) => "long",
                var t when t == typeof(double) => "double",
                var t when t == typeof(decimal) => "decimal",
                var t when t == typeof(float) => "float",
                var t when t == typeof(bool) => "bool",
                var t when t == typeof(DateTime) => "DateTime",
                var t when t == typeof(int?) => "int?",
                var t when t == typeof(long?) => "long?",
                var t when t == typeof(double?) => "double?",
                var t when t == typeof(decimal?) => "decimal?",
                var t when t == typeof(float?) => "float?",
                var t when t == typeof(bool?) => "bool?",
                var t when t == typeof(DateTime?) => "DateTime?",
                _ => type.Name
            };
        }

        #endregion
    }

    #endregion
}
