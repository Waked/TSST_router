using MPLS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPLS
{
    [Serializable]
    public class AggregatePacket
    {
        public MPLSPacket[] packets;

        public AggregatePacket(MPLSPacket[] packets)
        {
            this.packets = packets;
        }
    }
}
