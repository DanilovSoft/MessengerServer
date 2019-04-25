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
using MyWebSocket = DanilovSoft.WebSocket.WebSocket;

namespace wRPC
{
    public sealed class Listener : IDisposable
    {
        private readonly WebSocketServer _wsServ;
        private readonly ConcurrentDictionary<MyWebSocket, Context> _connections;
        private bool _disposed;

        // ctor.
        public Listener(int port)
        {
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
            _connections.TryAdd(e, new Context(e));
            e.Disconnected += Client_Disconnected;
            ReceaveAsync(e);
        }

        private void Client_Disconnected(object sender, EventArgs e)
        {
            if(_connections.Remove((MyWebSocket)sender, out Context context))
            {

            }
        }

        private async void ReceaveAsync(MyWebSocket webSocket)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);
            try
            {
                ValueWebSocketReceiveResult message;
                try
                {
                    message = await webSocket.ReceiveAsync(buffer.AsMemory(), CancellationToken.None);
                }
                catch (Exception)
                {
                    return;
                }

                if (message.EndOfMessage)
                {
                    Request request = ProtoBuf.Serializer.Deserialize<Request>(new MemoryStream(buffer, 0, message.Count));
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
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
