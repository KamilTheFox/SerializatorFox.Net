using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SerializatorFox
{
    public enum SerializationType
    {
        None = 0,
        PublicFields = 1 << 0,
        PrivateFields = 1 << 1,
        Properties = 1 << 2,
        All = PublicFields | PrivateFields | Properties
    }
}
