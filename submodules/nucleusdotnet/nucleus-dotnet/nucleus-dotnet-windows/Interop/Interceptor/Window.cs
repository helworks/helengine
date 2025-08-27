using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nucleus.Platform.Windows {
    /// <summary>
    /// contains collected window info
    /// </summary>
    public struct Window {
        public IntPtr hwnd;
        public string path;
        public string text;
        public string internal_text;
        public string class_name;
        public int level;
        public int parent_id;

        public override string ToString() {
            return $"{class_name} - {path} - {text}";
        }
    }
}
