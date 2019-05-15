using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Text;

namespace wRPC
{
    [ProtoContract]
    internal sealed class Header
    {
        /// <summary>
        /// Фиксированный размер структуры.
        /// </summary>
        public const int Size = 9;
        public bool IsRequest;
        public int Uid;
        public int ContentLength;

        public void Serialize(Stream stream)
        {
            using (var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true))
            {
                writer.Write(IsRequest);
                writer.Write(Uid);
                writer.Write(ContentLength);
            }
        }

        public static Header Deserialize(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: true))
            {
                var header = new Header();
                header.IsRequest = reader.ReadBoolean();
                header.Uid = reader.ReadInt32();
                header.ContentLength = reader.ReadInt32();

                return header;
            }
        }
    }
}
