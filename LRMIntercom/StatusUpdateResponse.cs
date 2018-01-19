using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LRMIntercom
{
    [Serializable]
    public class StatusUpdateResponse : GenericMessage
    {
        public uint assignedBw;
        public uint availableBw;

        public StatusUpdateResponse(string id, string asId, string snId, byte snppId, uint assignedBw, uint availableBw)
            : base(id, asId, snId, snppId)
        {
            messageType = "StatusUpdateResponse";
            this.assignedBw = assignedBw;
            this.availableBw = availableBw;
        }
    }
}
