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


            // Returns bandwidth that is assigned on given interface.
            public uint AssignedBandwidthAt(byte interfaceId)
            {
                BWPair bwpair = database[interfaceId];
                return bwpair.assignedBw;
            }

            // Returns bandwidth that is available on given interface.
            public uint AvailableBandwidthAt(byte interfaceId)
            {
                BWPair bwpair = database[interfaceId];
                return bwpair.capacityBw - bwpair.assignedBw;
            }

            // Returns true if given requested bandwidth is available on given SNPP,
            // or false if there is not.
            public bool AvailableBandwidthAt(byte interfaceId, uint bandwidth)
            {
                return AvailableBandwidthAt(interfaceId) >= bandwidth;
            }

            // Allocates given bandwidth on given SNPP (interface).
            public void AssignBandwidth(byte interfaceId, uint bandwidth)
            {
                database[interfaceId].assignedBw += bandwidth;
            }

            // Deallocates given bandwidth from given SNPP (interface).
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
