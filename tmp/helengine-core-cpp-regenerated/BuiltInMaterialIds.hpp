#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_string.hpp"

class BuiltInMaterialIds
{
public:
    virtual ~BuiltInMaterialIds() = default;

    static std::string StandardMaterialShaderAssetId;

    static std::string StandardRuntimeMaterialAssetId;

    static bool UsesStandardMeshTransform(std::string materialId);
};
