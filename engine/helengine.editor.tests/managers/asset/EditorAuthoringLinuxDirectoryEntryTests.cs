using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace helengine.editor.tests.managers.asset;

/// <summary>Verifies Linux dirent parsing before descriptor-relative recovery filters the mandatory dot entries.</summary>
public sealed class EditorAuthoringLinuxDirectoryEntryTests {
    /// <summary>Dot directory records must reach the enumeration loop so that it can skip them without aborting recovery.</summary>
    /// <param name="name">Valid UTF-8 record name, including filesystem navigation entries.</param>
    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("ação.txt")]
    public void ValidRecordReturnsItsName(string name) {
        byte[] bytes = Encoding.UTF8.GetBytes(name);
        IntPtr entry = Marshal.AllocHGlobal(512);
        try {
            Marshal.Copy(new byte[512], 0, entry, 512);
            Marshal.WriteInt16(entry, 16, checked((short)(20 + bytes.Length)));
            Marshal.Copy(bytes, 0, IntPtr.Add(entry, 19), bytes.Length);
            MethodInfo parser = typeof(EditorAuthoringMutationScope).GetMethod("ReadLinuxDirectoryEntryName", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.Equal(name, parser.Invoke(null, [entry]));
        } finally {
            Marshal.FreeHGlobal(entry);
        }
    }

    /// <summary>A name cannot escape its native record to borrow a terminator from a subsequent dirent.</summary>
    [Fact]
    public void MissingTerminatorWithinRecordIsRejected() {
        IntPtr entry = Marshal.AllocHGlobal(512);
        try {
            Marshal.Copy(new byte[512], 0, entry, 512);
            Marshal.WriteInt16(entry, 16, 23);
            Marshal.Copy(Encoding.UTF8.GetBytes("name"), 0, IntPtr.Add(entry, 19), 4);
            MethodInfo parser = typeof(EditorAuthoringMutationScope).GetMethod("ReadLinuxDirectoryEntryName", BindingFlags.Static | BindingFlags.NonPublic);
            TargetInvocationException failure = Assert.Throws<TargetInvocationException>(() => parser.Invoke(null, [entry]));
            Assert.IsType<InvalidDataException>(failure.InnerException);
        } finally {
            Marshal.FreeHGlobal(entry);
        }
    }
}
