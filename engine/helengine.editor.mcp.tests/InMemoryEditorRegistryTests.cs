using helengine.editor.automation;
using helengine.editor.mcp.coordinator;

namespace helengine.editor.mcp.tests;

/// <summary>
/// Verifies the in-memory coordinator registration and shutdown contract.
/// </summary>
public sealed class InMemoryEditorRegistryTests
{
    /// <summary>
    /// Confirms two runs of the same project remain separate registrations.
    /// </summary>
    [Fact]
    public void Same_project_instances_remain_distinct()
    {
        ManualTimeProvider Clock = new();
        InMemoryEditorRegistry Registry = CreateRegistry(Clock);
        EditorInstanceDescriptor First = CreateDescriptor("instance-a", StartedAt(1));
        EditorInstanceDescriptor Second = CreateDescriptor("instance-b", StartedAt(2));

        Assert.True(Registry.TryRegister(First));
        Assert.True(Registry.TryRegister(Second));

        IReadOnlyList<EditorInstanceDescriptor> Snapshot = Registry.GetSnapshot();
        Assert.Equal(2, Snapshot.Count);
        Assert.Equal(new[] { "instance-a", "instance-b" }, Snapshot.Select(Instance => Instance.InstanceId));
        Assert.All(Snapshot, Instance => Assert.Equal("Shared Project", Instance.ProjectName));
        Assert.All(Snapshot, Instance => Assert.Equal("C:\\projects\\shared", Instance.ProjectRootPath));
    }

    /// <summary>
    /// Confirms caller mutations cannot change a registered descriptor or a later snapshot.
    /// </summary>
    [Fact]
    public void Registration_and_snapshots_are_detached()
    {
        ManualTimeProvider Clock = new();
        InMemoryEditorRegistry Registry = CreateRegistry(Clock);
        EditorInstanceDescriptor Input = CreateDescriptor("detached-instance", StartedAt(1));

        Assert.True(Registry.TryRegister(Input));
        Input.ProjectName = "Changed input";
        IReadOnlyList<EditorInstanceDescriptor> FirstSnapshot = Registry.GetSnapshot();
        FirstSnapshot[0].ProjectName = "Changed snapshot";

        IReadOnlyList<EditorInstanceDescriptor> LaterSnapshot = Registry.GetSnapshot();
        Assert.Equal("Shared Project", Assert.Single(LaterSnapshot).ProjectName);
    }

    /// <summary>
    /// Confirms a coordinator with no successful registration waits for its initial grace period.
    /// </summary>
    [Fact]
    public void Initial_shutdown_waits_until_deadline()
    {
        ManualTimeProvider Clock = new();
        InMemoryEditorRegistry Registry = CreateRegistry(Clock, TimeSpan.FromSeconds(5));

        Assert.False(Registry.TryBeginShutdownIfIdle());
        Clock.Advance(TimeSpan.FromSeconds(4));
        Assert.False(Registry.TryBeginShutdownIfIdle());
        Clock.Advance(TimeSpan.FromSeconds(1));

        Assert.True(Registry.TryBeginShutdownIfIdle());
        Assert.True(Registry.IsShuttingDown);
    }

    /// <summary>
    /// Confirms the initial-registration grace period does not delay idle shutdown after registration.
    /// </summary>
    [Fact]
    public void Shutdown_after_last_unregister_is_immediate_after_first_registration()
    {
        ManualTimeProvider Clock = new();
        InMemoryEditorRegistry Registry = CreateRegistry(Clock, TimeSpan.FromHours(1));
        EditorInstanceDescriptor Instance = CreateDescriptor("first-instance", StartedAt(1));

        Assert.True(Registry.TryRegister(Instance));
        Assert.True(Registry.TryUnregister(Instance.InstanceId, Instance.StartedAt));

        Assert.True(Registry.TryBeginShutdownIfIdle());
        Assert.True(Registry.IsShuttingDown);
    }

