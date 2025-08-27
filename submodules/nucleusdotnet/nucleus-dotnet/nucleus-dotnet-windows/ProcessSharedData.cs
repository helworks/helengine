using Nucleus.Platform.Windows;
using Nucleus.Platform.Windows.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Nucleus.Platform.Windows.Interop.Kernel32Interop;
using static Nucleus.WindowScrape.HwndInterface;

namespace Nucleus.Platform.Windows {
        public class ProcessSharedData : IDisposable {
        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RELEASE = 0x8000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint PAGE_READWRITE = 4;

        private IntPtr hProc;
        private IntPtr lpData;
        private uint size;

        public IntPtr ProcessHandle {
            get { return hProc; }
        }

        private bool createdHProc;
        public ProcessSharedData(int processId, uint size) {
            this.size = size;
            createdHProc = true;
            hProc = Win32API.OpenProcess(Win32API.ProcessAccessFlags.All, false, processId);
            if (hProc == IntPtr.Zero) {
                throw new Exception("");
            }

            lpData = Kernel32Interop.VirtualAllocEx(hProc, IntPtr.Zero, size, MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE);
            if (lpData == IntPtr.Zero) {
                throw new Exception("");
            }
        }

        public ProcessSharedData(IntPtr processHandle, uint size) {
            this.size = size;
            hProc = processHandle;

            lpData = Kernel32Interop.VirtualAllocEx(hProc, IntPtr.Zero, size, MEM_RESERVE | MEM_COMMIT, PAGE_READWRITE);
            if (lpData == IntPtr.Zero) {
                throw new Exception("");
            }
        }

        public IntPtr GetData() {
            return lpData;
        }

        public void ReadData(IntPtr data) {
            uint nBytesRead = 0;
            bool bRet = Kernel32Interop.ReadProcessMemory(hProc, lpData, data, (int)size, ref nBytesRead);
            if (!bRet) {
                throw new Exception("");
            }
        }

        public void ReadData(IntPtr data, IntPtr subData, int newSize) {
            uint nBytesRead = 0;
            bool bRet = Kernel32Interop.ReadProcessMemory(hProc, subData, data, newSize, ref nBytesRead);
            if (!bRet) {
                throw new Exception("");
            }
        }

        public void Dispose() {
            if (createdHProc) {
                Win32API.CloseHandle(hProc);
            }
        }

        public void WriteData(IntPtr data) {
            uint nBytesRead = 0;
            bool bRet = Kernel32Interop.WriteProcessMemory(hProc, lpData, data, (int)size, ref nBytesRead);
            if (!bRet) {
                throw new Exception("");
            }
        }
    }
}
