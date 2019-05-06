using Contract;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyClientWebSocket = DanilovSoft.WebSocket.ClientWebSocket;

namespace wRPC
{
    [DebuggerDisplay("{DebugDisplay,nq}")]
    public class ClientContext : Context
    {
        #region Debug
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebugDisplay => "{" + $"{{{GetType().Name}}}, Connected = {_connected}" + "}";
        #endregion
        private readonly AsyncLock _asyncLock;
        private readonly Uri _uri;
        private volatile bool _connected;
        public byte[] BearerToken { get; set; }

        public ClientContext(Assembly callingAssembly, Uri uri) : base(callingAssembly)
        {
            _uri = uri;
            _asyncLock = new AsyncLock();

            WebSocket = new MyClientWebSocket();
            WebSocket.Disconnected += WebSocket_Disconnected;
        }

        private async void WebSocket_Disconnected(object sender, EventArgs e)
        {
            var ws = (MyClientWebSocket)sender;
            ws.Disconnected -= WebSocket_Disconnected;
            ws.Dispose();

            if (_connected)
            {
                using (await _asyncLock.LockAsync().ConfigureAwait(false))
                {
                    if (_connected)
                    {
                        _connected = false;
                        WebSocket = new MyClientWebSocket();
                    }
                }
            }
        }

        /// <summary>
        /// Выполнить подключение сокета если еще не подключен.
        /// </summary>
        /// <returns></returns>
        internal async Task ConnectIfNeededAsync()
        {
            // Fast-path.
            if (_connected)
            {
                return;
            }
            else
            {
                using (await _asyncLock.LockAsync().ConfigureAwait(false))
                {
                    if (!_connected)
                    {
                        // Копия ссылки.
                        var ws = (MyClientWebSocket)WebSocket;

                        try
                        {
                            await ws.ConnectAsync(_uri).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex);
                            ws.Dispose();
                            throw;
                        }

                        ThreadPool.UnsafeQueueUserWorkItem(StartReceivingLoop, ws);

                        try
                        {
                            await AuthorizeAsync(ws).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        // Обрыв соединения.
                        {
                            Debug.WriteLine(ex);
                            throw;
                        }

                        _connected = true;
                    }
                }
            }
        }

        private async Task AuthorizeAsync(WebSocket ws)
        {
            var token = BearerToken;
            if (token != null)
            {
                var message = new Message("Auth/AuthorizeToken")
                {
                    Args = new Message.Arg[]
                    {
                        new Message.Arg("token", BearerToken)
                    }
                };
                await ExecuteRequestAsync(message, typeof(void), doConnect: false).ConfigureAwait(false);
            }
        }

        ///// <summary>
        ///// Запускает бесконечный цикл считывающий из сокета зпросы и ответы.
        ///// </summary>
        //private async void StartReceivingLoop(object state)
        //{
        //    var ws = (MyClientWebSocket)state;
        //    using (var rentMem = new ArrayPool(4096))
        //    {
        //        using (var stream = new MemoryStream(rentMem.Buffer))
        //        {
        //            while (ws.State == WebSocketState.Open)
        //            {
        //                //stream.TryGetBuffer(out var segment);
        //                WebSocketReceiveResult message;
        //                try
        //                {
        //                    message = await ws.ReceiveAsync(new ArraySegment<byte>(rentMem.Buffer), CancellationToken.None);
        //                }
        //                catch (Exception ex)
        //                // Разрыв соединения.
        //                {
        //                    Debug.WriteLine(ex);

        //                    // Завершить текущий поток.
        //                    return;
        //                }

        //                stream.Position = 0;

        //                #region Десериализация.

        //                Response response;
        //                try
        //                {
        //                    response = MessagePackSerializer.Get<Response>().Unpack(stream);
        //                }
        //                catch (Exception ex)
        //                // Не должно быть ошибок десериализации.
        //                {
        //                    Debug.WriteLine(ex);
        //                    try
        //                    {
        //                        await ws.CloseAsync(WebSocketCloseStatus.ProtocolError, $"Unable to deserialize type \"{typeof(Response).Name}\"", CancellationToken.None);
        //                    }
        //                    catch { }
        //                    return;
        //                }
        //                #endregion

        //                // Передать ответ в очередь запросов.
        //                _requestQueue.OnResponse(response);
        //            }
        //        }
        //    }
        //}

        protected override void BeforeInvokePrepareController(Controller controller)
        {
            var clientController = (ClientController)controller;
            clientController.Context = this;
        }

        // Серверу всегда доступны методы клиента.
        protected override void InvokeMethodPermissionCheck(MethodInfo method, Type controllerType) { }

        public override void Dispose()
        {
            base.Dispose();
            _asyncLock.Dispose();
        }
    }
}
