#ifdef DrawText
#undef DrawText
#endif
#include "ShaderCompileRequest.hpp"
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
#include "system/diagnostics/stopwatch.hpp"
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

List<::ShaderDefine*>* ShaderCompileRequest::get_Defines()
{
return this->Defines;
}

std::string ShaderCompileRequest::get_EntryPoint()
{
return this->EntryPoint;
}

::ShaderCompileOptions* ShaderCompileRequest::get_Options()
{
return this->Options;
}

std::string ShaderCompileRequest::get_ProgramName()
{
return this->ProgramName;
}

::ShaderModel* ShaderCompileRequest::get_ShaderModel()
{
return this->ShaderModel;
}

::ShaderSourceInfo* ShaderCompileRequest::get_Source()
{
return this->Source;
}

::ShaderStage ShaderCompileRequest::get_Stage()
{
return this->Stage;
}

::ShaderCompileTarget ShaderCompileRequest::get_Target()
{
return this->Target;
}

std::string ShaderCompileRequest::get_Variant()
{
return this->Variant;
}

ShaderCompileRequest::ShaderCompileRequest(::ShaderSourceInfo* source, std::string programName, std::string entryPoint, ::ShaderStage stage, ::ShaderCompileTarget target, ::ShaderModel* shaderModel, std::string variant, List<::ShaderDefine*>* defines, ::ShaderCompileOptions* options) : Defines(), EntryPoint(), Options(), ProgramName(), ShaderModel(), Source(), Stage(), Target(), Variant()
{
    if (source == nullptr)
    {
throw new ArgumentNullException("source");
    }
    if (String::IsNullOrWhiteSpace(programName))
    {
throw ([&]() {
auto __ctor_arg_00000100 = "Program name must be provided.";
auto __ctor_arg_00000101 = "programName";
return new ArgumentException(__ctor_arg_00000100, __ctor_arg_00000101);
})();
    }
    if (String::IsNullOrWhiteSpace(entryPoint))
    {
throw ([&]() {
auto __ctor_arg_00000102 = "Entry point must be provided.";
auto __ctor_arg_00000103 = "entryPoint";
return new ArgumentException(__ctor_arg_00000102, __ctor_arg_00000103);
})();
    }
    if (shaderModel == nullptr)
    {
throw new ArgumentNullException("shaderModel");
    }
    if (String::IsNullOrWhiteSpace(variant))
    {
throw ([&]() {
auto __ctor_arg_00000104 = "Variant name must be provided.";
auto __ctor_arg_00000105 = "variant";
return new ArgumentException(__ctor_arg_00000104, __ctor_arg_00000105);
})();
    }
    if (defines == nullptr)
    {
throw new ArgumentNullException("defines");
    }
    if (options == nullptr)
    {
throw new ArgumentNullException("options");
    }
this->Source = source;
this->ProgramName = programName;
this->EntryPoint = entryPoint;
this->Stage = stage;
this->Target = target;
this->ShaderModel = shaderModel;
this->Variant = variant;
this->Defines = defines;
this->Options = options;
}

