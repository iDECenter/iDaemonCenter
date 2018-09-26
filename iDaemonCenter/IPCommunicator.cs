using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace iDaemonCenter {
    public interface IInterProcessCommunicator {
        void Start();
        InterProcessMessage GetMessage();
        void SendMessage(InterProcessMessage msg);
        void Close();
    }

    public class TcpCommunicator : IInterProcessCommunicator {
        private bool _running;

        private readonly int _targetPort;
        private readonly TcpClient _client;
        private NetworkStream _ns;

        private const int TcpConnectTimeout = 5000;

        public TcpCommunicator(int targetPort) {
            _targetPort = targetPort;
            _client = new TcpClient(new IPEndPoint(IPAddress.Loopback, 0));

            _running = false;
        }

        public void Start() {
            if (_running) return;

            _client.Connect(IPAddress.Loopback, _targetPort);
            _ns = _client.GetStream();
            _frag = "";
            _running = true;
        }

        private const int BufferSize = 2048;
        private readonly byte[] _buffer = new byte[BufferSize];

        private string _frag;
        private readonly Queue<InterProcessMessage> _messageQueue = new Queue<InterProcessMessage>();

        private const char PackageSeparator = '|';
        private static readonly Encoding TcpEncoding = new UTF8Encoding(false);
        
        /// <summary>Read a message.</summary>
        /// <returns>Message read or <c>null</c> when disconnected.</returns>
        public InterProcessMessage GetMessage() {
            if (!_running) return null;
            if (_messageQueue.Count > 0) return _messageQueue.Dequeue();

            while (_messageQueue.Count == 0) {
                int readLen = _ns.Read(_buffer, 0, BufferSize);
                if (readLen == 0) return null;

                _frag += TcpEncoding.GetString(_buffer, 0, readLen);

                int start = 0;
                int len = _frag.Length;
                while (start < len) {
                    int sepIndex = _frag.IndexOf(PackageSeparator, start);
                    if (sepIndex < 0) break;

                    _messageQueue.Enqueue(InterProcessMessage.Parse(_frag.Substring(start, sepIndex - start)));
                    start = sepIndex + 1;
                }

                _frag = _frag.Substring(start, len - start);
            }

            return _messageQueue.Dequeue();
        }

        public void SendMessage(InterProcessMessage msg) {
            if (!_running) return;

            var msgStr = msg.Serialize() + PackageSeparator;
            var outBuffer = TcpEncoding.GetBytes(msgStr);
            var len = outBuffer.Length;

            Console.WriteLine(msgStr);

            _ns.Write(outBuffer, 0, len);
            _ns.Flush();
        }

        public void Close() {
            if (!_running) return;

            _ns?.Close();
            _client?.Close();

            _running = false;
        }
    }

    public class StdioCommunicator : IInterProcessCommunicator {
        public void Close() {
            // throw new NotImplementedException();
        }

        public InterProcessMessage GetMessage() {
            string cont = Console.ReadLine();

            if (cont == null) return null;

            return InterProcessMessage.Parse(cont);
        }

        public void SendMessage(InterProcessMessage msg) {
            Console.WriteLine(msg.Serialize());
        }

        public void Start() {
            // throw new NotImplementedException();
        }
    }
}
