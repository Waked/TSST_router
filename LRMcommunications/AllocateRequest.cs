using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LRMcommunications
{
    [Serializable]

    public class AllocateRequest : Communications.Message
    {
        public AllocateRequest()
        {
            this.messageType = "AllocateRequest";
        }
        public AllocateRequest(string senderID, int senderPort, int connectionID, byte interfaceID, int bitrate, int seq)
        {
            this.messageType = "AllocateRequest";
            this.senderID = senderID;
            this.senderPort = senderPort;
            this.connectionID = connectionID;
            this.interfaceID = interfaceID;
            this.bitrate = bitrate;
            this.seq = seq;
        }

        public int connectionID;
        public byte interfaceID;
        public int bitrate;
        public int seq;
    }
}
