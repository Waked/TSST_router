using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPLS
{
    [Serializable]
    public class MPLSPacket
    {
        public string data;
        public Stack<int> labels;

        /*
         * MPLS Packet contains some data in string format and
         * a stack of labels
         */

        public MPLSPacket(int[] label, string data)
        {
            labels = new Stack<int>(label);
            this.data = data;
        }
    }
}
