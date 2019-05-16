using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text;

namespace wRPC
{
    [JsonObject]
    internal sealed class RequestMessage
    {
        [JsonIgnore]
        public Header Header;

        [JsonProperty]
        public string ActionName;

        [JsonProperty]
        public Arg[] Args;
    }
}
