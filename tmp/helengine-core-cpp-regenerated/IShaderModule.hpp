#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderModuleDefinition;

#include "ShaderModuleDefinition.hpp"
#include "runtime/native_string.hpp"

class IShaderModule
{
public:
    virtual ::ShaderModuleDefinition* BuildDefinition(std::string moduleRoot) = 0;
};
