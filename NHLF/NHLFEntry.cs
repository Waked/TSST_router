using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NHLF
{
    [Serializable]
    public class NHLFEntry
    {
        public int connectionID; // Jednak nie robimy lancuszka - na podstawie connectionID beda rozrozniane wpisy o tym samym wejsciu
        public byte interface_in;
        public int label_in;
        public bool is_swap_or_add;
        public byte interface_out;
        public int[] labels_out;

        public NHLFEntry(int connectionID, byte iface_in, int label_in, bool swap_add, byte iface_out = 0, int[] labels_out = null)
        {
            this.connectionID = connectionID;
            interface_in = iface_in;
            this.label_in = label_in;
            is_swap_or_add = swap_add;
            interface_out = iface_out;
            this.labels_out = labels_out;
        }

    }
}
