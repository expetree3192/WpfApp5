using System;

namespace WpfApp5.Services.Common
{
    /// <summary>
    /// 服務操作結果封裝 (泛型版本)
    /// </summary>
    /// <typeparam name="T">結果資料類型</typeparam>
    public class ServiceResult<T>
    {
        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 結果資料
        /// </summary>
        public T? Data { get; set; }

        /// <summary>
        /// 操作訊息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 錯誤代碼 (可選)
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>
        /// 例外資訊 (可選)
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// 操作時間戳
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// 建立成功結果
        /// </summary>
        public static ServiceResult<T> Success(T data, string message = "操作成功")
        {
            return new ServiceResult<T>
            {
                IsSuccess = true,
                Data = data,
                Message = message,
                Timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// 建立失敗結果
        /// </summary>
        public static ServiceResult<T> Failure(string message, string errorCode = "", Exception? exception = null)
        {
            return new ServiceResult<T>
            {
                IsSuccess = false,
                Message = message,
                ErrorCode = errorCode,
                Exception = exception,
                Timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// 轉換為字串表示
        /// </summary>
        public override string ToString()
        {
            var status = IsSuccess ? "成功" : "失敗";
            var result = $"[{status}] {Message}";

            if (!string.IsNullOrEmpty(ErrorCode))
            {
                result += $" (錯誤代碼: {ErrorCode})";
            }

            return result;
        }
    }

    /// <summary>
    /// 服務操作結果封裝 (無資料版本)
    /// </summary>
    public class ServiceResult : ServiceResult<object>
    {
        /// <summary>
        /// 建立成功結果
        /// </summary>
        public static ServiceResult Success(string message = "操作成功")
        {
            return new ServiceResult
            {
                IsSuccess = true,
                Message = message,
                Timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// 建立失敗結果
        /// </summary>
        public static new ServiceResult Failure(string message, string errorCode = "", Exception? exception = null)
        {
            return new ServiceResult
            {
                IsSuccess = false,
                Message = message,
                ErrorCode = errorCode,
                Exception = exception,
                Timestamp = DateTime.Now
            };
        }
    }

    /// <summary>
    /// ServiceResult 擴展方法
    /// </summary>
    public static class ServiceResultExtensions
    {
        /// <summary>
        /// 如果失敗則拋出例外
        /// </summary>
        public static ServiceResult<T> ThrowIfFailed<T>(this ServiceResult<T> result)
        {
            if (!result.IsSuccess)
            {
                var exception = result.Exception ?? new InvalidOperationException(result.Message);
                throw exception;
            }
            return result;
        }

        /// <summary>
        /// 轉換資料類型
        /// </summary>
        public static ServiceResult<TOut> Map<TIn, TOut>(this ServiceResult<TIn> result, Func<TIn, TOut> mapper)
        {
            if (!result.IsSuccess)
            {
                return ServiceResult<TOut>.Failure(result.Message, result.ErrorCode, result.Exception);
            }

            try
            {
                var mappedData = mapper(result.Data!);
                return ServiceResult<TOut>.Success(mappedData, result.Message);
            }
            catch (Exception ex)
            {
                return ServiceResult<TOut>.Failure($"資料轉換失敗: {ex.Message}", "MAPPING_ERROR", ex);
            }
        }
    }
}