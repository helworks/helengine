#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_list.hpp"
#include "runtime/native_string.hpp"
#include "runtime/array.hpp"

class ShaderVariant
{
public:
    virtual ~ShaderVariant() = default;

    List<std::string>* get_Defines();

    std::string Name;

    std::string get_Name();

    ShaderVariant(std::string name, Array<std::string>* defines);
private:
    Array<std::string>* defines;
};
