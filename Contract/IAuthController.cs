﻿using Contract.Dto;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using wRPC;

namespace Contract
{
    [ControllerContract("Auth")]
    public interface IAuthController
    {
        Task<AuthorizationResult> Authorize(string login, string password);
    }
}
