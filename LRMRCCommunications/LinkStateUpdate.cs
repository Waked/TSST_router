using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LRMRCCommunications
{
    [Serializable]
    public class LinkStateUpdate : Communications.Message
    {
        public Node beginNode;
        public Node endNode;
        public byte beginSNPP;
        public byte endSNPP;
        public int capacity;

        public LinkStateUpdate(
            string beginId,
            string beginAS,
            string beginSubnet,
            byte beginSNPP,
            string endId,
            string endAS,
            string endSubnet,
            byte endSNPP,
            int capacity)
        {
            beginNode = new Node(beginId, beginAS, beginSubnet);
            endNode = new Node(endId, endAS, endSubnet);
            this.beginSNPP = beginSNPP;
            this.endSNPP = endSNPP;
            this.capacity = capacity;
        }
    }
}
