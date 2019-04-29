using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using MyWebSocket = DanilovSoft.WebSocket.WebSocket;

namespace wRPC
{
    public abstract class BaseController : IDisposable
    {
        public Context Context { get; internal set; }
        internal Listener Listener;
        public ConcurrentDictionary<int, UserConnections> Connections => Listener.Connections;

        public BaseController()
        {

        }

        public virtual void Dispose()
        {
            
        }
    }
}
