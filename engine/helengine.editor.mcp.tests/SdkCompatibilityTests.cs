using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Collections.Concurrent;
using Xunit.Abstractions;

namespace helengine.editor.mcp.tests;

/// <summary>
/// Proves SDK 2.2.0 can exchange tool calls over both required transports on .NET 9.
/// </summary>
public sealed class SdkCompatibilityTests
{
    /// <summary>
    /// Captures negotiated SDK and protocol versions for the test report.
    /// </summary>
    /// <param name="Output">The test output writer.</param>
    public SdkCompatibilityTests(ITestOutputHelper Output)
    {
        this.Output = Output;
    }

    /// <summary>
    /// Holds the xUnit output writer for protocol and runtime version evidence.
    /// </summary>
    readonly ITestOutputHelper Output;

    /// <summary>Collects only the child server's stderr callback, leaving stdout on the MCP protocol pipe.</summary>
    readonly ConcurrentQueue<string> StandardErrorLines = new();

    /// <summary>
    /// Exchanges a real ping tool call over stateless HTTP and child-process stdio.
    /// </summary>
    /// <returns>A task that completes when both transports return the expected response.</returns>
    [Fact]
    public async Task Sdk_can_exchange_tool_calls_over_stdio_and_http_on_net9()
    {
        Assert.Equal(9, Environment.Version.Major);

        await using McpTestHttpServer httpServer = await McpTestHttpServer.StartAsync();
        HttpClientTransport httpTransport = new(new HttpClientTransportOptions
        {
            Endpoint = httpServer.Endpoint,
            TransportMode = HttpTransportMode.StreamableHttp,
            ConnectionTimeout = TimeSpan.FromSeconds(10)
        });
        await using McpClient httpClient = await McpClient.CreateAsync(httpTransport);
        string httpProtocolVersion = httpClient.NegotiatedProtocolVersion;
        CallToolResult httpToolResult = await httpClient.CallToolAsync("ping");
        string httpResult = Assert.IsType<TextContentBlock>(Assert.Single(httpToolResult.Content)).Text;
        Assert.Equal("pong", httpResult);
        Output.WriteLine($"HTTP: ModelContextProtocol.AspNetCore 2.2.0; protocol {httpProtocolVersion}; runtime {Environment.Version}.");

        StdioClientTransport stdioTransport = new(new StdioClientTransportOptions
        {
            Command = "dotnet",
            Arguments = new[] { typeof(McpSdkTestHost).Assembly.Location, "--stdio-server" },
            WorkingDirectory = AppContext.BaseDirectory,
            ShutdownTimeout = TimeSpan.FromSeconds(10),
            StandardErrorLines = line => this.StandardErrorLines.Enqueue(line)
        });
        await using McpClient stdioClient = await McpClient.CreateAsync(stdioTransport);
        string stdioProtocolVersion = stdioClient.NegotiatedProtocolVersion;
        CallToolResult stdioToolResult = await stdioClient.CallToolAsync("ping");
        string stdioResult = Assert.IsType<TextContentBlock>(Assert.Single(stdioToolResult.Content)).Text;
        Assert.Equal("pong", stdioResult);

        string runtimeDiagnostic = this.StandardErrorLines.FirstOrDefault(line => line.StartsWith("STDIO_RUNTIME=", StringComparison.Ordinal));
        DateTimeOffset diagnosticDeadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (runtimeDiagnostic == null && DateTimeOffset.UtcNow < diagnosticDeadline)
        {
            await Task.Delay(20);
            runtimeDiagnostic = this.StandardErrorLines.FirstOrDefault(line => line.StartsWith("STDIO_RUNTIME=", StringComparison.Ordinal));
        }

        Assert.NotNull(runtimeDiagnostic);
        Assert.StartsWith("STDIO_RUNTIME=9.", runtimeDiagnostic);
        Output.WriteLine($"stdio: ModelContextProtocol 2.2.0; protocol {stdioProtocolVersion}; host diagnostic {runtimeDiagnostic} captured through stderr. The successful ping confirms stdout remained valid MCP JSON-RPC.");
    }
}
