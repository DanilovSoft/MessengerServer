using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace wRPC
{
    public abstract class Controller : IDisposable
    {
        public Context Context { get; internal set; }
        internal Listener Listener;
        public ConcurrentDictionary<int, UserConnections> Connections => Listener.Connections;

        public Controller()
        {

        }

        public virtual void Dispose()
        {
            
        }
    }
}
