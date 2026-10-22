using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace helengine.editor.tests.managers.asset;

/// <summary>Verifies the SDK-validated Darwin inode64 dirent ABI independently of the current test host.</summary>
public sealed class EditorAuthoringDarwinDirectoryEntryTests {
    /// <summary>Darwin uses its explicit byte-length field and name offset rather than the Linux d_type offset.</summary>
    /// <param name="name">Valid UTF-8 leaf or mandatory dot entry.</param>
    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("ação.txt")]
    public void ValidDarwinRecordReturnsItsName(string name) {
        byte[] bytes = Encoding.UTF8.GetBytes(name);
        IntPtr entry = Marshal.AllocHGlobal(1048);
        try {
            Marshal.Copy(new byte[1048], 0, entry, 1048);
            Marshal.WriteInt16(entry, 16, checked((short)(22 + bytes.Length)));
            Marshal.WriteInt16(entry, 18, checked((short)bytes.Length));
            Marshal.WriteByte(entry, 20, 8);
            Marshal.Copy(bytes, 0, IntPtr.Add(entry, 21), bytes.Length);
            MethodInfo parser = typeof(EditorAuthoringMutationScope).GetMethod("ReadDarwinDirectoryEntryName", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(parser);
            Assert.Equal(name, parser.Invoke(null, [entry]));
        } finally {
            Marshal.FreeHGlobal(entry);
        }
    }
}
