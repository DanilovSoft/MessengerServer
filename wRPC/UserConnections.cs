﻿using DanilovSoft.WebSocket;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace wRPC
{
    /// <summary>
    /// Потокобезопасный список соединений пользователя.
    /// </summary>
    [DebuggerDisplay("{DebugDisplay,nq}")]
    [DebuggerTypeProxy(typeof(TypeProxy))]
    public class UserConnections : IList<Context>
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebugDisplay => "{" + $"Count = {_list.Count}" + "}";

        public readonly object SyncRoot = new object();
        /// <summary>
        /// Доступ осуществляется только через блокировку <see cref="SyncRoot"/>.
        /// </summary>
        private readonly List<Context> _list = new List<Context>();
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

        public Context this[int index]
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

        public void Add(Context context)
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

        public bool Contains(Context context)
        {
            lock(SyncRoot)
            {
                return _list.Contains(context);
            }
        }

        public void CopyTo(Context[] array, int arrayIndex)
        {
            lock(SyncRoot)
            {
                _list.CopyTo(array, arrayIndex);
            }
        }

        public int IndexOf(Context context)
        {
            lock(SyncRoot)
            {
                return _list.IndexOf(context);
            }
        }

        public void Insert(int index, Context context)
        {
            lock(SyncRoot)
            {
                _list.Insert(index, context);
            }
        }

        public bool Remove(Context context)
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
        public IEnumerator<Context> GetEnumerator()
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
            public Context[] Items => _self._list.ToArray();
        }
        #endregion
    }
}