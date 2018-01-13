using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TSSTRouter
{
    partial class LinkResourceManager
    {
        class BandwidthManagement
        {
            Dictionary<byte, BWPair> database = new Dictionary<byte, BWPair>();

            public BandwidthManagement(Dictionary<byte, uint> interfaceDefinitions)
            {
                foreach (var kvp in interfaceDefinitions)
                {
                    database[kvp.Key] = new BWPair(kvp.Value, 0);
                }
            }

            public uint AvailableBandwidthAt(byte interfaceId)
            {
                BWPair bwpair = database[interfaceId];
                return bwpair.capacityBw - bwpair.assignedBw;
            }

            public bool AvailableBandwidthAt(byte interfaceId, uint bandwidth)
            {
                return AvailableBandwidthAt(interfaceId) >= bandwidth;
            }

            public void AssignBandwidth(byte interfaceId, uint bandwidth)
            {
                database[interfaceId].assignedBw += bandwidth;
            }

            public void FreeBandwidth(byte interfaceId, uint bandwidth)
            {
                database[interfaceId].assignedBw -= interfaceId;
            }

            class BWPair
            {
                public uint capacityBw;
                public uint assignedBw;
                public BWPair(uint cap, uint assgn)
                {
                    capacityBw = cap;
                    assignedBw = assgn;
                }
            }
        }
    }
}
