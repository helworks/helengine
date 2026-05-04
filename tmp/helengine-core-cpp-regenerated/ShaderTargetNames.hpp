#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_string.hpp"
#include "ShaderCompileTarget.hpp"
#include "runtime/native_exceptions.hpp"
#include "ShaderCompileTarget.hpp"

class ShaderTargetNames
{
public:
    virtual ~ShaderTargetNames() = default;

    static std::string GetTargetName(::ShaderCompileTarget target);

    static ::ShaderCompileTarget ParseTarget(std::string name);

    static bool TryParseTarget(std::string name, ::ShaderCompileTarget& target);
};
