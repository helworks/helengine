using System.ComponentModel;
using ModelContextProtocol.Server;

namespace helengine.editor.mcp.tests;

/// <summary>
/// Exposes the one deterministic tool used to prove SDK transport compatibility.
/// </summary>
[McpServerToolType]
public sealed class McpTestTools
{
    /// <summary>
    /// Returns the fixed response used to verify a complete tool call.
    /// </summary>
    /// <returns>The text value <c>pong</c>.</returns>
    [McpServerTool(Name = "ping")]
    [Description("Returns a fixed pong response for SDK transport compatibility tests.")]
    public string Ping()
    {
        return "pong";
    }
}
