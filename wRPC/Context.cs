using DanilovSoft.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;

namespace wRPC
{
    public class Context
    {
        private readonly WebSocket _webSocket;

        public Context(WebSocket webSocket)
        {
            _webSocket = webSocket;
        }
    }
}
