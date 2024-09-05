using Trx.Communication.Channels.Sinks.Framing;
using Trx.Communication.Channels.Sinks;
using Trx.Communication.Channels;
using Trx.Communication.Channels.Tcp;
using Trx.Coordination.TupleSpace;
using Trx.Messaging;
using Trx.Messaging.Iso8583;
using Trx.Utilities;
using System.Security.Cryptography;
using NetCore8583;

namespace CSI_Assessment_Zone.Services
{
    public class TrxService
    {
        private const string ISO8583_IP = "";
        private const int ISO8583_PORT = 29001;
        private const int Field3ProcCode = 3;
        private const int Field7TransDateTime = 7;
        private const int Field11Trace = 11;
        private const int Field24Nii = 24;
        private const int Field41TerminalCode = 41;
        private const int Field42MerchantCode = 42;

        public readonly TcpClientChannel _client;
        public readonly VolatileStanSequencer _sequencer;
        private readonly IConfiguration _config;
        private string _terminalCode;

        private int _expiredRequests;
        private int _requestsCnt;
        private Timer _timer;
        public TrxService(TcpClientChannel tcpClientChannel, VolatileStanSequencer volatileStanSequencer, IConfiguration configuration)
        {
            _client = tcpClientChannel;
            _sequencer = volatileStanSequencer;
            _config = configuration;
        }
        public TrxService(IConfiguration configuration)
        {
            _config = configuration;
            // Initialize the pipeline
            var pipeline = new Pipeline();

            pipeline.Push(new ReconnectionSink());
            pipeline.Push(new NboFrameLengthSink(2) { IncludeHeaderLength = false, MaxFrameLength = 1024 });
            pipeline.Push(new MessageFormatterSink(new Iso8583MessageFormatter(@"Formatters\Iso8583Ascii1987.xml")));

            var ts = new TupleSpace<ReceiveDescriptor>();

            // Initialize the client using the pipeline
            _client = new TcpClientChannel(pipeline, ts, new FieldsMessagesIdentifier(new[] { 11 }))
            {
                RemotePort =_config.GetValue<int>("ISO8583Settings:Port"),
                RemoteInterface = _config.GetValue<string>("ISO8583Settings:IP"), // Replace with your ISO8583 server IP
                Name = "Brave"
            };

            _sequencer = new VolatileStanSequencer();  // Initialize or pass the sequencer as needed
        }
        public  bool connect()
        {
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

        public async Task<object> SendEchoMessage()
        {
            connect();
            var echoMsg = new Iso8583Message(800);

            DateTime transmissionDate = DateTime.Now;
            echoMsg.Fields.Add(Field7TransDateTime, string.Format("{0}{1}",
                string.Format("{0:00}{1:00}", transmissionDate.Month, transmissionDate.Day),
                string.Format("{0:00}{1:00}{2:00}", transmissionDate.Hour,
                    transmissionDate.Minute, transmissionDate.Second)));
            echoMsg.Fields.Add(Field11Trace, _sequencer.Increment().ToString());
            echoMsg.Fields.Add(11, "123456");
            echoMsg.Fields.Add(70, "301");

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
            var Response = sndCtrl.Request.ReceivedMessage;
            return Response;
        }
        public async Task<object> SendKeyExchangeMessage()
        {
            connect();
            var echoMsg = new Iso8583Message(800);
            DateTime transmissionDate = DateTime.Now;
            echoMsg.Fields.Add(Field7TransDateTime, string.Format("{0}{1}",
                string.Format("{0:00}{1:00}", transmissionDate.Month, transmissionDate.Day),
                string.Format("{0:00}{1:00}{2:00}", transmissionDate.Hour,
                    transmissionDate.Minute, transmissionDate.Second)));
            echoMsg.Fields.Add(Field11Trace, _sequencer.Increment().ToString());
            echoMsg.Fields.Add(11, "123456");
            echoMsg.Fields.Add(70, "101"); // Network Management Information Code for Key Exchange
            echoMsg.Fields.Add(48, GenerateKeyExchangeData());
            //echoMsg.Fields.Add(53, GenerateSecurityControlInfo());
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
            var Response = sndCtrl.Request.ReceivedMessage;
            return Response;
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
        private void ProcessKeyExchangeResponse(IsoMessage response)
        {
            // Check if the response is valid and the MTI is 0810
            if (response.Type.ToString() == "0810")
            {
                // Extract the session key from DE 48
                IsoValue field48 = response.GetField(48);
                string sessionKey = field48?.Value?.ToString();

                // Save the session key securely
                SaveSessionKey(sessionKey);

                // Optionally, check the response code (DE 39)
                IsoValue field39 = response.GetField(39);
                string responseCode = field39?.Value?.ToString();
                if (responseCode == "00")
                {
                    Console.WriteLine("Key exchange successful.");
                }
                else
                {
                    Console.WriteLine($"Key exchange failed with response code: {responseCode}");
                }
            }
        }

        private void SaveSessionKey(string sessionKeyData)
        {
            // Save the session key securely, e.g., in memory, a secure storage, or a database
            Console.WriteLine("Session key saved: " + sessionKeyData);
        }

    }
}
