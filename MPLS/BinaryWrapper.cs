using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPLS
{
    // Klasa opakowująca ciąg danych binarnych do przesłania przez Socket
    public class BinaryWrapper
    {
        public byte[] data;
        public byte interfaceId;
        public byte randomNumber;
        
        public BinaryWrapper(byte[] data, byte interfaceId, byte randomNumber)
        {
            this.data = data;
            this.interfaceId = interfaceId;
            this.randomNumber = randomNumber;
        }
        public BinaryWrapper(byte[] data)
            : this(data, 0, 0)
        {
        }

        public BinaryWrapper(byte[] data, bool withHeader)
        {
            interfaceId = data[0];
            randomNumber = data[1];
            this.data = data.Skip(2).ToArray();
        }

        public byte[] HeaderPlusData()
        {
            List<byte> dataBuilder = new List<byte>(data);
            dataBuilder.Insert(0, interfaceId);
            dataBuilder.Insert(1, randomNumber);
            return dataBuilder.ToArray();
        }
    }
}
