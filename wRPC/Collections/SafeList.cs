using System;
using System.Collections.Generic;
using System.Text;

namespace wRPC
{
    internal class SafeList<T>
    {
        private readonly List<T> _list;

        public SafeList()
        {
            _list = new List<T>();
        }

        public void Add(T item)
        {
            lock(_list)
            {
                _list.Add(item);
            }
        }

        public void Remove(T item)
        {
            lock(_list)
            {
                _list.Remove(item);
            }
        }
    }
}
