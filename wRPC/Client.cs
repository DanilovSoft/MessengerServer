using Contract;
using DynamicMethodsLib;
using MsgPack;
using MsgPack.Serialization;
using Ninject;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyClientWebSocket = DanilovSoft.WebSocket.ClientWebSocket;

namespace wRPC
{
    public sealed class Client : ClientContext
    {
        // ctor.
        public Client(string host, int port) : base(Assembly.GetCallingAssembly(), new Uri($"ws://{host}:{port}"))
        {
            
        }

        /// <summary>
        /// Производит предварительное подключение сокета к серверу.
        /// </summary>
        public Task ConnectAsync()
        {
            return ConnectIfNeededAsync();
        }
    }
}
