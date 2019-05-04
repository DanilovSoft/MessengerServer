﻿using DynamicMethodsLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace wRPC
{
    public class InterfaceProxy : TypeProxy
    {
        private readonly Context _context;
        private readonly string _controllerName;

        public InterfaceProxy((Context context, string controllerName) state)
        {
            _context = state.context;
            _controllerName = state.controllerName;
        }

        [DebuggerStepThrough]
        public override object Invoke(MethodInfo targetMethod, object[] args)
        {
            return _context.OnProxyCall(targetMethod, args, _controllerName);
        }
    }
}
