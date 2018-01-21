using Colorful;
using MPLS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Console = Colorful.Console;

namespace TSSTRouter
{
    // Klasa reprezentująca stan w ramach obsługi połączenia przychodzącego ("state")
    class StateObject
    {
        public Socket workSocket = null; // Client socket
        public const int BufferSize = 65536; // Size of receive buffer (64 KiB)
        public byte[] buffer = new byte[BufferSize]; // Receive buffer
    }

    partial class TransportFunction
    {
        class MediumAccessController
        {
            private bool outSocketConnected = false;

            public ushort rxPort; // Near-end socket port number
            public ushort txPort; // Far-end host socket port number

            private ManualResetEvent serverThreadPause = new ManualResetEvent(false);
            private ManualResetEvent clientThreadPause = new ManualResetEvent(false);

            public delegate void PacketHandlingCallback(BinaryWrapper wrappedPacketPayload, byte interfaceId);
            private PacketHandlingCallback packetHandlingCallback;

            private Socket clientSocket;

            public MediumAccessController(ushort rxPort, ushort txPort, PacketHandlingCallback packetHandlingCallback)
            {
                this.rxPort = rxPort;
                this.txPort = txPort;
                this.packetHandlingCallback = packetHandlingCallback;
            }

            // Method to start the driver - launch threads.
            public void Start()
            {
                Thread server = new Thread(StartListening);
                Thread client = new Thread(StartClient);

                server.Start();
                client.Start();
            }

            private void StartListening()
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

                        Socket handler = listener.Accept(); // Thread waits until someone tries to connect

                        Log.WriteLine("[RX] Connected on {0}", rxPort);

                        StateObject state = new StateObject() // Create the state object.
                        {
                            workSocket = handler
                        };

                        while (true)
                        {
                            serverThreadPause.Reset();

                            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);

                            serverThreadPause.WaitOne();
                        }
                    }
                    catch (SocketException)
                    {
                        Console.WriteLine("[RX] Reinitiating listener.");
                    }
                }
            }

            void ReadCallback(IAsyncResult ar)
            {
                serverThreadPause.Set();

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
                        // Use a Wrapper constructor that extracts header bytes into fields
                        BinaryWrapper receivedMsg = new BinaryWrapper(state.buffer.ToArray(), true);
                        //Log.WriteLine("[RX] Receive on interface {0}", receivedMsg.interfaceId);
                        packetHandlingCallback(receivedMsg, receivedMsg.interfaceId);
                    }

                    //handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
                }
                catch (SocketException)
                {
                    Log.WriteLine("[RX] Lost incoming connection.");
                    handler.Shutdown(SocketShutdown.Both);
                    handler.Close();
                }
            }

            private void StartClient()
            {
                IPHostEntry ipHostInfo = Dns.GetHostEntry("localhost");
                IPAddress ipAddress = ipHostInfo.AddressList[0];
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, txPort);
                Socket client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp); // Create a TCP/IP socket.
                clientSocket = client; // Make this socket class-wide accessible
                while (true)
                {
                    try
                    {
                        client.Connect(remoteEP);
                        Log.WriteLine("[TX] Connected to {0}", txPort);
                        outSocketConnected = true;
                        clientThreadPause.WaitOne();
                    }
                    catch (SocketException)
                    {
                        outSocketConnected = false;
                        // ...retry connection
                    }
                }
            }

            public void SendData(BinaryWrapper wrappedPacket, byte interfaceId)
            {
                if (!outSocketConnected)
                {
                    throw new Exception("Router is not connected with wirecloud's receiving end!");
                }

                wrappedPacket.interfaceId = interfaceId; // Header byte 1: interface identifier number
                wrappedPacket.randomNumber = (byte)((new Random()).Next() % 255); // Header byte 2: Random byte-sized ID generated based on the current timestamp

                byte[] rawData = wrappedPacket.HeaderPlusData();
                clientSocket.BeginSend(rawData, 0, rawData.Length, 0, new AsyncCallback(SendCallback), clientSocket); // Begin sending the data to the remote device.
            }

            private void SendCallback(IAsyncResult ar)
            {
                try
                {
                    Socket client = (Socket)ar.AsyncState; // Retrieve the socket from the state object.
                    int bytesSent = client.EndSend(ar); // Complete sending the data to the remote device.
                                                        //Log.WriteLine(style, "[TX] <== Sent {0} bytes to server.", bytesSent);
                }
                catch (Exception e)
                {
                    Log.WriteLine(e.ToString());
                }
            }
        }
    }
}
