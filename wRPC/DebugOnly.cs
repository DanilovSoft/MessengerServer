using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace wRPC
{
    internal static class DebugOnly
    {
        [DebuggerStepThrough]
        [Conditional("DEBUG")]
        public static void Break()
        {
            if (Debugger.IsAttached)
                Debugger.Break();
        }
    }
}
