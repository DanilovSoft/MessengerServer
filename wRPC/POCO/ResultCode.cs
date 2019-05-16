﻿using System;
using System.Collections.Generic;
using System.Text;

namespace wRPC
{
    public enum ResultCode : byte
    {
        Ok = 20,
        BadRequest = 40,
        Unauthorized = 41,
        ActionNotFound = 44,
        /// <summary>
        /// Unprocessable Entity.
        /// </summary>
        InvalidRequestFormat = 42,
        InternalError = 50,
    }
}
