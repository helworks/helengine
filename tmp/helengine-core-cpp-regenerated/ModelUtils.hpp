#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ModelAsset;
class float3;

#include "ModelAsset.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "ModelAsset.hpp"
#include "float3.hpp"

class ModelUtils
{
public:
    static ::ModelAsset* GenerateCubeMesh(::float3 position, ::float3 scale);

    static ::ModelAsset* GeneratePlaneMesh(::float3 position, ::float3 scale);
};
