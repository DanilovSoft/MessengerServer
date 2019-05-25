using System.IO;
using System.Text;

namespace wRPC
{
    internal sealed class ResponseHeader
    {
        /// <summary>
        /// Фиксированный размер структуры.
        /// </summary>
        public const int Size = 1;

        public ResultCode ResultCode;

        public void Serialize(Stream stream)
        {
            using (var writer = new BinaryWriter(stream, Encoding.Default, leaveOpen: true))
            {
                writer.Write((byte)ResultCode);
            }
        }

        public static ResponseHeader Deserialize(Stream stream)
        {
            var header = new ResponseHeader();
            using (var reader = new BinaryReader(stream, Encoding.Default, leaveOpen: true))
            {
                header.ResultCode = (ResultCode)reader.ReadByte();
            }
            return header;
        }
    }
}
