#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class IShaderBackend;
class ShaderCompileCacheKey;
class ShaderCompileResult;
class IShaderCompileCache;
class ContentManager;
class TextContent;
class ShaderSourceInfo;
class ShaderCompileRequest;
class ShaderSourceHasher;
class IShaderIncludeResolver;
class ShaderModel;
class ShaderDefine;
class ShaderCompileOptions;
class ShaderBindingPolicy;

#include "runtime/native_exceptions.hpp"
#include "IShaderBackend.hpp"
#include "ShaderCompileCacheKey.hpp"
#include "ShaderCompileResult.hpp"
#include "IShaderCompileCache.hpp"
#include "ShaderCompileResult.hpp"
#include "IShaderBackend.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/path.hpp"
#include "system/io/path.hpp"
#include "runtime/native_exceptions.hpp"
#include "ContentManager.hpp"
#include "TextContent.hpp"
#include "ContentManager.hpp"
#include "TextContent.hpp"
#include "ShaderSourceInfo.hpp"
#include "ShaderCompileRequest.hpp"
#include "runtime/native_dictionary.hpp"
#include "ShaderSourceHasher.hpp"
#include "ShaderCompileCacheKey.hpp"
#include "system/text/string-builder.hpp"
#include "runtime/native_dictionary.hpp"
#include "ShaderCompileTarget.hpp"
#include "IShaderCompileCache.hpp"
#include "IShaderIncludeResolver.hpp"
#include "ShaderSourceHasher.hpp"
#include "ShaderStage.hpp"
#include "ShaderModel.hpp"
#include "runtime/native_list.hpp"
#include "ShaderDefine.hpp"
#include "ShaderCompileOptions.hpp"
#include "ShaderBindingPolicy.hpp"

class ShaderCompileService
{
public:
    ::ShaderCompileResult* Compile(::ShaderCompileRequest* request);

    ::ShaderCompileResult* CompileFromFile(std::string path, std::string programName, std::string entryPoint, ::ShaderStage stage, ::ShaderCompileTarget target, ::ShaderModel* shaderModel, std::string variant, List<::ShaderDefine*>* defines, ::ShaderCompileOptions* options);

    void RegisterBackend(::IShaderBackend* backend);

    ShaderCompileService(::IShaderIncludeResolver* includeResolver, ::IShaderCompileCache* cache, ::ShaderSourceHasher* sourceHasher);
private:
    Dictionary<::ShaderCompileTarget, ::IShaderBackend*>* backends;

    ::IShaderCompileCache* cache;

    ::IShaderIncludeResolver* includeResolver;

    ::ShaderSourceHasher* sourceHasher;

    std::string BuildBindingPolicySignature(::ShaderBindingPolicy* policy);

    std::string BuildDefinesSignature(List<::ShaderDefine*>* defines);

    ::ShaderCompileCacheKey* CreateCacheKey(::ShaderCompileRequest* request);

    ::IShaderBackend* GetBackend(::ShaderCompileTarget target);
};
