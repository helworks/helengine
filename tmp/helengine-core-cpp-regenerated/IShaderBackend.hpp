#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderBackendCapabilities;
class ShaderCompileResult;
class ShaderCompileRequest;
class IShaderIncludeResolver;

#include "ShaderBackendCapabilities.hpp"
#include "ShaderCompileTarget.hpp"
#include "ShaderCompileResult.hpp"
#include "ShaderCompileRequest.hpp"
#include "IShaderIncludeResolver.hpp"

class IShaderBackend
{
public:
    virtual ::ShaderBackendCapabilities* get_Capabilities() = 0;

    virtual ::ShaderCompileTarget get_Target() = 0;

    virtual ::ShaderCompileResult* Compile(::ShaderCompileRequest* request, ::IShaderIncludeResolver* includeResolver) = 0;
};
