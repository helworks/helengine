using helengine.editor.automation;

namespace helengine.editor.mcp.coordinator;

/// <summary>
/// Holds live editor registrations and coordinates lease expiry with idle shutdown.
/// </summary>
public sealed class InMemoryEditorRegistry
{
    /// <summary>Serializes registration, expiry, snapshots, and shutdown decisions.</summary>
    readonly object Gate = new();
    /// <summary>Monotonic time source used for leases and initial startup grace.</summary>
    readonly TimeProvider Clock;
    /// <summary>Maximum interval between heartbeats before a registration expires.</summary>
    readonly TimeSpan LeaseDuration;
    /// <summary>How long a new coordinator waits for its first registration.</summary>
    readonly TimeSpan InitialRegistrationTimeout;
    /// <summary>Monotonic timestamp captured when this registry was created.</summary>
    readonly long CoordinatorStartedTimestamp;
    /// <summary>Detached descriptors keyed by the exact process instance identity.</summary>
    readonly Dictionary<string, EditorInstanceDescriptor> Instances = new(StringComparer.Ordinal);
    /// <summary>Monotonic timestamp of the last successful registration or heartbeat.</summary>
    readonly Dictionary<string, long> LastHeartbeatTimestamps = new(StringComparer.Ordinal);
    /// <summary>Whether any editor registration has ever been accepted.</summary>
    bool HasAcceptedRegistration;
    /// <summary>Whether the coordinator has atomically committed to shutting down.</summary>
    bool ShuttingDown;

    /// <summary>
    /// Creates an empty registry with explicit monotonic time and lifecycle policies.
    /// </summary>
    /// <param name="Clock">Monotonic time source for lease and startup deadline measurements.</param>
    /// <param name="LeaseDuration">Maximum time without a heartbeat before an instance expires.</param>
    /// <param name="InitialRegistrationTimeout">Grace period before an unused coordinator may stop.</param>
    public InMemoryEditorRegistry(TimeProvider Clock, TimeSpan LeaseDuration, TimeSpan InitialRegistrationTimeout)
    {
        if (Clock == null)
        {
            throw new ArgumentNullException(nameof(Clock));
        }

        if (LeaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(LeaseDuration));
        }

