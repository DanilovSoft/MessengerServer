using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace wRPC
{
    internal static class GlobalVars
    {
        //public static MessagePackSerializer<Message> MessageSerializer { get; }

        static GlobalVars()
        {
            //MessageSerializer = MessagePackSerializer.Get<Message>();
        }

        public static readonly Action DummyAction = delegate { };

        public static Dictionary<string, Type> FindAllControllers(Assembly assembly)
        {
            var controllers = new Dictionary<string, Type>(StringComparer.InvariantCultureIgnoreCase);
            Type[] types = assembly.GetExportedTypes();

            foreach (Type controllerType in types)
            {
                if (controllerType.IsSubclassOf(typeof(Controller)))
                {
                    controllers.Add(controllerType.Name, controllerType);
                }
            }
            return controllers;
        }
    }
}
