#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_string.hpp"

class RuntimeContentProcessorIds
{
public:
    static std::string MaterialAsset;

    static std::string ModelAsset;

    static std::string SceneAsset;

    static std::string ShaderAsset;

    static std::string TextAsset;

    static std::string TextureAsset;
};
