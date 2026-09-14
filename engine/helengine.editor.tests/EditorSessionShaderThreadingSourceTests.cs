namespace helengine.editor.tests;

/// <summary>
/// Verifies shader hot-reload notifications stay on the editor frame thread before mutating DirectX runtime shader resources.
/// </summary>
public sealed class EditorSessionShaderThreadingSourceTests {
    /// <summary>
    /// Ensures background shader-build callbacks only enqueue pending invalidations and the editor frame loop drains those invalidations before drawing.
    /// </summary>
    [Fact]
    public void Editor_session_shader_reload_source_queues_renderer_invalidations_to_the_frame_thread() {
        string sessionSource = File.ReadAllText(ResolveCurrentWorktreeSource("helengine.editor", "EditorSession.cs"));
        string queueSource = File.ReadAllText(ResolveCurrentWorktreeSource("helengine.editor", Path.Combine("managers", "rendering", "EditorShaderBuildNotificationQueue.cs")));

        // The background watcher callback may only hand the notification to the queue, and the
        // editor frame loop must be what drains it.
        Assert.Contains("ShaderBuildNotificationQueue.Enqueue(shaderName, packagePath);", sessionSource, StringComparison.Ordinal);
        Assert.Contains("ProcessPendingShaderBuildNotifications();", sessionSource, StringComparison.Ordinal);
        Assert.Contains("ShaderBuildNotificationQueue.ProcessPending();", sessionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidateShaderResources", sessionSource, StringComparison.Ordinal);

        // Renderer shader state is only ever invalidated from the drain path, under the queue lock
        // for dequeue and off the watcher thread.
        Assert.Contains("PendingNotifications.Enqueue(new KeyValuePair<string, string>(resolvedShaderName, packagePath));", queueSource, StringComparison.Ordinal);
        Assert.Contains("public void ProcessPending() {", queueSource, StringComparison.Ordinal);
        Assert.Contains("Render3D.InvalidateShaderResources(shaderAssetId, shaderAsset);", queueSource, StringComparison.Ordinal);
    }

    static string ResolveCurrentWorktreeSource(string projectDirectoryName, string fileName) {
        DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null) {
            string candidate = Path.Combine(current.FullName, projectDirectoryName, fileName);
            if (File.Exists(candidate)) {
                return candidate;
            }
            current = current.Parent;
        }

        throw new FileNotFoundException($"Could not locate '{projectDirectoryName}/{fileName}' from the current test worktree.");
    }
}
