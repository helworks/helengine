#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_enum.hpp"
#include "IComparable.hpp"
#include "IConvertible.hpp"
#include "ISpanFormattable.hpp"
#include "IFormattable.hpp"

enum class EditorBinaryRecordKind
{
    Asset = 1,
    AssetImportSettings = 2,
    ShaderCacheMetadata = 3,
    FontAsset = 4
};
