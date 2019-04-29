using Contract;
using MsgPack;
using MsgPack.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace wRPC
{
    internal static class ExtensionMethods
    {
        public static Response ErrorResponse(this Request request, RemoteException exception)
        {
            return new Response(request.Uid, MessagePackObject.Nil, exception.Message, exception.ErrorCode);
        }

        public static Response ErrorResponse(this Request request, string errorMessage, ErrorCode errorCode)
        {
            return new Response(request.Uid, MessagePackObject.Nil, errorMessage, errorCode);
        }

        public static byte[] Serialize(this Request request)
        {
            byte[] buffer;
            using (var mem = new MemoryStream())
            {
                var ser = MessagePackSerializer.Get<Request>();
                ser.Pack(mem, request);
                buffer = mem.ToArray();
            }
            return buffer;
        }
    }
}
