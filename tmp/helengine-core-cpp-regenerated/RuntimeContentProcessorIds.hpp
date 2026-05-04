#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_string.hpp"

class RuntimeContentProcessorIds
{
public:
    virtual ~RuntimeContentProcessorIds() = default;

    static std::string FontAsset;

    static std::string MaterialAsset;

    static std::string ModelAsset;

    static std::string SceneAsset;

    static std::string ShaderAsset;

    static std::string TextAsset;

    static std::string TextureAsset;
};
