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

        // ctor.
        public ServerController()
        {

        }
    }
}
