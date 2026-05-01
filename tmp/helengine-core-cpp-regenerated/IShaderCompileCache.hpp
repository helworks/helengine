#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderCompileCacheKey;
class ShaderCompileResult;

#include "ShaderCompileCacheKey.hpp"
#include "ShaderCompileResult.hpp"

class IShaderCompileCache
{
public:
    virtual void Store(::ShaderCompileCacheKey* key, ::ShaderCompileResult* result) = 0;

    virtual bool TryGet(::ShaderCompileCacheKey* key, ::ShaderCompileResult*& result) = 0;
};
