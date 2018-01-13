using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LRMcommunications
{
    [Serializable]

    public class DeallocateResponse : Communications.Message
    {
        public DeallocateResponse()
        {
            this.messageType = "DeallocateResponse";
        }
        public DeallocateResponse(string senderID, int senderPort, int seq)
        {
            this.messageType = "DeallocateResponse";
            this.senderID = senderID;
            this.senderPort = senderPort;
            this.seq = seq;
        }

        public int seq;
    }
}
