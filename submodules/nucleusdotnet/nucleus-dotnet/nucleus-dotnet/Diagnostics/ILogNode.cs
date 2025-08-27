using System.IO;

namespace Nucleus.Diagnostics {
    /// <summary>
    /// Interface for calling Log events on critical failure
    /// </summary>
    public interface ILogNode {
        void OnFailureLog(StreamWriter writer);
    }
}
