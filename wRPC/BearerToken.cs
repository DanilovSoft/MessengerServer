using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace wRPC
{
    [JsonObject]
    public class BearerToken
    {
        [JsonProperty]
        public byte[] Token;

        /// <summary>
        /// Время актуальности токена в секундах.
        /// </summary>
        [JsonProperty]
        public TimeSpan ExpiresAt;
    }
}
