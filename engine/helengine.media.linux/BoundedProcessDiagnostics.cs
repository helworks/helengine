using System.Text;
namespace helengine.media.linux;
/// <summary>Continuously drains a child stream while retaining only a bounded diagnostic suffix.</summary>
sealed class BoundedProcessDiagnostics {
    /// <summary>Maximum retained diagnostic characters per redirected stream.</summary>
    const int Capacity=16384;
    /// <summary>Bounded suffix protected from concurrent readers.</summary>
    readonly StringBuilder Text=new();
    /// <summary>Serializes reads of the bounded diagnostic suffix.</summary>
    readonly object Gate=new();
    /// <summary>Consumes all child output so diagnostics cannot block an encoder.</summary>
    public async Task DrainAsync(StreamReader reader) {
        char[] buffer=new char[1024];int count;
        while((count=await reader.ReadAsync(buffer))>0) {
            lock(Gate) {Text.Append(buffer,0,count);if(Text.Length>Capacity) {Text.Remove(0,Text.Length-Capacity);}}
        }
    }
    /// <summary>Returns the current bounded suffix for exception reporting.</summary>
    public override string ToString() {lock(Gate) {return Text.ToString();}}
}
