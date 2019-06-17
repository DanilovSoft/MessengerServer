﻿using System.Security;
using System.Threading.Tasks;
using Dto;
using wRPC;

namespace Contract
{
    [ControllerContract("Auth")]
    public interface IAuthController
    {
        Task<AuthorizationResult> Authorize(string login, string password);
        Task<AuthorizationResult> Register(string login, string password);
    }
}
