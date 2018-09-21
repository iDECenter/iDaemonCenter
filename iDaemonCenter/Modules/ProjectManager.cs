using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeminiLab.Core2.ML.Json;

using iDaemonCenter.Auxi;

namespace iDaemonCenter.Modules {
    class ProjectManager : DaemonModule {
        public const string ModuleName = "projmgr";

        private static readonly char DirSeparator = Path.DirectorySeparatorChar;

        private const string RootKey = "root";
        private const string SourceKey = "source";
        private const string TargetKey = "target";
        private const string DirMapKey = "dirmap";
        private const string ErrorKey = "error";

        private const string PathKey = "path";
        private const string SharedKey = "shared";
        private const string SubKey = "sub";
     
        private const string HostKey = "host";
        private const string DockerKey = "docker";
        private const string ReadonlyKey = "readonly";

        private List<(string Docker, string Host, bool Readonly)> instantiateNode(JsonObject node, DirectoryInfo targetDirectory, string relativePath) {
            var rv = new List<(string Docker, string Host, bool Readonly)>();

            bool shared;
            string path;
            bool ro;

            if (!node.TryGetJsonString(PathKey, out var pathv)) return rv; else path = pathv;
            if (node.TryGetJsonBool(SharedKey, out var sharedv)) shared = sharedv; else shared = false;
            if (node.TryGetJsonBool(ReadonlyKey, out var readonlyv)) ro = readonlyv; else ro = shared;
            if (!node.TryGetJsonObject(SubKey, out JsonObject sub)) sub = null;

            if (!targetDirectory.Exists) targetDirectory.Create();

            if (shared) {
                rv.Add((relativePath, path, ro));

                if (sub != null) {
                    foreach (var i in sub.Values) {
                        if (!(i.Value is JsonObject nextNode)) continue;
                        var name = i.Key;
                        var nextTargetDir = new DirectoryInfo(Path.Combine(targetDirectory.FullName, i.Key));
                        var nextRelPath = relativePath + DirSeparator + i.Key;

                        rv.AddRange(instantiateNode(nextNode, nextTargetDir, nextRelPath));
                    }
                }
            } else {
                if (relativePath == ".") rv.Add((relativePath, targetDirectory.FullName, ro));

                var pathInfo = new DirectoryInfo(path);
                foreach (var i in pathInfo.GetFiles()) {
                    i.CopyTo(Path.Combine(targetDirectory.FullName, i.Name), true);
                }

                foreach (var i in pathInfo.GetDirectories()) {
                    if (sub != null && sub.TryGetJsonObject(i.Name, out var nextNode)) {
                        rv.AddRange(instantiateNode(nextNode, new DirectoryInfo(Path.Combine(targetDirectory.FullName, i.Name)), relativePath + DirSeparator + i.Name));
                    } else {
                        i.CopyTo(new DirectoryInfo(Path.Combine(targetDirectory.FullName, i.Name)));
                    }
                }
            }

            return rv;
        }

        // all path here should be absolute!!!
        private void instantiate(InterProcessMessage msg) {
            var args = msg.Args;
            JsonValue dirmap = new JsonNull();
            JsonValue error = new JsonNull();

            if (!args.TryGetJsonObject(RootKey, out var root) || !args.TryGetJsonString(TargetKey, out var target)) {
                error = "invalid params";
            } else {
                dirmap = new JsonArray(instantiateNode(root, new DirectoryInfo(target), ".").Select(m => new JsonObject(new[] { new JsonObjectKeyValuePair(DockerKey, m.Docker), new JsonObjectKeyValuePair(HostKey, m.Host), new JsonObjectKeyValuePair(ReadonlyKey, m.Readonly) })));
            }

            SendMessage(InterProcessMessage.GetResultMessage(ModuleName, msg.Token, new JsonObject(
                dirmap is JsonNull
                ? new[] { new JsonObjectKeyValuePair(DirMapKey, dirmap), new JsonObjectKeyValuePair(ErrorKey, error) }
                : new[] { new JsonObjectKeyValuePair(DirMapKey, dirmap) }
            )));
        }

        private Dictionary<string, Action<InterProcessMessage>> _handlers = null;

        protected override void MessageHandler(InterProcessMessage msg) {
            if (_handlers == null) _handlers = new Dictionary<string, Action<InterProcessMessage>>() {
                ["instantiate"] = instantiate,
            };

            if (_handlers.ContainsKey(msg.Command)) {
                _handlers[msg.Command](msg);
            } else {
                SendMessage(InterProcessMessage.CommandNotFoundMessage(ModuleName, msg.Token));
            }
        }
    }
}
