using System;
using System.IO;

using GeminiLab.Core2.ML.Json;

// things for this daemon :
//   projects file manage
//   storage monitor
//   docker interop
//   and any other thing

// usage:
//   iDaemonCenter config file path

namespace iDaemonCenter {
    internal class Program {
        public static int Main(string[] args) {
            /*
            if (args.Length != 1) {
                Console.WriteLine("Invalid args.");
                return 1;
            }

            string configFile = args[0];
            int port = 0;
            try {
                var fs = new FileStream(configFile, FileMode.Open);
                var sr = new StreamReader(fs).ReadToEnd();
                var config = JsonParser.Parse(sr) as JsonObject ?? throw new FormatException();

                if (!config.TryGetValue("daemonport", out var value)) throw new FormatException();

                if (value is JsonNumber num && !num.IsFloat) {
                    port = num.ValueInt;
                } else if (value is JsonString str && int.TryParse(str, out var numInStr)) {
                    port = numInStr;
                } else {
                    throw new FormatException();
                }

                if (port <= 0 || port >= 65535) throw new FormatException();

                fs.Close();
            } catch (Exception ex) {
                Console.WriteLine("Cannot open configuration file.");
                Console.WriteLine($"{ex.GetType().Name}: {ex.Message}");
            }
            */

            // int port = 3154;

            var ipc = new StdioCommunicator(); // new TcpCommunicator(port);
            var daemon = new Daemon(ipc);

            daemon.Run();

            return 0;
        }
    }
}
