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
         *   TSSTRouter <path_to_config>
         * 
         * The minimum number of interfaces is 1
         */
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                try
                {
                    // Try to read config from file - may throw exception
                    Dictionary<string, string> config = ReadConfigFromFile(args[0]);
                    
                    // All of these may throw exception
                    string routerId             = Convert.ToString(config["id"]); // Converting string to string for readability
                    string autonomicSystemId    = Convert.ToString(config["asid"]);
                    string subnetworkId         = Convert.ToString(config["snid"]);
                    ushort wirecloudRxPort      = Convert.ToUInt16(config["wirecloudRemotePort"]);
                    ushort wirecloudTxPort      = Convert.ToUInt16(config["wirecloudLocalPort"]);
                    ushort mgmtRxPort           = Convert.ToUInt16(config["managementLocalPort"]);
                    ushort ccPort               = Convert.ToUInt16(config["ccPort"]);
                    int intervalMs              = Convert.ToInt32(config["operationInterval"]);
                    string ifaceDefString       = Convert.ToString(config["interfaces"]);
                    string fibPath              = "";
                    
                    Dictionary<byte, uint> interfaceDefinitions = ParseInterfaces(ifaceDefString);

                    // Try to get path to a forwarding table file
                    try
                    {
                        fibPath = config["fibPath"];
                    }
                    catch (Exception)
                    {
                    }

                    new Router(
                        routerId,
                        autonomicSystemId,
                        subnetworkId,
                        wirecloudRxPort,
                        wirecloudTxPort,
                        mgmtRxPort,
                        ccPort,
                        intervalMs,
                        fibPath,
                        interfaceDefinitions);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to start Router: {0}", e.Message);
                    Console.Read();
                }
            }
            else
            {
                Console.WriteLine("[INIT] Config file not specified, proceeding to init test router with example parameters.");
                new Router(
                    "R.2137",
                    "AS.100",
                    "SN.1",
                    57702,
                    57702,
                    58001,
                    58000,
                    1000,
                    "defaultRouting.rt",
                    new Dictionary<byte, uint> { [1] = 100, [2] = 100, [3] = 100 });
            }
        }

        // Reads whatever there is in a config file and returns a Dictionary.
        // The file must containt 'key = value' pairs in separate lines, otherwise
        // The method throws an exception
        private static Dictionary<string, string> ReadConfigFromFile(string pathToFile)
        {
            FileLoadException exception = new FileLoadException("Could not read config file!");
            Dictionary<string, string> config = new Dictionary<string, string>();
            StreamReader sr = new StreamReader(pathToFile);

            while (!sr.EndOfStream)
            {
                // Read line
                string line = sr.ReadLine();
                // Check if it is a equals-sign separated pair
                string[] kvpair = line.Split('=');
                if (kvpair.Length != 2)
                    throw exception;
                // Rremove leading/trailing whitespace
                string key = kvpair[0].Trim();
                string val = kvpair[1].Trim();
                // Input key/value pair into dictionary
                config[key] = val;
            }

            return config;
        }

        // Given a string, eg.
        //     1:100, 2:300, 3:300
        // parses the values into a dictionary of byte:uint, where
        // interface id is the byte key, and capacity is the uint value
        private static Dictionary<byte, uint> ParseInterfaces(string str)
        {
            Dictionary<byte, uint> interfaces = new Dictionary<byte, uint>();

            // Get CSVs and check if enough
            string[] pairs = str.Split(',');
            if (pairs.Length < 2)
                throw new Exception("Must have at least two interfaces!");

            // For each CSV, trim and apply method
            foreach (string s in pairs)
            {
                KeyValuePair<byte, uint> kvpair = ParseInterfaceDefinition(s.Trim()); // Trim - remove leading/trailing whitespace
                interfaces[kvpair.Key] = kvpair.Value; // Update dictionary with values
            }

            return interfaces;
        }

        // Given semicolon-separated numbers, eg.
        //     13:400
        // Returns a KeyValuePair of byte:uint
        private static KeyValuePair<byte, uint> ParseInterfaceDefinition(string str)
        {
            // This will be thrown when the string cannot be parsed
            ArgumentException exception = new ArgumentException("Given string is not a valid semicolon-separated pair!");
            
            string[] splitResults = str.Split(':'); // Extract semicolon-separated words

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
