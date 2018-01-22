using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LRMRCCommunications
{
    [Serializable]
    public class BatchUpdate : Communications.Message
    {
        public List<Link> linkList;

        public BatchUpdate()
        {
            messageType = "LRMRC.BatchUpdate";
            linkList = new List<Link>();
        }
    }
}
