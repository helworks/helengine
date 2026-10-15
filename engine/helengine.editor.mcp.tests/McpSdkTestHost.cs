using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace helengine.editor.mcp.tests;

/// <summary>
/// Starts the SDK's stdio server when this test assembly is launched as a child process.
/// </summary>
public static class McpSdkTestHost
{
    /// <summary>
    /// Runs only for the stdio child-process mode used by the SDK compatibility test.
    /// </summary>
    /// <param name="Arguments">The command-line arguments supplied to the process.</param>
    /// <returns>Zero after the stdio host shuts down.</returns>
    public static async Task<int> Main(string[] Arguments)
    {
        if (Arguments.Length != 1 || Arguments[0] != "--stdio-server")
        {
            return 2;
        }

        Console.Error.WriteLine($"STDIO_RUNTIME={Environment.Version}");
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(Array.Empty<string>());
        builder.Logging.ClearProviders();
        builder.Services.AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<McpTestTools>();

        await builder.Build().RunAsync();
        return 0;
    }
}
