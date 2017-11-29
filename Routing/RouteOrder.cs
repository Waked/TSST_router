using System;

namespace Routing
{
    [Serializable]
    public class RouteOrder
    {
        public RouteOrder(bool add_entry, RouteEntry entry)
        {
            this.add_entry = add_entry;
            this.entry = entry;
        }

        public bool add_entry;
        public RouteEntry entry;
    }
}
