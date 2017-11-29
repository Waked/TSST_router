using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace Routing
{
    public static class RouteInfoMethods
    {
        public static byte[] Serialize(RouteOrder order)
        {
            using (var memoryStream = new MemoryStream())
            {
                (new BinaryFormatter()).Serialize(memoryStream, order);
                return memoryStream.ToArray();
            }
        }

        public static RouteOrder Deserialize(byte[] data)
        {
            using (var memoryStream = new MemoryStream(data))
                return (RouteOrder)(new BinaryFormatter()).Deserialize(memoryStream);
        }
    }
}
