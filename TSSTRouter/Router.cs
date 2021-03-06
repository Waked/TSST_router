﻿using MPLS;
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
        // Static RNG used in other router components
        public static Random rng = new Random();

#if DEBUG
        public static int allocCounter = 2137;
        private Timer debugTimer;
#endif

        // Config information
        public string id;
        public string autonomicSystemId;
        public string subnetworkId;
        public ushort wirecloudLocalPort;
        public ushort wirecloudRemotePort;
        public ushort mgmtLocalPort;
        public ushort connectionControllerPort;
        public ushort routingControllerPort;
        public int sendIntervalMillis; // Interval between packet shipments, in milliseconds [ms]
        private string routingTablePath; // Path to existing routing table stored in an .rt file (may be empty)

        // Router components
        private LinkResourceManager LRM;
        private TransportFunction transportFunction;
        private Dictionary<byte, uint> routerInterfaceDefs;

        // Threads
        private Thread managementThread;
        //private List<Timer> periodicTasks; // Collection containing threads executed periodically

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
            ushort routingControllerPort,
            int intervalMs, string filePath,
            Dictionary<byte, uint> interfaceDefinitions
            )
        {
            id = routerId;
            autonomicSystemId = asId;
            this.subnetworkId = subnetworkId;
            this.mgmtLocalPort = mgmtLocalPort;
            this.connectionControllerPort = connectionControllerPort;
            this.routingControllerPort = routingControllerPort;
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
            Log.WriteLine(true, "Interfaces: " + string.Join(", ", routerInterfaceDefs.Select(def => def.Key + ": " + def.Value + "Mb/s").ToArray()));
            Log.WriteLine(true, "======");
            
            try
            {
                Log.WriteLine("Starting router... ");
                Log.ResetTimer();
                
                // Those objects create and start threads upon construction
                LRM = new LinkResourceManager(
                    id,
                    autonomicSystemId,
                    subnetworkId,
                    connectionControllerPort,
                    routingControllerPort,
                    mgmtLocalPort,
                    routerInterfaceDefs
                    );
                transportFunction = new TransportFunction(
                    sendIntervalMillis,
                    wirecloudLocalPort,
                    wirecloudRemotePort,
                    routerInterfaceDefs
                    );

                // Pass callbacks to components
                LRM.sendMgmtMessage = SendManagementMsg;
                LRM.sendPeerMessage = transportFunction.SendPeerMessage;
                transportFunction.handleMgmtPackets = LRM.HandleManagementPacket;

                // Launch rotuer management thread
                managementThread = new Thread(ReceiveManagementMsg);
                managementThread.Start();

#if DEBUG
                 // Launch debug thread
                //debugTimer = new Timer((object state) =>
                // {
                //     Log.WriteLine("[DEBUG] Free resources on iface {0}: {1}", 1, 0);
                // }, null, 5000, 1000);
#endif
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

            // This method incorporates an infinite loop, so this
            // thread shall never exit.
            KeyboardControl();
        }

        // Keyboard input handling -each keypress can be interpreted as
        // a control command.
        void KeyboardControl()
        {
            while (true)
            {
                switch (Console.ReadKey(true).Key)
                {
                    case ConsoleKey.T:
                        transportFunction.PrintRouteTable();
                        break;
                    case ConsoleKey.A:
                        LRM.PrintAssignments();
                        break;
                    case ConsoleKey.P:
                        if (Log.IsPaused)
                            Log.Unpause();
                        else
                            Log.Pause();
                        break;
#if DEBUG
                    case ConsoleKey.Enter:
                        MPLSPacket testPacket = new MPLSPacket(new int[] { 2137 }, "This is a test MPLSMessage.");
                        transportFunction.EnqueuePacketOnFirstQueue(testPacket);
                        break;
                    case ConsoleKey.U:
                        NHLFEntry entry = new NHLFEntry(10, 1, 17, true, 2, new int[] { 35 });
                        AddUpdateRequest testUpdateReq = new AddUpdateRequest("Helo it me", mgmtLocalPort, 2137, entry);
                        SendManagementMsg(mgmtLocalPort, testUpdateReq);
                        break;
                    case ConsoleKey.R:
                        RemoveRequest testRemoveReq = new RemoveRequest("Helo it me", mgmtLocalPort, 2137, 10);
                        SendManagementMsg(mgmtLocalPort, testRemoveReq);
                        break;
                    case ConsoleKey.L:
                        AllocateRequest testAllocReq = new AllocateRequest(id, mgmtLocalPort, allocCounter++, 1, 30, 1);
                        SendManagementMsg(mgmtLocalPort, testAllocReq);
                        break;
                    case ConsoleKey.D:
                        DeallocateRequest testDeallocReq = new DeallocateRequest(id, mgmtLocalPort, allocCounter--, 1);
                        SendManagementMsg(mgmtLocalPort, testDeallocReq);
                        break;
#endif
                    default:
                        break;
                }
            }
        }

        // Method to send management messages over UDP to a specified port.
        // The messages must be derivatives of the Message object in the
        // Communications library.
        void SendManagementMsg(ushort port, Communications.Message msg)
        {
            IPEndPoint remoteEP = new IPEndPoint(localhost, port);
            byte[] data = Communications.Serialization.Serialize(msg);
            mgmtTxSocket.SendTo(data, remoteEP);
        }

        // Method invoked in a separate thread, sets up a UDP listener
        // that listens for anything on a management port, then detemines
        // what kind of message it is (with what information) and handles
        // the response.
        void ReceiveManagementMsg()
        {
            UdpClient listener = new UdpClient(mgmtLocalPort);
            IPEndPoint groupEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), mgmtLocalPort);

            try
            {
                while (true)
                {
                    byte[] bytes = listener.Receive(ref groupEP);

                    Communications.Message msg = Communications.Serialization.Deserialize(bytes);
                                        
                    switch (msg.messageType)
                    {
                        case "NHLF.AddUpdateRequest":
                            Log.WriteLine("[MGMT CCI] {1}, port {2}: {0})", msg.messageType, msg.senderID, msg.senderPort);
                            AddUpdateRequest addUpdateReq = (AddUpdateRequest)msg;
                            transportFunction.UpdateRoutingTable(addUpdateReq.entry, true);
                            // !!! IT CAN GO TERRIBLY WRONG HERE !!!
                            SendManagementMsg(
                                (ushort)addUpdateReq.senderPort,
                                new AddUpdateResponse(id, mgmtLocalPort, addUpdateReq.seq, true)
                                );
                            Log.WriteLine("[MGMT CCI] Add/update forward: conn {2}, iface {0}, label {1}", addUpdateReq.entry.interface_in, addUpdateReq.entry.label_in, addUpdateReq.entry.connectionID);
                            break;
                        case "NHLF.RemoveRequest":
                            Log.WriteLine("[MGMT CCI] {1}, port {2}: {0})", msg.messageType, msg.senderID, msg.senderPort);
                            RemoveRequest removeReq = (RemoveRequest)msg;
                            bool status = transportFunction.RemoveFromRoutingTable(removeReq.connectionID);
                            SendManagementMsg(
                                (ushort)removeReq.senderPort,
                                new RemoveResponse(id, mgmtLocalPort, removeReq.seq, status)
                                );
                            Log.WriteLine("[MGMT CCI] {0} entries for connection {1}", status ? "Removed" : "Could not remove", removeReq.connectionID);
                            break;
                        case "AllocateRequest":
                            Log.WriteLine("[MGMT LRM] {1}, port {2}: {0})", msg.messageType, msg.senderID, msg.senderPort);
                            AllocateRequest allocateReq = (AllocateRequest)msg;
                            uint label = LRM.AssignBandwidthOnInterface(allocateReq.interfaceID, (uint)allocateReq.bitrate, allocateReq.connectionID);
                            SendManagementMsg(
                                (ushort)allocateReq.senderPort,
                                new AllocateResponse(
                                    id,
                                    mgmtLocalPort,
                                    (int)label,
                                    allocateReq.seq
                                    )
                                );
                            if (label != 0)
                                Log.WriteLine("[MGMT LRM] Allocate {0} Mb/s on iface {1} for connection {2})", allocateReq.bitrate, allocateReq.interfaceID, allocateReq.connectionID);
                            else
                                Log.WriteLine("[MGMT LRM] Could not allocate {0} Mb/s on iface {1} for connection {2})", allocateReq.bitrate, allocateReq.interfaceID, allocateReq.connectionID);
                            break;
                        case "DeallocateRequest":
                            Log.WriteLine("[MGMT LRM] {1}, port {2}: {0})", msg.messageType, msg.senderID, msg.senderPort);
                            DeallocateRequest deallocateReq = (DeallocateRequest)msg;
                            LRM.ReleaseAsignedBandwidth(deallocateReq.connectionID);
                            SendManagementMsg(
                                (ushort)deallocateReq.senderPort,
                                new DeallocateResponse(
                                    id,
                                    mgmtLocalPort,
                                    deallocateReq.seq
                                    )
                                );
                            Log.WriteLine("[MGMT LRM] Deallocate resources for connection {0})", deallocateReq.connectionID);
                            break;
#if DEBUG
                        case "NHLF.AddUpdateResponse":
                            AddUpdateResponse addUpdateResponse = (AddUpdateResponse)msg;
                            Log.WriteLine("[CCI] AddUpdateRequest for {0} {1}.", addUpdateResponse.senderID, addUpdateResponse.status ? "successful" : "failed");
                            break;
                        case "NHLF.RemoveResponse":
                            RemoveResponse removeResp = (RemoveResponse)msg;
                            Log.WriteLine("[CCI] Remove {0}", removeResp.status ? "some" : "none");
                            break;
                        case "LRMRC.LinkStateUpdate":
                            LinkStateUpdate linkStateUpdate = (LinkStateUpdate)msg;
                            Log.WriteLine("[RC] {0} --{2}-> {1}", linkStateUpdate.beginNode.id, linkStateUpdate.endNode.id, linkStateUpdate.capacity);
                            break;
#endif
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
