#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class IShaderIncludeResolver;
class TextContent;
class ContentManager;
class ShaderIncludeResult;

#include "IShaderIncludeResolver.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "TextContent.hpp"
#include "ContentManager.hpp"
#include "ShaderIncludeResult.hpp"
#include "system/io/path.hpp"
#include "system/io/path.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/file.hpp"
#include "system/io/file.hpp"
#include "ContentManager.hpp"
#include "ShaderIncludeResult.hpp"

class ShaderFilesystemIncludeResolver : public IShaderIncludeResolver
{
public:
    virtual ~ShaderFilesystemIncludeResolver() = default;

    ::ShaderIncludeResult* Resolve(std::string requestingFile, std::string includePath);

    ShaderFilesystemIncludeResolver(std::string rootDirectory);
private:
    ::ContentManager* IncludeContentManager;

    std::string rootDirectory;

    std::string ResolvePath(std::string requestingFile, std::string includePath);
};
