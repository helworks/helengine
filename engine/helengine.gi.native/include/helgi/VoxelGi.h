#pragma once
#include "Config.h"
#include "Field.h"
#include "Stats.h"
#include "Sun.h"
#include "Triangle.h"
#include <memory>
#include <vector>
namespace helgi {
/// <summary>Camera-independent finite voxel diffuse transport with retained contributor coverage and staged publication.</summary>
class VoxelGi {
    /// <summary>Owns CPU implementation details without exposing platform or rendering dependencies.</summary>
    class Impl;
    /// <summary>Private solver state with exclusive ownership.</summary>
    std::unique_ptr<Impl> State;
public:
    /// <summary>Validates explicit bounds and quality, then allocates an initial dark field.</summary>
    explicit VoxelGi(const Config& config);
    /// <summary>Releases retained geometry, coverage and field storage.</summary>
    ~VoxelGi();
    /// <summary>Copies validated geometry for an explicit identity/revision; an unchanged revision is a no-op.</summary>
    void SetContributor(std::uint64_t id, std::uint64_t revision, const std::vector<Triangle>& triangles);
    /// <summary>Removes a contributor if present; absent identities are a no-op.</summary>
    void RemoveContributor(std::uint64_t id);
    /// <summary>Sets validated sunlight; a direction or irradiance change queues a new field generation.</summary>
    void SetSun(const Sun& sun);
    /// <summary>Snapshots pending input if idle; new edits during processing are coalesced for the next generation.</summary>
    bool BeginUpdate();
    /// <summary>Processes at most maxWorkItems bounded units, starting queued work when idle; returns true on publication.</summary>
    bool AdvanceUpdate(std::uint32_t maxWorkItems);
    /// <summary>Returns the last complete atlas; the reference remains valid until the next publication.</summary>
    const Field& GetField() const;
    /// <summary>Trilinearly samples only visible empty probes, blending six lobes by normalized positive normal components.</summary>
    Vec3 SampleIrradiance(Vec3 position, Vec3 normal) const;
    /// <summary>Reports cumulative work and pending state without advancing the solver.</summary>
    Stats GetStats() const;
};
}
