using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace wRPC
{
    public abstract class ServerController : Controller
    {
        /// <summary>
        /// Контекст подключения на стороне сервера.
        /// </summary>
        public ServerContext Context { get; internal set; }

        /// <summary>
        /// Шорткат для Context.UserId.Value.
        /// </summary>
        public int UserId => Context.UserId.Value;

        // ctor.
        public ServerController()
        {

        }
    }
}
