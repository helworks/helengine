#ifdef DrawText
#undef DrawText
#endif
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

List<::ShaderBinding*>* ShaderProgramDefinition::get_Bindings()
{
return new List<ShaderBinding*>(this->bindings);}

std::string ShaderProgramDefinition::get_EntryPoint()
{
return this->EntryPoint;
}

List<::ShaderVertexElement*>* ShaderProgramDefinition::get_Inputs()
{
return new List<ShaderVertexElement*>(this->inputs);}

std::string ShaderProgramDefinition::get_Name()
{
return this->Name;
}

List<::ShaderVertexElement*>* ShaderProgramDefinition::get_Outputs()
{
return new List<ShaderVertexElement*>(this->outputs);}

::ShaderStage ShaderProgramDefinition::get_Stage()
{
return this->Stage;
}

List<::ShaderVariant*>* ShaderProgramDefinition::get_Variants()
{
return new List<ShaderVariant*>(this->variants);}

ShaderProgramDefinition::ShaderProgramDefinition(std::string name, ::ShaderStage stage, std::string entryPoint, Array<::ShaderBinding*>* bindings, Array<::ShaderVertexElement*>* inputs, Array<::ShaderVertexElement*>* outputs, Array<::ShaderVariant*>* variants) : EntryPoint(), Name(), Stage(), bindings(), inputs(), outputs(), variants()
{
    if (String::IsNullOrWhiteSpace(name))
    {
throw ([&]() {
auto __ctor_arg_05466484 = "Program name must be provided.";
auto __ctor_arg_781e92a9 = "name";
return new ArgumentException(__ctor_arg_05466484, __ctor_arg_781e92a9);
})();
    }
    if (String::IsNullOrWhiteSpace(entryPoint))
    {
throw ([&]() {
auto __ctor_arg_d7963a04 = "Entry point must be provided.";
auto __ctor_arg_b69a2902 = "entryPoint";
return new ArgumentException(__ctor_arg_d7963a04, __ctor_arg_b69a2902);
})();
    }
    if (bindings == nullptr)
    {
throw new ArgumentNullException("bindings");
    }
    if (inputs == nullptr)
    {
throw new ArgumentNullException("inputs");
    }
    if (outputs == nullptr)
    {
throw new ArgumentNullException("outputs");
    }
    if (variants == nullptr)
    {
throw new ArgumentNullException("variants");
    }
this->Name = name;
this->Stage = stage;
this->EntryPoint = entryPoint;
this->bindings = bindings;
this->inputs = inputs;
this->outputs = outputs;
this->variants = variants;
}

