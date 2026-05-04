#ifdef DrawText
#undef DrawText
#endif
#include "ShaderCompileDiagnostic.hpp"
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

int32_t ShaderCompileDiagnostic::get_Column()
{
return this->Column;
}

std::string ShaderCompileDiagnostic::get_FilePath()
{
return this->FilePath;
}

int32_t ShaderCompileDiagnostic::get_Line()
{
return this->Line;
}

std::string ShaderCompileDiagnostic::get_Message()
{
return this->Message;
}

::ShaderDiagnosticSeverity ShaderCompileDiagnostic::get_Severity()
{
return this->Severity;
}

ShaderCompileDiagnostic::ShaderCompileDiagnostic(::ShaderDiagnosticSeverity severity, std::string message, std::string filePath, int32_t line, int32_t column) : Column(0), FilePath(), Line(0), Message(), Severity()
{
    if (String::IsNullOrWhiteSpace(message))
    {
throw ([&]() {
auto __ctor_arg_000000FA = "Diagnostic message must be provided.";
auto __ctor_arg_000000FB = "message";
return new ArgumentException(__ctor_arg_000000FA, __ctor_arg_000000FB);
})();
    }
    if (String::IsNullOrEmpty(filePath))
    {
throw new ArgumentNullException("filePath");
    }
    if (line < 0)
    {
throw ([&]() {
auto __ctor_arg_000000FC = "line";
auto __ctor_arg_000000FD = "Line cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_000000FC, __ctor_arg_000000FD);
})();
    }
    if (column < 0)
    {
throw ([&]() {
auto __ctor_arg_000000FE = "column";
auto __ctor_arg_000000FF = "Column cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_000000FE, __ctor_arg_000000FF);
})();
    }
this->Severity = severity;
this->Message = message;
this->FilePath = filePath;
this->Line = line;
this->Column = column;
}

