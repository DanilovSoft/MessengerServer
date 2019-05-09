using DanilovSoft.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
        /// Словарь используемый только для чтения, поэтому потокобезопасен.
        /// Хранит все доступные контроллеры. Не учитывает регистр.
        /// </summary>
        internal readonly Dictionary<string, Type> Controllers;
        private readonly WebSocketServer _wsServ;
        /// <summary>
        /// Коллекция авторизованных пользователей.
        /// Ключ словаря — UserId авторизованного пользователя.
        /// </summary>
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
            Assembly controllersAssembly = Assembly.GetCallingAssembly();

            // Сборка с контроллерами не должна быть текущей сборкой.
            Debug.Assert(controllersAssembly != Assembly.GetExecutingAssembly());

            // Найти контроллеры в сборке.
            Controllers = GlobalVars.FindAllControllers(controllersAssembly);

            // Добавить контроллеры в IoC.
            foreach (Type controllerType in Controllers.Values)
            {
                IoC.AddScoped(controllerType);
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
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _wsServ.Connected -= Listener_OnConnected;
                _wsServ.Dispose();
            }
        }
    }
}
