using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TSST_router
{
    class Program
    {
        /*
         * Syntax of usage:
         * 
         *  > TSST_router <receive_port> <remote_port> <ifaceID_1> [ifaceID_2, ...]
         * 
         * The minimum number of interfaces is 1
         */
        static void Main(string[] args)
        {
            if (args.Length > 0 && args.Length < 7)
            {
                Console.WriteLine("Not enough parameters!\nPress any key to exit...");
                return;
            }

            try
            {
                string routerId = args[0];
                int localPort = Int32.Parse(args[1]);
                int remotePort = Int32.Parse(args[2]);
                int localMgmtPort = Int32.Parse(args[3]);
                int remoteMgmtPort = Int32.Parse(args[4]);
                int intervalMs = Int32.Parse(args[5]);
                byte[] ifaceIds;
                Router router;

                Console.Title = routerId.ToString();

                try // Without file path
                {
                    Int32.Parse(args[6]);

                    // Take all remaining args (args.Skip(6)), get the array and convert it using Byte.Parse method
                    ifaceIds = Array.ConvertAll(args.Skip(6).ToArray(), Byte.Parse);
                    router = new Router(routerId, localPort, remotePort, localMgmtPort, remoteMgmtPort, intervalMs, ifaceIds);
                }
                catch (Exception) // With file path
                {
                    string path = args[6];
                    ifaceIds = Array.ConvertAll(args.Skip(7).ToArray(), Byte.Parse);
                    router = new Router(routerId, localPort, remotePort, localMgmtPort, remoteMgmtPort, intervalMs, path, ifaceIds);
                }
            }
            catch (IndexOutOfRangeException)
            {
                Console.WriteLine("[INIT] Could not parse arguments, proceeding to init loopback router with example parameters.");
                Router router = new Router("R.2137", 57702, 57702, 58001, 58000, 1000, "defaultRouting.rt", new byte[] { 1, 2, 3 });
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not start Router:\n{0}", e);
                Console.Read();
            }
        }
    }
}
