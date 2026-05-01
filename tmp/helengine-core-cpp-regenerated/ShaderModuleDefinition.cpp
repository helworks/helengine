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
auto __ctor_arg_730b9e80 = "Program name must be provided.";
auto __ctor_arg_638617f5 = "programName";
return new ArgumentException(__ctor_arg_730b9e80, __ctor_arg_638617f5);
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
auto __ctor_arg_7690ed63 = "Module name must be provided.";
auto __ctor_arg_fa914f5f = "name";
return new ArgumentException(__ctor_arg_7690ed63, __ctor_arg_fa914f5f);
})();
    }
    if (programs == nullptr)
    {
throw new ArgumentNullException("programs");
    }
    if (programs->Length == 0)
    {
throw ([&]() {
auto __ctor_arg_be2a6d9d = "At least one program definition is required.";
auto __ctor_arg_62e09efa = "programs";
return new ArgumentException(__ctor_arg_be2a6d9d, __ctor_arg_62e09efa);
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
auto __ctor_arg_4b0af47d = "Program name must be provided.";
auto __ctor_arg_9d6620b8 = "programName";
return new ArgumentException(__ctor_arg_4b0af47d, __ctor_arg_9d6620b8);
})();
    }
    if (String::IsNullOrWhiteSpace(target))
    {
throw ([&]() {
auto __ctor_arg_18068244 = "Target must be provided.";
auto __ctor_arg_d06166d8 = "target";
return new ArgumentException(__ctor_arg_18068244, __ctor_arg_d06166d8);
})();
    }
    if (String::IsNullOrWhiteSpace(variant))
    {
throw ([&]() {
auto __ctor_arg_72a359fd = "Variant must be provided.";
auto __ctor_arg_5b338352 = "variant";
return new ArgumentException(__ctor_arg_72a359fd, __ctor_arg_5b338352);
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
auto __ctor_arg_1477ed60 = "Program name must be provided.";
auto __ctor_arg_29b553c3 = "programName";
return new ArgumentException(__ctor_arg_1477ed60, __ctor_arg_29b553c3);
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

