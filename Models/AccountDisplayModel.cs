using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Sinopac.Shioaji;

namespace WpfApp5.Models
{
    // 帳戶顯示模型 - 專門用於 UI 顯示的帳戶資料
    public class AccountDisplayModel : INotifyPropertyChanged
    {
        #region Private Fields
        private bool _isActive;
        private string _displayText = "";
        #endregion

        #region Properties

        public object? OriginalAccount { get; set; }    // 原始帳戶物件 (object 類型，用於相容性)

        // 取得強型別的 Sinopac.Shioaji.Account 物件
        public Account? Account
        {
            get
            {
                if (OriginalAccount is Account account)
                {
                    return account;
                }
                return null;
            }
        }
        // 安全取得 Account 物件 (如果為 null 則拋出例外)
        public Account GetAccountOrThrow()
        {
            if (OriginalAccount is Account account)
            {
                return account;
            }
            throw new InvalidOperationException("OriginalAccount 不是有效的 Account 物件");
        }
        
        public string AccountType { get; set; } = "";   // 帳戶類型 (如：S, F)
        public string AccountId { get; set; } = ""; // 帳戶 ID
        public string PersonId { get; set; } = "";  // 個人 ID
        public string BrokerId { get; set; } = "";  // 券商 ID
        public string Username { get; set; } = "";  // 用戶名稱

        // 帳戶是否啟用
        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(StatusIcon));
                OnPropertyChanged(nameof(StatusText));
            }
        }

        // 顯示文字
        public string DisplayText
        {
            get => _displayText;
            set
            {
                _displayText = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Computed Properties

        // 狀態顏色
        public Brush StatusColor => IsActive ? Brushes.Green : Brushes.Orange;

        // 狀態圖示
        public string StatusIcon => IsActive ? "✓" : "!";

        // 狀態文字
        public string StatusText => IsActive ? "帳戶可用" : "帳戶未啟用";

        // 帳戶類型顯示名稱
        public string AccountTypeDisplayName => AccountType switch
        {
            "S" => "Stocks",
            "F" => "Futures",
            _ => AccountType
        };

        // 詳細顯示文字 (包含用戶名)
        public string DetailedDisplayText => $"{AccountTypeDisplayName} - {AccountId} ({Username})";

        #endregion

        #region Static Factory Methods

        // 從 Sinopac.Shioaji.Account 物件建立 AccountDisplayModel
        public static AccountDisplayModel? FromAccount(object? account)
        {
            if (account == null) return null;

            try
            {
                // 先檢查是否為 Account 類型
                if (account is not Account typedAccount)
                {
                    System.Diagnostics.Debug.WriteLine($"[AccountDisplayModel] 物件不是 Account 類型: {account.GetType().FullName}");
                    return null;
                }

                // 使用強型別存取
                var model = new AccountDisplayModel
                {
                    OriginalAccount = account,
                    AccountType = typedAccount.account_type?.ToString() ?? "Unknown",
                    AccountId = typedAccount.account_id ?? "Unknown",
                    PersonId = typedAccount.person_id ?? "",
                    BrokerId = typedAccount.broker_id ?? "",
                    Username = typedAccount.username ?? "",
                    IsActive = typedAccount.signed
                };

                // 設定顯示格式
                model.DisplayText = $"{model.AccountTypeDisplayName} - {model.AccountId}";

                return model;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AccountDisplayModel] 轉換失敗: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[AccountDisplayModel] 物件類型: {account?.GetType().FullName}");
                return null;
            }
        }

        // 批量轉換帳戶清單
        public static List<AccountDisplayModel> FromAccounts(System.Collections.IEnumerable? accounts)
        {
            if (accounts == null) return [];

            var result = new List<AccountDisplayModel>();
            foreach (var account in accounts)
            {
                var model = FromAccount(account);
                if (model != null)
                {
                    result.Add(model);
                }
            }
            return result;
        }

        // 批量轉換帳戶清單 (強型別版本)
        public static List<AccountDisplayModel> FromAccounts(IEnumerable<Account> accounts)
        {
            if (accounts == null) return [];

            var result = new List<AccountDisplayModel>();
            foreach (var account in accounts)
            {
                var model = FromAccount(account);
                if (model != null)
                {
                    result.Add(model);
                }
            }
            return result;
        }

        // 根據帳戶類型篩選
        public static List<AccountDisplayModel> FilterByAccountType(List<AccountDisplayModel> accounts, string accountType)
        {
            return [.. accounts.Where(a => a.AccountType.Equals(accountType, StringComparison.OrdinalIgnoreCase))];
        }

        // 取得股票帳戶
        public static List<AccountDisplayModel> GetStockAccounts(List<AccountDisplayModel> accounts)
        {
            return FilterByAccountType(accounts, "S");
        }

        // 取得期貨帳戶
        public static List<AccountDisplayModel> GetFutureAccounts(List<AccountDisplayModel> accounts)
        {
            return FilterByAccountType(accounts, "F");
        }
        #endregion

        #region Conversion Methods

        /// <summary>
        /// 將 AccountDisplayModel 清單轉換為 Account 清單
        /// </summary>
        public static List<Account> ToAccountList(IEnumerable<AccountDisplayModel> displayModels)
        {
            var result = new List<Account>();
            foreach (var model in displayModels)
            {
                if (model.Account != null)
                {
                    result.Add(model.Account);
                }
            }
            return result;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Override Methods

        public override string ToString()
        {
            return DisplayText;
        }

        // 取得完整資訊字串
        public string ToDetailedString()
        {
            return $"{AccountTypeDisplayName}帳戶 {AccountId} - {Username} (券商:{BrokerId}, 狀態:{StatusText})";
        }

        #endregion
    }
}