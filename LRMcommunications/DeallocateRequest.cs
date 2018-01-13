using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LRMcommunications
{
    [Serializable]

    public class DeallocateRequest : Communications.Message
    {
        public DeallocateRequest()
        {
            this.messageType = "DeallocateRequest";
        }
        public DeallocateRequest(string senderID, int senderPort, int connectionID, int seq)
        {
            this.messageType = "DeallocateRequest";
            this.senderID = senderID;
            this.senderPort = senderPort;
            this.connectionID = connectionID;
            this.seq = seq;
        }

        public int connectionID;        // identifies a connection within a subnetwork - subnetwork CC maintains uniqueness 
        public int seq;
    }
}
