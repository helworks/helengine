using System;

namespace helengine.editor.launcher.Models;

/// <summary>
/// Defines the exact reusable identity for one shared platform artifact.
/// </summary>
public sealed class ArtifactIdentity {
    /// <summary>
    /// Initializes one exact artifact identity instance.
    /// </summary>
    /// <param name="kind">Artifact family managed by the launcher.</param>
    /// <param name="id">Stable catalog identifier for the artifact.</param>
    /// <param name="version">Exact artifact version required for reuse.</param>
    public ArtifactIdentity(PlatformArtifactKind kind, string id, string version) {
        Kind = kind;
        Id = id;
        Version = version;
    }

    /// <summary>
    /// Gets the artifact family managed by the launcher.
    /// </summary>
    public PlatformArtifactKind Kind { get; }

    /// <summary>
    /// Gets the stable catalog identifier for the artifact.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the exact artifact version required for reuse.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Determines whether this identity matches another identity by exact kind, id, and version.
    /// </summary>
    /// <param name="other">Other artifact identity to compare.</param>
    /// <returns><c>true</c> when the identities match exactly; otherwise <c>false</c>.</returns>
    public bool Equals(ArtifactIdentity other) {
        if (ReferenceEquals(other, null)) {
            return false;
        }

        return Kind == other.Kind
            && string.Equals(Id, other.Id, StringComparison.Ordinal)
            && string.Equals(Version, other.Version, StringComparison.Ordinal);
    }

    /// <summary>
    /// Determines whether this identity matches another object by exact kind, id, and version.
    /// </summary>
    /// <param name="obj">Other object to compare.</param>
    /// <returns><c>true</c> when the supplied object is an equal artifact identity; otherwise <c>false</c>.</returns>
#pragma warning disable CS8765
    public override bool Equals(object obj) {
        if (obj is ArtifactIdentity other) {
            return Equals(other);
        }

        return false;
    }
#pragma warning restore CS8765

    /// <summary>
    /// Builds one stable hash code from the exact artifact identity values.
    /// </summary>
    /// <returns>Stable hash code for dictionary and set lookups.</returns>
    public override int GetHashCode() {
        return HashCode.Combine(Kind, Id, Version);
    }

    /// <summary>
    /// Returns one readable artifact identity string for diagnostics and persisted manifests.
    /// </summary>
    /// <returns>Readable exact artifact identity string.</returns>
    public override string ToString() {
        return $"{Kind}:{Id}@{Version}";
    }
}
