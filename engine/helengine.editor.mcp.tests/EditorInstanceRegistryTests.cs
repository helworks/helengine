using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using helengine.editor.automation;
using helengine.editor.automation.discovery;

namespace helengine.editor.mcp.tests;

/// <summary>
/// Exercises real local registry and credential files, including their Windows access checks.
/// </summary>
public sealed class EditorInstanceRegistryTests
{
    /// <summary>Environment variable that points tests to a visible task-specific artifacts directory.</summary>
    const string ArtifactsEnvironmentVariable = "HELENGINE_EDITOR_MCP_ARTIFACTS";

    /// <summary>
    /// Verifies registry replacement stays atomic and descriptor and credential files remain private.
    /// </summary>
    [Fact]
    public async Task Publish_is_atomic_and_private()
    {
        RequireWindows();
        string Artifacts = GetArtifactsRoot();
        string RegistryDirectory = GetFixtureDirectory("atomic-registry");
        string CredentialDirectory = GetFixtureDirectory("private-credentials");
        List<string> Issues = new();
        EditorInstanceRegistry Registry = new(RegistryDirectory, Issues.Add);
        EditorLocalCredentialStore Credentials = new(CredentialDirectory);
        const string InstanceId = "atomic-instance";
        string LargeProjectName = new('P', 128 * 1024);

        Registry.Publish(CreateInstance(InstanceId, LargeProjectName, Path.Combine(Artifacts, "never-started-project")));
        string EntryPath = Assert.Single(Directory.GetFiles(RegistryDirectory, "*.json"));
        using ManualResetEventSlim Start = new(false);
        using ManualResetEventSlim FirstRead = new(false);
        List<string> InvalidSnapshots = new();
        int ReadCount = 0;

        Task Writer = Task.Run(() =>
        {
            Start.Wait();
            FirstRead.Wait();
            for (int Index = 0; Index < 24; Index++)
            {
                Registry.Publish(CreateInstance(InstanceId, LargeProjectName + Index, Path.Combine(Artifacts, "never-started-project")));
            }
        });

        Task Reader = Task.Run(() =>
        {
            Start.Wait();
            do
            {
                Interlocked.Increment(ref ReadCount);
                try
                {
                    using FileStream Stream = new(EntryPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan);
                    EditorInstanceDescriptor DirectSnapshot = JsonSerializer.Deserialize<EditorInstanceDescriptor>(Stream);
                    if (DirectSnapshot == null || DirectSnapshot.InstanceId != InstanceId ||
                        !DirectSnapshot.ProjectName.StartsWith(LargeProjectName, StringComparison.Ordinal) ||
                        DirectSnapshot.ProjectRootPath != Path.Combine(Artifacts, "never-started-project"))
                    {
                        lock (InvalidSnapshots)
                        {
                            InvalidSnapshots.Add($"The direct descriptor read was incomplete or changed identity; targetExists={File.Exists(EntryPath)}.");
                        }
                    }
                }
                catch (Exception Error)
                {
                    lock (InvalidSnapshots)
                    {
                        InvalidSnapshots.Add($"The direct descriptor read failed with {Error.GetType().Name}: {Error.Message}; targetExists={File.Exists(EntryPath)}.");
                    }
                }

                FirstRead.Set();
                Thread.Yield();
            }
            while (!Writer.IsCompleted);
        });

        Start.Set();
        await Task.WhenAll(Writer, Reader);

        Assert.True(ReadCount > 0);
        Assert.True(InvalidSnapshots.Count == 0, InvalidSnapshots.FirstOrDefault());
        Assert.Empty(Issues);
        EditorInstanceDescriptor Latest = Assert.Single(Registry.ReadCandidates());
        Assert.Equal(InstanceId, Latest.InstanceId);
        Assert.Equal(LargeProjectName + "23", Latest.ProjectName);

        string Credential = Credentials.Create(InstanceId);
        Assert.Equal(Credential, Credentials.Read(InstanceId));
        EditorInstanceDescriptor Published = Assert.Single(Registry.ReadCandidates());
        string DescriptorJson = JsonSerializer.Serialize(Published);
        using JsonDocument DescriptorDocument = JsonDocument.Parse(DescriptorJson);
        Assert.False(DescriptorDocument.RootElement.TryGetProperty("credential", out _));
        Assert.DoesNotContain(Credential, DescriptorJson, StringComparison.Ordinal);

        string RegistryFile = Assert.Single(Directory.GetFiles(RegistryDirectory, "*.json"));
        string CredentialFile = Assert.Single(Directory.GetFiles(CredentialDirectory, "*.token"));
        AssertPrivateAccess(RegistryDirectory, RegistryFile);
        AssertPrivateAccess(CredentialDirectory, CredentialFile);
    }

