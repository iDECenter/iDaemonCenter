using System;
using System.Collections.Generic;
using System.Text;

namespace iDaemonCenter.Modules {
    class ProjectManager : DaemonModule {
        public const string ModuleName = "projmgr";

        protected override void MessageHandler(InterProcessMessage msg) {
            if (msg.Command == "instantiate") {

            }
        }
    }
}
