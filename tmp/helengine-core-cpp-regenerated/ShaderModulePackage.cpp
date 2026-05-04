#ifdef DrawText
#undef DrawText
#endif
#include "ShaderModulePackage.hpp"
#include "ShaderProgramBinary.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/path.hpp"
#include "runtime/array.hpp"
#include "ContentManager.hpp"
#include "RawByteContent.hpp"
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
#include "runtime/native_stack.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/diagnostics/stopwatch.hpp"
#include "system/guid.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream-reader.hpp"
#include "system/io/stream.hpp"
#include "system/io/string-reader.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

::ShaderModuleDefinition* ShaderModulePackage::get_Definition()
{
return this->Definition;
}

std::string ShaderModulePackage::get_RootPath()
{
return this->RootPath;
}

::ShaderCompileTarget ShaderModulePackage::get_Target()
{
return this->Target;
}

::ShaderProgramBinary* ShaderModulePackage::GetBinary(std::string programName, std::string variant)
{
return this->Definition->GetBinary(programName, this->targetName, variant);}

Array<uint8_t>* ShaderModulePackage::GetBinaryData(std::string programName, std::string variant)
{
::ShaderProgramBinary *binary = this->GetBinary(programName, variant);
    if (binary->get_Bytecode() != nullptr && binary->get_Bytecode()->Length > 0)
    {
return binary->get_Bytecode();    }
const std::string path = this->GetBinaryPath(programName, variant);
::ContentManager *contentManager = new ::ContentManager(this->RootPath);
::RawByteContent *binaryContent = contentManager->Load<RawByteContent*>(path, nullptr);
return binaryContent->get_Bytes();}

std::string ShaderModulePackage::GetBinaryPath(std::string programName, std::string variant)
{
::ShaderProgramBinary *binary = this->GetBinary(programName, variant);
    if (String::IsNullOrWhiteSpace(binary->get_Path()))
    {
throw new InvalidOperationException("The shader binary does not include a file path.");
    }
    if (Path::IsPathRooted(binary->get_Path()))
    {
return binary->get_Path();    }
return Path::Combine(this->RootPath, binary->get_Path());}

ShaderModulePackage::ShaderModulePackage(::ShaderCompileTarget target, std::string rootPath, ::ShaderModuleDefinition* definition) : Definition(), RootPath(), Target(), targetName()
{
    if (String::IsNullOrWhiteSpace(rootPath))
    {
throw ([&]() {
auto __ctor_arg_00000144 = "Package root path must be provided.";
auto __ctor_arg_00000145 = "rootPath";
return new ArgumentException(__ctor_arg_00000144, __ctor_arg_00000145);
})();
    }
    if (definition == nullptr)
    {
throw new ArgumentNullException("definition");
    }
this->Target = target;
this->RootPath = rootPath;
this->Definition = definition;
this->targetName = ShaderTargetNames::GetTargetName(target);
}

