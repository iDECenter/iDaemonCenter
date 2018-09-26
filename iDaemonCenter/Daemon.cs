using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using GeminiLab.Core2.ML.Json;
using iDaemonCenter.Modules;

namespace iDaemonCenter {
    public class Daemon {
        readonly IInterProcessCommunicator _ipc;

        public Daemon(IInterProcessCommunicator ipc) {
            _ipc = ipc;
        }
        
        public void Run() {
            _ipc.Start();

            AddModules();
            foreach (var m in _modules) {
                m.Value.SetDaemon(this);
                new Thread(m.Value.MessageLoop).Start();
            }
            
            while (true) {
                InterProcessMessage msg = _ipc.GetMessage();
                if (msg == null) { Console.Error.WriteLine("null received. exit."); break; }

                // Console.WriteLine($"msg is module {msg.Module}, command {msg.Command}, args {msg.Args.ToStringForNetwork()}");
                
                if (InterProcessMessage.IsTerminator(msg)) break;

                if (_modules.TryGetValue(msg.Module, out var module)) {
                    module.OnMessage(msg);
                }
            }

            foreach (var m in _modules) m.Value.OnMessage(InterProcessMessage.Terminator);

            _ipc.Close();
        }

        private readonly Dictionary<string, DaemonModule> _modules = new Dictionary<string, DaemonModule>();
        protected virtual void AddModule(string moduleName, DaemonModule module) {
            _modules[moduleName] = module;
        }

        protected virtual void AddModules() {
            AddModule(ProjectManager.ModuleName, new ProjectManager());
            AddModule(DockerInterop.ModuleName, new DockerInterop());
        }

        public void SendMessage(InterProcessMessage msg) {
            _ipc.SendMessage(msg);
        }
    }
}
