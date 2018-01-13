using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Communications
{
    [Serializable]

    public class Message
    {
        public Message() { }

        public string messageType;
        public string senderID;
        public int senderPort;
    }
}
