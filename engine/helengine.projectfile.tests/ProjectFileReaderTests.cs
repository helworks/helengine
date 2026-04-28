using helengine.projectfile;
using Xunit;

namespace helengine.projectfile.tests;

/// <summary>
/// Verifies canonical `.heproj` files are read with structured validation instead of ad hoc launcher or editor parsing.
/// </summary>
public sealed class ProjectFileReaderTests : IDisposable {
    readonly string TempDirectoryPath;

    /// <summary>
    /// Creates one isolated temporary directory used by the current test instance.
    /// </summary>
    public ProjectFileReaderTests() {
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
    /// Ensures valid canonical project files produce a structured success result.
    /// </summary>
    [Fact]
    public async Task ReadAsync_WhenProjectFileIsValid_ReturnsTheDocument() {
        string projectFilePath = Path.Combine(TempDirectoryPath, "sample.heproj");
        await File.WriteAllTextAsync(
            projectFilePath,
            """
            {
              "projectFormatVersion": 1,
              "name": "Sample Project",
              "version": "2.0.0",
              "requiredEngineVersion": "0.4.0",
              "supportedPlatforms": [ "windows", "future-console" ],
              "created": "2026-04-01T00:00:00Z",
              "lastOpened": "2026-04-20T00:00:00Z",
              "description": "Shared project file"
            }
            """);

        ProjectFileReader reader = new ProjectFileReader();

        ProjectFileReadResult result = await reader.ReadAsync(projectFilePath);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Document);
        Assert.Equal("Sample Project", result.Document.Name);
        Assert.Equal("0.4.0", result.Document.RequiredEngineVersion);
        Assert.Equal("future-console", result.Document.SupportedPlatforms[1]);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    /// Ensures invalid JSON reports one structured invalid-json error.
    /// </summary>
    [Fact]
    public async Task ReadAsync_WhenJsonIsInvalid_ReturnsInvalidJsonError() {
        string projectFilePath = Path.Combine(TempDirectoryPath, "invalid.heproj");
        await File.WriteAllTextAsync(projectFilePath, "{");

        ProjectFileReader reader = new ProjectFileReader();

        ProjectFileReadResult result = await reader.ReadAsync(projectFilePath);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == ProjectFileReadErrorCode.InvalidJson);
    }

    /// <summary>
    /// Ensures required canonical fields cannot be omitted silently.
    /// </summary>
    [Fact]
    public async Task ReadAsync_WhenRequiredFieldIsMissing_ReturnsMissingRequiredFieldError() {
        string projectFilePath = Path.Combine(TempDirectoryPath, "missing-name.heproj");
        await File.WriteAllTextAsync(
            projectFilePath,
            """
            {
              "projectFormatVersion": 1,
              "version": "2.0.0",
              "requiredEngineVersion": "0.4.0",
              "supportedPlatforms": [ "windows" ],
              "created": "2026-04-01T00:00:00Z",
              "lastOpened": "2026-04-20T00:00:00Z"
            }
            """);

        ProjectFileReader reader = new ProjectFileReader();

        ProjectFileReadResult result = await reader.ReadAsync(projectFilePath);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == ProjectFileReadErrorCode.MissingRequiredField && error.FieldName == "name");
    }

    /// <summary>
    /// Ensures newer unsupported format versions fail explicitly instead of being guessed at runtime.
    /// </summary>
    [Fact]
    public async Task ReadAsync_WhenFormatVersionIsUnsupported_ReturnsUnsupportedFormatVersionError() {
        string projectFilePath = Path.Combine(TempDirectoryPath, "future.heproj");
        await File.WriteAllTextAsync(
            projectFilePath,
            """
            {
              "projectFormatVersion": 2,
              "name": "Future Project",
              "version": "2.0.0",
              "requiredEngineVersion": "0.4.0",
              "supportedPlatforms": [ "windows" ],
              "created": "2026-04-01T00:00:00Z",
              "lastOpened": "2026-04-20T00:00:00Z"
            }
            """);

        ProjectFileReader reader = new ProjectFileReader();

        ProjectFileReadResult result = await reader.ReadAsync(projectFilePath);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == ProjectFileReadErrorCode.UnsupportedFormatVersion);
    }

    /// <summary>
    /// Ensures the shared reader can be synchronously awaited from one thread with a non-pumping synchronization context without deadlocking.
    /// </summary>
    [Fact]
    public async Task ReadAsync_WhenSynchronouslyWaitedOnThreadWithSynchronizationContext_CompletesWithoutCapturingSynchronizationContext() {
        string projectFilePath = Path.Combine(TempDirectoryPath, "sync-read.heproj");
        await File.WriteAllTextAsync(
            projectFilePath,
            """
            {
              "projectFormatVersion": 1,
              "name": "Sample Project",
              "version": "2.0.0",
              "requiredEngineVersion": "0.4.0",
              "supportedPlatforms": [ "windows" ],
              "created": "2026-04-01T00:00:00Z",
              "lastOpened": "2026-04-20T00:00:00Z"
            }
            """);

        ProjectFileReader reader = new ProjectFileReader();
        ProjectFileReadResult readResult = null;
        Exception capturedException = null;
        Thread thread = new Thread(() => {
            SynchronizationContext.SetSynchronizationContext(new BlockingSynchronizationContext());

            try {
                readResult = reader.ReadAsync(projectFilePath).GetAwaiter().GetResult();
            } catch (Exception exception) {
                capturedException = exception;
            }
        }) {
            IsBackground = true
        };
        thread.Start();

        bool completed = thread.Join(TimeSpan.FromSeconds(5));

        Assert.True(completed, "ProjectFileReader.ReadAsync deadlocked when synchronously waited on a thread with a synchronization context.");
        Assert.Null(capturedException);
        Assert.NotNull(readResult);
        Assert.True(readResult.Succeeded);
    }
}
