using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LRMcommunications
{
    [Serializable]

    public class AllocateResponse : Communications.Message
    {
        public AllocateResponse()
        {
            this.messageType = "AllocateResponse";
        }
        public AllocateResponse(string senderID, int senderPort, int label, int seq)
        {
            this.messageType = "AllocateResponse";
            this.senderID = senderID;
            this.senderPort = senderPort;
            this.label = label;
            this.seq = seq;
        }

        public int label;
        public int seq;
    }
}
