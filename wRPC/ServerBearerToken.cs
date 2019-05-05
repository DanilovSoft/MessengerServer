using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace wRPC
{
    [ProtoContract]
    internal struct ServerBearerToken
    {
        [ProtoMember(1)]
        public int UserId;

        [ProtoMember(2, DataFormat = DataFormat.WellKnown)]
        public DateTime Validity;
    }
}
