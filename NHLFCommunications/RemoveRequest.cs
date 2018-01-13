using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NHLF;

namespace NHLFCommunications
{
    [Serializable]
    public class RemoveRequest : Communications.Message
    {
        public int connectionID;

        public RemoveRequest(string senderID, int senderPort, int connectionID)
        {
            messageType = "NHLF.RemoveRequest";
            this.senderID = senderID;
            this.senderPort = senderPort;
            this.connectionID = connectionID;
        }
    }
}
