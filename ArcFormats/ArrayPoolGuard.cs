using System;
using System.Buffers;
using System.Threading;

namespace GameRes.Utility
{
    internal struct ArrayPoolGuard<T> : IDisposable
    {
        private readonly ArrayPool<T> m_pool;
        private T[] m_array;

        public T[] Array
        {
            get
            {
                if (m_array == null)
                    throw new ObjectDisposedException(nameof(ArrayPoolGuard<T>));
                return m_array;
            }
        }

        public ArrayPoolGuard(ArrayPool<T> pool, int minimumLength)
        {
            m_pool = pool;
            m_array = pool.Rent(minimumLength);
            if (m_array == null)
                throw new InvalidOperationException("ArrayPool.Rent returned null");
        }

        public static implicit operator T[](ArrayPoolGuard<T> guard) => guard.Array;

        public void Dispose()
        {
            var oldSavedArray = Interlocked.CompareExchange(ref m_array, null, m_array);
            if (oldSavedArray == null) return;
            m_pool.Return(oldSavedArray);
            m_array = null;
        }

        #region Array Properties

        public T this[int index]
        {
            get => Array[index];
            set => Array[index] = value;
        }

        public int Length => Array.Length;

        #endregion
    }

    internal static class ArrayPoolExtension
    {
        /// <summary>
        /// Used in conjunction with the using statement to borrow arrays from the pool and automatically return it at the end of using
        /// </summary>
        /// <param name="pool">The array pool rent from</param>
        /// <param name="min_length">The minimum size of the array</param>
        /// <typeparam name="T">Array element type</typeparam>
        /// <returns>An <see cref="ArrayPoolGuard{T}"/> struct，it will return the array to the pool when disposed being invoked</returns>
        public static ArrayPoolGuard<T> RentSafe<T>(this ArrayPool<T> pool, int min_length)
        {
            return new ArrayPoolGuard<T>(pool, min_length);
        }
    }
}