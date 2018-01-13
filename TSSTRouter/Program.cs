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

namespace TSSTRouter
{
    class Program
    {
        /*
         * Syntax of CLI invocation:
         * 
         *   TSSTRouter <wirecloud_rx_port> <wirecloud_tx_port> \
         *              <management_rx_port> <management_tx_port> \
         *              <processing_interval_ms> \
         *              <ifaceID_1>:<bandwidth1> [ifaceID_2:bandwidth2, ...]
         * 
         * The minimum number of interfaces is 1
         */
        static void Main(string[] args)
        {
            if (args.Length > 0 && args.Length < 7)
            {
                Console.WriteLine("Insufficient parameters!\nPress any key to exit...");
                return;
            }

            try
            {
                string routerId = args[0];
                ushort wirecloudRxPort = ushort.Parse(args[1]);
                ushort wirecloudTxPort = ushort.Parse(args[2]);
                ushort mgmtRxPort = ushort.Parse(args[3]);
                ushort mgmtTxPort = ushort.Parse(args[4]);
                int intervalMs = Int32.Parse(args[5]);

                Dictionary<byte, uint> interfaceDefinitions = new Dictionary<byte, uint>();

                Router router;

                Console.Title = routerId.ToString();

                string path = "";

                try // Without file path
                {
                    KeyValuePair<byte, uint> pair = ParseInterfaceDefinition(args[6]);
                    interfaceDefinitions.Add(pair.Key, pair.Value);
                }
                catch (Exception) // With file path
                {
                    path = args[6];
                }

                // Take all remaining args (args.Skip(6)), get the array and convert it using Byte.Parse method
                foreach (var param in args.Skip(7).ToArray())
                {
                    KeyValuePair<byte, uint> pair = ParseInterfaceDefinition(param);
                    interfaceDefinitions.Add(pair.Key, pair.Value);
                }

                router = new Router(routerId, wirecloudRxPort, wirecloudTxPort, mgmtRxPort, mgmtTxPort, intervalMs, path, interfaceDefinitions);
            }
            catch (IndexOutOfRangeException)
            {
                Console.WriteLine("[INIT] Not enough arguments, proceeding to init loopback router with example parameters.");
                Router router = new Router("R.2137", 57702, 57702, 58001, 58000, 1000, "defaultRouting.rt", new Dictionary<byte, uint>{ [1] = 100 , [2] = 100, [3] = 100 });
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not start Router:\n{0}", e);
                Console.Read();
            }
        }

        private static KeyValuePair<byte, uint> ParseInterfaceDefinition(string input)
        {
            // This will be thrown when the string cannot be parsed
            ArgumentException exception = new ArgumentException("Given string is not a valid semicolon-separated pair!");
            
            string[] splitResults = input.Split(':'); // Extract semicolon-separated words

            if (splitResults.Length != 2) // Should contain two words
                throw exception;

            try
            {
                return new KeyValuePair<byte, uint>(Byte.Parse(splitResults[0]), uint.Parse(splitResults[1]));
            }
            catch (Exception e)
            {
                throw e;
            }
        }
    }
}
