using System;
using System.Collections.Concurrent;

namespace WpfApp5.Utils
{
    /// <summary>
    /// 通用對象池，用於重用對象以減少 GC 壓力
    /// </summary>
    /// <typeparam name="T">對象類型</typeparam>
    public class ObjectPool<T> where T : class
    {
        private readonly ConcurrentBag<T> _objects;
        private readonly Func<T> _createFunc;
        private readonly Func<T, bool> _actionOnReturn;
        private readonly int _maxSize;

        /// <summary>
        /// 建立對象池
        /// </summary>
        /// <param name="createFunc">建立新對象的函數</param>
        /// <param name="actionOnReturn">對象返回池時的清理函數，返回 true 表示可以放回池中</param>
        /// <param name="maxSize">池的最大容量</param>
        public ObjectPool(Func<T> createFunc, Func<T, bool>? actionOnReturn = null, int maxSize = 100)
        {
            _objects = new ConcurrentBag<T>();
            _createFunc = createFunc ?? throw new ArgumentNullException(nameof(createFunc));
            _actionOnReturn = actionOnReturn ?? (obj => true);
            _maxSize = maxSize;
        }

        /// <summary>
        /// 從池中獲取對象
        /// </summary>
        public T Get()
        {
            if (_objects.TryTake(out T? item))
            {
                return item;
            }
            return _createFunc();
        }

        /// <summary>
        /// 將對象返回池中
        /// </summary>
        public void Return(T item)
        {
            if (item == null) return;

            if (_objects.Count < _maxSize && _actionOnReturn(item))
            {
                _objects.Add(item);
            }
        }

        /// <summary>
        /// 清空對象池
        /// </summary>
        public void Clear()
        {
            while (_objects.TryTake(out _)) { }
        }

        /// <summary>
        /// 獲取當前池中對象數量
        /// </summary>
        public int Count => _objects.Count;
    }
}
