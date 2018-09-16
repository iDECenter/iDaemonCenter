using System;
using GeminiLab.Core2.ML.Json;

namespace iDaemonCenter {
    public class InterProcessMessage {
        public const string InvalidModuleName = "$invalid";
        public const string AnyModuleName = "$any";
        public const string TerminatorCommandName = "$terminator";
        public const string ResultCommandName = "$result";

        private const string ModuleKey = "module";
        private const string CommandKey = "command";
        private const string TokenKey = "token";
        private const string ArgsKey = "args";

        public readonly string Module;
        public readonly string Command;
        public readonly int Token;
        public readonly JsonObject Args;

        private InterProcessMessage(string module, string command, int token, JsonObject args) {
            Module = module;
            Command = command;
            Token = token;
            Args = args;
        }

        private InterProcessMessage() : this(InvalidModuleName, "", 0, null) { }
        
        public static InterProcessMessage Invalid { get; } = new InterProcessMessage();
        public static InterProcessMessage Terminator { get; } = new InterProcessMessage(AnyModuleName, TerminatorCommandName, 0, null);

        public static bool IsInvalid(InterProcessMessage msg) => msg.Module == InvalidModuleName;
        public static bool IsTerminator(InterProcessMessage msg) => msg.Module == AnyModuleName && msg.Command == TerminatorCommandName;

        public static InterProcessMessage GetResultMessage(string moduleName, int token, JsonObject args) {
            return new InterProcessMessage(moduleName, ResultCommandName, token, args);
        }

        public static InterProcessMessage Parse(string v) {
            try {
                if (!(JsonParser.Parse(v) is JsonObject obj)
                    || !obj.TryGetJsonString(ModuleKey, out var moduleStr)
                    || !obj.TryGetJsonString(CommandKey, out var commandStr)
                    || !obj.TryReadInt(TokenKey, out var tokenNum)
                    || !obj.TryGetJsonObject(ArgsKey, out var argsObj)) return Invalid;

                return new InterProcessMessage(moduleStr, commandStr, tokenNum, argsObj);
            } catch (JsonParsingException) {
                return Invalid;
            }
        }

        public string Serialize() {
            return new JsonObject(new[] {
                new JsonObjectKeyValuePair(ModuleKey, Module),
                new JsonObjectKeyValuePair(CommandKey, Command),
                new JsonObjectKeyValuePair(ArgsKey, Args)
            }).ToStringForNetwork();
        }
    }
}