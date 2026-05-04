#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_string.hpp"

class ShaderVertexElement
{
public:
    virtual ~ShaderVertexElement() = default;

    std::string Format;

    std::string get_Format();

    int32_t Index;

    int32_t get_Index();

    std::string Semantic;

    std::string get_Semantic();

    ShaderVertexElement(std::string semantic, int32_t index, std::string format);
};
