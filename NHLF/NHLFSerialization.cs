using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace NHLF
{
    public static class NHLFSerialization
    {
        public static byte[] Serialize(NHLFMgmtMessage order)
        {
            using (var memoryStream = new MemoryStream())
            {
                (new BinaryFormatter()).Serialize(memoryStream, order);
                return memoryStream.ToArray();
            }
        }

        public static NHLFMgmtMessage Deserialize(byte[] data)
        {
            using (var memoryStream = new MemoryStream(data))
                return (NHLFMgmtMessage)(new BinaryFormatter()).Deserialize(memoryStream);
        }
    }
}
