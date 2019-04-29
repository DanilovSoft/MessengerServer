using System;
using System.Collections.Generic;
using System.Text;

namespace Contract
{
    public enum ErrorCode : short
    {
        ActionNotFound = 404,
        InternalError = 500,

        /// <summary>
        /// Unprocessable Entity.
        /// </summary>
        InvalidRequestFormat = 422,
    }
}
