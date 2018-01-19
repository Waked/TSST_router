using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LRMIntercom
{
    [Serializable]
    public class StatusUpdateRequest : GenericMessage
    {
        public StatusUpdateRequest(string id, string asId, string snId, byte snppId)
            : base(id, asId, snId, snppId)
        {
            messageType = "StatusUpdateRequest";
        }
    }
}
