using Contract;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace wRPC
{
    internal static class ExtensionMethods
    {
        public static Message ErrorResponse(this Message request, RemoteException exception)
        {
            return new Message(request.Uid, result: null, exception.Message, exception.ErrorCode);
        }

        public static Message ErrorResponse(this Message request, string errorMessage, ErrorCode errorCode)
        {
            return new Message(request.Uid, result: null, errorMessage, errorCode);
        }

        public static ArrayPool Serialize(this Message request, out int size)
        {
            var ar = new ArrayPool(4096);
            using (var mem = new MemoryStream(ar.Buffer))
            {
                try
                {
                    using (var binaryWriter = new BinaryWriter(mem, new UTF8Encoding(false, true), leaveOpen: true))
                    using (var bson = new BsonDataWriter(binaryWriter))
                    {
                        var ser = new JsonSerializer();
                        ser.Serialize(bson, request);
                    }
                }
                catch
                {
                    ar.Dispose();
                    throw;
                }
                size = (int)mem.Position;
            }
            return ar;
        }

        public static void Serialize(this Message request, Stream stream)
        {
            using (var binaryWriter = new BinaryWriter(stream, new UTF8Encoding(false, true), leaveOpen: true))
            using (var bson = new BsonDataWriter(binaryWriter))
            {
                var ser = new JsonSerializer();
                ser.Serialize(bson, request);
            }
        }

        public static T Deserialize<T>(Stream stream)
        {
            using (var binaryWriter = new BinaryReader(stream, new UTF8Encoding(), leaveOpen: true))
            using (var bson = new BsonDataReader(binaryWriter))
            {
                var ser = new JsonSerializer();
                return ser.Deserialize<T>(bson);
            }
        }
    }
}
