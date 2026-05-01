#ifdef DrawText
#undef DrawText
#endif
#include "ShaderBinaryAsset.hpp"
#include "ShaderProgramBinary.hpp"
#include "runtime/native_exceptions.hpp"
#include "ShaderBinaryAsset.hpp"
#include "runtime/native_string.hpp"
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

ShaderBinaryAsset::ShaderBinaryAsset() : Bytecode(), ProgramName(), Stage(), TargetName(), Variant()
{
}

::ShaderBinaryAsset* ShaderBinaryAsset::FromBinary(::ShaderProgramBinary* binary)
{
    if (binary == nullptr)
    {
throw new ArgumentNullException("binary");
    }
    if (binary->get_Bytecode() == nullptr || binary->get_Bytecode()->Length == 0)
    {
throw new InvalidOperationException("Shader binary must include embedded bytecode for packaging.");
    }
::ShaderBinaryAsset *asset = ([&]() {
auto __object_bbb79718 = new ::ShaderBinaryAsset();
__object_bbb79718->ProgramName = binary->get_ProgramName();
__object_bbb79718->Stage = binary->get_Stage();
__object_bbb79718->TargetName = binary->get_Target();
__object_bbb79718->Variant = binary->get_Variant();
__object_bbb79718->Bytecode = binary->get_Bytecode();
return __object_bbb79718;
})();
return asset;}

::ShaderProgramBinary* ShaderBinaryAsset::ToBinary()
{
this->Validate();
return new ::ShaderProgramBinary(this->ProgramName, this->Stage, this->TargetName, this->Variant, this->Bytecode);}

void ShaderBinaryAsset::Validate()
{
    if (String::IsNullOrWhiteSpace(this->ProgramName))
    {
throw new InvalidOperationException("Binary program name must be provided.");
    }
else     if (String::IsNullOrWhiteSpace(this->TargetName))
    {
throw new InvalidOperationException("Binary target name must be provided.");
    }
else     if (String::IsNullOrWhiteSpace(this->Variant))
    {
throw new InvalidOperationException("Binary variant name must be provided.");
    }
else     if (this->Bytecode == nullptr || this->Bytecode->Length == 0)
    {
throw new InvalidOperationException("Binary bytecode payload must be provided.");
    }
}

