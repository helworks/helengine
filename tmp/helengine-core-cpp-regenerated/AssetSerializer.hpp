#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class EditorAssetBinarySerializer;
class EngineBinaryHeader;
class EngineBinaryHeaderSerializer;
class Asset;

#include "runtime/native_exceptions.hpp"
#include "EditorAssetBinarySerializer.hpp"
#include "EditorAssetBinarySerializer.hpp"
#include "EngineBinaryHeader.hpp"
#include "EngineBinaryHeaderSerializer.hpp"
#include "EngineBinaryHeaderSerializer.hpp"
#include "Asset.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/memory-stream.hpp"
#include "runtime/array.hpp"
#include "Asset.hpp"
#include "system/io/stream.hpp"
#include "runtime/array.hpp"

class AssetSerializer
{
public:
    static ::Asset* Deserialize(::Stream* stream);

    static ::Asset* DeserializeFromBytes(Array<uint8_t>* data);

    static void Serialize(::Stream* stream, ::Asset* asset);

    static Array<uint8_t>* SerializeToBytes(::Asset* asset);
};
