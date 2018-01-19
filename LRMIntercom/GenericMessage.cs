using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace LRMIntercom
{
    [Serializable]
    public class GenericMessage
    {
        public string messageType = "GenericMessage";
        public string id;
        public string asId;
        public string snId;
        public byte snppId;

        public GenericMessage(string id, string asId, string snId, byte snppId)
        {
            this.id = id;
            this.asId = asId;
            this.snId = snId;
            this.snppId = snppId;
        }

        public static byte[] Serialize(GenericMessage genmsg)
        {
            using (var memoryStream = new MemoryStream())
            {
                (new BinaryFormatter()).Serialize(memoryStream, genmsg);
                return memoryStream.ToArray(); // Pass an array of serialized bytes as argument
            }
        }

        public static GenericMessage Deserialize(byte[] data)
        {
            using (var memoryStream = new MemoryStream(data))
            {
                return (GenericMessage)(new BinaryFormatter()).Deserialize(memoryStream);
            }
        }
    }
}
