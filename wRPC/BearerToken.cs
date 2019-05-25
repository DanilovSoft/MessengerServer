using Newtonsoft.Json;
using System;

namespace wRPC
{
    [JsonObject]
    public sealed class BearerToken
    {
        /// <summary>
        /// Зашифрованное тело токена.
        /// </summary>
        [JsonProperty]
        public byte[] Key;

        /// <summary>
        /// Время актуальности токена.
        /// </summary>
        [JsonProperty]
        public TimeSpan ExpiresAt;
    }
}
