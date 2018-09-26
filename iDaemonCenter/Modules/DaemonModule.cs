using System;
using System.Collections.Generic;
using System.Threading;

namespace iDaemonCenter.Modules {
    public abstract class DaemonModule {
        private const int MessageWaitingInterval = 50;

        protected Daemon Daemon;
        public virtual void SetDaemon(Daemon daemon) {
            Daemon = daemon;
        }

        private readonly Queue<InterProcessMessage> _msg = new Queue<InterProcessMessage>();
        public virtual void OnMessage(InterProcessMessage msg) {
            lock (_msg) _msg.Enqueue(msg);
         }

        public virtual void MessageLoop() {
            while (true) {
                InterProcessMessage msg;
                lock (_msg) {
                    msg = _msg.Count > 0 ? _msg.Dequeue() : null;
                }

                if (msg == null) {
                    Thread.Sleep(MessageWaitingInterval); continue;
                }

                if (InterProcessMessage.IsTerminator(msg)) break;

                try {
                    MessageHandler(msg);
                } catch (Exception ex) {
                    Console.Error.WriteLine(ex.GetType().FullName);
                    Console.Error.WriteLine(ex.Message);

                    throw new Exception();
                }
            }
        }

        protected virtual void MessageHandler(InterProcessMessage msg) {
            // do nothing here
        }

        protected virtual void SendMessage(InterProcessMessage msg) {
            Daemon?.SendMessage(msg);
        }
    }
}
