using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace wRPC
{
    public abstract class Controller : IDisposable
    {
        public Controller()
        {

        }

        public virtual void Dispose()
        {
            
        }

        //protected virtual void OnException(Exception exception)
        //{

        //}
    }
}
