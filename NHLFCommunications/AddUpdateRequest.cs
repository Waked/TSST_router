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

        public AddUpdateRequest(string senderID, int senderPort, NHLFEntry entry)
        {
            messageType = "NHLF.AddUpdateRequest";
            this.senderID = senderID;
            this.senderPort = senderPort;
            this.entry = entry;
        }
    }
}
