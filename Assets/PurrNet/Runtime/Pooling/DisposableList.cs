using System;
using System.Collections;
using System.Collections.Generic;

namespace PurrNet.Pooling
{
    public struct DisposableList<T> : IList<T>, IDisposable
    {
        private bool _disposed;
        private readonly List<T> _list;
        
        public DisposableList(int capacity = 0)
        {
            _list = ListPool<T>.Instantiate();
            
            if (_list.Capacity < capacity)
                _list.Capacity = capacity;
            
            _disposed = false;
        }
        
        public void Dispose()
        {
            ListPool<T>.Destroy(_list);
            _disposed = true;
        }

        public IEnumerator<T> GetEnumerator()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DisposableList<T>));
            return _list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DisposableList<T>));
            return GetEnumerator();
        }

        public void Add(T item)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DisposableList<T>));
            _list.Add(item);
        }

        public void Clear()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DisposableList<T>));
            _list.Clear();
        }

        public bool Contains(T item)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DisposableList<T>));
            return _list.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DisposableList<T>));
            _list.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DisposableList<T>));
            return _list.Remove(item);
        }

        public int Count
        {
            get
            {
                if (_disposed) throw new ObjectDisposedException(nameof(DisposableList<T>));
                return _list.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                if (_disposed) throw new ObjectDisposedException(nameof(DisposableList<T>));
                return false;
            }
        }

        public int IndexOf(T item)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DisposableList<T>));
            return _list.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DisposableList<T>));
            _list.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DisposableList<T>));
            _list.RemoveAt(index);
        }

        public T this[int index]
        {
            get
            {
                if (_disposed) throw new ObjectDisposedException(nameof(DisposableList<T>));
                return _list[index];
            }
            set
            {
                if (_disposed) throw new ObjectDisposedException(nameof(DisposableList<T>));
                _list[index] = value;
            }
        }
    }
}
