using System.Runtime.InteropServices;

namespace helengine.editor {
    public static class FolderDialog {
        public static string OpenFolderDialog() {
            var dialog = (IFileOpenDialog)new FileOpenDialog();
            dialog.SetOptions(FOS.FOS_PICKFOLDERS | FOS.FOS_FORCEFILESYSTEM);

            try {
                if (dialog.Show(IntPtr.Zero) == 0) {
                    dialog.GetResult(out IShellItem item);
                    item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out IntPtr ptr);
                    string path = Marshal.PtrToStringAuto(ptr);
                    Marshal.FreeCoTaskMem(ptr);
                    return path;
                }
            } catch { }
            return null;
        }
    }

    [ComImport]
    [Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
    [ClassInterface(ClassInterfaceType.None)]
    internal class FileOpenDialog { }

    [ComImport]
    [Guid("D57C7288-D4AD-4768-BE02-9D969532D960")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IFileOpenDialog {
        int Show(IntPtr parent);
        void SetFileTypes();
        void SetFileTypeIndex();
        void GetFileTypeIndex();
        void Advise();
        void Unadvise();
        void SetOptions(FOS fos);
        void GetOptions();
        void SetDefaultFolder();
        void SetFolder();
        void GetFolder();
        void GetCurrentSelection();
        void SetFileName();
        void GetFileName();
        void SetTitle();
        void SetOkButtonLabel();
        void SetFileNameLabel();
        void GetResult(out IShellItem ppsi);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItem {
        void BindToHandler();
        void GetParent();
        void GetDisplayName(SIGDN sigdnName, out IntPtr ppszName);
    }

    internal enum SIGDN : uint {
        SIGDN_FILESYSPATH = 0x80058000
    }

    [Flags]
    internal enum FOS : uint {
        FOS_PICKFOLDERS = 0x00000020,
        FOS_FORCEFILESYSTEM = 0x00000040
    }
}
