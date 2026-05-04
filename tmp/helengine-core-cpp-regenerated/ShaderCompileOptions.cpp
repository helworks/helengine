#ifdef DrawText
#undef DrawText
#endif
#include "ShaderCompileOptions.hpp"
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

::ShaderBindingPolicy* ShaderCompileOptions::get_BindingPolicy()
{
return this->BindingPolicy;
}

bool ShaderCompileOptions::get_GenerateDebugInfo()
{
return this->GenerateDebugInfo;
}

bool ShaderCompileOptions::get_Optimize()
{
return this->Optimize;
}

bool ShaderCompileOptions::get_TreatWarningsAsErrors()
{
return this->TreatWarningsAsErrors;
}

ShaderCompileOptions::ShaderCompileOptions(::ShaderBindingPolicy* bindingPolicy, bool generateDebugInfo, bool optimize, bool treatWarningsAsErrors) : BindingPolicy(), GenerateDebugInfo(), Optimize(), TreatWarningsAsErrors()
{
    if (bindingPolicy == nullptr)
    {
throw new ArgumentNullException("bindingPolicy");
    }
this->BindingPolicy = bindingPolicy;
this->GenerateDebugInfo = generateDebugInfo;
this->Optimize = optimize;
this->TreatWarningsAsErrors = treatWarningsAsErrors;
}

