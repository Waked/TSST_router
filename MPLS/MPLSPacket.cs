using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPLS
{
    [Serializable]
    public class MPLSPacket
    {
        public string data;
        public Stack<int> labels;
        public QOS qos;

        public object managementObject = null;

        /*
         * MPLS Packet contains some data in string format and
         * a stack of labels
         */

        public MPLSPacket(int[] label, string data, QOS qos = QOS.HIGH)
        {
            labels = new Stack<int>(label);
            this.data = data;
            this.qos = qos;
        }

        public static MPLSPacket EncapsulatedManagementObject(object managementObject)
        {
            // Ensure that the managementObject is serializable
            if (!managementObject.GetType().IsSerializable)
                throw new ArgumentException("Passed management object is not serializable!");

            MPLSPacket packet = new MPLSPacket(null, "");
            packet.managementObject = managementObject;

            return packet;
        } 
    }

    public enum QOS
    {
        HIGH,
        MEDIUM,
        LOW
    }
}
