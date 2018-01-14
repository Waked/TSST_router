using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NHLF;

namespace NHLFCommunications
{
    [Serializable]
    public class AddUpdateRequest : Communications.Message
    {
        public NHLFEntry entry;
        public int seq;

        public AddUpdateRequest(string senderID, int senderPort, int seq, NHLFEntry entry)
        {
            messageType = "NHLF.AddUpdateRequest";
            this.senderID = senderID;
            this.senderPort = senderPort;
            this.seq = seq;
            this.entry = entry;
        }
    }
}
