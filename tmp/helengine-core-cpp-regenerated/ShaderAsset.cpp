#ifdef DrawText
#undef DrawText
#endif
#include "ShaderAsset.hpp"
#include "runtime/array.hpp"
#include "ShaderModuleDefinition.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_string.hpp"
#include "ShaderTargetNames.hpp"
#include "ShaderAsset.hpp"
#include "runtime/native_list.hpp"
#include "ShaderProgramDefinition.hpp"
#include "ShaderProgramBinary.hpp"
#include "ShaderProgramAsset.hpp"
#include "ShaderBinaryAsset.hpp"
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
#include "system/binary_primitives.hpp"
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
#include "system/security/cryptography/sha256.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

ShaderAsset::ShaderAsset() : Binaries(), Name(), Programs(), TargetName()
{
}

::ShaderModuleDefinition* ShaderAsset::BuildDefinition()
{
this->Validate();
Array<::ShaderProgramDefinition*> *programDefinitions = this->BuildProgramDefinitions();
Array<::ShaderProgramBinary*> *binaryDefinitions = this->BuildBinaryDefinitions();
return new ::ShaderModuleDefinition(this->Name, programDefinitions, binaryDefinitions);}

::ShaderAsset* ShaderAsset::FromDefinition(::ShaderModuleDefinition* definition, ::ShaderCompileTarget target)
{
    if (definition == nullptr)
    {
throw new ArgumentNullException("definition");
    }
const std::string targetName = ShaderTargetNames::GetTargetName(target);
Array<::ShaderProgramAsset*> *programs = BuildProgramAssets(definition);
Array<::ShaderBinaryAsset*> *binaries = BuildBinaryAssets(definition, targetName);
::ShaderAsset *asset = ([&]() {
auto __object_000001A4 = new ::ShaderAsset();
__object_000001A4->set_Id(definition->get_Name());
__object_000001A4->Name = definition->get_Name();
__object_000001A4->TargetName = targetName;
__object_000001A4->Programs = programs;
__object_000001A4->Binaries = binaries;
return __object_000001A4;
})();
return asset;}

std::string ShaderAsset::get_Id()
{
return this->Asset::get_Id();
}

void ShaderAsset::set_Id(std::string value)
{
this->Asset::set_Id(value);
}

Array<::ShaderBinaryAsset*>* ShaderAsset::BuildBinaryAssets(::ShaderModuleDefinition* definition, std::string targetName)
{
List<::ShaderProgramBinary*> *binaries = definition->get_Binaries();
List<::ShaderBinaryAsset*> *assets = new List<::ShaderBinaryAsset*>();
for (int32_t i = 0; i < binaries->get_Count(); i++) {
::ShaderProgramBinary *binary = (*binaries)[i];
    if (!String::Equals(binary->get_Target(), targetName, StringComparison::OrdinalIgnoreCase))
    {
continue;
    }
assets->Add(ShaderBinaryAsset::FromBinary(binary));
}
    if (assets->Count() == 0)
    {
throw new InvalidOperationException("Shader module does not contain binaries for the requested target.");
    }
return assets->ToArray();}

Array<::ShaderProgramBinary*>* ShaderAsset::BuildBinaryDefinitions()
{
Array<::ShaderProgramBinary*> *binaries = new Array<ShaderProgramBinary*>(this->Binaries->Length);
for (int32_t i = 0; i < this->Binaries->Length; i++) {
::ShaderBinaryAsset *binary = (*this->Binaries)[i];
    if (binary == nullptr)
    {
throw new InvalidOperationException("Shader asset contains a null binary entry.");
    }
(*binaries)[i] = binary->ToBinary();
}
return binaries;}

Array<::ShaderProgramAsset*>* ShaderAsset::BuildProgramAssets(::ShaderModuleDefinition* definition)
{
List<::ShaderProgramDefinition*> *programs = definition->get_Programs();
Array<::ShaderProgramAsset*> *assets = new Array<ShaderProgramAsset*>(programs->get_Count());
for (int32_t i = 0; i < programs->get_Count(); i++) {
::ShaderProgramDefinition *program = (*programs)[i];
(*assets)[i] = ShaderProgramAsset::FromDefinition(program);
}
return assets;}

Array<::ShaderProgramDefinition*>* ShaderAsset::BuildProgramDefinitions()
{
Array<::ShaderProgramDefinition*> *definitions = new Array<ShaderProgramDefinition*>(this->Programs->Length);
for (int32_t i = 0; i < this->Programs->Length; i++) {
::ShaderProgramAsset *program = (*this->Programs)[i];
    if (program == nullptr)
    {
throw new InvalidOperationException("Shader asset contains a null program entry.");
    }
(*definitions)[i] = program->ToDefinition();
}
return definitions;}

void ShaderAsset::Validate()
{
    if (String::IsNullOrWhiteSpace(this->Name))
    {
throw new InvalidOperationException("Shader asset name must be provided.");
    }
else     if (String::IsNullOrWhiteSpace(this->TargetName))
    {
throw new InvalidOperationException("Shader asset target name must be provided.");
    }
else     if (this->Programs == nullptr)
    {
throw new InvalidOperationException("Shader asset programs must be provided.");
    }
else     if (this->Programs->Length == 0)
    {
throw new InvalidOperationException("Shader asset must include at least one program.");
    }
else     if (this->Binaries == nullptr)
    {
throw new InvalidOperationException("Shader asset binaries must be provided.");
    }
else     if (this->Binaries->Length == 0)
    {
throw new InvalidOperationException("Shader asset must include at least one binary.");
    }
}

