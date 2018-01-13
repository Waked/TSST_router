using System;

namespace NHLF
{
    [Serializable]
    public class NHLFMgmtMessage
    {
        public NHLFMgmtMessage(bool addOrSwap, NHLFEntry entry)
        {
            this.addOrSwap = addOrSwap;
            this.entry = entry;
        }

        public bool addOrSwap;
        public NHLFEntry entry;
    }
}
