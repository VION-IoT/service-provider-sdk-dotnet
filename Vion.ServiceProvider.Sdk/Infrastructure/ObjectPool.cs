using System;
using System.Collections.Concurrent;

namespace Vion.ServiceProvider.Sdk.Infrastructure
{
    /// <summary>
    ///     A small, thread-safe pool of reusable objects. Rented items are reset before they are handed out and again
    ///     when returned; the pool keeps at most <c>maxSize</c> items and falls back to the factory when empty.
    /// </summary>
    /// <typeparam name="T">The pooled object type.</typeparam>
    internal sealed class ObjectPool<T>
    {
        private readonly Func<T> _factory;

        private readonly int _maxSize;

        private readonly ConcurrentBag<T> _pool = [];

        private readonly Action<T> _reset;

        public ObjectPool(Func<T> factory, Action<T> reset, int maxSize = 128)
        {
            _factory = factory;
            _reset = reset;
            _maxSize = maxSize;
        }

        public T Rent()
        {
            if (_pool.TryTake(out var item))
            {
                _reset(item);
                return item;
            }

            return _factory();
        }

        public void Return(T item)
        {
            _reset(item);
            if (_pool.Count < _maxSize)
            {
                _pool.Add(item);
            }
        }
    }
}
