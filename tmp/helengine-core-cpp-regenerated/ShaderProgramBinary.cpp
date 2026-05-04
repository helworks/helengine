#ifdef DrawText
#undef DrawText
#endif
#include "ShaderProgramBinary.hpp"
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

Array<uint8_t>* ShaderProgramBinary::get_Bytecode()
{
return this->bytecode;}

std::string ShaderProgramBinary::get_Path()
{
return this->Path;
}

std::string ShaderProgramBinary::get_ProgramName()
{
return this->ProgramName;
}

::ShaderStage ShaderProgramBinary::get_Stage()
{
return this->Stage;
}

std::string ShaderProgramBinary::get_Target()
{
return this->Target;
}

std::string ShaderProgramBinary::get_Variant()
{
return this->Variant;
}

ShaderProgramBinary::ShaderProgramBinary(std::string programName, ::ShaderStage stage, std::string target, std::string variant, std::string path) : Path(), ProgramName(), Stage(), Target(), Variant(), bytecode()
{
    if (String::IsNullOrWhiteSpace(programName))
    {
throw ([&]() {
auto __ctor_arg_00000151 = "Program name must be provided.";
auto __ctor_arg_00000152 = "programName";
return new ArgumentException(__ctor_arg_00000151, __ctor_arg_00000152);
})();
    }
    if (String::IsNullOrWhiteSpace(target))
    {
throw ([&]() {
auto __ctor_arg_00000153 = "Target must be provided.";
auto __ctor_arg_00000154 = "target";
return new ArgumentException(__ctor_arg_00000153, __ctor_arg_00000154);
})();
    }
    if (String::IsNullOrWhiteSpace(variant))
    {
throw ([&]() {
auto __ctor_arg_00000155 = "Variant must be provided.";
auto __ctor_arg_00000156 = "variant";
return new ArgumentException(__ctor_arg_00000155, __ctor_arg_00000156);
})();
    }
    if (String::IsNullOrWhiteSpace(path))
    {
throw ([&]() {
auto __ctor_arg_00000157 = "Path must be provided.";
auto __ctor_arg_00000158 = "path";
return new ArgumentException(__ctor_arg_00000157, __ctor_arg_00000158);
})();
    }
this->ProgramName = programName;
this->Stage = stage;
this->Target = target;
this->Variant = variant;
this->Path = path;
this->bytecode = nullptr;
}

ShaderProgramBinary::ShaderProgramBinary(std::string programName, ::ShaderStage stage, std::string target, std::string variant, Array<uint8_t>* bytecode) : Path(), ProgramName(), Stage(), Target(), Variant(), bytecode()
{
    if (String::IsNullOrWhiteSpace(programName))
    {
throw ([&]() {
auto __ctor_arg_00000159 = "Program name must be provided.";
auto __ctor_arg_0000015A = "programName";
return new ArgumentException(__ctor_arg_00000159, __ctor_arg_0000015A);
})();
    }
    if (String::IsNullOrWhiteSpace(target))
    {
throw ([&]() {
auto __ctor_arg_0000015B = "Target must be provided.";
auto __ctor_arg_0000015C = "target";
return new ArgumentException(__ctor_arg_0000015B, __ctor_arg_0000015C);
})();
    }
    if (String::IsNullOrWhiteSpace(variant))
    {
throw ([&]() {
auto __ctor_arg_0000015D = "Variant must be provided.";
auto __ctor_arg_0000015E = "variant";
return new ArgumentException(__ctor_arg_0000015D, __ctor_arg_0000015E);
})();
    }
    if (bytecode == nullptr)
    {
throw new ArgumentNullException("bytecode");
    }
    if (bytecode->Length == 0)
    {
throw ([&]() {
auto __ctor_arg_0000015F = "Bytecode payload must be provided.";
auto __ctor_arg_00000160 = "bytecode";
return new ArgumentException(__ctor_arg_0000015F, __ctor_arg_00000160);
})();
    }
this->ProgramName = programName;
this->Stage = stage;
this->Target = target;
this->Variant = variant;
this->Path = String::Empty;
this->bytecode = bytecode;
}

