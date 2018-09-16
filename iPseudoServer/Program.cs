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
            Console.WriteLine("Server start");

            var listener = new TcpListener(IPAddress.Loopback, 4396);
            listener.Start();

            while (true) {
                var client = listener.AcceptTcpClient();

                Console.WriteLine($"Accept from {client.Client.RemoteEndPoint}");

                var ns = client.GetStream();
                var tw = new StreamWriter(ns, new UTF8Encoding(false));
                var tr = new StreamReader(ns, new UTF8Encoding(false));

                tw.Write(new JsonObject(
                                new[] {
                                    new JsonObjectKeyValuePair("module", "dockerop"),
                                    new JsonObjectKeyValuePair("command", "create"),
                                    new JsonObjectKeyValuePair("token", 1312),
                                    new JsonObjectKeyValuePair("args", new JsonObject(
                                        new [] {
                                            new JsonObjectKeyValuePair("portmap", new JsonArray(
                                                new [] {
                                                    new JsonObject(
                                                        new[] {
                                                            new JsonObjectKeyValuePair("host", 1589),
                                                            new JsonObjectKeyValuePair("docker", 8080)
                                                        }
                                                    )
                                                }
                                            )),
                                            new JsonObjectKeyValuePair("dirmap", new JsonArray(
                                                new [] {
                                                    new JsonObject(
                                                        new[] {
                                                            new JsonObjectKeyValuePair("host", "/home/gemini/rbq"),
                                                            new JsonObjectKeyValuePair("docker", "/workspace")
                                                        }
                                                    )
                                                }
                                            )),
                                            new JsonObjectKeyValuePair("img", "idec/idec")
                                        }
                                    ))
                                }
                            ).ToStringForNetwork() + "$");
                tw.Flush();

                while (!ns.DataAvailable);

                ns.Close();
                client.Close();
            }
        }
    }
}
