using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SerializatorFox
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class)]
    public class SerializeDepthAttribute : Attribute
    {
        public bool DeepSerialization { get; }
        public SerializeDepthAttribute(bool deepSerialization = true)
        {
            DeepSerialization = deepSerialization;
        }
    }
}
