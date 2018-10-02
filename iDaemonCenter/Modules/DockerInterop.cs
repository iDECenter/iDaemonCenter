using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Linq;

using GeminiLab.Core2.Collections;
using GeminiLab.Core2.ML.Json;

namespace iDaemonCenter.Modules {
    class DockerInterop : DaemonModule {
        public const string ModuleName = "dockerop";

        private ProcessStartInfo getStartInfo(string args) {
            return new ProcessStartInfo("docker", args) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        }

        private bool dockerWrapper(string command, string args, out string rv) {
            Console.Error.WriteLine($"{command} {args}");
            var p = new Process { StartInfo = getStartInfo($"{command} {args}") };

            p.Start();
            p.WaitForExit();

            if (p.ExitCode != 0) {
                rv = p.StandardError.ReadToEnd();
                return false;
            }

            rv = p.StandardOutput.ReadLine()?.Trim() ?? "";
            return true;
        }

        private void dockerContainerOpWrapper(string command, string args, int token) {
            SendMessage(InterProcessMessage.GetResultMessage(ModuleName, token, new JsonObject(
                dockerWrapper(command, args, out var rv)
                    ? new[] { new JsonObjectKeyValuePair(CidKey, rv) }
                    : new[] { new JsonObjectKeyValuePair(CidKey, new JsonNull()), new JsonObjectKeyValuePair(ErrorKey, rv) }
            )));
        }

        private const string ImageNameKey = "img";
        private const string PortMapKey = "portmap";
        private const string DirMapKey = "dirmap";
        private const string UlimitKey = "ulimit";
        private const string ExtraKey = "extra";
        private const string CidKey = "cid";
        private const string ErrorKey = "error";

        private const string HostKey = "host";
        private const string DockerKey = "docker";
        private const string ItemKey = "item";
        private const string ValueKey = "value";
        private const string ReadonlyKey = "readonly";

        private void dockerCreate(InterProcessMessage msg) {
            var args = msg.Args;

            var imageName = "$invalidimg";
            var portmap = new List<(int Host, int Docker)>();
            var dirmap = new List<(string Host, string Docker, bool Readonly)>();
            var ulimits = new List<(string Item, string Value)>();
            var extra = "";

            if (args.TryGetJsonString(ImageNameKey, out var image)) imageName = image;

            if (args.TryGetJsonArray(PortMapKey, out var portarray))
                foreach (var it in portarray)
                    if (it is JsonObject o && o.TryReadInt(HostKey, out var host) && o.TryReadInt(DockerKey, out var docker))
                        portmap.Add((host, docker));

            if (args.TryGetJsonArray(DirMapKey, out var dirarray))
                foreach (var it in dirarray)
                    if (it is JsonObject o && o.TryGetJsonString(HostKey, out var host) && o.TryGetJsonString(DockerKey, out var docker))
                        dirmap.Add((host, docker, o.TryGetJsonBool(ReadonlyKey, out var ro) && ro.Value));
                        

            if (args.TryGetJsonArray(UlimitKey, out var ulimitarray))
                foreach (var it in ulimitarray)
                    if (it is JsonObject o && o.TryGetJsonString(ItemKey, out var item) && o.TryGetJsonString(ValueKey, out var value))
                        ulimits.Add((item, value));

            if (args.TryGetJsonString(ExtraKey, out var ext)) extra = ext;

            var portmapStr = portmap.Count > 0 ? $"{portmap.Select(map => $"-p {map.Host}:{map.Docker}").JoinBy(" ")}" : "";
            var dirmapStr = dirmap.Count > 0 ? $"{dirmap.Select(map => $"-v {map.Host}:{map.Docker}" + (map.Readonly ? ":ro" : "")).JoinBy(" ")}" : "";
            var ulimitsStr = ulimits.Count > 0 ? $"--ulimit {ulimits.Select(limit => $"{limit.Item}={limit.Value}").JoinBy(" ")}" : "";

            dockerContainerOpWrapper("create", $"{portmapStr} {dirmapStr} {ulimitsStr} {extra} {imageName}", msg.Token);
        }

        private void dockerStart(InterProcessMessage msg) {
            var args = msg.Args;

            var cid = "$invalidcid";
            if (args.TryGetJsonString(CidKey, out var cidStr)) cid = cidStr;

            dockerContainerOpWrapper("start", cid, msg.Token);
        }

        private void dockerKill(InterProcessMessage msg) {
            var args = msg.Args;

            var cid = "$invalidcid";
            if (args.TryGetJsonString(CidKey, out var cidStr)) cid = cidStr;

            dockerContainerOpWrapper("kill", cid, msg.Token);
        }

