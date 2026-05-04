#ifdef DrawText
#undef DrawText
#endif
#include "RuntimeSceneAssetReferenceResolver.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_string.hpp"
#include "ModelAsset.hpp"
#include "ContentManager.hpp"
#include "RuntimeModel.hpp"
#include "MaterialAsset.hpp"
#include "ShaderAsset.hpp"
#include "RuntimeMaterial.hpp"
#include "FontAsset.hpp"
#include "system/io/path.hpp"
#include "RuntimeContentProcessorIds.hpp"
#include "Core.hpp"
#include "SceneAssetReferenceSourceKind.hpp"
#include "ShaderTargetNames.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_cast.hpp"
#include "runtime/native_datetime.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_event.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_nullable.hpp"
#include "runtime/native_span.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/guid.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream-reader.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

::FontAsset* RuntimeSceneAssetReferenceResolver::ResolveFont(::SceneAssetReference* reference)
{
    if (reference == nullptr)
    {
throw new ArgumentNullException("reference");
    }
const std::string fullPath = this->ResolveFileBackedAssetPath(reference);
return this->AssetContentManager->Load<FontAsset*>(fullPath, RuntimeContentProcessorIds::FontAsset);}

::RuntimeMaterial* RuntimeSceneAssetReferenceResolver::ResolveMaterial(::SceneAssetReference* reference)
{
    if (reference == nullptr)
    {
throw new ArgumentNullException("reference");
    }
const std::string fullPath = this->ResolveFileBackedAssetPath(reference);
::MaterialAsset *materialAsset = this->AssetContentManager->Load<MaterialAsset*>(fullPath, RuntimeContentProcessorIds::MaterialAsset);
::ShaderAsset *shaderAsset = this->AssetContentManager->Load<ShaderAsset*>(this->ResolveShaderPackagePath(materialAsset->ShaderAssetId), RuntimeContentProcessorIds::ShaderAsset);
return Core::get_Instance()->get_RenderManager3D()->BuildMaterialFromRaw(materialAsset, shaderAsset);}

::RuntimeModel* RuntimeSceneAssetReferenceResolver::ResolveModel(::SceneAssetReference* reference)
{
    if (reference == nullptr)
    {
throw new ArgumentNullException("reference");
    }
const std::string fullPath = this->ResolveFileBackedAssetPath(reference);
::ModelAsset *modelAsset = this->AssetContentManager->Load<ModelAsset*>(fullPath, RuntimeContentProcessorIds::ModelAsset);
return Core::get_Instance()->get_RenderManager3D()->BuildModelFromRaw(modelAsset);}

RuntimeSceneAssetReferenceResolver::RuntimeSceneAssetReferenceResolver(::ContentManager* assetContentManager, std::string contentRootPath, ::ShaderCompileTarget shaderTarget) : AssetContentManager(), ContentRootPath(), ShaderTarget()
{
    if (assetContentManager == nullptr)
    {
throw new ArgumentNullException("assetContentManager");
    }
    if (String::IsNullOrWhiteSpace(contentRootPath))
    {
throw ([&]() {
auto __ctor_arg_000000CE = "Content root path must be provided.";
auto __ctor_arg_000000CF = "contentRootPath";
return new ArgumentException(__ctor_arg_000000CE, __ctor_arg_000000CF);
})();
    }
this->ContentRootPath = Path::GetFullPath(contentRootPath);
this->AssetContentManager = assetContentManager;
this->ShaderTarget = shaderTarget;
}

std::string RuntimeSceneAssetReferenceResolver::FontDirectoryName = "fonts";

std::string RuntimeSceneAssetReferenceResolver::ShaderDirectoryName = "cooked/shaders";

std::string RuntimeSceneAssetReferenceResolver::ShaderPackageExtension = ".hasset";

std::string RuntimeSceneAssetReferenceResolver::EnsureTrailingDirectorySeparator(std::string path)
{
    if (String::EndsWith(path, Path::DirectorySeparatorChar) || String::EndsWith(path, Path::AltDirectorySeparatorChar))
    {
return path;    }
return String::Concat(path, Path::DirectorySeparatorChar);}

std::string RuntimeSceneAssetReferenceResolver::ResolveFileBackedAssetPath(::SceneAssetReference* reference)
{
    if (reference->get_SourceKind() != SceneAssetReferenceSourceKind::FileSystem)
    {
throw new InvalidOperationException("Player builds currently require file-backed packaged scene references.");
    }
    if (String::IsNullOrWhiteSpace(reference->get_RelativePath()))
    {
throw new InvalidOperationException("Packaged scene asset references must include a relative path.");
    }
const std::string fullPath = Path::GetFullPath(Path::Combine(this->ContentRootPath, reference->get_RelativePath()));
const std::string contentRootPrefix = this->EnsureTrailingDirectorySeparator(this->ContentRootPath);
    if (!String::StartsWith(fullPath, contentRootPrefix, StringComparison::OrdinalIgnoreCase))
    {
throw new InvalidOperationException("Packaged scene asset reference path must stay inside the content root.");
    }
return fullPath;}

std::string RuntimeSceneAssetReferenceResolver::ResolveShaderPackagePath(std::string shaderAssetId)
{
    if (String::IsNullOrWhiteSpace(shaderAssetId))
    {
throw new InvalidOperationException("Packaged material assets must include a shader asset id.");
    }
const std::string fileName = String::Concat(shaderAssetId, ".", ShaderTargetNames::GetTargetName(this->ShaderTarget), ShaderPackageExtension);
return Path::Combine(this->ContentRootPath, ShaderDirectoryName, fileName);}

