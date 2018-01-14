using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LRMRCCommunications
{
    /*
     * Klasa opisujaca wezel sieciowy, tj. jego:
     * - identyfikator urzadzenia
     * - system autonomiczny
     * - identyfikator podsieci
     */
    [Serializable]
    public class Node
    {
        public string id;
        public string asId;
        public string snId;

        public Node(string id, string asId, string snId)
        {
            this.id = id;
            this.asId = asId;
            this.snId = snId;
        }
    }
}
