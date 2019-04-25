using DanilovSoft.WebSocket;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace wRPC
{
    public sealed class Listener : IDisposable
    {
        private readonly WebSocketServer _wsServ;
        private readonly ConcurrentBag<WebSocket> _connections;
        private bool _disposed;

        // ctor.
        public Listener(int port)
        {
            _connections = new ConcurrentBag<WebSocket>();
            _wsServ = new WebSocketServer();
            _wsServ.Bind(new IPEndPoint(IPAddress.Any, port));
            _wsServ.Connected += WebSocket_OnConnected;
        }

        public void StartAccept()
        {
            _wsServ.StartAccept();
        }

        private void WebSocket_OnConnected(object sender, WebSocket e)
        {
            _connections.Add(e);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _wsServ.Connected -= WebSocket_OnConnected;
                _wsServ.Dispose();
            }
        }
    }
}
