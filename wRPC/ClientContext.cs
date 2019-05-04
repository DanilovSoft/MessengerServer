﻿using Contract;
using MsgPack.Serialization;
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
    public class ClientContext : Context
    {
        private readonly AsyncLock _asyncLock;
        private readonly Uri _uri;

        public ClientContext(Assembly callingAssembly, Uri uri) : base(callingAssembly)
        {
            _uri = uri;
            _asyncLock = new AsyncLock();
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

                        //var context = Context = new Context(ws, IoC, listener: null);
                        _connected = true;
                        ThreadPool.UnsafeQueueUserWorkItem(StartReceivingLoop, ws);
                    }
                }
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

        public override void Dispose()
        {
            base.Dispose();
            _asyncLock.Dispose();
        }
    }
}
