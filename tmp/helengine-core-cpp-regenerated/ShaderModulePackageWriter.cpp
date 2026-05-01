#ifdef DrawText
#undef DrawText
#endif
#include "ShaderModulePackageWriter.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/path.hpp"
#include "Directory.hpp"
#include "ShaderAsset.hpp"
#include "system/io/file-stream.hpp"
#include "FileMode.hpp"
#include "FileAccess.hpp"
#include "FileShare.hpp"
#include "AssetSerializer.hpp"
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
#include "system/io/directory.hpp"
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

void ShaderModulePackageWriter::Write(std::string packagePath, ::ShaderModuleDefinition* definition, ::ShaderCompileTarget target)
{
    if (String::IsNullOrWhiteSpace(packagePath))
    {
throw ([&]() {
auto __ctor_arg_f97699d7 = "Package path must be provided.";
auto __ctor_arg_6c8108ae = "packagePath";
return new ArgumentException(__ctor_arg_f97699d7, __ctor_arg_6c8108ae);
})();
    }
    if (definition == nullptr)
    {
throw new ArgumentNullException("definition");
    }
const std::string directory = Path::GetDirectoryName(packagePath);
    if (String::IsNullOrWhiteSpace(directory))
    {
throw new InvalidOperationException("Package directory could not be resolved.");
    }
Directory::CreateDirectory(directory);
::ShaderAsset *asset = ShaderAsset::FromDefinition(definition, target);
{
FileStream *stream = new FileStream(packagePath, FileMode::Create, FileAccess::Write, FileShare::None);
AssetSerializer::Serialize(stream, asset);
if (stream != nullptr) {
stream->Dispose();
}
}
}

