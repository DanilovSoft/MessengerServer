using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;

namespace Contract
{
    [ProtoContract]
    public class Request
    {
        [ProtoMember(1)]
        public string ActionName { get; set; }
    }
}
