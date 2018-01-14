using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NHLF;

namespace NHLFCommunications
{
    [Serializable]
    public class RemoveResponse : Communications.Message
    {
        public bool status; // True - success, false - failure
        public int seq;

        public RemoveResponse(string senderID, int senderPort, int seq, bool status)
        {
            messageType = "NHLF.RemoveResponse";
            this.senderID = senderID;
            this.senderPort = senderPort;
            this.seq = seq;
            this.status = status;
        }
    }
}
