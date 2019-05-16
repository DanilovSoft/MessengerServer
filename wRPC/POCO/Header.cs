using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Text;

namespace wRPC
{
    internal sealed class Header
    {
        /// <summary>
        /// Фиксированный размер структуры.
        /// </summary>
        public const int Size = 7;
        public bool IsRequest;
        public short Uid;
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
            var header = new Header();
            using (var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: true))
            {
                header.IsRequest = reader.ReadBoolean();
                header.Uid = reader.ReadInt16();
                header.ContentLength = reader.ReadInt32();
            }
            return header;
        }
    }
}