    /// <summary>
    /// Confirms heartbeat extends a lease using monotonic elapsed time.
    /// </summary>
    [Fact]
    public void Heartbeat_renews_lease_using_monotonic_time()
    {
        ManualTimeProvider Clock = new();
        InMemoryEditorRegistry Registry = CreateRegistry(Clock, TimeSpan.FromHours(1), TimeSpan.FromSeconds(10));
        EditorInstanceDescriptor Instance = CreateDescriptor("heartbeat-instance", StartedAt(1));

        Assert.True(Registry.TryRegister(Instance));
        Clock.Advance(TimeSpan.FromSeconds(8));
        Assert.True(Registry.TryHeartbeat(Instance.InstanceId, Instance.StartedAt));
        Clock.Advance(TimeSpan.FromSeconds(8));

        Assert.Single(Registry.GetSnapshot());
        Assert.False(Registry.TryBeginShutdownIfIdle());
    }

    /// <summary>
    /// Confirms an expired lease is removed and allows idle shutdown.
    /// </summary>
    [Fact]
    public void Expired_lease_is_removed_and_shutdown_can_start()
    {
        ManualTimeProvider Clock = new();
        InMemoryEditorRegistry Registry = CreateRegistry(Clock, TimeSpan.FromHours(1), TimeSpan.FromSeconds(10));
        EditorInstanceDescriptor Instance = CreateDescriptor("expired-instance", StartedAt(1));
        Assert.True(Registry.TryRegister(Instance));

        Clock.Advance(TimeSpan.FromSeconds(10));

        Assert.Empty(Registry.GetSnapshot());
        Assert.True(Registry.TryBeginShutdownIfIdle());
    }

    /// <summary>
    /// Confirms an old incarnation cannot unregister a replacement with the same instance identity.
    /// </summary>
    [Fact]
    public void Unregister_requires_matching_incarnation()
    {
        ManualTimeProvider Clock = new();
        InMemoryEditorRegistry Registry = CreateRegistry(Clock);
        EditorInstanceDescriptor Current = CreateDescriptor("reused-instance-id", StartedAt(2));
        Assert.True(Registry.TryRegister(Current));

        Assert.False(Registry.TryUnregister(Current.InstanceId, StartedAt(1)));

        Assert.Equal(Current.StartedAt, Assert.Single(Registry.GetSnapshot()).StartedAt);
    }

    /// <summary>
    /// Confirms a repeated registration for the same incarnation refreshes metadata and its lease.
    /// </summary>
    [Fact]
    public void Same_incarnation_registration_refreshes_metadata_and_lease()
    {
        ManualTimeProvider Clock = new();
        InMemoryEditorRegistry Registry = CreateRegistry(Clock, TimeSpan.FromHours(1), TimeSpan.FromSeconds(10));
        EditorInstanceDescriptor Initial = CreateDescriptor("refresh-instance", StartedAt(1));
        Assert.True(Registry.TryRegister(Initial));
        Clock.Advance(TimeSpan.FromSeconds(8));
        EditorInstanceDescriptor Updated = CreateDescriptor("refresh-instance", Initial.StartedAt, "Renamed Project");

        Assert.True(Registry.TryRegister(Updated));
        Clock.Advance(TimeSpan.FromSeconds(8));

        EditorInstanceDescriptor Snapshot = Assert.Single(Registry.GetSnapshot());
        Assert.Equal("Renamed Project", Snapshot.ProjectName);
        Assert.True(Registry.TryHeartbeat(Updated.InstanceId, Updated.StartedAt));
    }

    /// <summary>
    /// Confirms a live identity cannot be overwritten by a different process incarnation.
    /// </summary>
    [Fact]
    public void Live_instance_id_rejects_different_incarnation()
    {
        ManualTimeProvider Clock = new();
        InMemoryEditorRegistry Registry = CreateRegistry(Clock);
        EditorInstanceDescriptor Current = CreateDescriptor("live-instance", StartedAt(1));
        EditorInstanceDescriptor Different = CreateDescriptor("live-instance", StartedAt(2), "Replacement Project");
        Assert.True(Registry.TryRegister(Current));

        Assert.False(Registry.TryRegister(Different));

        EditorInstanceDescriptor Snapshot = Assert.Single(Registry.GetSnapshot());
        Assert.Equal(Current.StartedAt, Snapshot.StartedAt);
        Assert.Equal(Current.ProjectName, Snapshot.ProjectName);
    }

