using MPLS;
using NHLF;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LRMIntercom;

namespace TSSTRouter
{
    partial class TransportFunction
    {
        // TransportFunction components
        MediumAccessController MAC;
        private TELinkEnd[] routerInterfaces;

        // TransportFunction structures
        private Timer packageSendTimer; // A thread to send TCP packets to the web
        private List<NHLFEntry> routingTable; // The routing table is an array of route entries, defined in external library Routing.dll

        // TransportFunction data
        public ushort RxPort { get; private set; }
        public ushort TxPort { get; private set; }

        // Transport network-passed packet handler
        public delegate void HandleMgmtPackets(byte localsnpp, GenericMessage genmsg);
        public HandleMgmtPackets handleMgmtPackets = null;


        public TransportFunction(int operationInterval, ushort rxPort, ushort txPort, Dictionary<byte, uint> interfaceDefinitions)
        {
            MAC = new MediumAccessController(rxPort, txPort, PacketHandler);
            RxPort = rxPort;
            TxPort = txPort;
            routingTable = new List<NHLFEntry>();

            /*
             * Create a new interface for each received ID, and all of them
             * as an array in the relevant property.
             */
            List<TELinkEnd> ifaces = new List<TELinkEnd>();
            foreach (var interfaceDefinition in interfaceDefinitions)
            {
                ifaces.Add(new TELinkEnd(interfaceDefinition));
            }
            routerInterfaces = ifaces.ToArray();

            // Start periodically sending all packets enqueued in the interfaces
            packageSendTimer = new Timer(SendPacketsCallback, null, 0, operationInterval);

            Initialize();
        }


        private void Initialize()
        {
            MAC.Start();
        }

        void SendPacketsCallback(object state)
        {
            foreach (TELinkEnd iface in routerInterfaces)
            {
                MPLSPacket[] queuedPackets = iface.GetPacketsAndEmptyQueue(); // Loads all packets stored in a queue and clears that interface's queue
                if (queuedPackets.Length > 0)
                {
                    AggregatePacket finalPacket = new AggregatePacket(queuedPackets);
                    BinaryWrapper wrappedPacket = MPLSMethods.Serialize(finalPacket);

                    MAC.SendData(wrappedPacket, iface.Id);

                    Log.WriteLine("[TX {0}] => {1} MPLS packet(s)", iface.Id, queuedPackets.Length);
                }
            }
        }

        public void SendPeerMessage(byte snppId, GenericMessage genmsg)
        {
            //Log.WriteLine("[TX] PeerMessage: {0}, SNPP {1}", genmsg.messageType, snppId);

            MPLSPacket packet = new MPLSPacket(new int[] { 0 }, "");
            packet.managementObject = genmsg;

            AggregatePacket aggregatePacket = new AggregatePacket(new MPLSPacket[] { packet });
            BinaryWrapper wrappedPacket = MPLSMethods.Serialize(aggregatePacket);

            MAC.SendData(wrappedPacket, snppId);
            //Log.WriteLine("[TX {0}] => {1}, iface {2}", snppId, genmsg.messageType, snppId);
        }

        void PacketHandler(BinaryWrapper packet, byte interfaceId)
        {
            AggregatePacket receivedPacket = MPLSMethods.Deserialize(packet);

            //Log.WriteLine("[RX {0}] \t {1} packet(s)", interfaceId, receivedPacket.packets.Length);

            foreach (MPLSPacket mplspacket in receivedPacket.packets)
            {
                if (mplspacket.managementObject != null)
                {
                    GenericMessage genmsg = (GenericMessage)mplspacket.managementObject;
                    //Log.WriteLine("[RX {0}] \t {1}", interfaceId, genmsg.messageType);
                    handleMgmtPackets(interfaceId, genmsg);
                }
                else
                {
                    Log.WriteLine("[RX] Packet on interface {0}", interfaceId);
                    Route(mplspacket, interfaceId);
                }
            }
        }

        /*
         * Takes a packet and places it in a corresponding queue for output.
         */
        void Route(MPLSPacket packet, byte iface, int connectionID = 0)
        {
            NHLFEntry routeEntry;
            int topLabel = packet.labels.Pop();
            try
            {
                var queryResults = from entry in routingTable
                                   where entry.interface_in == iface && entry.label_in == topLabel && (connectionID == 0 || entry.connectionID == connectionID)
                                   select entry;
                // The query should always return a single entry (.Single() throws error if not single ;P)
                routeEntry = queryResults.Single();
            }
            catch (InvalidOperationException)
            {
                // If no routing information is found, abandon route method
                Log.WriteLine("[FWD] ({0}, {1}) ==> DROP", iface, topLabel);
                return;
            }

            if (routeEntry.is_swap_or_add) // If the label is to be swapped or added...
            {
                foreach (int label in routeEntry.labels_out)
                {
                    packet.labels.Push(label);
                }
                /*
                 * This mysterious construct does the following things:
                 *  1. Query the interface list in search of the one
                 *      that has an ID [i.InterfaceId] equal to the one
                 *      carried in the routing entry [routeEntry.interface_out]
                 *  2. Ensure that only single interface is received as
                 *      a result (there should not be multiple interfaces
                 *      with the same ID) and select it [.Single()]
                 *  3. Execute a method [.EnqueuePacket()] on that interface
                 *      to insert the packet in a correspoding output queue
                 *          
                 *                                              Sincerely,
                 *                                                Waked
                 */
                try
                {
                    var queryResults = from i in routerInterfaces
                                       where i.Id == routeEntry.interface_out
                                       select i;
                    TELinkEnd outputIface = queryResults.Single();
                    outputIface.EnqueuePacket(packet);

                    Log.WriteLine("[FWD] ({0}, {1}) ==> ({2}, {3}))", iface, topLabel, routeEntry.interface_out, string.Join(";", routeEntry.labels_out));
                }
                catch (InvalidOperationException)
                {
                    // In case there is no such interface to output (the routing
                    // infromation is flawed), abandon routing.
                    Log.WriteLine("[FWD] ({0}, {1}) ==> DROP", iface, topLabel);
                }
            }
            else // ...otherwise, reroute the packet without the top-most label
            {
                Log.WriteLine("[FWD] ({0}, {1}) ==> Reforward({0}, {2})", iface, topLabel, packet.labels.Peek());
                // Reroute the packet with the connectionID of the previous entry
                Route(packet, iface, routeEntry.connectionID);
            }

        }

