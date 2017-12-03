using Colorful;
using MPLS;
using NHLF;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Console = Colorful.Console;
using MPLSSim_Logger;

using static TSST_router.StaticMethods; // Enables straight-forward use of methods in another class (StaticMethods class)

// TODO secure this program from multiple connections - by default
// each asynchronous callback for receiver will try to empty queues.
// Since this would not be intended, there is a need to implement
// a security measure that limits open connections to one.

namespace TSST_router
{
    // Klasa reprezentująca stan w ramach obsługi połączenia przychodzącego ("state")
    class StateObject
    {
        public Socket workSocket = null; // Client socket
        public const int BufferSize = 65536; // Size of receive buffer (64 KiB)
        public byte[] buffer = new byte[BufferSize]; // Receive buffer
    }

    class ClientConnectState
    {
        public Socket clientSocket = null;
        public IPEndPoint remoteEP = null;
    }

    class Interface
    {
        public byte InterfaceId { get; }
        private Queue<MPLSPacket> packetQueue;

        public Interface(byte id)
        {
            InterfaceId = id;
            packetQueue = new Queue<MPLSPacket>();
        }

        public void EnqueuePacket(MPLSPacket packet)
        {
            packetQueue.Enqueue(packet);
        }

        public MPLSPacket[] GetPacketsAndClear()
        {
            MPLSPacket[] returnArray = packetQueue.ToArray();
            packetQueue.Clear();
            return returnArray;
        }
    }

    // Model routera MPLS łączący się z tzw. chmurą kablową przez dwa sockety (uplink i downlink)
    class Router
    {

        // Config information
        public string id;
        public int rxPort; // Receiver socket port number
        public int txPort; // "Remote" host socket port number
        public int mgmtLocalPort;
        public int mgmtRemotePort;
        public int sendIntervalMillis; // Interval between packet shipments, in milliseconds [ms]

        private string routingTablePath; // Path to existing routing table stored in an .rt file (may be empty)
        private List<NHLFEntry> routingTable; // The routing table is an array of route entries, defined in external library Routing.dll

        private ManualResetEvent allDone = new ManualResetEvent(false); // Thread pauser for receiver
        private ManualResetEvent connectDone = new ManualResetEvent(false); // Thread pausers for transmitter
        private ManualResetEvent sendDone = new ManualResetEvent(false);
        private ManualResetEvent receiveDone = new ManualResetEvent(false);
        private ManualResetEvent threadStopper = new ManualResetEvent(false);

        // This will be used for logging purposes
        private Stopwatch time;
        
        private Timer NMSPollTimer; // A thread that sends UDP keep-alive packets to the NMS
        private Timer packageSendTimer; // A thread to send TCP packets to the web

        // Interface Array - constant set defined on device creation
        // Iterable type makes it easier to handle
        private Interface[] routerInterfaces;
        private byte[] routerInterfaceIds;

        // Colorful.Console stylesheet used to style console output
        StyleSheet style;

        public Router(string routerId, int listenPort, int cloudPort, int mgmtLocal, int mgmtRemote, int intervalMs, string filePath, byte[] interfaceIds)
        {
            id = routerId;
            rxPort = listenPort;
            txPort = cloudPort;
            mgmtLocalPort = mgmtLocal;
            mgmtRemotePort = mgmtRemote;
            sendIntervalMillis = intervalMs;
            routingTablePath = filePath;
            routerInterfaceIds = interfaceIds;
            
            Init();
        }

        // Constructor - two parameters are required, listening port and remote (wirecloud) port
        public Router(string routerId, int listenPort, int cloudPort, int mgmtLocal, int mgmtRemote, int intervalMs, byte[] interfaceIds)
            : this(routerId, listenPort, cloudPort, mgmtLocal, mgmtRemote, intervalMs, "", interfaceIds)
        {
        }

