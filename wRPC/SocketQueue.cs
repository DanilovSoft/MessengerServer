﻿using DanilovSoft.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace wRPC
{
    internal sealed class SocketQueue : IDisposable
    {
        private int _state;
        private int _disposed;
        public WebSocket WebSocket { get; }

        /// <summary>
        /// Коллекция запросов ожидающие ответ от удалённой стороны.
        /// </summary>
        public RequestQueue RequestQueue { get; }

        public SocketQueue(WebSocket webSocket)
        {
            WebSocket = webSocket;
            RequestQueue = new RequestQueue();
        }

        /// <summary>
        /// Атомарно пытается захватить эксклюзивное право на текущий объект.
        /// </summary>
        public bool TryOwn()
        {
            bool owned = Interlocked.CompareExchange(ref _state, 1, 0) == 0;
            return owned;
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
            {
                WebSocket.Dispose();
            }
        }
    }
}