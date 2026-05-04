#ifdef DrawText
#undef DrawText
#endif
#include "ShaderTargetNames.hpp"
#include "runtime/native_string.hpp"
#include "ShaderCompileTarget.hpp"
#include "runtime/native_exceptions.hpp"
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
#include "system/security/cryptography/sha256.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

std::string ShaderTargetNames::GetTargetName(::ShaderCompileTarget target)
{
switch (target) {
case ShaderCompileTarget::DirectX9: {
return "dx9";}
case ShaderCompileTarget::DirectX11: {
return "dx11";}
case ShaderCompileTarget::DirectX12: {
return "dx12";}
case ShaderCompileTarget::Vulkan: {
return "vulkan";}
case ShaderCompileTarget::Metal: {
return "metal";}
default:  {
throw ([&]() {
auto __ctor_arg_00000169 = "target";
auto __ctor_arg_0000016A = "Unsupported compile target.";
return new ArgumentOutOfRangeException(__ctor_arg_00000169, __ctor_arg_0000016A);
})();
}
}

}

::ShaderCompileTarget ShaderTargetNames::ParseTarget(std::string name)
{
::ShaderCompileTarget target;
    if (!TryParseTarget(name, target))
    {
throw ([&]() {
auto __ctor_arg_0000016B = "Target name was not recognized.";
auto __ctor_arg_0000016C = "name";
return new ArgumentException(__ctor_arg_0000016B, __ctor_arg_0000016C);
})();
    }
return target;}

bool ShaderTargetNames::TryParseTarget(std::string name, ::ShaderCompileTarget& target)
{
    if (String::IsNullOrWhiteSpace(name))
    {
target = ShaderCompileTarget::DirectX11;
return false;    }
{
const std::string __switchValue19_1 = String::ToLowerInvariant(String::Trim(name));
if (String::Equals(__switchValue19_1, "dx9")) {
target = ShaderCompileTarget::DirectX9;
return true;}
else if (String::Equals(__switchValue19_1, "dx11")) {
target = ShaderCompileTarget::DirectX11;
return true;}
else if (String::Equals(__switchValue19_1, "dx12")) {
target = ShaderCompileTarget::DirectX12;
return true;}
else if (String::Equals(__switchValue19_1, "vulkan")) {
target = ShaderCompileTarget::Vulkan;
return true;}
else if (String::Equals(__switchValue19_1, "metal")) {
target = ShaderCompileTarget::Metal;
return true;}
else {
target = ShaderCompileTarget::DirectX11;
return false;}
}

}

