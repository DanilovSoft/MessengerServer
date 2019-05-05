﻿using Ninject;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using MyClientWebSocket = DanilovSoft.WebSocket.ClientWebSocket;
using MyWebSocket = DanilovSoft.WebSocket.WebSocket;

namespace wRPC
{
    [DebuggerDisplay("{DebugDisplay,nq}")]
    public sealed class ServerContext : Context
    {
        #region Debug
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebugDisplay => "{" + $"{{{GetType().Name}}}, UserId = {UserId?.ToString() ?? "Null"}" + "}";
        #endregion
        /// <summary>
        /// Объект синхронизации текущего экземпляра.
        /// </summary>
        private readonly object _syncObj = new object();
        /// <summary>
        /// Сервер который принял текущее соединение.
        /// </summary>
        public Listener Listener { get; }
        public int? UserId { get; private set; }
        /// <summary>
        /// <see langword="true"/> если клиент авторизован сервером.
        /// </summary>
        public bool IsAuthorized { get; private set; }
        /// <summary>
        /// Смежные соединения текущего пользователя.
        /// </summary>
        public UserConnections Connections { get; private set; }
        private readonly RijndaelEnhanced _jwt;

        public ServerContext(MyWebSocket clientConnection, StandardKernel ioc, Listener listener) : base(clientConnection, ioc)
        {
            Listener = listener;

            // Копируем список контроллеров сервера.
            Controllers = listener.Controllers;

            string passPhrase = "Pas5pr@se";        // can be any string
            string initVector = "@1B2c3D4e5F6g7H8"; // must be 16 bytes
            _jwt = new RijndaelEnhanced(passPhrase, initVector);

            // Начать обработку запросов текущего пользователя.
            StartReceivingLoop(WebSocket);
        }

        /// <summary>
            /// Производит авторизацию текущего подключения.
            /// </summary>
            /// <param name="userId"></param>
            /// <exception cref="RemoteException"/>
        public BearerToken Authorize(int userId)
        {
            // Функцию могут вызвать из нескольких потоков.
            lock (_syncObj)
            {
                InnerAuthorize(userId);

                var tokenValidity = TimeSpan.FromDays(2);
                var serverBearer = new ServerBearerToken
                {
                    UserId = userId,
                    Validity = DateTime.Now + tokenValidity,
                };

                byte[] serialized;
                using (var mem = new MemoryStream(capacity: 18))
                {
                    ProtoBuf.Serializer.Serialize(mem, serverBearer);
                    serialized = mem.ToArray();
                }

                // Закриптовать в бинарник идентификатор пользователя.
                byte[] encryptedToken = _jwt.EncryptToBytes(serialized);

                var token = new BearerToken
                {
                    Token = encryptedToken,
                    ExpiresAt = tokenValidity
                };

                return token;
            }
        }

        /// <summary>
        /// Производит авторизацию текущего подключения по токену.
        /// </summary>
        /// <param name="userId"></param>
        /// <exception cref="RemoteException"/>
        public void AuthorizeToken(byte[] encriptedToken)
        {
            byte[] decripted = _jwt.DecryptToBytes(encriptedToken);

            ServerBearerToken bearerToken;
            using (var mem = new MemoryStream(decripted))
            {
                bearerToken = ProtoBuf.Serializer.Deserialize<ServerBearerToken>(mem);
            }

            if (DateTime.Now < bearerToken.Validity)
            {
                InnerAuthorize(bearerToken.UserId);
            }
        }

        private void InnerAuthorize(int userId)
        {
            // Функцию могут вызвать из нескольких потоков.
            lock (_syncObj)
            {
                if (!IsAuthorized)
                {
                    // Авторизуем контекст пользователя.
                    UserId = userId;
                    IsAuthorized = true;

                    // Добавляем соединение в словарь.
                    Connections = AddConnection(userId);

                    // Подпишемся на дисконнект.
                    // Событие сработает даже если соединение уже разорвано.
                    WebSocket.Disconnected += WebSocket_Disconnected;
                }
                else
                    throw new RemoteException($"You are already authorized as 'UserId: {UserId}'", ErrorCode.BadRequest);
            }
        }

        /// <summary>
        /// Потокобезопасно добавляет текущее соединение в словарь.
        /// </summary>
        private UserConnections AddConnection(int userId)
        {
            do
            {
                // Берем существующую структуру или создаем новую.
                UserConnections userConnections = Listener.Connections.GetOrAdd(userId, uid => new UserConnections(uid));

                // Может случиться так что мы взяли существующую коллекцию но её удаляют из словаря в текущий момент.
                lock (userConnections.SyncRoot) // Захватить эксклюзивный доступ.
                {
                    // Если коллекцию еще не удалили из словаря то можем безопасно добавить в неё соединение.
                    if (!userConnections.IsDestroyed)
                    {
                        userConnections.Add(this);
                        return userConnections;
                    }
                }
            } while (true);
        }

        private void WebSocket_Disconnected(object sender, EventArgs e)
        {
            // Копия на случай null.
            UserConnections cons = Connections;

            if (cons != null)
            {
                // Захватить эксклюзивный доступ.
                lock (cons.SyncRoot)
                {
                    // Текущее соединение нужно безусловно удалить.
                    if (cons.Remove(this))
                    {
                        // Если соединений больше не осталось то удалить себя из словаря.
                        if (!cons.IsDestroyed && cons.Count == 0)
                        {
                            // Использовать текущую структуру больше нельзя.
                            cons.IsDestroyed = true;

                            Listener.Connections.TryRemove(UserId.Value, out _);
                        }
                    }
                }
            }
        }

        protected override void BeforeInvokePrepareController(Controller controller)
        {
            var serverController = (ServerController)controller;
            serverController.Context = this;
            //serverController.Listener = Listener;
        }

        /// <summary>
        /// Проверяет доступность запрашиваемого метода пользователем.
        /// </summary>
        /// <exception cref="RemoteException"/>
        protected override void InvokeMethodPermissionCheck(MethodInfo method, Type controllerType)
        {
            // Проверить доступен ли метод пользователю.
            if (IsAuthorized)
                return;

            // Разрешить если метод помечен как разрешенный для не авторизованных пользователей.
            if (Attribute.IsDefined(method, typeof(AllowAnonymousAttribute)))
                return;

            // Разрешить если контроллер помечен как разрешенный для не акторизованных пользователей.
            if (Attribute.IsDefined(controllerType, typeof(AllowAnonymousAttribute)))
                return;

            throw new RemoteException("The request requires user authentication", ErrorCode.Unauthorized);
        }

        public override void Dispose()
        {
            base.Dispose();
            _jwt.Dispose();
        }
    }
}
