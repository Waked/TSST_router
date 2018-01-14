using MPLS;
using NHLF;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MPLSSim_Logger;
using NHLFCommunications;
using LRMcommunications;
using LRMRCCommunications;

namespace TSSTRouter
{

    class ClientConnectState
    {
        public Socket clientSocket = null;
        public IPEndPoint remoteEP = null;
    }

    // Model routera MPLS łączący się z tzw. chmurą kablową przez dwa sockety (uplink i downlink)
    class Router
    {
        // Config information
        public string id;
        public string autonomicSystemId;
        public string subnetworkId;
        public ushort wirecloudLocalPort;
        public ushort wirecloudRemotePort;
        public ushort mgmtLocalPort;
        public ushort connectionControllerPort;
        public int sendIntervalMillis; // Interval between packet shipments, in milliseconds [ms]
        private string routingTablePath; // Path to existing routing table stored in an .rt file (may be empty)

        // Router components
        private LinkResourceManager LRM;
        private TransportFunction transportFunction;
        private Dictionary<byte, uint> routerInterfaceDefs;

        // Threads
        private Thread managementThread;
        private List<Timer> periodicTasks; // Collection containing threads executed periodically

        // Networking elements
        IPAddress localhost = IPAddress.Parse("127.0.0.1");
        Socket mgmtTxSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp); // Instant initialization

        public Router(
            string routerId,
            string asId,
            string subnetworkId,
            ushort wirecloudLocalPort,
            ushort wirecloudRemotePort,
            ushort mgmtLocalPort,
            ushort connectionControllerPort,
            int intervalMs, string filePath,
            Dictionary<byte, uint> interfaceDefinitions
            )
        {
            id = routerId;
            this.mgmtLocalPort = mgmtLocalPort;
            this.connectionControllerPort = connectionControllerPort;
            this.wirecloudLocalPort = wirecloudLocalPort;
            this.wirecloudRemotePort = wirecloudRemotePort;
            sendIntervalMillis = intervalMs;
            routingTablePath = filePath;
            routerInterfaceDefs = interfaceDefinitions;

            Init();
        }

        void Init()
        {
            LoggingLib.Connect();
            LoggingLib.SendMessage(string.Format("This is a message from router (ID: {0})", id));
            
            // Console setup and initial printouts
            Console.Title = id;
            Log.PrintAsciiTitle(id);
            Log.WriteLine(true, "Ports: Wirecloud({0}, {1}), NMS({2}, {3})",
                wirecloudLocalPort, wirecloudRemotePort,
                mgmtLocalPort, connectionControllerPort);
            Log.WriteLine(true, "Interfaces: " + string.Join(", ", routerInterfaceDefs.ToString()));
            Log.WriteLine(true, "======");
            
            try
            {
                Log.WriteLine("Starting router... ");
                Log.ResetTimer();
                
                // Those objects create and start threads upon construction
                LRM = new LinkResourceManager(id, routerInterfaceDefs);
                transportFunction = new TransportFunction(sendIntervalMillis, wirecloudLocalPort, wirecloudRemotePort, routerInterfaceDefs);

                managementThread = new Thread(StartManagement);
                managementThread.Start();
            }
            catch (ThreadStartException tse)
            {
                Log.WriteLine("failed:");
                Console.WriteLine(tse);
            }

            /*
             * Initialize routing table - in case the path is an empty
             * string or the file does not exist, an empty table will
             * be created.
             */
            try
            {
                transportFunction.ParseRoutingTable(routingTablePath);
            }
            catch (FileNotFoundException) // ParseRoutingTable throws FileNotFoundException if file is not found (duh!)
            {
                Log.WriteLine("[ERROR] Failed to load routing table from file (file not found)");
            }
            
            // Launch
            //StartPollingNMS(58000);

            while (true)
            {
                switch (Console.ReadKey(true).Key)
                {
                    case ConsoleKey.T:
                        transportFunction.PrintRouteTable();
                        break;
#if DEBUG
                    case ConsoleKey.Enter:
                        MPLSPacket testPacket = new MPLSPacket(new int[] { 2137 }, "This is a test MPLSMessage.");
                        transportFunction.EnqueuePacketOnFirstQueue(testPacket);
                        break;
#endif
                    case ConsoleKey.U:
                        NHLFEntry entry = new NHLFEntry(10, 1, 17, true, 2, new int[] { 35 } );
                        AddUpdateRequest testUpdateReq = new AddUpdateRequest("Helo it me", mgmtLocalPort, 2137, entry);
                        sendMgmtMsg(mgmtLocalPort, testUpdateReq);
                        break;

                    default:
                        break;
                }
            }
        }

        // Method to send management messages over UDP to a specified port.
        // The messages must be derivatives of the Message object in the
        // Communications library.
        void sendMgmtMsg(ushort port, Communications.Message msg)
        {
            IPEndPoint remoteEP = new IPEndPoint(localhost, port);
            byte[] data = Communications.Serialization.Serialize(msg);
            mgmtTxSocket.SendTo(data, remoteEP);
        }

        void StartManagement()
        {
            UdpClient listener = new UdpClient(mgmtLocalPort);
            IPEndPoint groupEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), mgmtLocalPort);

            try
            {
                while (true)
                {
                    byte[] bytes = listener.Receive(ref groupEP);

                    Communications.Message msg = Communications.Serialization.Deserialize(bytes);

                    Log.WriteLine("[MGMT] {0} from {1}, port {2})", msg.messageType, msg.senderID, msg.senderPort);
                    
                    switch (msg.messageType)
                    {
                        case "NHLF.AddUpdateRequest":
                            AddUpdateRequest addUpdateReq = (AddUpdateRequest) msg;
                            transportFunction.UpdateRoutingTable(addUpdateReq.entry, true);
                            break;
                        default:
                            break;
                    }
                    
                    // NHLFMgmtMessage mgmtMessage = NHLFSerialization.Deserialize(bytes);

                    //NHLFEntry newEntry = mgmtMessage.entry;
                    
                    //string result = transportFunction.UpdateRoutingTable(newEntry, mgmtMessage.addOrSwap);
                }

            }
            catch (Exception e)
            {
                Log.WriteLine(e.ToString());
            }
            finally
            {
                listener.Close();
            }
        }
        
    }
}
