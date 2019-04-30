using System;
using System.Collections.Generic;
using System.Text;

namespace wRPC
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AllowAnonymousAttribute : Attribute
    {

    }
}