        if (InitialRegistrationTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(InitialRegistrationTimeout));
        }

        this.Clock = Clock;
        this.LeaseDuration = LeaseDuration;
        this.InitialRegistrationTimeout = InitialRegistrationTimeout;
        CoordinatorStartedTimestamp = Clock.GetTimestamp();
    }

    /// <summary>Gets whether an idle shutdown decision has already been committed.</summary>
    public bool IsShuttingDown
    {
        get
        {
            lock (Gate)
            {
                return ShuttingDown;
            }
        }
    }

    /// <summary>
    /// Registers an editor or refreshes metadata for the same live process incarnation.
    /// </summary>
    /// <param name="Instance">The descriptor to copy into this registry.</param>
    /// <returns>True if this incarnation was accepted; otherwise false if shutdown or a different incarnation owns the identity.</returns>
    public bool TryRegister(EditorInstanceDescriptor Instance)
    {
        ValidateDescriptor(Instance);

        lock (Gate)
        {
            RemoveExpiredRegistrations();
            if (ShuttingDown)
            {
                return false;
            }

            if (Instances.TryGetValue(Instance.InstanceId, out EditorInstanceDescriptor Existing) && Existing.StartedAt != Instance.StartedAt)
            {
                return false;
            }

            Instances[Instance.InstanceId] = CopyDescriptor(Instance);
            LastHeartbeatTimestamps[Instance.InstanceId] = Clock.GetTimestamp();
            HasAcceptedRegistration = true;
            return true;
        }
    }

    /// <summary>
    /// Renews a live registration only when both its instance ID and start time match.
    /// </summary>
    /// <param name="InstanceId">The exact editor process identity.</param>
    /// <param name="StartedAt">The start time identifying that process incarnation.</param>
    /// <returns>True when the matching live registration was renewed.</returns>
    public bool TryHeartbeat(string InstanceId, DateTimeOffset StartedAt)
    {
        lock (Gate)
        {
            RemoveExpiredRegistrations();
            if (ShuttingDown || !Instances.TryGetValue(InstanceId, out EditorInstanceDescriptor Existing) || Existing.StartedAt != StartedAt)
            {
                return false;
            }

            LastHeartbeatTimestamps[InstanceId] = Clock.GetTimestamp();
            return true;
        }
    }

    /// <summary>
    /// Removes only the live registration matching the exact process incarnation.
    /// </summary>
    /// <param name="InstanceId">The exact editor process identity.</param>
    /// <param name="StartedAt">The start time identifying that process incarnation.</param>
    /// <returns>True when that registration was removed.</returns>
    public bool TryUnregister(string InstanceId, DateTimeOffset StartedAt)
    {
        lock (Gate)
        {
            RemoveExpiredRegistrations();
            if (ShuttingDown || !Instances.TryGetValue(InstanceId, out EditorInstanceDescriptor Existing) || Existing.StartedAt != StartedAt)
            {
                return false;
            }

            Instances.Remove(InstanceId);
            LastHeartbeatTimestamps.Remove(InstanceId);
            return true;
        }
    }

    /// <summary>
    /// Returns descriptors copied from the current live registrations in identity order.
    /// </summary>
    /// <returns>A detached read-only list that cannot mutate registry state.</returns>
    public IReadOnlyList<EditorInstanceDescriptor> GetSnapshot()
    {
        lock (Gate)
        {
            RemoveExpiredRegistrations();
            EditorInstanceDescriptor[] Snapshot = Instances.Values
                .OrderBy(Instance => Instance.InstanceId, StringComparer.Ordinal)
                .Select(CopyDescriptor)
                .ToArray();
            return Array.AsReadOnly(Snapshot);
        }
    }

    /// <summary>
    /// Atomically commits to shutdown when the registry is idle and its grace policy permits it.
    /// </summary>
    /// <returns>True if shutdown is already committed or was committed by this call.</returns>
    public bool TryBeginShutdownIfIdle()
    {
        lock (Gate)
        {
            RemoveExpiredRegistrations();
            if (ShuttingDown)
            {
                return true;
            }

            if (Instances.Count > 0)
            {
                return false;
            }

            if (!HasAcceptedRegistration && Clock.GetElapsedTime(CoordinatorStartedTimestamp) < InitialRegistrationTimeout)
            {
                return false;
            }

            ShuttingDown = true;
            return true;
        }
    }

    /// <summary>
    /// Removes registrations whose last monotonic heartbeat exceeded the configured lease.
    /// </summary>
    void RemoveExpiredRegistrations()
    {
        long CurrentTimestamp = Clock.GetTimestamp();
        List<string> ExpiredInstanceIds = new();

        foreach (KeyValuePair<string, long> Registration in LastHeartbeatTimestamps)
        {
            if (Clock.GetElapsedTime(Registration.Value, CurrentTimestamp) >= LeaseDuration)
            {
                ExpiredInstanceIds.Add(Registration.Key);
            }
        }

        foreach (string InstanceId in ExpiredInstanceIds)
        {
            LastHeartbeatTimestamps.Remove(InstanceId);
            Instances.Remove(InstanceId);
        }
    }

    /// <summary>
    /// Validates the identity fields used to distinguish one editor process incarnation.
    /// </summary>
    /// <param name="Instance">The descriptor supplied by an editor.</param>
    static void ValidateDescriptor(EditorInstanceDescriptor Instance)
    {
        if (Instance == null)
        {
            throw new ArgumentNullException(nameof(Instance));
        }

        if (string.IsNullOrWhiteSpace(Instance.InstanceId))
        {
            throw new ArgumentException("An editor instance ID is required.", nameof(Instance));
        }

        if (Instance.StartedAt == default)
        {
            throw new ArgumentException("An editor start time is required to identify its process incarnation.", nameof(Instance));
        }
    }

    /// <summary>Creates a detached copy of a mutable editor descriptor.</summary>
    /// <param name="Instance">The descriptor to copy.</param>
    /// <returns>A distinct DTO carrying the same public metadata.</returns>
    static EditorInstanceDescriptor CopyDescriptor(EditorInstanceDescriptor Instance)
    {
        return new EditorInstanceDescriptor
        {
            InstanceId = Instance.InstanceId,
            ProjectName = Instance.ProjectName,
            ProjectRootPath = Instance.ProjectRootPath,
            ProjectGeneration = Instance.ProjectGeneration,
            Endpoint = Instance.Endpoint,
            ContractVersion = Instance.ContractVersion,
            EditorVersion = Instance.EditorVersion,
            ProcessId = Instance.ProcessId,
            StartedAt = Instance.StartedAt
        };
    }
}
