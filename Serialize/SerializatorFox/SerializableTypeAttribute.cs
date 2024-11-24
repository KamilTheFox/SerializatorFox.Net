using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SerializatorFox
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class SerializableTypeAttribute : Attribute
    {
        public int Version { get; }

        public SerializationType type { get; }

        public SerializableTypeAttribute(int version = 1, SerializationType Types = SerializationType.All)
        {
            Version = version;
            type = Types;
        }
    }
}
