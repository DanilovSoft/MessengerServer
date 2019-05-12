using System;
using System.Collections.Generic;
using System.Text;

namespace wRPC
{
    public abstract class ClientController : Controller
    {
        /// <summary>
        /// Контекст подключения на стороне клиента.
        /// </summary>
        public ClientConnection Context { get; internal set; }
    }
}
