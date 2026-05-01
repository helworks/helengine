#ifdef DrawText
#undef DrawText
#endif
#include "ShaderCompileResult.hpp"
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
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"

::ShaderCompiledBinary* ShaderCompileResult::get_Binary()
{
return this->Binary;
}

List<::ShaderCompileDiagnostic*>* ShaderCompileResult::get_Diagnostics()
{
return this->Diagnostics;
}

::ShaderProgramDefinition* ShaderCompileResult::get_ProgramDefinition()
{
return this->ProgramDefinition;
}

::ShaderCompileRequest* ShaderCompileResult::get_Request()
{
return this->Request;
}

bool ShaderCompileResult::get_Success()
{
return this->Success;
}

ShaderCompileResult::ShaderCompileResult(::ShaderCompileRequest* request, ::ShaderProgramDefinition* programDefinition, ::ShaderCompiledBinary* binary, List<::ShaderCompileDiagnostic*>* diagnostics, bool success) : Binary(), Diagnostics(), ProgramDefinition(), Request(), Success()
{
    if (request == nullptr)
    {
throw new ArgumentNullException("request");
    }
    if (programDefinition == nullptr)
    {
throw new ArgumentNullException("programDefinition");
    }
    if (binary == nullptr)
    {
throw new ArgumentNullException("binary");
    }
    if (diagnostics == nullptr)
    {
throw new ArgumentNullException("diagnostics");
    }
this->Request = request;
this->ProgramDefinition = programDefinition;
this->Binary = binary;
this->Diagnostics = diagnostics;
this->Success = success;
}