        private const string CidsKey = "cids";

        private void dockerKillMany(InterProcessMessage msg) {
            var args = msg.Args;

            var list = new List<string>();

            if (!args.TryGetJsonArray(CidsKey, out var cids)) {
                SendMessage(InterProcessMessage.GetResultMessage(ModuleName, msg.Token, new JsonObject(new[] { new JsonObjectKeyValuePair(ErrorKey, "invalid args") })));
                return;
            }

            foreach (var i in cids) {
                if (i is JsonString str) list.Add(str);
            }

            if (list.Count == 0) {
                SendMessage(InterProcessMessage.GetResultMessage(ModuleName, msg.Token, new JsonObject(new[] { new JsonObjectKeyValuePair(ErrorKey, new JsonArray()) })));
            } else {
                var p = new Process { StartInfo = getStartInfo($"kill {list.JoinBy(" ")}") };

                p.Start();
                p.WaitForExit();

                var rcids = p.StandardOutput
                    .ReadToEnd()
                    .Split('\n')
                    .Select(line => line.Trim())
                    .Where(line => line.Length == 64)
                    .Select(cid => new JsonString(cid));

                if (p.ExitCode == 0) {
                    SendMessage(InterProcessMessage.GetResultMessage(ModuleName, msg.Token, new JsonObject(new[] { new JsonObjectKeyValuePair(CidsKey, new JsonArray(rcids)) })));
                } else {
                    SendMessage(InterProcessMessage.GetResultMessage(ModuleName, msg.Token, new JsonObject(new[] { new JsonObjectKeyValuePair(CidsKey, new JsonArray(rcids)), new JsonObjectKeyValuePair(ErrorKey, p.StandardError.ReadToEnd()) })));
                }
            }
        }

        private void dockerPs(InterProcessMessage msg) {
            var p = new Process { StartInfo = getStartInfo("ps --no-trunc") };

            p.Start();
            p.WaitForExit();

            if (p.ExitCode == 0) {
                var cids = p.StandardOutput
                    .ReadToEnd()
                    .Split('\n')
                    .Skip(1)
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0)
                    .Select(line => line.Split(' ')[0])
                    .Where(line => line.Length == 64)
                    .Select(cid => new JsonString(cid));

                SendMessage(InterProcessMessage.GetResultMessage(ModuleName, msg.Token, new JsonObject(new[] { new JsonObjectKeyValuePair(CidKey, new JsonArray(cids)) })));
            } else {
                SendMessage(InterProcessMessage.GetResultMessage(ModuleName, msg.Token, new JsonObject(new[] { new JsonObjectKeyValuePair(CidKey, new JsonNull()), new JsonObjectKeyValuePair(ErrorKey, p.StandardError.ReadToEnd()) })));
            }
        }

        private void dockerPsall(InterProcessMessage msg) {
            var p = new Process { StartInfo = getStartInfo("ps -a --no-trunc") };

            p.Start();
            p.WaitForExit();

            if (p.ExitCode == 0) {
                var cids = p.StandardOutput
                    .ReadToEnd()
                    .Split('\n')
                    .Skip(1)
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0)
                    .Select(line => line.Split(' ')[0])
                    .Where(line => line.Length == 64)
                    .Select(cid => new JsonString(cid));

                SendMessage(InterProcessMessage.GetResultMessage(ModuleName, msg.Token, new JsonObject(new[] { new JsonObjectKeyValuePair("cid", new JsonArray(cids)) })));
            } else {
                SendMessage(InterProcessMessage.GetResultMessage(ModuleName, msg.Token, new JsonObject(new[] { new JsonObjectKeyValuePair("cid", new JsonNull()), new JsonObjectKeyValuePair("error", p.StandardError.ReadToEnd()) })));
            }
        }

        private Dictionary<string, Action<InterProcessMessage>> _handlers = null;

        protected override void MessageHandler(InterProcessMessage msg) {
            if (_handlers == null) _handlers = new Dictionary<string, Action<InterProcessMessage>>() {
                ["create"] = dockerCreate,
                ["start"] = dockerStart,
                ["kill"] = dockerKill,
                ["killmany"] = dockerKillMany,
                ["ps"] = dockerPs,
                ["psall"] = dockerPsall
            };

            if (_handlers.ContainsKey(msg.Command)) {
                _handlers[msg.Command](msg);
            } else {
                SendMessage(InterProcessMessage.CommandNotFoundMessage(ModuleName, msg.Token));
            }
        }
    }
}