        // Adds, swaps or removes (depending on situation) an entry in the
        // routing table. Returns a textual representation of the operation
        // undertaken - "Added", "Swapped" or "Removed"
        public string UpdateRoutingTable(NHLFEntry newEntry, bool addOrSwap)
        {
            NHLFEntry existingEntry = null;
            bool entrySwapped = true;
            string actionTaken;

            try
            {
                var queryResults = from entry in routingTable
                                   where entry.interface_in == newEntry.interface_in && entry.label_in == newEntry.label_in
                                   select entry;

                existingEntry = queryResults.Single();
                routingTable.Remove(existingEntry);
            }
            catch (InvalidOperationException)
            {
                entrySwapped = false;
            }

            if (addOrSwap)
            {
                routingTable.Add(newEntry);
                if (entrySwapped)
                    actionTaken = "Swapped";
                else
                    actionTaken = "Added";
            }
            else
            {
                actionTaken = "Removed";
            }

            return actionTaken;
        }

        /* 
         * ParseRoutingTable(string)
         * 
         * This method parses routing information from a specially formatted
         * text file. The data format is as follows:
         * 
         *  interface_in    label_in    add/swap(bool)  interface_out   label(s)_out
         * 
         * Row elements are tab-separated (\t) and labels (if adding more) are comma-separated.
         * 
         * @returns RouteEntry[] - "list of rows" in the routing table
         */
        public void ParseRoutingTable(string pathToFile)
        {
            List<NHLFEntry> newEntryList = new List<NHLFEntry>();

            // If the filepath is empty, return an empty object (the table is clear)
            if (pathToFile == "")
                return;

            try
            {
                string[] tableRows = File.ReadAllLines(pathToFile, Encoding.UTF8);

                // Iterate over each line in routing config file
                foreach (string row in tableRows)
                {
                    if (row[0] == '#') // Check if a row is a comment - if true, skip it
                        continue;
                    string[] attributes = row.Split('\t');
                    NHLFEntry newEntry = new NHLFEntry(
                        int.Parse(attributes[0]),
                        byte.Parse(attributes[1]),
                        int.Parse(attributes[2]),
                        bool.Parse(attributes[3]),
                        byte.Parse(attributes[4]),
                        Array.ConvertAll(attributes[5].Split(','), int.Parse) // Convert labels as CSVs into int[]
                        );
                    newEntryList.Add(newEntry);
                }
                routingTable.Concat(newEntryList); // Concat - instead of assignment, not to erase existing entries
            }
            catch (FileNotFoundException)
            {
                throw new FileNotFoundException();
            }
        }

        public void PrintRouteTable()
        {
            Log.WriteLine("[FWD] Current table:");
            Log.WriteLine(true, "\t┌───────┬───────┬───────┬───────┬───────┬───────┐\n" +
                                "\t│Conn_Id│If_in  │Lol_in │Method │If_out │Lbl_out│\n" +
                                "\t├───────┼───────┼───────┼───────┼───────┼───────┤");
            foreach (NHLFEntry entry in routingTable)
            {
                Log.WriteLine(true, "\t│{0}│{1}│{2}│{3}│{4}│{5}│",
                    entry.connectionID + (entry.connectionID > 999999 ? "" : "\t"),
                    entry.interface_in + "\t",
                    entry.label_in + (entry.label_in > 999999 ? "" : "\t"), // If the label is greater than a million, place no tab
                    entry.is_swap_or_add ? "Add/Swp" : "Rmv    ",
                    (entry.is_swap_or_add ? entry.interface_out.ToString() : "") + "\t", // If the method is "Remove", no need to print this
                    entry.is_swap_or_add ? entry.labels_out[0] + (entry.labels_out[0] > 999999 ? "" : "\t") : ""  // Same as above two
                    );

                int i = 1;
                // If there is a label stack, print additional rows:
                while (entry.labels_out.Length > i)
                {
                    string lbl_out_str = "";
                    if (entry.labels_out.Length > i)
                        lbl_out_str = entry.labels_out[i] + (entry.labels_out[i] > 999999 ? "" : "\t");
                    Log.WriteLine(true, "\t│\t│\t│\t│\t│\t│{0}|", lbl_out_str); // Same thing as above - if over million, no tab
                    i++;
                }
            }
            Log.WriteLine(true, "\t└───────┴───────┴───────┴───────┴───────┴───────┘");
        }

#if DEBUG
        public void EnqueuePacketOnFirstQueue(MPLSPacket packet)
        {
            routerInterfaces[0].EnqueuePacket(packet);
        }
#endif
    }
}
