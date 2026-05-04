#ifdef DrawText
#undef DrawText
#endif
#include "ShaderModuleDefinition.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "ShaderProgramBinary.hpp"
#include "ShaderProgramDefinition.hpp"
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

List<::ShaderProgramBinary*>* ShaderModuleDefinition::get_Binaries()
{
return new List<ShaderProgramBinary*>(this->binaries);}

std::string ShaderModuleDefinition::get_Name()
{
return this->Name;
}

List<::ShaderProgramDefinition*>* ShaderModuleDefinition::get_Programs()
{
return new List<ShaderProgramDefinition*>(this->programs);}

::ShaderProgramBinary* ShaderModuleDefinition::GetBinary(std::string programName, std::string target, std::string variant)
{
::ShaderProgramBinary *binary;
    if (this->TryGetBinary(programName, target, variant, binary))
    {
return binary;    }
throw new InvalidOperationException("No compiled shader binary was found for the requested selection.");
}

::ShaderProgramDefinition* ShaderModuleDefinition::GetProgram(std::string programName)
{
    if (String::IsNullOrWhiteSpace(programName))
    {
throw ([&]() {
auto __ctor_arg_00000136 = "Program name must be provided.";
auto __ctor_arg_00000137 = "programName";
return new ArgumentException(__ctor_arg_00000136, __ctor_arg_00000137);
})();
    }
for (int32_t i = 0; i < this->programs->Length; i++) {
::ShaderProgramDefinition *program = (*this->programs)[i];
    if (String::Equals(program->get_Name(), programName, StringComparison::Ordinal))
    {
return program;    }
}
throw new InvalidOperationException("No shader program was found for the requested name.");
}

ShaderModuleDefinition::ShaderModuleDefinition(std::string name, Array<::ShaderProgramDefinition*>* programs, Array<::ShaderProgramBinary*>* binaries) : Name(), binaries(), programs()
{
    if (String::IsNullOrWhiteSpace(name))
    {
throw ([&]() {
auto __ctor_arg_00000138 = "Module name must be provided.";
auto __ctor_arg_00000139 = "name";
return new ArgumentException(__ctor_arg_00000138, __ctor_arg_00000139);
})();
    }
    if (programs == nullptr)
    {
throw new ArgumentNullException("programs");
    }
    if (programs->Length == 0)
    {
throw ([&]() {
auto __ctor_arg_0000013A = "At least one program definition is required.";
auto __ctor_arg_0000013B = "programs";
return new ArgumentException(__ctor_arg_0000013A, __ctor_arg_0000013B);
})();
    }
    if (binaries == nullptr)
    {
throw new ArgumentNullException("binaries");
    }
this->Name = name;
this->programs = programs;
this->binaries = binaries;
}

bool ShaderModuleDefinition::TryGetBinary(std::string programName, std::string target, std::string variant, ::ShaderProgramBinary*& binary)
{
    if (String::IsNullOrWhiteSpace(programName))
    {
throw ([&]() {
auto __ctor_arg_0000013C = "Program name must be provided.";
auto __ctor_arg_0000013D = "programName";
return new ArgumentException(__ctor_arg_0000013C, __ctor_arg_0000013D);
})();
    }
    if (String::IsNullOrWhiteSpace(target))
    {
throw ([&]() {
auto __ctor_arg_0000013E = "Target must be provided.";
auto __ctor_arg_0000013F = "target";
return new ArgumentException(__ctor_arg_0000013E, __ctor_arg_0000013F);
})();
    }
    if (String::IsNullOrWhiteSpace(variant))
    {
throw ([&]() {
auto __ctor_arg_00000140 = "Variant must be provided.";
auto __ctor_arg_00000141 = "variant";
return new ArgumentException(__ctor_arg_00000140, __ctor_arg_00000141);
})();
    }
for (int32_t i = 0; i < this->binaries->Length; i++) {
::ShaderProgramBinary *candidate = (*this->binaries)[i];
    if (!String::Equals(candidate->get_ProgramName(), programName, StringComparison::Ordinal))
    {
continue;
    }
    if (!String::Equals(candidate->get_Target(), target, StringComparison::OrdinalIgnoreCase))
    {
continue;
    }
    if (String::Equals(candidate->get_Variant(), variant, StringComparison::Ordinal))
    {
binary = candidate;
return true;    }
}
binary = nullptr;
return false;}

bool ShaderModuleDefinition::TryGetProgram(std::string programName, ::ShaderProgramDefinition*& program)
{
    if (String::IsNullOrWhiteSpace(programName))
    {
throw ([&]() {
auto __ctor_arg_00000142 = "Program name must be provided.";
auto __ctor_arg_00000143 = "programName";
return new ArgumentException(__ctor_arg_00000142, __ctor_arg_00000143);
})();
    }
for (int32_t i = 0; i < this->programs->Length; i++) {
::ShaderProgramDefinition *candidate = (*this->programs)[i];
    if (String::Equals(candidate->get_Name(), programName, StringComparison::Ordinal))
    {
program = candidate;
return true;    }
}
program = nullptr;
return false;}

