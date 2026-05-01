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

enum class EditorAssetBinaryValueKind
{
    TextureAsset = 1,
    ModelAsset = 2,
    ShaderAsset = 3,
    TextAsset = 4,
    MaterialAsset = 5,
    SceneAsset = 6
};
