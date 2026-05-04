#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class EngineBinaryHeader;
class EngineBinaryHeaderSerializer;
class FontAsset;
class EngineBinaryReader;
class FontInfo;
class TextureAsset;
class RuntimeTexture;
class Core;
class RenderManager2D;

#include "runtime/native_exceptions.hpp"
#include "EngineBinaryHeader.hpp"
#include "EngineBinaryHeaderSerializer.hpp"
#include "EngineBinaryHeaderSerializer.hpp"
#include "FontAsset.hpp"
#include "runtime/native_exceptions.hpp"
#include "EngineBinaryReader.hpp"
#include "EngineBinaryReader.hpp"
#include "FontInfo.hpp"
#include "TextureAsset.hpp"
#include "runtime/native_dictionary.hpp"
#include "RuntimeTexture.hpp"
#include "Core.hpp"
#include "Core.hpp"
#include "RenderManager2D.hpp"
#include "FontAsset.hpp"
#include "EditorBinaryRecordKind.hpp"
#include "system/io/stream.hpp"

class FontAssetBinarySerializer
{
public:
    virtual ~FontAssetBinarySerializer() = default;

    static uint8_t CurrentVersion;

    static uint16_t FormatId;

    static ::EditorBinaryRecordKind RecordKind;

    static ::FontAsset* Deserialize(::Stream* stream);

    static ::FontAsset* Deserialize(::Stream* stream, ::EngineBinaryHeader* header);
private:
    static void ValidateHeader(::EngineBinaryHeader* header);
};
