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

        private bool dockerWrapper(string command, string args, out string rv) {
            var startInfo = new ProcessStartInfo("docker", $"{command} {args}") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            var p = new Process { StartInfo = startInfo };

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
                    ? new[] {new JsonObjectKeyValuePair("cid", rv)} 
                    : new[] {new JsonObjectKeyValuePair("cid", new JsonNull()), new JsonObjectKeyValuePair("error", rv)}
            )));
        }

        private const string ImageNameKey = "img";
        private const string PortMapKey = "portmap";
        private const string DirMapKey = "dirmap";
        private const string UlimitKey = "ulimit";
        private const string ExtraKey = "extra";
        private const string CidKey = "cid";

        private const string HostKey = "host";
        private const string DockerKey = "docker";
        private const string ItemKey = "item";
        private const string ValueKey = "value";

        private void dockerCreate(InterProcessMessage msg) {
            var args = msg.Args;

            var imageName = "$invalidimg";
            var portmap = new List<(int Host, int Docker)>();
            var dirmap = new List<(string Host, string Docker)>();
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
                        dirmap.Add((host, docker));

            if (args.TryGetJsonArray(UlimitKey, out var ulimitarray))
                foreach (var it in ulimitarray)
                    if (it is JsonObject o && o.TryGetJsonString(ItemKey, out var item) && o.TryGetJsonString(ValueKey, out var value))
                        ulimits.Add((item, value));

            if (args.TryGetJsonString(ExtraKey, out var ext)) extra = ext;

            var portmapStr = portmap.Count > 0 ? $"-p {portmap.Select(map => $"{map.Host}:{map.Docker}").JoinBy(" ")}" : "";
            var dirmapStr = dirmap.Count > 0 ? $"-v {dirmap.Select(map => $"{map.Host}:{map.Docker}").JoinBy(" ")}" : "";
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

        protected override void MessageHandler(InterProcessMessage msg) {
            switch (msg.Command) {
            case "create":
                dockerCreate(msg);
                break;
            }
        }
    }
}