    /// <summary>
    /// Verifies one corrupt record reports a diagnostic without suppressing a healthy candidate.
    /// </summary>
    [Fact]
    public void Corrupt_entry_does_not_hide_healthy_instances()
    {
        RequireWindows();
        string RegistryDirectory = GetFixtureDirectory("corrupt-entry");
        List<string> Issues = new();
        EditorInstanceRegistry Registry = new(RegistryDirectory, Issues.Add);
        Registry.Publish(CreateInstance("healthy-instance", "Healthy Project", GetArtifactsRoot()));
        File.WriteAllText(Path.Combine(RegistryDirectory, "manual-corrupt.json"), "{ this is not json");

        IReadOnlyList<EditorInstanceDescriptor> Candidates = Registry.ReadCandidates();

        Assert.Equal(1, Candidates.Count);
        Assert.Equal("healthy-instance", Candidates[0].InstanceId);
        Assert.Contains(Issues, Issue => Issue.Contains("manual-corrupt.json", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifies project name and root path do not collapse two editor instance identities.
    /// </summary>
    [Fact]
    public void Same_name_and_same_project_keep_distinct_instances()
    {
        RequireWindows();
        string RegistryDirectory = GetFixtureDirectory("distinct-instances");
        EditorInstanceRegistry Registry = new(RegistryDirectory, _ => { });
        string ProjectRoot = Path.Combine(GetArtifactsRoot(), "Shared Project");
        Registry.Publish(CreateInstance("instance-copy-a", "Same Project", ProjectRoot));
        Registry.Publish(CreateInstance("instance-copy-b", "Same Project", ProjectRoot));

        IReadOnlyList<EditorInstanceDescriptor> Candidates = Registry.ReadCandidates();

        Assert.Equal(2, Candidates.Count);
        Assert.Equal(2, Candidates.Select(Candidate => Candidate.InstanceId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(Candidates, Candidate =>
        {
            Assert.Equal("Same Project", Candidate.ProjectName);
            Assert.Equal(ProjectRoot, Candidate.ProjectRootPath);
        });
    }

    /// <summary>
    /// Verifies cleanup for an old identity cannot remove its same-project replacement.
    /// </summary>
    [Fact]
    public void Cleanup_cannot_remove_replacement()
    {
        RequireWindows();
        string RegistryDirectory = GetFixtureDirectory("replacement-cleanup");
        EditorInstanceRegistry Registry = new(RegistryDirectory, _ => { });
        string CredentialDirectory = GetFixtureDirectory("replacement-credentials");
        EditorLocalCredentialStore Credentials = new(CredentialDirectory);
        string ProjectRoot = Path.Combine(GetArtifactsRoot(), "Replaced Project");
        Registry.Publish(CreateInstance("old-instance", "Same Project", ProjectRoot));
        string OldCredential = Credentials.Create("old-instance");
        Registry.Publish(CreateInstance("replacement-instance", "Same Project", ProjectRoot));
        string ReplacementCredential = Credentials.Create("replacement-instance");
        string ReplacementCredentialForOldInstance = null;

        Registry.Remove("old-instance", () => Registry.Publish(CreateInstance("old-instance", "Replaced Same Identity", ProjectRoot)));
        Credentials.Delete("old-instance", () =>
        {
            string CredentialPath = EditorRegistryPaths.GetEntryPath(CredentialDirectory, "old-instance", ".token");
            string TemporaryPath = CredentialPath + ".replacement-" + Guid.NewGuid().ToString("N");
            ReplacementCredentialForOldInstance = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            byte[] ReplacementBytes = JsonSerializer.SerializeToUtf8Bytes(new
            {
                InstanceId = "old-instance",
                Token = ReplacementCredentialForOldInstance
            });
            using (FileStream Stream = FileSystemAclExtensions.Create(new FileInfo(TemporaryPath), FileMode.CreateNew, FileSystemRights.FullControl, FileShare.None, 4096, FileOptions.WriteThrough, EditorRegistryPaths.CreatePrivateFileSecurity()))
            {
                Stream.Write(ReplacementBytes, 0, ReplacementBytes.Length);
                Stream.Flush(true);
            }

            File.Delete(CredentialPath);
            File.Move(TemporaryPath, CredentialPath);
        });

        IReadOnlyList<EditorInstanceDescriptor> Remaining = Registry.ReadCandidates();
        Assert.Equal(2, Remaining.Count);
        Assert.Contains(Remaining, Candidate => Candidate.InstanceId == "replacement-instance");
        Assert.Contains(Remaining, Candidate => Candidate.InstanceId == "old-instance" && Candidate.ProjectName == "Replaced Same Identity");
        Assert.Equal(ReplacementCredential, Credentials.Read("replacement-instance"));
        Assert.Equal(ReplacementCredentialForOldInstance, Credentials.Read("old-instance"));
        Assert.NotEqual(OldCredential, ReplacementCredential);

        Registry.Remove("replacement-instance");
        Credentials.Delete("replacement-instance");
        Assert.DoesNotContain(Registry.ReadCandidates(), Candidate => Candidate.InstanceId == "replacement-instance");
        Assert.Throws<FileNotFoundException>(() => Credentials.Read("replacement-instance"));
    }

    /// <summary>
    /// Verifies Unicode project roots retain their exact casing through a filesystem round trip.
    /// </summary>
    [Fact]
    public void Unicode_and_case_paths_roundtrip()
    {
        RequireWindows();
        string Artifacts = GetArtifactsRoot();
        string ProjectRootOnDisk = Path.Combine(Artifacts, "Projects", "Helengine-Café-Ω", "Scene Editor");
        Directory.CreateDirectory(ProjectRootOnDisk);
        string ProjectRootSpelling = ProjectRootOnDisk.ToUpperInvariant();
        Assert.True(Directory.Exists(ProjectRootSpelling));

        EditorInstanceRegistry Registry = new(GetFixtureDirectory("unicode-path"), _ => { });
        Registry.Publish(CreateInstance("unicode-instance", "Café Ω", ProjectRootSpelling));

        EditorInstanceDescriptor Candidate = Assert.Single(Registry.ReadCandidates());

        Assert.Equal(ProjectRootSpelling, Candidate.ProjectRootPath);
        Assert.Contains("CAFÉ-Ω", Candidate.ProjectRootPath, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies a directory junction in a requested private storage path is rejected before writing through it.
    /// </summary>
    [Fact]
    public void Private_storage_path_rejects_directory_junctions()
    {
        RequireWindows();
        string Artifacts = GetArtifactsRoot();
        string TargetDirectory = Path.Combine(Artifacts, "junction-target-" + Guid.NewGuid().ToString("N"));
        string JunctionPath = Path.Combine(Artifacts, "junction-link-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TargetDirectory);

        try
        {
            ProcessStartInfo StartInfo = new("cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            StartInfo.ArgumentList.Add("/d");
            StartInfo.ArgumentList.Add("/c");
            StartInfo.ArgumentList.Add("mklink");
            StartInfo.ArgumentList.Add("/J");
            StartInfo.ArgumentList.Add(JunctionPath);
            StartInfo.ArgumentList.Add(TargetDirectory);

            using Process JunctionCreator = Process.Start(StartInfo) ?? throw new InvalidOperationException("Windows could not start the junction test helper.");
            string StandardOutput = JunctionCreator.StandardOutput.ReadToEnd();
            string StandardError = JunctionCreator.StandardError.ReadToEnd();
            JunctionCreator.WaitForExit();
            Assert.True(JunctionCreator.ExitCode == 0, $"Could not create the isolated test junction: {StandardOutput} {StandardError}");

            Assert.Throws<IOException>(() => new EditorInstanceRegistry(Path.Combine(JunctionPath, "registry"), _ => { }));
            Assert.False(Directory.Exists(Path.Combine(TargetDirectory, "registry")));
        }
        finally
        {
            if (Directory.Exists(JunctionPath))
            {
                Directory.Delete(JunctionPath);
            }

            if (Directory.Exists(TargetDirectory))
            {
                Directory.Delete(TargetDirectory, true);
            }
        }
    }

    /// <summary>
    /// Creates a descriptor with stable non-secret values for a local registry scenario.
    /// </summary>
    /// <param name="InstanceId">The identity used to distinguish one editor process.</param>
    /// <param name="ProjectName">The displayed project name.</param>
    /// <param name="ProjectRoot">The project path whose spelling should be persisted.</param>
    /// <returns>A descriptor that deliberately does not prove its HTTP endpoint is live.</returns>
    static EditorInstanceDescriptor CreateInstance(string InstanceId, string ProjectName, string ProjectRoot)
    {
        return new EditorInstanceDescriptor
        {
            InstanceId = InstanceId,
            ProjectName = ProjectName,
            ProjectRootPath = ProjectRoot,
            ProjectGeneration = "generation-one",
            Endpoint = "http://127.0.0.1:1/mcp",
            ContractVersion = 1,
            EditorVersion = "test-editor",
            ProcessId = Environment.ProcessId,
            StartedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Returns a unique fixture path below the visible artifacts directory supplied by the test command.
    /// </summary>
    /// <param name="Name">The behavior being exercised by the fixture.</param>
    /// <returns>An absolute path reserved for this test invocation.</returns>
    static string GetFixtureDirectory(string Name)
    {
        string Artifacts = GetArtifactsRoot();
        return Path.Combine(Artifacts, "task02-fixtures", Name + "-" + Guid.NewGuid().ToString("N"));
    }

    /// <summary>
    /// Requires an explicit visible artifacts root and refuses to fall back to a temporary directory.
    /// </summary>
    /// <returns>The absolute path configured by the task's PowerShell test command.</returns>
    static string GetArtifactsRoot()
    {
        string Artifacts = Environment.GetEnvironmentVariable(ArtifactsEnvironmentVariable);
        Assert.False(string.IsNullOrWhiteSpace(Artifacts), $"Set {ArtifactsEnvironmentVariable} to the task artifacts directory.");
        Assert.True(Path.IsPathFullyQualified(Artifacts), $"{ArtifactsEnvironmentVariable} must be an absolute path.");
        return Path.GetFullPath(Artifacts);
    }

    /// <summary>
    /// Ensures the native ACL test runs on the supported Windows local-storage platform.
    /// </summary>
    static void RequireWindows()
    {
        Assert.True(OperatingSystem.IsWindows(), "The local registry's ACL contract is Windows-specific.");
    }

    /// <summary>
    /// Performs native effective-rights checks for the owner and an unrelated built-in service SID.
    /// </summary>
    /// <param name="DirectoryPath">The protected directory under test.</param>
    /// <param name="FilePath">The protected atomically published file under test.</param>
    static void AssertPrivateAccess(string DirectoryPath, string FilePath)
    {
        SecurityIdentifier CurrentUser = WindowsIdentity.GetCurrent().User;
        SecurityIdentifier UnauthorizedUser = new(WellKnownSidType.NetworkServiceSid, null);
        Assert.NotEqual(CurrentUser, UnauthorizedUser);

        DirectorySecurity DirectoryAcl = FileSystemAclExtensions.GetAccessControl(new DirectoryInfo(DirectoryPath));
        FileSecurity FileAcl = FileSystemAclExtensions.GetAccessControl(new FileInfo(FilePath));
        FileSystemRights RequiredRights = FileSystemRights.ReadData | FileSystemRights.WriteData | FileSystemRights.Delete;

        Assert.Equal(RequiredRights, GetEffectiveRights(DirectoryAcl, CurrentUser) & RequiredRights);
        Assert.Equal(RequiredRights, GetEffectiveRights(FileAcl, CurrentUser) & RequiredRights);
        Assert.Equal((FileSystemRights)0, GetEffectiveRights(DirectoryAcl, UnauthorizedUser) & RequiredRights);
        Assert.Equal((FileSystemRights)0, GetEffectiveRights(FileAcl, UnauthorizedUser) & RequiredRights);

        using FileStream Stream = new(FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        Stream.Flush(true);
    }

    /// <summary>
    /// Evaluates effective access rights for a SID using the Windows ACL API without changing machine state.
    /// </summary>
    /// <param name="Security">The access control whose DACL should be checked.</param>
    /// <param name="UserSid">The SID whose effective file rights are being evaluated.</param>
    /// <returns>The effective rights returned by Windows for that SID and DACL.</returns>
    static FileSystemRights GetEffectiveRights(FileSystemSecurity Security, SecurityIdentifier UserSid)
    {
        byte[] DescriptorBytes = Security.GetSecurityDescriptorBinaryForm();
        RawSecurityDescriptor Descriptor = new(DescriptorBytes, 0);
        GenericAcl Dacl = Descriptor.DiscretionaryAcl;
        Assert.NotNull(Dacl);
        byte[] DaclBytes = new byte[Dacl.BinaryLength];
        Dacl.GetBinaryForm(DaclBytes, 0);
        byte[] SidBytes = new byte[UserSid.BinaryLength];
        UserSid.GetBinaryForm(SidBytes, 0);
        IntPtr DaclPointer = Marshal.AllocHGlobal(DaclBytes.Length);
        IntPtr SidPointer = Marshal.AllocHGlobal(SidBytes.Length);

        try
        {
            Marshal.Copy(DaclBytes, 0, DaclPointer, DaclBytes.Length);
            Marshal.Copy(SidBytes, 0, SidPointer, SidBytes.Length);
            NativeTrustee Trustee = new()
            {
                MultipleTrustee = IntPtr.Zero,
                MultipleTrusteeOperation = 0,
                TrusteeForm = 0,
                TrusteeType = 0,
                Name = SidPointer
            };

            uint Error = GetEffectiveRightsFromAclW(DaclPointer, ref Trustee, out uint Rights);
            if (Error != 0)
            {
                throw new Win32Exception((int)Error, "Windows could not evaluate the test directory ACL.");
            }

            return (FileSystemRights)Rights;
        }
        finally
        {
            Marshal.FreeHGlobal(SidPointer);
            Marshal.FreeHGlobal(DaclPointer);
        }
    }

    /// <summary>
    /// Native trustee layout passed to GetEffectiveRightsFromAclW for a SID-based ACL check.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NativeTrustee
    {
        /// <summary>Gets or sets an optional second trustee in a compound access check.</summary>
        public IntPtr MultipleTrustee;

        /// <summary>Gets or sets the multiple-trustee operation value; zero means none.</summary>
        public int MultipleTrusteeOperation;

        /// <summary>Gets or sets the trustee form value; zero identifies the name pointer as a SID.</summary>
        public int TrusteeForm;

        /// <summary>Gets or sets the trustee type value used by the native ACL API.</summary>
        public int TrusteeType;

        /// <summary>Gets or sets the unmanaged SID pointer used as the trustee name.</summary>
        public IntPtr Name;
    }

    /// <summary>
    /// Queries the effective access mask granted by one DACL to a SID without modifying the ACL.
    /// </summary>
    /// <param name="Acl">A pointer to the binary DACL representation.</param>
    /// <param name="Trustee">A SID-form trustee descriptor.</param>
    /// <param name="AccessRights">The access mask granted to the trustee by Windows.</param>
    /// <returns>Zero when the native ACL check completed successfully.</returns>
    [DllImport("advapi32.dll", EntryPoint = "GetEffectiveRightsFromAclW", CharSet = CharSet.Unicode)]
    static extern uint GetEffectiveRightsFromAclW(IntPtr Acl, ref NativeTrustee Trustee, out uint AccessRights);
}
