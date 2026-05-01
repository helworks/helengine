#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderAsset;
class AssetSerializer;
class ShaderModuleDefinition;

#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/path.hpp"
#include "system/io/path.hpp"
#include "runtime/native_exceptions.hpp"
#include "Directory.hpp"
#include "Directory.hpp"
#include "ShaderAsset.hpp"
#include "ShaderAsset.hpp"
#include "system/io/file-stream.hpp"
#include "FileMode.hpp"
#include "FileMode.hpp"
#include "FileAccess.hpp"
#include "FileAccess.hpp"
#include "FileShare.hpp"
#include "FileShare.hpp"
#include "AssetSerializer.hpp"
#include "AssetSerializer.hpp"
#include "ShaderModuleDefinition.hpp"
#include "ShaderCompileTarget.hpp"

class ShaderModulePackageWriter
{
public:
    void Write(std::string packagePath, ::ShaderModuleDefinition* definition, ::ShaderCompileTarget target);
};
