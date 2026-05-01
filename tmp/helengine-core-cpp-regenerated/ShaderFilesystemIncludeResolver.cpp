#ifdef DrawText
#undef DrawText
#endif
#include "ShaderFilesystemIncludeResolver.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "TextContent.hpp"
#include "ContentManager.hpp"
#include "ShaderIncludeResult.hpp"
#include "system/io/path.hpp"
#include "system/io/file.hpp"
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
#include "system/binary_primitives.hpp"
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
#include "system/security/cryptography/sha256.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

::ShaderIncludeResult* ShaderFilesystemIncludeResolver::Resolve(std::string requestingFile, std::string includePath)
{
    if (String::IsNullOrWhiteSpace(requestingFile))
    {
throw ([&]() {
auto __ctor_arg_de537904 = "Requesting file must be provided.";
auto __ctor_arg_8daafa74 = "requestingFile";
return new ArgumentException(__ctor_arg_de537904, __ctor_arg_8daafa74);
})();
    }
    if (String::IsNullOrWhiteSpace(includePath))
    {
throw ([&]() {
auto __ctor_arg_add6f306 = "Include path must be provided.";
auto __ctor_arg_120bc322 = "includePath";
return new ArgumentException(__ctor_arg_add6f306, __ctor_arg_120bc322);
})();
    }
const std::string resolvedPath = this->ResolvePath(requestingFile, includePath);
::TextContent *sourceContent = this->IncludeContentManager->Load<TextContent*>(resolvedPath, nullptr);
return new ::ShaderIncludeResult(resolvedPath, sourceContent->get_Text());}

ShaderFilesystemIncludeResolver::ShaderFilesystemIncludeResolver(std::string rootDirectory) : IncludeContentManager(), rootDirectory()
{
    if (String::IsNullOrWhiteSpace(rootDirectory))
    {
throw ([&]() {
auto __ctor_arg_98f8161f = "Root directory must be provided.";
auto __ctor_arg_f328c19c = "rootDirectory";
return new ArgumentException(__ctor_arg_98f8161f, __ctor_arg_f328c19c);
})();
    }
    if (!Directory::Exists(rootDirectory))
    {
throw new DirectoryNotFoundException("Include root directory does not exist.");
    }
this->rootDirectory = rootDirectory;
this->IncludeContentManager = new ::ContentManager(rootDirectory);
}

std::string ShaderFilesystemIncludeResolver::ResolvePath(std::string requestingFile, std::string includePath)
{
    if (Path::IsPathRooted(includePath))
    {
    if (!File::Exists(includePath))
    {
throw new FileNotFoundException("Include file does not exist.", includePath);
    }
return includePath;    }
const std::string requestingDirectory = Path::GetDirectoryName(requestingFile);
    if (String::IsNullOrWhiteSpace(requestingDirectory))
    {
throw new InvalidOperationException("Requesting file path does not include a directory.");
    }
const std::string candidate = Path::Combine(requestingDirectory, includePath);
    if (File::Exists(candidate))
    {
return candidate;    }
const std::string rootedCandidate = Path::Combine(this->rootDirectory, includePath);
    if (File::Exists(rootedCandidate))
    {
return rootedCandidate;    }
throw new FileNotFoundException("Include file does not exist.", includePath);
}

