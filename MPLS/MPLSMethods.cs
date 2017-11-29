using MPLS;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace MPLS
{
    public static class MPLSMethods
    {
        // Metoda serializująca obiekt do MemoryStream, następnie do Byte[] i opakowująca to w Message
        public static BinaryWrapper Serialize(AggregatePacket packet)
        {
            using (var memoryStream = new MemoryStream())
            {
                (new BinaryFormatter()).Serialize(memoryStream, packet);
                return new BinaryWrapper(memoryStream.ToArray()); // Pass an array of serialized bytes as argument
            }
        }

        // Metoda deserializująca dane z wrappera Message
        public static AggregatePacket Deserialize(BinaryWrapper message)
        {
            using (var memoryStream = new MemoryStream(message.data))
                return (AggregatePacket)(new BinaryFormatter()).Deserialize(memoryStream);
        }

        // Dodatkowa metoda do przekształcania kolejki interfejsu bezpośrednio do Message
        // (nie jest bardzo wymagana do pracy programu)
        public static BinaryWrapper PackQueueIntoMsg(Queue<MPLSPacket> queue)
        {
            AggregatePacket aggrPacket = new AggregatePacket(queue.ToArray());
            return Serialize(aggrPacket);
        }
    }
}
