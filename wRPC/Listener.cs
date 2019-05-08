using Contract;
using DanilovSoft.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyWebSocket = DanilovSoft.WebSocket.WebSocket;

namespace wRPC
{
    public sealed class Listener : IDisposable
    {
        /// <summary>
        /// Потокобезопасный словарь используемый только для чтения.
        /// Хранит все доступные контроллеры. Не учитывает регистр.
        /// </summary>
        internal readonly Dictionary<string, Type> Controllers;
        private readonly WebSocketServer _wsServ;
        // Ключ словаря должен быть сериализуемым идентификатором.
        public ConcurrentDictionary<int, UserConnections> Connections { get; } = new ConcurrentDictionary<int, UserConnections>();
        ///// <summary>
        ///// Хранит не авторизованные подключения.
        ///// </summary>
        //private readonly SafeList<Context> _contextList = new SafeList<Context>();
        private bool _disposed;
        public ServiceCollection IoC { get; }
        private int _startAccept;

        // ctor.
        public Listener(int port)
        {
            IoC = new ServiceCollection();
            _wsServ = new WebSocketServer();
            _wsServ.Bind(new IPEndPoint(IPAddress.Any, port));
            _wsServ.Connected += Listener_OnConnected;

            // Контроллеры будем искать в сборке которая вызвала текущую функцию.
            var controllersAssembly = Assembly.GetCallingAssembly();
            Controllers = GlobalVars.FindAllControllers(controllersAssembly);

            foreach (Type controllerType in Controllers.Values)
            {
                IoC.AddScoped(controllerType);
                //IoC.Bind(controllerType).ToSelf();
            }
        }

        public void StartAccept()
        {
            if (Interlocked.CompareExchange(ref _startAccept, 1, 0) == 0)
            {
                _wsServ.StartAccept();
            }
            else
                throw new InvalidOperationException("Already started");
        }

        private void Listener_OnConnected(object sender, MyWebSocket clientConnection)
        {
            // Создать контекст для текущего подключения.
            var context = new ServerContext(clientConnection, IoC, this);

            //_contextList.Add(context);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _wsServ.Connected -= Listener_OnConnected;
                _wsServ.Dispose();
                //IoC.Dispose();
            }
        }
    }
}
