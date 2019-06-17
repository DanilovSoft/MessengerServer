using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace wRPC
{
    public interface IActionResult
    {
        void ExecuteResult(ActionContext context);
    }
}
