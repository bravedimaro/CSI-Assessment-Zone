using CSI_Assessment_Zone.Models;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Trx.Communication.Channels.Sinks.Framing;
using Trx.Communication.Channels.Sinks;
using Trx.Communication.Channels;
using Trx.Communication.Channels.Tcp;
using Trx.Coordination.TupleSpace;
using Trx.Messaging;
using Trx.Messaging.Iso8583;
using Trx.Logging;
using imohsenb.iso8583.utils;
using System;
using Trx.Utilities;

namespace CSI_Assessment_Zone.Services
{
    public class ISO8583Service
    {
        private const string ISO8583_IP = "";
        private const int ISO8583_PORT = 29001;
        private const int Field3ProcCode = 3;
        private const int Field7TransDateTime = 7;
        private const int Field11Trace = 11;
        private const int Field24Nii = 24;
        private const int Field41TerminalCode = 41;
        private const int Field42MerchantCode = 42;

        public  TcpClientChannel _client2;
        public  VolatileStanSequencer _sequencer2;

        private  string _terminalCode;

        private int _expiredRequests;
        private int _requestsCnt;
        private Timer _timer;
        public async void connect()
        {
            _client2.Connect();
            Task.Delay(1000).Wait();
        }
        public async Task<ISO8583Message> SendMessage(ISO8583Message message)
        {
            using (var client = new TcpClient())
            {
                await client.ConnectAsync(ISO8583_IP, ISO8583_PORT);
                using (var stream = client.GetStream())
                {
                    var packedMessage = message.Pack();
                    byte[] data = Encoding.ASCII.GetBytes(packedMessage);
                    await stream.WriteAsync(data, 0, data.Length);

                    byte[] responseBuffer = new byte[1024];
                    int bytesRead = await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);
                    string responseContent = Encoding.ASCII.GetString(responseBuffer, 0, bytesRead);

                    return ISO8583Message.Unpack(responseContent);
                }
            }
        }

        public async Task<ISO8583Message> SendEchoMessage()
        {
            var echoMessage = new ISO8583Message { MTI = "0800" };
            echoMessage.SetField(7, DateTime.Now.ToString("MMddHHmmss"));
            echoMessage.SetField(11, GenerateSTAN());
            
            string terminalCode = null;

            connect();
            var pipeline = new Pipeline();

            pipeline.Push(new ReconnectionSink());
            pipeline.Push(new NboFrameLengthSink(2) { IncludeHeaderLength = false, MaxFrameLength = 1024 });
            pipeline.Push(
                new MessageFormatterSink(new Iso8583MessageFormatter((@"Formatters\Iso8583Ascii1987.xml"))));
            var ts = new TupleSpace<ReceiveDescriptor>();

            // Create a client peer to connect to remote system. The messages
            // will be matched using fields 41 and 11.
            _client2 = new TcpClientChannel(pipeline, ts, new FieldsMessagesIdentifier(new[] { 11 }))
            {
                RemotePort = 29001,
                RemoteInterface = ISO8583_IP,
                Name = "Brave"
            };

            //_terminalCode2 = terminalCode;

            _sequencer2 = new VolatileStanSequencer();

            var echoMsg = new Iso8583Message(800);

            DateTime transmissionDate = DateTime.Now;
            echoMsg.Fields.Add(Field7TransDateTime, string.Format("{0}{1}",
                string.Format("{0:00}{1:00}", transmissionDate.Month, transmissionDate.Day),
                string.Format("{0:00}{1:00}{2:00}", transmissionDate.Hour,
                    transmissionDate.Minute, transmissionDate.Second)));
            echoMsg.Fields.Add(Field11Trace, _sequencer2.Increment().ToString());
            echoMsg.Fields.Add(11, "123456");
           
            SendRequestHandlerCtrl sndCtrl = _client2.SendExpectingResponse(echoMsg, 1000, false, null);
            sndCtrl.WaitCompletion(); // Wait send completion.
            if (!sndCtrl.Successful)
            {
                Console.WriteLine(string.Format("Merchant: unsuccessful request # {0} ({1}.",
                    _sequencer2.CurrentValue(), sndCtrl.Message));
                if (sndCtrl.Error != null)
                    Console.WriteLine(sndCtrl.Error);
            }
            sndCtrl.Request.WaitResponse();

            if (sndCtrl.Request.IsExpired)
                _expiredRequests++;
            else
                _requestsCnt++;
            return null;
        }

