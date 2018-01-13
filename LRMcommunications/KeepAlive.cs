using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LRMcommunications
{
    [Serializable]

    public class KeepAlive : Communications.Message
    {
        public KeepAlive()
        {
            this.messageType = "KeepAlive";
        }
        public KeepAlive(string senderID, int senderPort)
        {
            this.messageType = "KeepAlive";
            this.senderID = senderID;
            this.senderPort = senderPort;     // this port is to be saved as a communication port for the entire router: LRM & CCI
        }
    }
}
