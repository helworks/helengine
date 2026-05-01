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
auto __ctor_arg_6f930cf8 = "Program name must be provided.";
auto __ctor_arg_f524b403 = "programName";
return new ArgumentException(__ctor_arg_6f930cf8, __ctor_arg_f524b403);
})();
    }
    if (String::IsNullOrWhiteSpace(target))
    {
throw ([&]() {
auto __ctor_arg_6ecede63 = "Target must be provided.";
auto __ctor_arg_389c65cb = "target";
return new ArgumentException(__ctor_arg_6ecede63, __ctor_arg_389c65cb);
})();
    }
    if (String::IsNullOrWhiteSpace(variant))
    {
throw ([&]() {
auto __ctor_arg_4453813b = "Variant must be provided.";
auto __ctor_arg_d1e09a6c = "variant";
return new ArgumentException(__ctor_arg_4453813b, __ctor_arg_d1e09a6c);
})();
    }
    if (String::IsNullOrWhiteSpace(path))
    {
throw ([&]() {
auto __ctor_arg_0b29a140 = "Path must be provided.";
auto __ctor_arg_def4db76 = "path";
return new ArgumentException(__ctor_arg_0b29a140, __ctor_arg_def4db76);
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
auto __ctor_arg_d7ceac2c = "Program name must be provided.";
auto __ctor_arg_032ad697 = "programName";
return new ArgumentException(__ctor_arg_d7ceac2c, __ctor_arg_032ad697);
})();
    }
    if (String::IsNullOrWhiteSpace(target))
    {
throw ([&]() {
auto __ctor_arg_922d042c = "Target must be provided.";
auto __ctor_arg_cb7f364b = "target";
return new ArgumentException(__ctor_arg_922d042c, __ctor_arg_cb7f364b);
})();
    }
    if (String::IsNullOrWhiteSpace(variant))
    {
throw ([&]() {
auto __ctor_arg_876ed86b = "Variant must be provided.";
auto __ctor_arg_0bfa1047 = "variant";
return new ArgumentException(__ctor_arg_876ed86b, __ctor_arg_0bfa1047);
})();
    }
    if (bytecode == nullptr)
    {
throw new ArgumentNullException("bytecode");
    }
    if (bytecode->Length == 0)
    {
throw ([&]() {
auto __ctor_arg_4c5986fe = "Bytecode payload must be provided.";
auto __ctor_arg_b023df70 = "bytecode";
return new ArgumentException(__ctor_arg_4c5986fe, __ctor_arg_b023df70);
})();
    }
this->ProgramName = programName;
this->Stage = stage;
this->Target = target;
this->Variant = variant;
this->Path = String::Empty;
this->bytecode = bytecode;
}

