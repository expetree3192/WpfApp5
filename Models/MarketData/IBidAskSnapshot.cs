// 📁 Models/MarketData/IBidAskSnapshot.cs - 新建檔案

using System;

namespace WpfApp5.Models.MarketData
{
    /// <summary>
    /// 🎯 買賣價快照介面 - 統一不同資料類型的快照操作
    /// </summary>
    public interface IBidAskSnapshot
    {
        /// <summary>當時的最佳買價（快照，不會隨時間改變）</summary>
        decimal BidPrice1 { get; set; }

        /// <summary>當時的最佳賣價（快照，不會隨時間改變）</summary>
        decimal AskPrice1 { get; set; }

        /// <summary>當時的最佳買量（快照，不會隨時間改變）</summary>
        int BidVolume1 { get; set; }

        /// <summary>當時的最佳賣量（快照，不會隨時間改變）</summary>
        int AskVolume1 { get; set; }
    }
}