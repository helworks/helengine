using helengine.projectfile;
using Xunit;

namespace helengine.projectfile.tests;

/// <summary>
/// Verifies canonical `.heproj` files are written in one stable JSON shape shared by the launcher and editor.
/// </summary>
public sealed class ProjectFileWriterTests : IDisposable {
    readonly string TempDirectoryPath;

    /// <summary>
    /// Creates one isolated temporary directory used by the current test instance.
    /// </summary>
    public ProjectFileWriterTests() {
        TempDirectoryPath = Path.Combine(Path.GetTempPath(), "helengine-projectfile-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempDirectoryPath);
    }

    /// <summary>
    /// Deletes the temporary project-file test directory after the current test completes.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempDirectoryPath)) {
            Directory.Delete(TempDirectoryPath, true);
        }
    }

    /// <summary>
    /// Ensures canonical project files are written with the expected camelCase property names.
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenDocumentIsValid_WritesCanonicalCamelCaseShape() {
        string projectFilePath = Path.Combine(TempDirectoryPath, "sample.heproj");
        ProjectFileDocument document = CreateDocument();
        ProjectFileWriter writer = new ProjectFileWriter();

        await writer.WriteAsync(projectFilePath, document);

        string json = await File.ReadAllTextAsync(projectFilePath);

        Assert.Contains("\"projectFormatVersion\": 1", json);
        Assert.Contains("\"requiredEngineVersion\": \"0.4.0\"", json);
        Assert.Contains("\"supportedPlatforms\": [", json);
    }

    /// <summary>
    /// Ensures documents written by the shared writer can be read back by the shared reader without losing metadata.
    /// </summary>
    [Fact]
    public async Task WriteAsync_WhenDocumentIsWritten_RoundTripsThroughReader() {
        string projectFilePath = Path.Combine(TempDirectoryPath, "sample.heproj");
        ProjectFileDocument document = CreateDocument();
        ProjectFileWriter writer = new ProjectFileWriter();
        ProjectFileReader reader = new ProjectFileReader();

        await writer.WriteAsync(projectFilePath, document);

        ProjectFileReadResult result = await reader.ReadAsync(projectFilePath);

        Assert.True(result.Succeeded);
        Assert.Equal("Sample Project", result.Document.Name);
        Assert.Equal("future-console", result.Document.SupportedPlatforms[1]);
    }

    /// <summary>
    /// Ensures the shared writer can be synchronously awaited from one thread with a non-pumping synchronization context without deadlocking.
    /// </summary>
    [Fact]
    public void WriteAsync_WhenSynchronouslyWaitedOnThreadWithSynchronizationContext_CompletesWithoutCapturingSynchronizationContext() {
        string projectFilePath = Path.Combine(TempDirectoryPath, "sync-write.heproj");
        ProjectFileWriter writer = new ProjectFileWriter();
        ProjectFileDocument document = CreateDocument();
        Exception capturedException = null;
        Thread thread = new Thread(() => {
            SynchronizationContext.SetSynchronizationContext(new BlockingSynchronizationContext());

            try {
                writer.WriteAsync(projectFilePath, document).GetAwaiter().GetResult();
            } catch (Exception exception) {
                capturedException = exception;
            }
        }) {
            IsBackground = true
        };
        thread.Start();

        bool completed = thread.Join(TimeSpan.FromSeconds(5));

        Assert.True(completed, "ProjectFileWriter.WriteAsync deadlocked when synchronously waited on a thread with a synchronization context.");
        Assert.Null(capturedException);
        Assert.True(File.Exists(projectFilePath));
    }

    /// <summary>
    /// Creates one canonical test document shared by the writer tests.
    /// </summary>
    /// <returns>Canonical project document populated with representative launcher and editor metadata.</returns>
    static ProjectFileDocument CreateDocument() {
        return new ProjectFileDocument {
            Name = "Sample Project",
            Version = "2.0.0",
            RequiredEngineVersion = "0.4.0",
            SupportedPlatforms = new List<string> { "windows", "future-console" },
            Created = DateTime.Parse("2026-04-01T00:00:00Z").ToUniversalTime(),
            LastOpened = DateTime.Parse("2026-04-20T00:00:00Z").ToUniversalTime(),
            Description = "Shared project file"
        };
    }
}