        void Init()
        {
            // Prepare console styling
            style = new StyleSheet(Color.LightGray);
            style.AddStyle("ROUTE", Color.CornflowerBlue);
            style.AddStyle("RX",    Color.DeepPink);
            style.AddStyle("TX",    Color.LawnGreen);
            style.AddStyle("MGMT",  Color.Orange);
            style.AddStyle("ERROR", Color.Red);
            style.AddStyle("DROP",  Color.OrangeRed);

            LoggingLib.Connect();
            LoggingLib.SendMessage(string.Format("This is a message from router (ID: {0})", id));

            /*
             * Create a new interface for each received ID, and all of them
             * as an array in the relevant property.
             */
            List<Interface> ifaces = new List<Interface>();
            foreach (var ifId in routerInterfaceIds)
            {
                ifaces.Add(new Interface(ifId));
            }
            routerInterfaces = ifaces.ToArray();

            /*
             * Initialize routing table - in case the path is an empty
             * string or the file does not exist, an empty table will
             * be created.
             */
            try
            {
                routingTable = ParseRoutingTable(routingTablePath);
            }
            catch (FileNotFoundException) // ParseRoutingTable throws FileNotFoundException if file is not found (duh!)
            {
                Console.WriteLineStyled(style, "[ERROR] Failed to load routing table from file (file not found)");
                routingTable = ParseRoutingTable("");
            }

            Console.Title = id;
            Console.WriteAscii(id, Color.CornflowerBlue);

            Console.WriteLine("Ports: Wirecloud({0}, {1}), NMS({2}, {3})", rxPort, txPort, mgmtLocalPort, mgmtRemotePort);
            Console.WriteLine("Interfaces: " + string.Join(", ", routerInterfaceIds));
            Console.WriteLine("======");

            time = new Stopwatch();
            Thread server = new Thread(StartListening);
            Thread client = new Thread(StartClient);
            Thread management = new Thread(StartManagement);
            try
            {
                Console.WriteLine("Starting router... ");
                server.Start();
                client.Start();
                management.Start();
                time.Start(); // Start the output timer after client begins
            }
            catch (Exception e)
            {
                Console.WriteLineStyled("failed:", style);
                Console.WriteLine(e);
            }

            // Launch
            StartPolling(58000);

            while (true)
            {
                Console.ReadKey();

                /*
                 * Tutaj zdefiniowane jest dzialanie po wcisnieciu dowolnego klawisza podczas dzialania
                 * programu:
                 * - tryb DEBUG - wysylanie testowego pakietu o etykiecie 2137 na pierwszy z brzegu interfejs
                 * - tryb RELEASE - wypisanie tabeli trasowania pakietow
                 */
#if DEBUG
                MPLSPacket testPacket = new MPLSPacket(new int[] { 2137 }, "This is a test MPLSMessage.");
                routerInterfaces[0].EnqueuePacket(testPacket);
#else
                PrintRouteTable();
#endif
            }
        }

