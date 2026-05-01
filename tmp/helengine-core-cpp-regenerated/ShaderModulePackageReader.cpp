#ifdef DrawText
#undef DrawText
#endif
#include "ShaderModulePackageReader.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/path.hpp"
#include "ShaderAsset.hpp"
#include "ShaderCompileTarget.hpp"
#include "ShaderTargetNames.hpp"
#include "ShaderModuleDefinition.hpp"
#include "ShaderModulePackage.hpp"
#include "ContentManager.hpp"
#include "AssetContentProcessor.hpp"
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
#include "runtime/native_stack.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/io/string-reader.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

::ShaderModulePackage* ShaderModulePackageReader::Read(std::string packagePath)
{
    if (String::IsNullOrWhiteSpace(packagePath))
    {
throw ([&]() {
auto __ctor_arg_f70798d3 = "Package path must be provided.";
auto __ctor_arg_e8822243 = "packagePath";
return new ArgumentException(__ctor_arg_f70798d3, __ctor_arg_e8822243);
})();
    }
    if (!File::Exists(packagePath))
    {
throw new FileNotFoundException("Shader package was not found.", packagePath);
    }
const std::string directory = Path::GetDirectoryName(packagePath);
    if (String::IsNullOrWhiteSpace(directory))
    {
throw new InvalidOperationException("Package directory could not be resolved.");
    }
::ShaderAsset *asset = this->ReadAsset(packagePath);
::ShaderCompileTarget target = ShaderTargetNames::ParseTarget(asset->TargetName);
::ShaderModuleDefinition *definition = asset->BuildDefinition();
return new ::ShaderModulePackage(target, directory, definition);}

ShaderModulePackageReader::ShaderModulePackageReader(std::string rootDirectory) : PackageContentManager()
{
    if (String::IsNullOrWhiteSpace(rootDirectory))
    {
throw ([&]() {
auto __ctor_arg_4eabeca7 = "Root directory must be provided.";
auto __ctor_arg_4d9bb497 = "rootDirectory";
return new ArgumentException(__ctor_arg_4eabeca7, __ctor_arg_4d9bb497);
})();
    }
this->PackageContentManager = new ::ContentManager(rootDirectory);
this->PackageContentManager->RegisterProcessor<ShaderAsset*>("shader-package", new ::AssetContentProcessor_1<::ShaderAsset*>(), new Array<std::string>({ ".shader.asset" }));
}

::ShaderAsset* ShaderModulePackageReader::ReadAsset(std::string packagePath)
{
return this->PackageContentManager->Load<ShaderAsset*>(packagePath, "shader-package");}

