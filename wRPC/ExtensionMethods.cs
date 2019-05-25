using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;

namespace wRPC
{
    internal static class ExtensionMethods
    {
        private static volatile Encoding _UTF8NoBOM;
        static ExtensionMethods()
        {
            _UTF8NoBOM = new UTF8Encoding(false, true);
        }

        public static Message ErrorResponse(this RequestMessage request, RemoteException remoteException)
        {
            return new Message(request.Header.Uid, result: null, remoteException.Message, remoteException.ErrorCode);
        }

        public static Message ErrorResponse(this RequestMessage request, string errorMessage, ResultCode errorCode)
        {
            return new Message(request.Header.Uid, result: null, errorMessage, errorCode);
        }

        /// <summary>
        /// Сериализует объект в JSON.
        /// </summary>
        public static void SerializeObject(object value, Stream stream)
        {
            using (var writer = new StreamWriter(stream, _UTF8NoBOM, bufferSize: 1024, leaveOpen: true))
            using (var bson = new JsonTextWriter(writer))
            {
                var ser = new JsonSerializer();
                ser.Serialize(bson, value);
            }
        }

        //public static void SerializeObject(object value, Stream stream)
        //{
        //    using (var binaryWriter = new BinaryWriter(stream, new UTF8Encoding(false, true), leaveOpen: true))
        //    using (var bson = new BsonDataWriter(binaryWriter))
        //    {
        //        var ser = new JsonSerializer();
        //        ser.Serialize(bson, value);
        //    }
        //}

        /// <summary>
        /// Десериализует JSON.
        /// </summary>
        public static T Deserialize<T>(Stream stream)
        {
            using (var reader = new StreamReader(stream, _UTF8NoBOM, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
            using (var json = new JsonTextReader(reader))
            {
                var ser = new JsonSerializer();
                return ser.Deserialize<T>(json);
            }
        }

        /// <summary>
        /// Десериализует JSON.
        /// </summary>
        public static object Deserialize(Stream stream, Type objectType)
        {
            using (var reader = new StreamReader(stream, _UTF8NoBOM, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
            using (var json = new JsonTextReader(reader))
            {
                var ser = new JsonSerializer();
                return ser.Deserialize(json, objectType);
            }
        }

        ///// <summary>
        ///// Десериализует BSON.
        ///// </summary>
        //public static T Deserialize<T>(Stream stream)
        //{
        //    using (var binaryWriter = new BinaryReader(stream, new UTF8Encoding(), leaveOpen: true))
        //    using (var bson = new BsonDataReader(binaryWriter))
        //    {
        //        var ser = new JsonSerializer();
        //        return ser.Deserialize<T>(bson);
        //    }
        //}

        ///// <summary>
        ///// Десериализует BSON.
        ///// </summary>
        //public static object Deserialize(Stream stream, Type objectType)
        //{
        //    using (var binaryWriter = new BinaryReader(stream, new UTF8Encoding(), leaveOpen: true))
        //    using (var bson = new BsonDataReader(binaryWriter))
        //    {
        //        var ser = new JsonSerializer();
        //        return ser.Deserialize(bson, objectType);
        //    }
        //}
    }
}
