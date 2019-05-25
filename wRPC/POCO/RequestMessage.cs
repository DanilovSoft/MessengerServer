using Newtonsoft.Json;

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
