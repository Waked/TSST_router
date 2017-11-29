using Routing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TSST_router
{
    public static class StaticMethods
    {

        /*  TODO
         *  
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
        public static List<RouteEntry> ParseRoutingTable(string pathToFile)
        {
            List<RouteEntry> newEntryList = new List<RouteEntry>();

            // If the filepath is empty, return an empty object (the table is clear)
            if (pathToFile == "")
                return newEntryList;

            try
            {
                string[] tableRows = File.ReadAllLines(pathToFile, Encoding.UTF8);

                // Iterate over each line in routing config file
                foreach (string row in tableRows)
                {
                    if (row[0] == '#') // Check if a row is a comment - if true, skip it
                        continue;
                    string[] attributes = row.Split('\t');
                    RouteEntry newEntry = new RouteEntry(
                        byte.Parse(attributes[0]),
                        int.Parse(attributes[1]),
                        bool.Parse(attributes[2]),
                        byte.Parse(attributes[3]),
                        Array.ConvertAll(attributes[4].Split(','), int.Parse) // Convert labels as CSVs into int[]
                        );
                    newEntryList.Add(newEntry);
                }
                return newEntryList;
            }
            catch (FileNotFoundException)
            {
                throw new FileNotFoundException();
            }
            
        }

    }
}
