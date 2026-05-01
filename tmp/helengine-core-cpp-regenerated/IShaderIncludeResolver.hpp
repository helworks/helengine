#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderIncludeResult;

#include "ShaderIncludeResult.hpp"
#include "runtime/native_string.hpp"

class IShaderIncludeResolver
{
public:
    virtual ::ShaderIncludeResult* Resolve(std::string requestingFile, std::string includePath) = 0;
};
