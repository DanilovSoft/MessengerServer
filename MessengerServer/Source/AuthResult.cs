using DanilovSoft.MicroORM;
using System;
using System.Collections.Generic;
using System.Text;

namespace MessengerServer
{
    internal sealed class AuthResult
    {
        [SqlProperty("user_id")]
        public int UserId;

        [SqlProperty("login")]
        public string Login;

        [SqlProperty("avatar_url")]
        public string AvatarUrl;

        [SqlProperty("display_name")]
        public string DisplayName;
    }
}
