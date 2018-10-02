using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Threading;

using GeminiLab.Core2.ML.Json;

namespace iPseudoServer {
    class Program {
        public static void Main(string[] args) {
            Console.WriteLine("Neither working nor used anymore");
            Environment.Exit(0);
            Console.WriteLine("Server start");

            var listener = new TcpListener(IPAddress.Loopback, 3154);
            listener.Start();

            while (true) {
                var client = listener.AcceptTcpClient();

                Console.WriteLine($"Accept from {client.Client.RemoteEndPoint}");

                var ns = client.GetStream();
                var tw = new StreamWriter(ns, new UTF8Encoding(false));
                var tr = new StreamReader(ns, new UTF8Encoding(false));

                tw.Write("{\"module\":\"projmgr\",\"command\":\"instantiate\",\"token\":97399723,\"args\":{\"root\":{\"path\":\"/home/gemini/rbq/idec/iDECenter/template/template_stm32f1\",\"shared\":false,\"shared_comment\":\"false is default\"},\"target\":\"/home/gemini/rbq/idec/iDECenter/workspace/gemini/fafafa/\"}}|");
                tw.Flush();

                byte[] buffer = new byte[4096];
                int len = ns.Read(buffer, 0, 4096);

                Console.WriteLine(Encoding.UTF8.GetString(buffer, 0, len));

                tw.Write(new JsonObject(
                                new[] {
                                    new JsonObjectKeyValuePair("module", "$any"),
                                    new JsonObjectKeyValuePair("command", "$terminator"),
                                    new JsonObjectKeyValuePair("token", 1444),
                                    new JsonObjectKeyValuePair("args", new JsonObject())
                                }
                            ).ToStringForNetwork() + "|");
                tw.Flush();

                ns.Close();
                client.Close();
            }
        }
    }
}
