using MsgPack.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace wRPC
{
    public class BearerToken
    {
        [MessagePackMember(1)]
        public byte[] Token;

        /// <summary>
        /// Время актуальности токена в секундах.
        /// </summary>
        [MessagePackMember(2)]
        public TimeSpan ExpiresAt;
    }
}
