#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Asset;
class float3;
class float2;

#include "Asset.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "float3.hpp"
#include "runtime/array.hpp"
#include "float2.hpp"

class ModelAsset : public Asset
{
public:
    ModelAsset();

    Array<uint16_t>* Indices16;

    Array<uint32_t>* Indices32;

    Array<::float3>* Normals;

    Array<::float3>* Positions;

    Array<::float2>* TexCoords;

    std::string get_Id();

    void set_Id(std::string value);
};
