using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TSSTRouter
{
    partial class LinkResourceManager
    {
        // Link Resource Manager components
        private BandwidthManagement BWMgmt;

        private List<Assignment> assignments;

        public LinkResourceManager(string routerId, Dictionary<byte, uint> interfaceDefinitions)
        {
            BWMgmt = new BandwidthManagement(interfaceDefinitions);
            assignments = new List<Assignment>();
        }

        /*
         * Returns a label if assignment was successful, or zero if it was not.
         */
        public uint AssignBandwidthOnInterface(byte interfaceId, uint bandwidth)
        {
            if (BWMgmt.AvailableBandwidthAt(interfaceId, bandwidth)) // If given bandwidth available at given iface
            {
                BWMgmt.AssignBandwidth(interfaceId, bandwidth);
                uint newLabel = NextFreeLabelOnInterface(interfaceId);
                Assignment newAssignment = new Assignment(interfaceId, bandwidth, newLabel);
                assignments.Add(newAssignment);
                return newLabel;
            }
            else
            {
                return 0;
            }
        }

        public void PrintAssignments()
        {
            Log.WriteLine("[LRM] Assigned resources:");
            foreach (var assignment in assignments)
            {
                Log.WriteLine(true, "");
            }
        }

        public uint NextFreeLabelOnInterface(byte interfaceId)
        {
            // The process of selecting a label -
            // increment from 1 until there is a free
            // label found.
            uint label = 1;
            var query = from assignment in assignments
                        where assignment.label == label && assignment.ifaceId == interfaceId
                        select assignment;
            while (query.Count() > 1)
                label++;
            return label;
        }

        class Assignment
        {
            public byte ifaceId;
            public uint assignedBandwidth;
            public uint label;

            public Assignment(byte ifaceId, uint bandwidth, uint label)
            {
                this.ifaceId = ifaceId;
                assignedBandwidth = bandwidth;
                this.label = label;
            }
        }
    }
}
