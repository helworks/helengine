#ifdef DrawText
#undef DrawText
#endif
#include "ShaderCompileCacheKey.hpp"
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

std::string ShaderCompileCacheKey::get_BindingPolicySignature()
{
return this->BindingPolicySignature;
}

std::string ShaderCompileCacheKey::get_DefinesSignature()
{
return this->DefinesSignature;
}

std::string ShaderCompileCacheKey::get_EntryPoint()
{
return this->EntryPoint;
}

std::string ShaderCompileCacheKey::get_ProgramName()
{
return this->ProgramName;
}

::ShaderModel* ShaderCompileCacheKey::get_ShaderModel()
{
return this->ShaderModel;
}

std::string ShaderCompileCacheKey::get_SourceHash()
{
return this->SourceHash;
}

::ShaderStage ShaderCompileCacheKey::get_Stage()
{
return this->Stage;
}

::ShaderCompileTarget ShaderCompileCacheKey::get_Target()
{
return this->Target;
}

std::string ShaderCompileCacheKey::get_Variant()
{
return this->Variant;
}

ShaderCompileCacheKey::ShaderCompileCacheKey(std::string sourceHash, std::string programName, std::string entryPoint, ::ShaderStage stage, ::ShaderCompileTarget target, ::ShaderModel* shaderModel, std::string variant, std::string definesSignature, std::string bindingPolicySignature) : BindingPolicySignature(), DefinesSignature(), EntryPoint(), ProgramName(), ShaderModel(), SourceHash(), Stage(), Target(), Variant(), key()
{
    if (String::IsNullOrWhiteSpace(sourceHash))
    {
throw ([&]() {
auto __ctor_arg_000000F2 = "Source hash must be provided.";
auto __ctor_arg_000000F3 = "sourceHash";
return new ArgumentException(__ctor_arg_000000F2, __ctor_arg_000000F3);
})();
    }
    if (String::IsNullOrWhiteSpace(programName))
    {
throw ([&]() {
auto __ctor_arg_000000F4 = "Program name must be provided.";
auto __ctor_arg_000000F5 = "programName";
return new ArgumentException(__ctor_arg_000000F4, __ctor_arg_000000F5);
})();
    }
    if (String::IsNullOrWhiteSpace(entryPoint))
    {
throw ([&]() {
auto __ctor_arg_000000F6 = "Entry point must be provided.";
auto __ctor_arg_000000F7 = "entryPoint";
return new ArgumentException(__ctor_arg_000000F6, __ctor_arg_000000F7);
})();
    }
    if (shaderModel == nullptr)
    {
throw new ArgumentNullException("shaderModel");
    }
    if (String::IsNullOrWhiteSpace(variant))
    {
throw ([&]() {
auto __ctor_arg_000000F8 = "Variant name must be provided.";
auto __ctor_arg_000000F9 = "variant";
return new ArgumentException(__ctor_arg_000000F8, __ctor_arg_000000F9);
})();
    }
    if (String::IsNullOrEmpty(definesSignature))
    {
throw new ArgumentNullException("definesSignature");
    }
    if (String::IsNullOrEmpty(bindingPolicySignature))
    {
throw new ArgumentNullException("bindingPolicySignature");
    }
this->SourceHash = sourceHash;
this->ProgramName = programName;
this->EntryPoint = entryPoint;
this->Stage = stage;
this->Target = target;
this->ShaderModel = shaderModel;
this->Variant = variant;
this->DefinesSignature = definesSignature;
this->BindingPolicySignature = bindingPolicySignature;
this->key = this->BuildKey();
}

std::string ShaderCompileCacheKey::ToString()
{
return this->key;}

std::string ShaderCompileCacheKey::BuildKey()
{
return String::Join("|", this->SourceHash, this->ProgramName, this->EntryPoint, std::to_string(static_cast<int32_t>(this->Stage)), std::to_string(static_cast<int32_t>(this->Target)), this->ShaderModel->ToString(), this->Variant, this->DefinesSignature, this->BindingPolicySignature);}

