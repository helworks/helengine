using System.ComponentModel;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32.SafeHandles;

namespace helengine.editor.automation.discovery;

/// <summary>
/// Stores one cryptographically random bearer credential per local editor instance.
/// </summary>
public sealed class EditorLocalCredentialStore
{
    /// <summary>File suffix for records containing private bearer credentials.</summary>
    const string CredentialExtension = ".token";
    /// <summary>JSON options that reject unrecognized private credential fields.</summary>
    static readonly JsonSerializerOptions CredentialJsonOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    /// <summary>Absolute protected directory holding this store's credential files.</summary>
    readonly string DirectoryPath;

    /// <summary>Creates a credential store below an absolute directory protected for the current Windows user.</summary>
    /// <param name="DirectoryPath">The absolute private directory used for bearer credentials.</param>
    public EditorLocalCredentialStore(string DirectoryPath)
    {
        this.DirectoryPath = EditorRegistryPaths.PreparePrivateDirectory(DirectoryPath);
    }

    /// <summary>Creates a new 256-bit bearer token and publishes it atomically for one instance identity.</summary>
    /// <param name="InstanceId">The exact case-sensitive identity of the editor instance.</param>
    /// <returns>The newly created lowercase hexadecimal bearer token.</returns>
    public string Create(string InstanceId)
    {
        ValidateInstanceId(InstanceId);
        EditorRegistryPaths.VerifyPrivateDirectory(DirectoryPath);
        string EntryPath = EditorRegistryPaths.GetEntryPath(DirectoryPath, InstanceId, CredentialExtension);
        if (EditorRegistryPaths.TryGetAttributes(EntryPath, out FileAttributes ExistingAttributes))
        {
            if ((ExistingAttributes & FileAttributes.ReparsePoint) != 0 || (ExistingAttributes & FileAttributes.Directory) != 0)
            {
                throw new IOException("A credential destination cannot be a directory or reparse point.");
            }

            throw new IOException("A credential already exists for this editor instance.");
        }

        byte[] RandomBytes = RandomNumberGenerator.GetBytes(32);
        string Token;
        try
        {
            Token = Convert.ToHexString(RandomBytes).ToLowerInvariant();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(RandomBytes);
        }

        byte[] CredentialBytes = JsonSerializer.SerializeToUtf8Bytes(new CredentialRecord
        {
            InstanceId = InstanceId,
            Token = Token
        });
        WriteAtomically(EntryPath, CredentialBytes);
        return Token;
    }

    /// <summary>Reads a token only when the private file still contains the exact requested instance identity.</summary>
    /// <param name="InstanceId">The exact case-sensitive identity of the editor instance.</param>
    /// <returns>The bearer token created for that instance.</returns>
    public string Read(string InstanceId)
    {
        ValidateInstanceId(InstanceId);
        EditorRegistryPaths.VerifyPrivateDirectory(DirectoryPath);
        string EntryPath = EditorRegistryPaths.GetEntryPath(DirectoryPath, InstanceId, CredentialExtension);
        if (!EditorRegistryPaths.TryGetAttributes(EntryPath, out FileAttributes Attributes))
        {
            throw new FileNotFoundException("No local credential exists for this editor instance.", EntryPath);
        }

        if ((Attributes & FileAttributes.ReparsePoint) != 0 || (Attributes & FileAttributes.Directory) != 0)
        {
            throw new IOException("A local credential cannot be read through a directory or reparse point.");
        }

        CredentialRecord Record = ReadRecord(EntryPath);
        ValidateRecord(Record, InstanceId);
        return Record.Token;
    }

    /// <summary>Deletes only the regular credential file whose contents still identify the requested instance.</summary>
    /// <param name="InstanceId">The exact case-sensitive identity of the editor instance.</param>
    public void Delete(string InstanceId)
    {
        Delete(InstanceId, null);
    }

    /// <summary>Deletes only the validated credential file object, preserving any replacement at its path.</summary>
    /// <param name="InstanceId">The exact case-sensitive identity whose credential is being deleted.</param>
    /// <param name="BeforeDelete">An internal synchronization action used to exercise replacement races in tests.</param>
    internal void Delete(string InstanceId, Action BeforeDelete)
    {
        ValidateInstanceId(InstanceId);
        EditorRegistryPaths.VerifyPrivateDirectory(DirectoryPath);
        string EntryPath = EditorRegistryPaths.GetEntryPath(DirectoryPath, InstanceId, CredentialExtension);
        if (!EditorRegistryPaths.TryGetAttributes(EntryPath, out FileAttributes Attributes))
        {
            return;
        }

        if ((Attributes & FileAttributes.ReparsePoint) != 0 || (Attributes & FileAttributes.Directory) != 0)
        {
            throw new IOException("Refused to delete a credential directory or reparse point.");
        }

        SafeFileHandle EntryHandle;
        try
        {
            EntryHandle = EditorRegistryPaths.OpenRegularFileForConditionalDelete(EntryPath);
        }
        catch (Win32Exception Error) when (Error.NativeErrorCode == 2 || Error.NativeErrorCode == 3)
        {
            return;
        }

        using (EntryHandle)
        {
            using FileStream Stream = new(EntryHandle, FileAccess.Read, 4096, false);
            CredentialRecord Record = ReadRecord(Stream);
            ValidateRecord(Record, InstanceId);
            BeforeDelete?.Invoke();
            EditorRegistryPaths.VerifyPrivateDirectory(DirectoryPath);
            EditorRegistryPaths.DeleteOpenedFile(Stream.SafeFileHandle);
        }
    }

