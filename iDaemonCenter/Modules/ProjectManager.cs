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

            if (!targetDirectory.Exists) {
                targetDirectory.Create();
                Chmod.chmod(targetDirectory.FullName, Chmod.P_755);
            }

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
                    Chmod.chmod(Path.Combine(targetDirectory.FullName, i.Name), Chmod.P_755);
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

        private const string IsFileKey = "@isfile";
        private const string ContentKey = "@content";
        private const string PathSpecifiedKey = "@path";
        private const string ReturnThisKey = "@returnthis";
        private const string OverwriteKey = "@overwrite";

        private void writeToPath(FileInfo info, string content) {
            var fs = info.OpenWrite();
            var writer = new StreamWriter(fs, new UTF8Encoding(false));

            writer.Write(content);
            writer.Close();
            fs.Close();
        }

        // encoding is utf-8 no bom
        // wo yi jing qin ding le
        private string ensuredirNode(string path, JsonObject node) {
            bool isFile = false;
            string pathSpecified = null;
            string content = null;
            bool returnthis = false;
            bool overwrite = false;

            string rv = null;

            if (node.TryGetJsonBool(IsFileKey, out var isFileBool)) isFile = isFileBool;
            if (node.TryGetJsonBool(OverwriteKey, out var overwriteBool)) overwrite = overwriteBool;
            if (node.TryGetJsonString(ContentKey, out var contentStr)) content = contentStr;
            if (node.TryGetJsonBool(ReturnThisKey, out var returnThisBool)) returnthis = returnThisBool;
            if (node.TryGetJsonString(PathSpecifiedKey, out var pathSpecifiedStr)) pathSpecified = pathSpecifiedStr;

            if (pathSpecified != null) path = pathSpecified;
            if (isFile) {
                var info = new FileInfo(path);
                content = content ?? "";

                if (!info.Exists || overwrite) {
                    writeToPath(info, content);
                }

                Chmod.chmod(path, Chmod.P_755);
            } else {
                var info = new DirectoryInfo(path);
                if (!info.Exists) info.Create();

                Chmod.chmod(path, Chmod.P_755);

                foreach (var i in node.Values) {
                    if (((string)(i.Key))[0] != '@' && i.Value is JsonObject nextNode) {
                        rv = ensuredirNode(Path.Combine(path, i.Key), nextNode);
                    }
                }
            }

            if (returnthis) return path;
            return rv == null ? path : rv;
        }

        private void ensuredir(InterProcessMessage msg) {
            var args = msg.Args;

            string error = null;

            if (!args.TryGetJsonObject(RootKey, out var root)) {
                error = "invalid args";
                SendMessage(InterProcessMessage.GetResultMessage(ModuleName, msg.Token, new JsonObject(
                    new[] {
                        new JsonObjectKeyValuePair(PathKey, new JsonNull()),
                        new JsonObjectKeyValuePair(ErrorKey, error)
                    }
                )));

                return;
            }

            string rv = ensuredirNode(".", root);
            SendMessage(InterProcessMessage.GetResultMessage(ModuleName, msg.Token, new JsonObject(
                new[] {
                    new JsonObjectKeyValuePair(PathKey, rv)
                }
            )));
        }

        private void deleteDir(InterProcessMessage msg) {
            var args = msg.Args;

            string error = null;

            if (!args.TryGetJsonString(PathKey, out var path)) {
                error = "invalid args";
                SendMessage(InterProcessMessage.GetResultMessage(ModuleName, msg.Token, new JsonObject(
                    new[] {
                        new JsonObjectKeyValuePair(PathKey, new JsonNull()),
                        new JsonObjectKeyValuePair(ErrorKey, error)
                    }
                )));
            } else {
                try {
                    Directory.Delete(path, true);
                } catch (Exception ex) {
                    ; // if any file don't want to leave, let it go
                }
                SendMessage(InterProcessMessage.GetResultMessage(ModuleName, msg.Token, new JsonObject(
                    new[] {
                        new JsonObjectKeyValuePair(PathKey, path)
                    }
                )));
            }
        }

        private void moveDir(InterProcessMessage msg) {
            var args = msg.Args;

            string error = null;

            if (!args.TryGetJsonString(PathKey, out var path) || !args.TryGetJsonString(TargetKey, out var target)) {
                error = "invalid args";
                SendMessage(InterProcessMessage.GetResultMessage(ModuleName, msg.Token, new JsonObject(
                    new[] {
                        new JsonObjectKeyValuePair(PathKey, new JsonNull()),
                        new JsonObjectKeyValuePair(TargetKey, new JsonNull()),
                        new JsonObjectKeyValuePair(ErrorKey, error)
                    }
                )));
            } else {
                new DirectoryInfo(path).CopyTo(new DirectoryInfo(target));
                try {
                    Directory.Delete(path, true);
                } catch (Exception ex) {
                    ;
                }
                SendMessage(InterProcessMessage.GetResultMessage(ModuleName, msg.Token, new JsonObject(
                    new[] {
                        new JsonObjectKeyValuePair(PathKey, path),
                        new JsonObjectKeyValuePair(TargetKey, target)
                    }
                )));
            }
        }

        private Dictionary<string, Action<InterProcessMessage>> _handlers = null;

        protected override void MessageHandler(InterProcessMessage msg) {
            if (_handlers == null) _handlers = new Dictionary<string, Action<InterProcessMessage>>() {
                ["instantiate"] = instantiate,
                ["ensuredir"] = ensuredir,
                ["deletedir"] = deleteDir,
                ["movedir"] = moveDir
            };

            if (_handlers.ContainsKey(msg.Command)) {
                _handlers[msg.Command](msg);
            } else {
                SendMessage(InterProcessMessage.CommandNotFoundMessage(ModuleName, msg.Token));
            }
        }
    }
}
