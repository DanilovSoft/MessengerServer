using DynamicMethodsLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace wRPC
{
    public class InterfaceProxy : TypeProxy
    {
        private readonly Client _client;
        private readonly string _controllerName;

        public InterfaceProxy((Client client, string controllerName) state)
        {
            _client = state.client;
            _controllerName = state.controllerName;
        }

        [DebuggerStepThrough]
        public override object Invoke(MethodInfo targetMethod, object[] args)
        {
            return _client.OnProxyCall(targetMethod, args, _controllerName);
        }
    }
}
