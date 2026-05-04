#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class EngineBinaryHeader;
class EngineBinaryHeaderSerializer;
class Asset;

#include "runtime/native_exceptions.hpp"
#include "EngineBinaryHeader.hpp"
#include "EngineBinaryHeaderSerializer.hpp"
#include "EngineBinaryHeaderSerializer.hpp"
#include "Asset.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/memory-stream.hpp"
#include "Asset.hpp"
#include "system/io/stream.hpp"
#include "runtime/array.hpp"

class AssetSerializer
{
public:
    virtual ~AssetSerializer() = default;

    static ::Asset* Deserialize(::Stream* stream);

    static ::Asset* DeserializeFromBytes(Array<uint8_t>* data);
};
