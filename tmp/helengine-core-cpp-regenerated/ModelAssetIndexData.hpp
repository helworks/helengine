#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ModelAsset;

#include "runtime/native_exceptions.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "ModelAsset.hpp"

class ModelAssetIndexData
{
public:
    int32_t IndexCount;

    int32_t get_IndexCount();

    Array<uint16_t>* Indices16;

    Array<uint16_t>* get_Indices16();

    Array<uint32_t>* Indices32;

    Array<uint32_t>* get_Indices32();

    bool Uses32BitIndices;

    bool get_Uses32BitIndices();

    static ::ModelAssetIndexData* Resolve(::ModelAsset* asset);
private:
    static bool HasIndices(Array<uint16_t>* indices);

    static bool HasIndices(Array<uint32_t>* indices);

    ModelAssetIndexData(bool uses32BitIndices, int32_t indexCount, Array<uint16_t>* indices16, Array<uint32_t>* indices32);
};
