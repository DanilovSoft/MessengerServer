using System;
using System.Collections.Generic;
using System.Text;

namespace wRPC
{
    public static class Warmup
    {
        /// <summary>
        /// Выполняет AOT(Ahead-Of-Time) оптимизацию.
        /// </summary>
        public static void DoWarmup()
        {
            //System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Client).TypeHandle);
        }
    }
}
