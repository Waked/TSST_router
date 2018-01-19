using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSSTRouter;
using LRMIntercom;
using System.Threading;

namespace TSSTRouter
{
    partial class LinkResourceManager
    {
        // Link Resource Manager components
        private BandwidthManagement BWMgmt;

        // Operational data
        string routerId;
        string asId;
        string snId;
        private List<Assignment> assignments;
        Dictionary<byte, uint> interfaceDefinitions;
        Dictionary<byte, PeerInformation> peers;

        // Transport network message send handler
        public delegate void SendPeerMessage(byte snpp, GenericMessage msg);
        public SendPeerMessage sendPeerMessage = null;

        // Management network message send handler
        public delegate void SendMgmtMessage(ushort port, Communications.Message msg);
        public SendMgmtMessage sendMgmtMessage = null;

        // Threads
        Timer sendPeerUpdateRequests = null;


        // Constructor
        public LinkResourceManager(string routerId, string asId, string snId, ushort ccPort, ushort rcPort, Dictionary<byte, uint> interfaceDefinitions)
        {
            this.routerId = routerId;
            this.asId = asId;
            this.snId = snId;
            this.interfaceDefinitions = interfaceDefinitions;

            // Initialization
            BWMgmt = new BandwidthManagement(interfaceDefinitions);
            assignments = new List<Assignment>();
            sendPeerUpdateRequests = new Timer(SendUpdateRequestsCallback, null, 1000, 3000); // Begin after 1 sec, repeat every 3 sec
            peers = new Dictionary<byte, PeerInformation>();
            // Begin with null peers on each interface
            foreach (KeyValuePair<byte, uint> kvpair in interfaceDefinitions)
            {
                peers[kvpair.Key] = null;
            }
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

        // This method sends StatusUpdateRequest on all available SNPPs (interfaces),
        // if an appropriate method to do so exists.
        public void SendUpdateRequestsCallback(object state)
        {
            try
            {
                foreach (KeyValuePair<byte, uint> kvpair in interfaceDefinitions)
                {
                    StatusUpdateRequest request = new StatusUpdateRequest(routerId, asId, snId, kvpair.Key);
                    sendPeerMessage(kvpair.Key, request);
                }
                //Log.WriteLine("[LRM] Sent peer update requests");
            }
            catch (NullReferenceException)
            {
                Log.WriteLine("[LRM] Message callback unassigned");
            }
        }

        public void HandleManagementPacket(byte localsnpp, GenericMessage genmsg)
        {
            //Log.WriteLine("[LRM] Processing \"{0}\" from interface {1}", genmsg.messageType, localsnpp);
            switch (genmsg.messageType)
            {
                case "StatusUpdateRequest":
                    StatusUpdateRequest request = (StatusUpdateRequest)genmsg;
                    uint assignedBw = BWMgmt.AssignedBandwidthAt(localsnpp);
                    uint availableBw = BWMgmt.AvailableBandwidthAt(localsnpp);
                    StatusUpdateResponse resp = new StatusUpdateResponse(routerId, asId, snId, localsnpp, assignedBw, availableBw);
                    try
                    {
                        sendPeerMessage(localsnpp, resp);
                    }
                    catch (NullReferenceException)
                    {
                        Log.WriteLine("[LRM] ERROR: No SendPeerMessage delegate assigned");
                    }
                    break;

                case "StatusUpdateResponse":
                    StatusUpdateResponse response = (StatusUpdateResponse)genmsg;

                    if (peers[localsnpp] == null)
                    {
                        // Create new peer object
                        peers[localsnpp] = new PeerInformation(
                            response.snppId,
                            response.id,
                            response.asId,
                            response.snId,
                            response.assignedBw,
                            response.availableBw,
                            HandleLinkFailureCallback
                            );
                        Log.WriteLine("[LRM] Creating new Peer object");
                    }
                    else
                    {
                        // Update existing peer object
                        PeerInformation peer = peers[localsnpp];
                        peer.remoteRouterId = response.id; // TODO - co jeśli zmieni sie ID
                        peer.remoteAsId = response.asId;
                        peer.remoteSnId = response.snId;
                        peer.remoteAssignedBw = response.assignedBw;
                        peer.remoteAvailableBw = response.availableBw;
                        peer.timeoutTimer.Change(5000, 0);
                        if (!peer.isActive)
                        {
                            Log.WriteLine("[LRM] Peer {0} is back", peer.remoteRouterId);
                            peer.isActive = true;
                        }
                        else
                        {
                            Log.WriteLine("[LRM] Update on peer {0}", peer.remoteRouterId);
                        }
                    }
                    break;

                default:
                    break;
            }
        }

        public void HandleLinkFailureCallback(object state)
        {
            PeerInformation peer = (PeerInformation)state;
            peer.isActive = false;
            Log.WriteLine("[LRM] Lost peer {0}", peer.remoteRouterId);
        }

        class PeerInformation
        {
            public byte remoteSNPP;
            public string remoteRouterId;
            public string remoteAsId;
            public string remoteSnId;
            public uint remoteAssignedBw;
            public uint remoteAvailableBw;
            public bool isActive;
            public Timer timeoutTimer;

            public PeerInformation(
                byte snpp,
                string id,
                string asId,
                string snId,
                uint assignedBw,
                uint availableBw,
                TimerCallback failureCallback
                )
            {
                remoteSNPP = snpp;
                remoteRouterId = id;
                remoteAsId = asId;
                remoteSnId = snId;
                remoteAssignedBw = assignedBw;
                remoteAvailableBw = availableBw;
                isActive = true;
                timeoutTimer = new Timer(failureCallback, this, 5000, 0);
            }
        }

        // Class describing bandwidth assignment
        private class Assignment
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
