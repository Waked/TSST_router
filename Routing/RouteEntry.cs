﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Routing
{
    [Serializable]
    public class RouteEntry
    {
        public byte interface_in;
        public int label_in;
        public bool is_swap_or_add;
        public byte interface_out;
        public int[] labels_out;

        public RouteEntry(byte iface_in, int label_in, bool swap_add, byte iface_out, int[] labels_out)
        {
            interface_in = iface_in;
            this.label_in = label_in;
            is_swap_or_add = swap_add;
            interface_out = iface_out;
            this.labels_out = labels_out;
        }
    }
}
