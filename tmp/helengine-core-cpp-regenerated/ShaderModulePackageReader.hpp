#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderAsset;
class ShaderTargetNames;
class ShaderModuleDefinition;
class ShaderModulePackage;
class ContentManager;

#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/path.hpp"
#include "system/io/path.hpp"
#include "runtime/native_exceptions.hpp"
#include "ShaderAsset.hpp"
#include "ShaderCompileTarget.hpp"
#include "ShaderTargetNames.hpp"
#include "ShaderTargetNames.hpp"
#include "ShaderModuleDefinition.hpp"
#include "ShaderAsset.hpp"
#include "ShaderModulePackage.hpp"
#include "ContentManager.hpp"
#include "ShaderModulePackage.hpp"

class ShaderModulePackageReader
{
public:
    virtual ~ShaderModulePackageReader() = default;

    ::ShaderModulePackage* Read(std::string packagePath);

    ShaderModulePackageReader(std::string rootDirectory);
private:
    ::ContentManager* PackageContentManager;

    ::ShaderAsset* ReadAsset(std::string packagePath);
};