    /// <summary>
    /// Confirms concurrent registration and idle shutdown cannot both succeed.
    /// </summary>
    [Fact]
    public async Task Registration_and_idle_shutdown_are_serialized()
    {
        ManualTimeProvider Clock = new();
        InMemoryEditorRegistry Registry = CreateRegistry(Clock, TimeSpan.FromSeconds(1));
        Clock.Advance(TimeSpan.FromSeconds(1));
        EditorInstanceDescriptor Instance = CreateDescriptor("racing-instance", StartedAt(1));
        using CountdownEvent Ready = new(2);
        using ManualResetEventSlim Start = new(false);

        Task<bool> Registration = Task.Run(() =>
        {
            Ready.Signal();
            Start.Wait();
            return Registry.TryRegister(Instance);
        });
        Task<bool> Shutdown = Task.Run(() =>
        {
            Ready.Signal();
            Start.Wait();
            return Registry.TryBeginShutdownIfIdle();
        });

        Ready.Wait();
        Start.Set();
        await Task.WhenAll(Registration, Shutdown);
        bool RegistrationAccepted = await Registration;
        bool ShutdownAccepted = await Shutdown;

        if (RegistrationAccepted)
        {
            Assert.False(ShutdownAccepted);
            Assert.False(Registry.IsShuttingDown);
            Assert.Single(Registry.GetSnapshot());
        }
        else
        {
            Assert.True(ShutdownAccepted);
            Assert.True(Registry.IsShuttingDown);
            Assert.Empty(Registry.GetSnapshot());
        }
    }

    /// <summary>
    /// Creates a registry with explicit monotonic-time policies.
    /// </summary>
    /// <param name="Clock">The fake clock used by the test.</param>
    /// <param name="InitialRegistrationTimeout">The optional deadline for the first registration.</param>
    /// <param name="LeaseDuration">The optional registration lease duration.</param>
    /// <returns>A new in-memory registry for the test.</returns>
    static InMemoryEditorRegistry CreateRegistry(ManualTimeProvider Clock, TimeSpan? InitialRegistrationTimeout = null, TimeSpan? LeaseDuration = null)
    {
        return new InMemoryEditorRegistry(Clock, LeaseDuration ?? TimeSpan.FromSeconds(10), InitialRegistrationTimeout ?? TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Creates a stable identity timestamp for one synthetic editor incarnation.
    /// </summary>
    /// <param name="Second">The second offset from the test date.</param>
    /// <returns>A unique test timestamp stored as descriptor identity metadata.</returns>
    static DateTimeOffset StartedAt(int Second)
    {
        return new DateTimeOffset(2026, 10, 15, 0, 0, Second, TimeSpan.Zero);
    }

    /// <summary>
    /// Creates a descriptor for an editor process without filesystem or transport side effects.
    /// </summary>
    /// <param name="InstanceId">The independent editor process identity.</param>
    /// <param name="StartedAt">The start time identifying this process incarnation.</param>
    /// <param name="ProjectName">The displayed project name.</param>
    /// <returns>A mutable DTO whose isolation is verified by the tests.</returns>
    static EditorInstanceDescriptor CreateDescriptor(string InstanceId, DateTimeOffset StartedAt, string ProjectName = "Shared Project")
    {
        return new EditorInstanceDescriptor
        {
            InstanceId = InstanceId,
            ProjectName = ProjectName,
            ProjectRootPath = "C:\\projects\\shared",
            ProjectGeneration = "generation-1",
            Endpoint = "http://127.0.0.1:5001/mcp",
            ContractVersion = 1,
            EditorVersion = "test-version",
            ProcessId = 100,
            StartedAt = StartedAt
        };
    }
}
