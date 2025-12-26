using System;

namespace WpfApp5.Utils
{
    /// <summary>
    /// Dynamic 類型安全存取輔助類（簡化版）
    /// </summary>
    public static class DynamicHelper
    {
        /// <summary>
        /// 安全地取得 dynamic 物件的屬性值
        /// </summary>
        public static T SafeGetProperty<T>(dynamic obj, string propertyName, T defaultValue = default!)
        {
            try
            {
                if (obj == null) return defaultValue;

                var type = obj.GetType();
                var property = type.GetProperty(propertyName);
                if (property != null)
                {
                    var value = property.GetValue(obj);
                    if (value is T directValue)
                        return directValue;

                    if (typeof(T) != typeof(object))
                        return (T)Convert.ChangeType(value, typeof(T));
                }

                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 安全地將 dynamic 物件轉換為字串
        /// </summary>
        public static string SafeToString(dynamic obj, string defaultValue = "")
        {
            try
            {
                return obj?.ToString() ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 檢查 dynamic 物件是否為 null 或空
        /// </summary>
        public static bool IsNullOrEmpty(dynamic obj)
        {
            try
            {
                return obj == null;
            }
            catch
            {
                return true;
            }
        }
    }
}