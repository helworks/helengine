#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_string.hpp"

class ShaderDefine
{
public:
    virtual ~ShaderDefine() = default;

    std::string Name;

    std::string get_Name();

    std::string Value;

    std::string get_Value();

    ShaderDefine(std::string name, std::string value);
};
