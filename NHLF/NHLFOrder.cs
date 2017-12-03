using System;

namespace NHLF
{
    [Serializable]
    public class NHLFOrder
    {
        public NHLFOrder(bool add_entry, NHLFEntry entry)
        {
            this.add_entry = add_entry;
            this.entry = entry;
        }

        public bool add_entry;
        public NHLFEntry entry;
    }
}
