using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace wRPC
{
    internal static class ExtensionMethods
    {
        public static Message ErrorResponse(this Message request, RemoteException remoteException)
        {
            return new Message(request.Uid, result: null, remoteException.Message, remoteException.ErrorCode);
        }

        public static Message ErrorResponse(this Message request, string errorMessage, ErrorCode errorCode)
        {
            return new Message(request.Uid, result: null, errorMessage, errorCode);
        }

        public static ArrayPool Serialize(this Message request, out int size)
        {
            var ar = new ArrayPool(4096);
            using (var mem = new MemoryStream(ar.Array))
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

        public static void Serialize(object value, Stream stream)
        {
            using (var binaryWriter = new BinaryWriter(stream, new UTF8Encoding(false, true), leaveOpen: true))
            using (var bson = new BsonDataWriter(binaryWriter))
            {
                var ser = new JsonSerializer();
                ser.Serialize(bson, value);
            }
        }

        public static void Serialize(this Message message, Stream stream)
        {
            using (var binaryWriter = new BinaryWriter(stream, new UTF8Encoding(false, true), leaveOpen: true))
            using (var bson = new BsonDataWriter(binaryWriter))
            {
                var ser = new JsonSerializer();
                ser.Serialize(bson, message);
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

        public static object Deserialize(Stream stream, Type objectType)
        {
            using (var binaryWriter = new BinaryReader(stream, new UTF8Encoding(), leaveOpen: true))
            using (var bson = new BsonDataReader(binaryWriter))
            {
                var ser = new JsonSerializer();
                return ser.Deserialize(bson, objectType);
            }
        }
    }
}
