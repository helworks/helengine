#pragma once
#include "Contributor.h"
#include "Coverage.h"
#include "Cell.h"
#include "helgi/Field.h"
#include "helgi/Sun.h"
#include <map>
#include <memory>
namespace helgi {
/// <summary>Private staged generation; publication only occurs after its last integration ray.</summary>
struct Work {
    /// <summary>Input generation captured atomically at the application call boundary.</summary>
    std::uint64_t Generation;
    /// <summary>Sun captured for this entire solve.</summary>
    Sun Light;
    /// <summary>Shared immutable contributor payloads in deterministic identity order.</summary>
    std::map<std::uint64_t, std::shared_ptr<const Contributor>> Inputs;
    /// <summary>Coverage belonging to this snapshot, reused by unchanged revisions.</summary>
    std::map<std::uint64_t, std::shared_ptr<Coverage>> Coverages;
    /// <summary>Current contributor being voxelized or aggregated.</summary>
    std::map<std::uint64_t, std::shared_ptr<const Contributor>>::iterator Input;
    /// <summary>Private aggregate occupancy and surface radiance.</summary>
    std::vector<Cell> Cells;
    /// <summary>Private atlas that cannot be observed before completion.</summary>
    Field Output;
    /// <summary>Phase: voxelize, aggregate, illuminate, integrate, publish.</summary>
    unsigned int Phase = 0;
    /// <summary>Current triangle or aggregate hit within a contributor.</summary>
    std::size_t Item = 0;
    /// <summary>Current cell for illumination/integration.</summary>
    std::size_t CellIndex = 0;
    /// <summary>Current integration lobe.</summary>
    unsigned int Lobe = 0;
    /// <summary>Current deterministic hemisphere sample.</summary>
    unsigned int Ray = 0;
    /// <summary>Inclusive candidate box bounds X/Y/Z then maximum X/Y/Z.</summary>
    std::array<int, 6> Bounds{};
    /// <summary>Current candidate coordinates X/Y/Z.</summary>
    std::array<int, 3> Candidate{};
    /// <summary>Whether a triangle candidate range has been initialized.</summary>
    bool CandidateReady = false;
    /// <summary>Number of voxel candidate tests, capped per generation.</summary>
    std::size_t CandidateTests = 0;
    /// <summary>Total retained intersections, capped per generation.</summary>
    std::size_t HitCount = 0;
};
}
