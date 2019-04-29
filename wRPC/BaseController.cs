using System;
using System.Collections.Generic;
using System.Text;

namespace wRPC
{
    public abstract class BaseController : IDisposable
    {
        public Context Context { get; internal set; }

        public BaseController()
        {

        }

        public virtual void Dispose()
        {
            
        }
    }
}
