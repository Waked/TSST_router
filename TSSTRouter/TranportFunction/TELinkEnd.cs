using MPLS;
using System;
using System.Collections.Generic;

namespace TSSTRouter
{
    partial class TransportFunction
    {
        class TELinkEnd
        {
            public byte Id { get; }
            public uint Capacity { get; }
            private Queue<MPLSPacket> packetQueue;

            public TELinkEnd(KeyValuePair<byte, uint> definition)
            {
                Id = definition.Key;
                Capacity = definition.Value;
                packetQueue = new Queue<MPLSPacket>();
            }

            public void EnqueuePacket(MPLSPacket packet)
            {
                packetQueue.Enqueue(packet);
            }

            public MPLSPacket[] GetPacketsAndEmptyQueue()
            {
                MPLSPacket[] returnArray = packetQueue.ToArray();
                packetQueue.Clear();
                return returnArray;
            }
        }
    }
}
