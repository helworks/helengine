using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.AspNetCore;

namespace helengine.editor.mcp.tests;

/// <summary>
/// Runs a real loopback Kestrel host for the SDK's Streamable HTTP client test.
/// </summary>
public sealed class McpTestHttpServer : IAsyncDisposable
{
    /// <summary>
    /// Creates a server wrapper for its running ASP.NET application and endpoint.
    /// </summary>
    /// <param name="Application">The running ASP.NET Core application.</param>
    /// <param name="Endpoint">The assigned Streamable HTTP endpoint.</param>
    McpTestHttpServer(WebApplication Application, Uri Endpoint)
    {
        this.Application = Application;
        this.Endpoint = Endpoint;
    }

    /// <summary>
    /// Gets the assigned MCP route on the loopback listener.
    /// </summary>
    public Uri Endpoint { get; }

    /// <summary>
    /// Holds the running application until the test disposes this wrapper.
    /// </summary>
    readonly WebApplication Application;

    /// <summary>
    /// Starts Kestrel on an ephemeral loopback port and maps the stateless MCP route.
    /// </summary>
    /// <returns>A running HTTP test server with its actual bound endpoint.</returns>
    public static async Task<McpTestHttpServer> StartAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = Array.Empty<string>()
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddMcpServer()
            .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
            .WithTools<McpTestTools>();

        WebApplication application = builder.Build();
        application.MapMcp("/mcp");
        await application.StartAsync();

        try
        {
            IServerAddressesFeature addresses = application.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>();
            string address = addresses.Addresses.Single();
            Uri endpoint = new(new Uri(address), "/mcp");
            return new McpTestHttpServer(application, endpoint);
        }
        catch
        {
            await application.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Stops Kestrel and releases the server's resources.
    /// </summary>
    /// <returns>A task that completes after application disposal.</returns>
    public ValueTask DisposeAsync()
    {
        return Application.DisposeAsync();
    }
}