        public async Task<ISO8583Message> SendKeyExchangeMessage()
        {
            var keyExchangeMessage = new ISO8583Message { MTI = "0800" };
            keyExchangeMessage.SetField(7, DateTime.Now.ToString("MMddHHmmss"));
            keyExchangeMessage.SetField(11, GenerateSTAN());
            keyExchangeMessage.SetField(53, GenerateKeyExchangeData());
            return await SendMessage(keyExchangeMessage);
        }

        public async Task<ISO8583Message> SendFinancialMessage(string pan, string pin, int amount, string currencyCode,string ZPK)
        {
            var financialMessage = new ISO8583Message { MTI = "0200" };
            financialMessage.SetField(2, pan);
            financialMessage.SetField(3, "000000");
            financialMessage.SetField(4, amount.ToString("D12"));
            financialMessage.SetField(7, DateTime.Now.ToString("MMddHHmmss"));
            financialMessage.SetField(11, GenerateSTAN());
            financialMessage.SetField(49, currencyCode);
            financialMessage.SetField(52, EncryptPinBlock(pin, pan,ZPK));
            return await SendMessage(financialMessage);
        }

        private string GenerateSTAN()
        {
            return new Random().Next(100000, 999999).ToString();
        }

        private string GenerateKeyExchangeData()
        {
            // Generate a Diffie-Hellman key pair
            using (ECDiffieHellmanCng diffieHellman = new ECDiffieHellmanCng())
            {
                diffieHellman.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash;
                diffieHellman.HashAlgorithm = CngAlgorithm.Sha256;

                // Export the public key to be shared with the other party
                byte[] publicKey = diffieHellman.PublicKey.ToByteArray();

                // Convert the public key to a string format for exchange (e.g., Base64)
                string publicKeyString = Convert.ToBase64String(publicKey);

                // Return the public key string to be sent to the other party
                return publicKeyString;
            }
        }

        private string EncryptPinBlock(string pin, string pan, string zpk)
        {
            // Generate the PIN block (ISO 9564-1 Format 0)
            string pinBlock = GeneratePinBlock(pin, pan);

            // Convert the pinBlock and zpk to bytes
            byte[] pinBlockBytes = HexStringToByteArray(pinBlock);
            byte[] zpkBytes = HexStringToByteArray(zpk);

            // Encrypt using Triple DES with ZPK
            using (TripleDESCryptoServiceProvider tripleDES = new TripleDESCryptoServiceProvider())
            {
                tripleDES.Key = zpkBytes;
                tripleDES.Mode = CipherMode.ECB;
                tripleDES.Padding = PaddingMode.None;

                using (ICryptoTransform encryptor = tripleDES.CreateEncryptor())
                {
                    byte[] encryptedPinBlock = encryptor.TransformFinalBlock(pinBlockBytes, 0, pinBlockBytes.Length);
                    return ByteArrayToHexString(encryptedPinBlock);
                }
            }
        }
        private string GeneratePinBlock(string pin, string pan)
        {
            // Convert PIN to a 16-digit block
            string pinBlock = "0" + pin.Length + pin + new string('F', 14 - pin.Length);

            // Extract the last 12 digits of the PAN, excluding the check digit
            string panBlock = "0000" + pan.Substring(pan.Length - 13, 12);

            // XOR the PIN block and PAN block to get the final PIN block
            string finalPinBlock = XorHexStrings(pinBlock, panBlock);
            return finalPinBlock;
        }

        // Helper method to XOR two hexadecimal strings
        private string XorHexStrings(string hex1, string hex2)
        {
            byte[] bytes1 = HexStringToByteArray(hex1);
            byte[] bytes2 = HexStringToByteArray(hex2);

            byte[] xorResult = new byte[bytes1.Length];
            for (int i = 0; i < bytes1.Length; i++)
            {
                xorResult[i] = (byte)(bytes1[i] ^ bytes2[i]);
            }

            return ByteArrayToHexString(xorResult);
        }
        // Helper method to convert a hex string to a byte array
        private byte[] HexStringToByteArray(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }

