using System.Diagnostics;

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