    /// <summary>Writes a credential to a same-directory private file before atomically publishing it without replacement.</summary>
    /// <param name="EntryPath">The final credential path inside the secured root.</param>
    /// <param name="Contents">The complete serialized credential record.</param>
    static void WriteAtomically(string EntryPath, byte[] Contents)
    {
        string TempPath = EntryPath + ".tmp-" + Guid.NewGuid().ToString("N");
        string ParentDirectory = Path.GetDirectoryName(EntryPath);
        EditorRegistryPaths.VerifyPrivateDirectory(ParentDirectory);

        try
        {
            FileSecurity FileSecurity = EditorRegistryPaths.CreatePrivateFileSecurity();
            using (FileStream Stream = FileSystemAclExtensions.Create(new FileInfo(TempPath), FileMode.CreateNew, FileSystemRights.FullControl, FileShare.None, 4096, FileOptions.WriteThrough, FileSecurity))
            {
                Stream.Write(Contents, 0, Contents.Length);
                Stream.Flush(true);
            }

            EditorRegistryPaths.VerifyPrivateDirectory(ParentDirectory);
            if (EditorRegistryPaths.TryGetAttributes(EntryPath, out FileAttributes ExistingAttributes))
            {
                if ((ExistingAttributes & FileAttributes.ReparsePoint) != 0 || (ExistingAttributes & FileAttributes.Directory) != 0)
                {
                    throw new IOException("A credential destination cannot be a directory or reparse point.");
                }

                throw new IOException("A credential already exists for this editor instance.");
            }

            File.Move(TempPath, EntryPath);
        }
        finally
        {
            if (EditorRegistryPaths.TryGetAttributes(TempPath, out FileAttributes TempAttributes) &&
                (TempAttributes & FileAttributes.ReparsePoint) == 0 &&
                (TempAttributes & FileAttributes.Directory) == 0)
            {
                File.Delete(TempPath);
            }
        }
    }

    /// <summary>Reads one private JSON credential record.</summary>
    /// <param name="EntryPath">The already checked credential file path.</param>
    /// <returns>The stored instance identity and bearer token.</returns>
    static CredentialRecord ReadRecord(string EntryPath)
    {
        using FileStream Stream = new(EntryPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan);
        return ReadRecord(Stream);
    }

    /// <summary>Deserializes one credential record from the exact file stream validated for deletion.</summary>
    /// <param name="Stream">The stream bound to the private credential file object.</param>
    /// <returns>The identity and bearer token stored in the file.</returns>
    static CredentialRecord ReadRecord(Stream Stream)
    {
        CredentialRecord Record = JsonSerializer.Deserialize<CredentialRecord>(Stream, CredentialJsonOptions);
        return Record ?? throw new InvalidDataException("The local credential record is empty.");
    }

    /// <summary>Checks that the record identity matches and its credential is a complete 256-bit token.</summary>
    /// <param name="Record">The deserialized credential record.</param>
    /// <param name="ExpectedInstanceId">The exact identity selected by the caller.</param>
    static void ValidateRecord(CredentialRecord Record, string ExpectedInstanceId)
    {
        if (!string.Equals(Record.InstanceId, ExpectedInstanceId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The local credential file does not match the requested editor instance.");
        }

        if (Record.Token == null || Record.Token.Length != 64 || Record.Token.Any(Character => !Uri.IsHexDigit(Character)))
        {
            throw new InvalidDataException("The local credential record does not contain a valid 256-bit token.");
        }
    }

    /// <summary>Rejects empty instance identities before deriving a private file name.</summary>
    /// <param name="InstanceId">The exact identity to validate.</param>
    static void ValidateInstanceId(string InstanceId)
    {
        if (string.IsNullOrWhiteSpace(InstanceId))
        {
            throw new ArgumentException("An instance identity is required.", nameof(InstanceId));
        }
    }

    /// <summary>Private serialized fields retained separately from public editor descriptors.</summary>
    sealed class CredentialRecord
    {
        /// <summary>Gets or sets the real case-sensitive editor identity used to verify the hashed filename.</summary>
        public string InstanceId { get; set; }

        /// <summary>Gets or sets the bearer token protected by the file's user-only ACL.</summary>
        public string Token { get; set; }
    }
}
