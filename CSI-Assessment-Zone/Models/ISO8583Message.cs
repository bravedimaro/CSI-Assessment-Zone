using Newtonsoft.Json;

namespace CSI_Assessment_Zone.Models
{
    public class ISO8583Message
    {
        public string MTI { get; set; }
        public Dictionary<int, string> Fields { get; set; }

        public ISO8583Message()
        {
            Fields = new Dictionary<int, string>();
        }

        public void SetField(int fieldNumber, string value)
        {
            Fields[fieldNumber] = value;
        }

        public string GetField(int fieldNumber)
        {
            return Fields.TryGetValue(fieldNumber, out string value) ? value : null;
        }

        public string Pack()
        {
            // TODO: Implement actual packing logic
            return JsonConvert.SerializeObject(this);
        }

        public static ISO8583Message Unpack(string packedMessage)
        {
            // TODO: Implement actual unpacking logic
            return JsonConvert.DeserializeObject<ISO8583Message>(packedMessage);
        }
    }
}
