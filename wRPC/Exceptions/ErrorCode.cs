using System;
using System.Collections.Generic;
using System.Text;

namespace wRPC
{
    public enum ErrorCode : short
    {
        BadRequest = 400,
        Unauthorized = 401,
        ActionNotFound = 404,
        /// <summary>
        /// Unprocessable Entity.
        /// </summary>
        InvalidRequestFormat = 422,
        InternalError = 500,
    }
}