        // Helper method to convert a byte array to a hex string
        private string ByteArrayToHexString(byte[] bytes)
        {
            StringBuilder hex = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                hex.AppendFormat("{0:X2}", b);
            }
            return hex.ToString();
        }
       

    }
    public class Merchant
    {
        private const int Field3ProcCode = 3;
        private const int Field7TransDateTime = 7;
        private const int Field11Trace = 11;
        private const int Field24Nii = 24;
        private const int Field41TerminalCode = 41;
        private const int Field42MerchantCode = 42;

        private readonly TcpClientChannel _client;
        private readonly VolatileStanSequencer _sequencer;

        private readonly string _terminalCode;

        private int _expiredRequests;
        private int _requestsCnt;
        private Timer _timer;

        /// <summary>
        /// Initializes a new instance of this class.
        /// </summary>
        /// <param name="hostname">
        /// The host name of the computer where the Acquirer.exe program is running.
        /// </param>
        public Merchant(string hostname, string terminalCode)
        {
            var pipeline = new Pipeline();
            
            pipeline.Push(new ReconnectionSink());
            pipeline.Push(new NboFrameLengthSink(2) { IncludeHeaderLength = false, MaxFrameLength = 1024 });
            pipeline.Push(
                new MessageFormatterSink(new Iso8583MessageFormatter((@"Formatters\Iso8583Ascii1987.xml"))));
            var ts = new TupleSpace<ReceiveDescriptor>();

            // Create a client peer to connect to remote system. The messages
            // will be matched using fields 41 and 11.
            _client = new TcpClientChannel(pipeline, ts, new FieldsMessagesIdentifier(new[] { 11 }))
            {
                RemotePort = 29001,
                RemoteInterface = hostname,
                Name = "Merchant"
            };

            _terminalCode = terminalCode;

            _sequencer = new VolatileStanSequencer();
        }

        /// <summary>
        /// Returns the number of requests made.
        /// </summary>
        public int RequestsCount
        {
            get { return _requestsCnt; }
        }

        /// <summary>
        /// Returns the number of expired requests (not responded by the remote peer).
        /// </summary>
        public int ExpiredRequests
        {
            get { return _expiredRequests; }
        }

        /// <summary>
        /// Called when the timer ticks.
        /// </summary>
        /// <param name="state">
        /// Null.
        /// </param>
        private void OnTimer(object state)
        {
            lock (this)
            {
                if (_client.IsConnected)
                {
                    // Build echo test message.
                    var echoMsg = new Iso8583Message(800);
                   // echoMsg.Fields.Add(Field3ProcCode, "990000");
                    DateTime transmissionDate = DateTime.Now;
                    echoMsg.Fields.Add(Field7TransDateTime, string.Format("{0}{1}",
                        string.Format("{0:00}{1:00}", transmissionDate.Month, transmissionDate.Day),
                        string.Format("{0:00}{1:00}{2:00}", transmissionDate.Hour,
                            transmissionDate.Minute, transmissionDate.Second)));
                    echoMsg.Fields.Add(Field11Trace, _sequencer.Increment().ToString());
                    echoMsg.Fields.Add(11, "123456");
                    //echoMsg.Fields.Add(Field24Nii, "101");
                    //echoMsg.Fields.Add(Field41TerminalCode, _terminalCode);
                    //echoMsg.Fields.Add(Field42MerchantCode, "MC-1");

                    SendRequestHandlerCtrl sndCtrl = _client.SendExpectingResponse(echoMsg, 1000, false, null);
                    sndCtrl.WaitCompletion(); // Wait send completion.
                    if (!sndCtrl.Successful)
                    {
                        Console.WriteLine(string.Format("Merchant: unsuccessful request # {0} ({1}.",
                            _sequencer.CurrentValue(), sndCtrl.Message));
                        if (sndCtrl.Error != null)
                            Console.WriteLine(sndCtrl.Error);
                    }
                    sndCtrl.Request.WaitResponse();
                    var tt = sndCtrl.Request.ReceivedMessage;

                    if (sndCtrl.Request.IsExpired)
                        _expiredRequests++;
                    else
                        _requestsCnt++;
                }
            }
        }

        public bool Start()
        {
            _timer = new Timer(OnTimer, null, 1000, 2000);

            ChannelRequestCtrl ctrl = _client.Connect();
            ctrl.WaitCompletion();

            if (!ctrl.Successful)
            {
                Console.WriteLine("Merchant: can't connect to acquirer...");
                if (ctrl.Error != null)
                    Console.WriteLine(ctrl.Error);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Stop merchant activity.
        /// </summary>
        public void Stop()
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }


        
    }
}
