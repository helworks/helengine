using System.IO;

namespace Nucleus.IO {
    /// <summary>
    /// A holder for a .NET DriveInfo and some Info
    /// </summary>
    public struct SearchStorageInfo {
        public DriveInfo Drive { get; private set; }
        public string Info { get; private set; }

        public SearchStorageInfo(DriveInfo drive) {
            Drive = drive;
            Info = "";
        }

        public void SetInfo(string info) {
            Info = info;
        }

        public override string ToString() {
            return Info;
        }
    }
}
