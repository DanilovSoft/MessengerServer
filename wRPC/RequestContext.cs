using System;
using System.Collections.Generic;
using System.Text;

namespace wRPC
{
    /// <summary>
    /// Контекст запроса.
    /// </summary>
    internal sealed class RequestContext
    {
        //public RequestMessage RequestMessage { get; set; }

        /// <summary>
        /// Запрашиваемый экшен контроллера.
        /// </summary>
        public ControllerAction ActionToInvoke { get; internal set; }
    }
}
