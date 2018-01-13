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
        public ushort mgmtLocalPort;
        public ushort mgmtRemotePort;
        public int sendIntervalMillis; // Interval between packet shipments, in milliseconds [ms]

        // Router components
        private LinkResourceManager LRM;
        private TransportFunction transportFunction;
        private Dictionary<byte, uint> routerInterfaceDefs;

        private string routingTablePath; // Path to existing routing table stored in an .rt file (may be empty)
        
        private Timer NMSPollTimer; // A thread that sends UDP keep-alive packets to the NMS

        public Router(string routerId, ushort listenPort, ushort cloudPort,ushort mgmtLocal, ushort mgmtRemote,
            int intervalMs, string filePath, Dictionary<byte, uint> interfaceDefinitions)
        {
            id = routerId;
            mgmtLocalPort = mgmtLocal;
            mgmtRemotePort = mgmtRemote;
            sendIntervalMillis = intervalMs;
            routingTablePath = filePath;
            routerInterfaceDefs = interfaceDefinitions;
            
            LRM = new LinkResourceManager(routerId, interfaceDefinitions);
            transportFunction = new TransportFunction(intervalMs, listenPort, cloudPort, interfaceDefinitions);

            Init();
        }

        // Constructor - two parameters are required, listening port and remote (wirecloud) port
        public Router(string routerId, ushort listenPort, ushort cloudPort, ushort mgmtLocal, ushort mgmtRemote,
            int intervalMs, Dictionary<byte, uint> interfaceDefinitions)
            : this(routerId, listenPort, cloudPort, mgmtLocal, mgmtRemote, intervalMs, "", interfaceDefinitions)
        {
        }

        void Init()
        {
            LoggingLib.Connect();
            LoggingLib.SendMessage(string.Format("This is a message from router (ID: {0})", id));
            
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

            Console.Title = id;
            Log.PrintAsciiTitle(id);

            Log.WriteLine(true, "Ports: Wirecloud({0}, {1}), NMS({2}, {3})",
                transportFunction.RxPort, transportFunction.TxPort,
                mgmtLocalPort, mgmtRemotePort);
            Log.WriteLine(true, "Interfaces: " + string.Join(", ", routerInterfaceDefs.ToString()));
            Log.WriteLine(true, "======");

            Thread management = new Thread(StartManagement);
            try
            {
                Log.WriteLine("Starting router... ");
                Log.ResetTimer();
                management.Start();
            }
            catch (Exception e)
            {
                Log.WriteLine("failed:");
                Console.WriteLine(e);
            }

            // Launch
            StartPollingNMS(58000);

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
                    default:
                        break;
                }
            }
        }
        
        class PollStateObject
        {
            public byte[] msg;
            public Socket sock;
            public IPEndPoint ep;

        }
        private void PollNMS(object state)
        {
            PollStateObject pso = (PollStateObject)state;
            pso.sock.SendTo(pso.msg, pso.ep);
        }

        void StartPollingNMS(int remotePort)
        {
            IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, mgmtRemotePort);
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            byte[] msg = Encoding.ASCII.GetBytes(id);
            int interval = sendIntervalMillis / 2;

            PollStateObject pollStateObject = new PollStateObject() { msg = msg, sock = s, ep = remoteEP };

            NMSPollTimer = new Timer(PollNMS, pollStateObject, 0, interval);
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

                    NHLFMgmtMessage mgmtMessage = NHLFSerialization.Deserialize(bytes);

                    NHLFEntry newEntry = mgmtMessage.entry;
                    
                    string result = transportFunction.UpdateRoutingTable(newEntry, mgmtMessage.addOrSwap);

                    Log.WriteLine("[MGMT] {0} entry at ({1}, {2})", result, newEntry.interface_in, newEntry.label_in);
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
