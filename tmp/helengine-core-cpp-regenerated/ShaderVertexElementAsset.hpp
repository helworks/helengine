#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderVertexElement;

#include "ShaderVertexElement.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "ShaderVertexElement.hpp"

class ShaderVertexElementAsset
{
public:
    ShaderVertexElementAsset();

    std::string Format;

    int32_t Index;

    std::string Semantic;

    static ::ShaderVertexElementAsset* FromVertexElement(::ShaderVertexElement* element);

    ::ShaderVertexElement* ToVertexElement();
private:
    void Validate();
};
