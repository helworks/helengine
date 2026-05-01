#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ModelAsset;
class ContentManager;
class RuntimeModel;
class MaterialAsset;
class ShaderAsset;
class RuntimeMaterial;
class SceneAssetReference;

#include "runtime/native_exceptions.hpp"
#include "runtime/native_string.hpp"
#include "ModelAsset.hpp"
#include "ContentManager.hpp"
#include "RuntimeModel.hpp"
#include "MaterialAsset.hpp"
#include "ShaderAsset.hpp"
#include "RuntimeMaterial.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/path.hpp"
#include "system/io/path.hpp"
#include "ContentManager.hpp"
#include "ShaderCompileTarget.hpp"
#include "RuntimeMaterial.hpp"
#include "SceneAssetReference.hpp"
#include "RuntimeModel.hpp"

class RuntimeSceneAssetReferenceResolver
{
public:
    ::RuntimeMaterial* ResolveMaterial(::SceneAssetReference* reference);

    ::RuntimeModel* ResolveModel(::SceneAssetReference* reference);

    RuntimeSceneAssetReferenceResolver(::ContentManager* assetContentManager, std::string contentRootPath, ::ShaderCompileTarget shaderTarget);
private:
    static std::string ShaderDirectoryName;

    static std::string ShaderPackageExtension;

    ::ContentManager* AssetContentManager;

    std::string ContentRootPath;

    ::ShaderCompileTarget ShaderTarget;

    std::string EnsureTrailingDirectorySeparator(std::string path);

    std::string ResolveFileBackedAssetPath(::SceneAssetReference* reference);

    std::string ResolveShaderPackagePath(std::string shaderAssetId);
};
