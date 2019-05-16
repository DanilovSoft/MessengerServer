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
        public static Message ErrorResponse(this RequestMessage request, RemoteException remoteException)
        {
            return new Message(request.Header.Uid, result: null, remoteException.Message, remoteException.ErrorCode);
        }

        public static Message ErrorResponse(this RequestMessage request, string errorMessage, ResultCode errorCode)
        {
            return new Message(request.Header.Uid, result: null, errorMessage, errorCode);
        }

        public static void SerializeObject(object value, Stream stream)
        {
            using (var binaryWriter = new BinaryWriter(stream, new UTF8Encoding(false, true), leaveOpen: true))
            using (var bson = new BsonDataWriter(binaryWriter))
            {
                var ser = new JsonSerializer();
                ser.Serialize(bson, value);
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
