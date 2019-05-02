using Contract;
using DanilovSoft.WebSocket;
using MsgPack.Serialization;
using Ninject;
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
        internal readonly Dictionary<string, Type> _controllers;
        private readonly WebSocketServer _wsServ;
        /// <summary>
        /// Ключ словаря должен быть сериализуемым идентификатором.
        /// </summary>
        internal readonly ConcurrentDictionary<int, UserConnections> Connections = new ConcurrentDictionary<int, UserConnections>();
        private bool _disposed;
        public StandardKernel IOC { get; }
        private int _startAccept;

        static Listener()
        {
            MessagePackSerializer.PrepareType<Request>();
            MessagePackSerializer.PrepareType<Response>();
        }

        // ctor.
        public Listener(int port)
        {
            IOC = new StandardKernel();
            _wsServ = new WebSocketServer();
            _wsServ.Bind(new IPEndPoint(IPAddress.Any, port));
            _wsServ.Connected += WebSocket_OnConnected;

            // Контроллеры будем искать в сборке которая вызвала текущую функцию.
            var controllersAssembly = Assembly.GetCallingAssembly();

            _controllers = FindAllControllers(controllersAssembly);
            foreach (Type controllerType in _controllers.Values)
            {
                IOC.Bind(controllerType).ToSelf();
            }
        }

        private static Dictionary<string, Type> FindAllControllers(Assembly assembly)
        {
            var controllers = new Dictionary<string, Type>(StringComparer.InvariantCultureIgnoreCase);
            Type[] types = assembly.GetTypes();

            foreach (Type controllerType in types)
            {
                if (controllerType.IsSubclassOf(typeof(Controller)))
                {
                    controllers.Add(controllerType.Name, controllerType);
                }
            }
            return controllers;
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

        private void WebSocket_OnConnected(object sender, MyWebSocket e)
        {
            // Создать контекст для текущего подключения.
            var context = new Context(e, IOC, this);

            // Начать обработку запросов текущего пользователя.
            context.StartReceive();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _wsServ.Connected -= WebSocket_OnConnected;
                _wsServ.Dispose();
                IOC.Dispose();
            }
        }
    }
}
