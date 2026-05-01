#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class IShaderCompileCache;
class ShaderCompileResult;
class ShaderCompileCacheKey;

#include "IShaderCompileCache.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_string.hpp"
#include "ShaderCompileResult.hpp"
#include "ShaderCompileCacheKey.hpp"

class ShaderMemoryCompileCache : public IShaderCompileCache
{
public:
    ShaderMemoryCompileCache();

    void Store(::ShaderCompileCacheKey* key, ::ShaderCompileResult* result);

    bool TryGet(::ShaderCompileCacheKey* key, ::ShaderCompileResult*& result);
private:
    Dictionary<std::string, ::ShaderCompileResult*>* cache;
};
