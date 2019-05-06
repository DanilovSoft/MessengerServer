using System;
using System.Reflection;
using System.Threading.Tasks;

namespace wRPC
{
    public sealed class Client : ClientContext
    {
        // ctor.
        public Client(Uri uri) : base(Assembly.GetCallingAssembly(), uri)
        {

        }

        // ctor.
        public Client(string host, int port) : base(Assembly.GetCallingAssembly(), new Uri($"ws://{host}:{port}"))
        {
            
        }

        /// <summary>
        /// Производит предварительное подключение сокета к серверу.
        /// </summary>
        public Task ConnectAsync()
        {
            return ConnectIfNeededAsync();
        }
    }
}
