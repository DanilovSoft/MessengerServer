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
        public static Message ErrorResponse(this Message request, RemoteException exception)
        {
            return new Message(request.Uid, MessagePackObject.Nil, exception.Message, exception.ErrorCode);
        }

        public static Message ErrorResponse(this Message request, string errorMessage, ErrorCode errorCode)
        {
            return new Message(request.Uid, MessagePackObject.Nil, errorMessage, errorCode);
        }

        public static ArrayPool Serialize(this Message request, out int size)
        {
            var ar = new ArrayPool(4096);
            using (var mem = new MemoryStream(ar.Buffer))
            {
                try
                {
                    GlobalVars.MessageSerializer.Pack(mem, request);
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
    }
}
