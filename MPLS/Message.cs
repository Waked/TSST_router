using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPLS
{
    // Klasa opakowująca ciąg danych binarnych do przesłania przez Socket
    public class Message
    {
        public byte[] data;
        public byte[] header = new byte[2];

        public Message(byte[] data)
        {
            this.data = data;
        }

        public byte[] HeaderPlusData()
        {
            List<byte> dataBuilder = new List<byte>(data);
            // Prepend the List with header bytes
            dataBuilder.InsertRange(0, header);
            return dataBuilder.ToArray();
        }
        
    }
}