        void StartListening()
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry("localhost");
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, rxPort);
            Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            while (true) // In case something breaks - try again to establish connection
            {
                try
                {
                    listener.Bind(localEndPoint);
                    listener.Listen(100);

                    while (true)
                    {
                        Socket handler = listener.Accept(); // Thread waits until someone tries to connect

                        Console.WriteLineStyled(style, Timestamp() + "[RX] Connected on {0}", rxPort);

                        StateObject state = new StateObject(); // Create the state object.
                        state.workSocket = handler;

                        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);

                        threadStopper.WaitOne();
                    }
                }
                catch (SocketException)
                {
                    Console.WriteLineStyled(style, Timestamp() + "[RX] Reinitiating listener.");
                }
            }
        }

        void ReadCallback(IAsyncResult ar)
        {
            // Retrieve the state object and the handler socket  
            // from the asynchronous state object.  
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            try
            {
                // Read data from the client socket.   
                int bytesRead = handler.EndReceive(ar);

                if (bytesRead > 0) 
                {
                    BinaryWrapper receivedMsg = new BinaryWrapper(state.buffer.ToArray(), true); // Use a Wrapper constructor that extracts header bytes into fields
                    AggregatePacket receivedPacket = MPLSMethods.Deserialize(receivedMsg);

                    Console.WriteLineStyled(style, Timestamp() + "[RX] <== {0} packets received.", receivedPacket.packets.Length);

                    foreach (MPLSPacket mplspacket in receivedPacket.packets)
                        Route(mplspacket, receivedMsg.interfaceId);
                }

                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
            }
            catch (SocketException)
            {
                Console.WriteLineStyled(style, Timestamp() + "[RX] Lost incoming connection.");
                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }
        }

        void StartClient()
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry("localhost");
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, txPort);
            Socket client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp); // Create a TCP/IP socket.
            while (true)
            {
                try
                {
                    client.Connect(remoteEP);
                    Console.WriteLineStyled(style, Timestamp() + "[TX] Connected to {0}", txPort);

                    packageSendTimer = new Timer(SendPackets, client, 0, sendIntervalMillis);

                    threadStopper.WaitOne();
                }
                catch (SocketException)
                {

                }
            }
        }

        void SendPackets(object state)
        {
            // Console.WriteLine("Sending");
            Socket client = (Socket)state;

            foreach (Interface iface in routerInterfaces)
            {
                MPLSPacket[] queuedPackets = iface.GetPacketsAndClear(); // Loads all packets stored in a queue and clears that interface's queue
                if (queuedPackets.Length > 0)
                {
                    AggregatePacket finalPacket = new AggregatePacket(queuedPackets);
                    BinaryWrapper socketMessage = MPLSMethods.Serialize(finalPacket);
                    Random rng = new Random(); // Random number generator to fill in second header byte

                    socketMessage.interfaceId = iface.InterfaceId; // Header byte 1: interface identifier number
                    socketMessage.randomNumber = (byte)(rng.Next() % 255); // Header byte 2: Random byte-sized ID generated based on the current timestamp

                    SendMessage(client, socketMessage.HeaderPlusData());
                    sendDone.WaitOne();
                    Console.WriteLineStyled(style, Timestamp() + "[TX] ==> {0} packets sent.", queuedPackets.Length);
                }
            }
        }

        void ConnectCallback(IAsyncResult ar)
        {
            ClientConnectState state = (ClientConnectState)ar.AsyncState;
            Socket client = state.clientSocket; // Retrieve the socket from the state object.
            IPEndPoint remoteEP = state.remoteEP;
            try
            {
                client.EndConnect(ar); // Complete the connection.
                connectDone.Set(); // Signal that the connection has been made.
                Console.WriteLineStyled(style, Timestamp() + "[TX] Connected to {0}", txPort);
            }
            catch (SocketException)
            {
                client.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), state); // Connect to the remote endpoint.
            }
        }

        void SendMessage(Socket client, byte[] byteData)
        {
            client.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), client); // Begin sending the data to the remote device.
        }

        void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket)ar.AsyncState; // Retrieve the socket from the state object.
                int bytesSent = client.EndSend(ar); // Complete sending the data to the remote device.
                //Console.WriteLineStyled(style, Timestamp() + "[TX] <== Sent {0} bytes to server.", bytesSent);
                sendDone.Set(); // Signal that all bytes have been sent.
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        
        /*
         * Takes a packet and places it in a corresponding queue for output.
         */
        void Route(MPLSPacket packet, byte iface)
        {
            NHLFEntry routeEntry;
            int topLabel = packet.labels.Pop();
            try
            {
                var queryResults = from entry in routingTable
                                   where entry.interface_in == iface && entry.label_in == topLabel
                                   select entry;
                // The query should always return a single entry (.Single() throws error if not single ;P)
                routeEntry = queryResults.Single();
            }
            catch (InvalidOperationException)
            {
                // If no routing information is found, abandon route method
                Console.WriteLineStyled(style, Timestamp() + "[ROUTE] ({0}, {1}) ==> DROP", iface, topLabel);
                SendRemoteLog("[ROUTE] ({0}, {1}) ==> DROP", iface, topLabel);
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
                                       where i.InterfaceId == routeEntry.interface_out
                                       select i;
                    Interface outputIface = queryResults.Single();
                    outputIface.EnqueuePacket(packet);
                    
                    Console.WriteLineStyled(style, Timestamp() + "[ROUTE] ({0}, {1}) ==> ({2}, {3}))", iface, topLabel, routeEntry.interface_out, string.Join(";", routeEntry.labels_out));
                    SendRemoteLog("[ROUTE] ({0}, {1}) ==> ({2}, {3}))", iface, topLabel, routeEntry.interface_out, string.Join(";", routeEntry.labels_out));
                }
                catch (InvalidOperationException)
                {
                    // In case there is no such interface to output (the routing
                    // infromation is flawed), abandon routing.
                    Console.WriteLineStyled(style, Timestamp() + "[ROUTE] ({0}, {1}) ==> DROP", iface, topLabel);
                    SendRemoteLog("[ROUTE] ({0}, {1}) ==> DROP", iface, topLabel);
                }
            }
            else // ...otherwise, reroute the packet without the top-most label
            {
                Console.WriteLineStyled(style, Timestamp() + "[ROUTE] ({0}, {1}) ==> Reroute({0}, {2})", iface, topLabel, packet.labels.Peek());
                SendRemoteLog("[ROUTE] ({0}, {1}) ==> Reroute({0}, {2})", iface, topLabel, packet.labels.Peek());
                Route(packet, iface);
            }

        }

        void PrintRouteTable()
        {
            Console.WriteLineStyled(style, Timestamp() + "[ROUTE] Current table:");
            Console.WriteLine("\t┌───────┬───────┬───────┬───────┬───────┐\n" +
                              "\t│If_in  │Lbl_in │Method │If_out │Lbl_out│\n" +
                              "\t├───────┼───────┼───────┼───────┼───────┤");
            foreach (NHLFEntry entry in routingTable)
            {
                Console.WriteLine("\t│{0}\t│{1}│{2}│{3}\t│{4}│",
                    entry.interface_in,
                    entry.label_in + (entry.label_in > 999999 ? "" : "\t"), // If the label is greater than a million, place no tab
                    entry.is_swap_or_add ? "Add/Swp" : "Rmv    ",
                    entry.is_swap_or_add ? entry.interface_out.ToString() : "", // If the method is "Remove", no need to print this
                    entry.is_swap_or_add ? entry.labels_out[0] + (entry.labels_out[0] > 999999 ? "" : "\t") : ""); // Same as above two

                // If there is a label stack, print additional rows:
                if (entry.labels_out.Length > 1)
                    foreach (int label in entry.labels_out.Skip(1))
                    {
                        Console.WriteLine("\t│\t│\t│\t│\t│{0}│",
                            label + (label > 999999 ? "" : "\t")); // Same story as above - if over million, no tab
                    }
            }
            Console.WriteLine("\t└───────┴───────┴───────┴───────┴───────┘");
        }

        string Timestamp()
        {
            return String.Format("{0:00}:{1:00}.{2:000} ", time.Elapsed.Minutes, time.Elapsed.Seconds, time.Elapsed.Milliseconds);
        }

        private void SendRemoteLog(string format, params object[] args)
        {
            string identifier = string.Format("[{0}]", id);
            LoggingLib.SendMessage(identifier + string.Format(format, args));
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

        void StartPolling(int remotePort)
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

                    NHLFOrder order = NHLFSerialization.Deserialize(bytes);

                    NHLFEntry newEntry = order.entry;
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

                    if (order.add_entry)
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

                    Console.WriteLineStyled(style, Timestamp() + "[MGMT] {0} entry at ({1}, {2})", actionTaken, newEntry.interface_in, newEntry.label_in);
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            finally
            {
                listener.Close();
            }
        }
        
    }
}
