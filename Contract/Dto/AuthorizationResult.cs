using System;
using System.Collections.Generic;
using System.Text;
using wRPC;

namespace Contract.Dto
{
    public sealed class AuthorizationResult
    {
        public BearerToken BearerToken;
        public int UserId;
        public string UserName;
        public Uri ImageUrl;
    }
}
