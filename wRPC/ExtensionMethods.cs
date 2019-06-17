using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;

namespace wRPC
{
    internal static class ExtensionMethods
    {
        private static readonly Encoding _UTF8NoBOM;

        // ctor.
        static ExtensionMethods()
        {
            _UTF8NoBOM = new UTF8Encoding(false, true);
        }

        /// <summary>
        /// Сериализует объект в JSON.
        /// </summary>
        public static void SerializeObjectJson(Stream destination, object instance)
        {
            using (var writer = new StreamWriter(destination, _UTF8NoBOM, bufferSize: 1024, leaveOpen: true))
            using (var json = new JsonTextWriter(writer))
            {
                var ser = new JsonSerializer();
                ser.Serialize(json, instance);
            }
        }

        public static bool TryReadLengthPrefix(Stream source, out int length)
        {
            return ProtoBuf.Serializer.TryReadLengthPrefix(source, ProtoBuf.PrefixStyle.Base128, out length);
        }

        internal static object DeserializeProtobuf(Stream source, Type type)
        {
            return ProtoBuf.Serializer.Deserialize(type, source);
        }

        internal static void SerializeObjectProtobuf(Stream destination, object instance)
        {
            ProtoBuf.Serializer.Serialize(destination, instance);
        }

        /// <summary>
        /// Десериализует JSON.
        /// </summary>
        public static T DeserializeJson<T>(Stream stream)
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
        public static object DeserializeJson(Stream stream, Type objectType)
        {
            using (var reader = new StreamReader(stream, _UTF8NoBOM, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
            using (var json = new JsonTextReader(reader))
            {
                var ser = new JsonSerializer();
                return ser.Deserialize(json, objectType);
            }
        }

        /// <summary>
        /// Читает строку в формате Utf-8.
        /// </summary>
        public static string ReadAsString(this Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                // Десериализовать тело как строку.
                string errorMessage = reader.ReadString();
                return errorMessage;
            }
        }

        /// <summary>
        /// Записывает строку в формате Utf-8.
        /// </summary>
        public static void WriteStringBinary(this Stream destination, string message)
        {
            using (var writer = new BinaryWriter(destination, Encoding.UTF8, leaveOpen: true))
                writer.Write(message);
        }
    }
}
