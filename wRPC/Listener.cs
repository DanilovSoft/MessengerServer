using Contract;
using DanilovSoft.WebSocket;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyWebSocket = DanilovSoft.WebSocket.WebSocket;

namespace wRPC
{
    public sealed class Listener : IDisposable
    {
        private readonly WebSocketServer _wsServ;
        private readonly ConcurrentDictionary<MyWebSocket, Context> _connections;
        private readonly BaseController _defaultController;
        private bool _disposed;

        // ctor.
        public Listener(int port, BaseController defaultController)
        {
            _defaultController = defaultController;
            _connections = new ConcurrentDictionary<MyWebSocket, Context>();
            _wsServ = new WebSocketServer();
            _wsServ.Bind(new IPEndPoint(IPAddress.Any, port));
            _wsServ.Connected += WebSocket_OnConnected;
        }

        public void StartAccept()
        {
            _wsServ.StartAccept();
        }

        private void WebSocket_OnConnected(object sender, MyWebSocket e)
        {
            var context = new Context(e, _defaultController);
            _connections.TryAdd(e, context);
            e.Disconnected += Client_Disconnected;
            context.StartReceive();
        }

        private void Client_Disconnected(object sender, EventArgs e)
        {
            if(_connections.Remove((MyWebSocket)sender, out Context context))
            {

            }
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
