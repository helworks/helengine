#ifdef DrawText
#undef DrawText
#endif
#include "ShaderCompiledBinary.hpp"
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
#include "system/text/string-builder.hpp"

Array<uint8_t>* ShaderCompiledBinary::get_Bytecode()
{
return this->Bytecode;
}

std::string ShaderCompiledBinary::get_EntryPoint()
{
return this->EntryPoint;
}

::ShaderStage ShaderCompiledBinary::get_Stage()
{
return this->Stage;
}

::ShaderCompileTarget ShaderCompiledBinary::get_Target()
{
return this->Target;
}

std::string ShaderCompiledBinary::get_Variant()
{
return this->Variant;
}

ShaderCompiledBinary::ShaderCompiledBinary(::ShaderCompileTarget target, ::ShaderStage stage, std::string entryPoint, std::string variant, Array<uint8_t>* bytecode) : Bytecode(), EntryPoint(), Stage(), Target(), Variant()
{
    if (String::IsNullOrWhiteSpace(entryPoint))
    {
throw ([&]() {
auto __ctor_arg_2082ad11 = "Entry point must be provided.";
auto __ctor_arg_e8a8906b = "entryPoint";
return new ArgumentException(__ctor_arg_2082ad11, __ctor_arg_e8a8906b);
})();
    }
    if (String::IsNullOrWhiteSpace(variant))
    {
throw ([&]() {
auto __ctor_arg_7e626ebc = "Variant name must be provided.";
auto __ctor_arg_eece8dfc = "variant";
return new ArgumentException(__ctor_arg_7e626ebc, __ctor_arg_eece8dfc);
})();
    }
    if (bytecode == nullptr)
    {
throw new ArgumentNullException("bytecode");
    }
    if (bytecode->Length == 0)
    {
throw ([&]() {
auto __ctor_arg_89f82c57 = "Bytecode payload must be provided.";
auto __ctor_arg_193d24c4 = "bytecode";
return new ArgumentException(__ctor_arg_89f82c57, __ctor_arg_193d24c4);
})();
    }
this->Target = target;
this->Stage = stage;
this->EntryPoint = entryPoint;
this->Variant = variant;
this->Bytecode = bytecode;
}

