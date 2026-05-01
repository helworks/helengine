#ifdef DrawText
#undef DrawText
#endif
#include "ShaderPlatformDefines.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/array.hpp"
#include "runtime/native_string.hpp"
#include "ShaderDefine.hpp"
#include "ShaderCompileTarget.hpp"
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

Array<::ShaderDefine*>* ShaderPlatformDefines::BuildDefines(::ShaderCompileTarget target, ::ShaderModel* shaderModel, List<::ShaderDefine*>* additionalDefines)
{
    if (shaderModel == nullptr)
    {
throw new ArgumentNullException("shaderModel");
    }
    if (additionalDefines == nullptr)
    {
throw new ArgumentNullException("additionalDefines");
    }
List<::ShaderDefine*> *defines = new List<::ShaderDefine*>(additionalDefines->get_Count() + 3);
AddTargetDefine(target, defines);
defines->Add(([&]() {
auto __ctor_arg_88ae15ee = "HEL_SM_MAJOR";
auto __ctor_arg_1c0cc623 = std::to_string(shaderModel->get_Major());
return new ::ShaderDefine(__ctor_arg_88ae15ee, __ctor_arg_1c0cc623);
})());
defines->Add(([&]() {
auto __ctor_arg_c6254b5a = "HEL_SM_MINOR";
auto __ctor_arg_5c38682f = std::to_string(shaderModel->get_Minor());
return new ::ShaderDefine(__ctor_arg_c6254b5a, __ctor_arg_5c38682f);
})());
for (int32_t i = 0; i < additionalDefines->get_Count(); i++) {
defines->Add((*additionalDefines)[i]);
}
return defines->ToArray();}

void ShaderPlatformDefines::AddTargetDefine(::ShaderCompileTarget target, List<::ShaderDefine*>* defines)
{
const std::string defineName = GetTargetDefineName(target);
defines->Add(new ::ShaderDefine(defineName, "1"));
}

std::string ShaderPlatformDefines::GetTargetDefineName(::ShaderCompileTarget target)
{
switch (target) {
case ShaderCompileTarget::DirectX9: {
return "HEL_API_DX9";}
case ShaderCompileTarget::DirectX11: {
return "HEL_API_DX11";}
case ShaderCompileTarget::DirectX12: {
return "HEL_API_DX12";}
case ShaderCompileTarget::Vulkan: {
return "HEL_API_VULKAN";}
case ShaderCompileTarget::Metal: {
return "HEL_API_METAL";}
default:  {
throw ([&]() {
auto __ctor_arg_3c0be096 = "target";
auto __ctor_arg_cdda3505 = "Unsupported compile target.";
return new ArgumentOutOfRangeException(__ctor_arg_3c0be096, __ctor_arg_cdda3505);
})();
}
}

}

