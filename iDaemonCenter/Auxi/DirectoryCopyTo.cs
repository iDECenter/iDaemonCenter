using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace iDaemonCenter.Auxi {
    internal static class DirectoryCopyTo {
        public static void CopyTo(this DirectoryInfo source, DirectoryInfo target) {
            if (!target.Exists) { 
                target.Create();
                Chmod.chmod(target.FullName, Chmod.P_755);
            }

            foreach (var i in source.GetFiles()) {
                i.CopyTo(Path.Combine(target.FullName, i.Name), true);
                Chmod.chmod(Path.Combine(target.FullName, i.Name), Chmod.P_755);
            }
            
            foreach (var d in source.GetDirectories()) d.CopyTo(new DirectoryInfo(Path.Combine(target.FullName, d.Name)));
        }
    }
}
