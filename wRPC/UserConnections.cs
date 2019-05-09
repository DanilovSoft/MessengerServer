using DanilovSoft.WebSocket;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace wRPC
{
    /// <summary>
    /// Потокобезопасный список авторизованных соединений пользователя.
    /// </summary>
    [DebuggerDisplay("{DebugDisplay,nq}")]
    [DebuggerTypeProxy(typeof(TypeProxy))]
    public class UserConnections : IList<ServerContext>
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebugDisplay => "{" + $"Count = {_list.Count}" + "}";

        public readonly object SyncRoot = new object();
        /// <summary>
        /// Доступ осуществляется только через блокировку <see cref="SyncRoot"/>.
        /// </summary>
        private readonly List<ServerContext> _list = new List<ServerContext>();
        public readonly int UserId;

        /// <summary>
        /// Доступ осуществляется только через блокировку <see cref="SyncRoot"/>.
        /// Если коллекция уже была удалена из словаря подключений, то значение будет <see langword="true"/> 
        /// и испольльзовать этот экземпляр больше нельзя.
        /// </summary>
        public bool IsDestroyed { get; set; }

        public UserConnections(int userId)
        {
            UserId = userId;
        }

        public ServerContext this[int index]
        {
            get
            {
                lock(SyncRoot)
                {
                    return _list[index];
                }
            }
            set
            {
                lock (SyncRoot)
                {
                    _list[index] = value;
                }
            }
        }

        public int Count
        {
            get
            {
                lock(SyncRoot)
                {
                    return _list.Count;
                }
            }
        }

        public bool IsReadOnly => false;

        public void Add(ServerContext context)
        {
            lock(SyncRoot)
            {
                _list.Add(context);
            }
        }

        public void Clear()
        {
            lock(SyncRoot)
            {
                _list.Clear();
            }
        }

        public bool Contains(ServerContext context)
        {
            lock(SyncRoot)
            {
                return _list.Contains(context);
            }
        }

        public void CopyTo(ServerContext[] array, int arrayIndex)
        {
            lock(SyncRoot)
            {
                _list.CopyTo(array, arrayIndex);
            }
        }

        public int IndexOf(ServerContext context)
        {
            lock(SyncRoot)
            {
                return _list.IndexOf(context);
            }
        }

        public void Insert(int index, ServerContext context)
        {
            lock(SyncRoot)
            {
                _list.Insert(index, context);
            }
        }

        public bool Remove(ServerContext context)
        {
            lock(SyncRoot)
            {
                return _list.Remove(context);
            }
        }

        public void RemoveAt(int index)
        {
            lock (SyncRoot)
            {
                _list.RemoveAt(index);
            }
        }

        /// <summary>
        /// Возвращает копию своей коллекции.
        /// </summary>
        public IEnumerator<ServerContext> GetEnumerator()
        {
            lock(SyncRoot)
            {
                return _list.ToList().GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #region Debug
        [DebuggerNonUserCode]
        private class TypeProxy
        {
            private readonly UserConnections _self;
            public TypeProxy(UserConnections self)
            {
                _self = self;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public ServerContext[] Items => _self._list.ToArray();
        }
        #endregion
    }
}
