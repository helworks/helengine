#ifdef DrawText
#undef DrawText
#endif
#include "ShaderBindingPolicy.hpp"
#include "runtime/native_exceptions.hpp"
#include "ShaderResourceType.hpp"
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

int32_t ShaderBindingPolicy::get_ConstantBufferShift()
{
return this->ConstantBufferShift;
}

int32_t ShaderBindingPolicy::get_DefaultSpace()
{
return this->DefaultSpace;
}

int32_t ShaderBindingPolicy::get_SamplerShift()
{
return this->SamplerShift;
}

int32_t ShaderBindingPolicy::get_StorageShift()
{
return this->StorageShift;
}

int32_t ShaderBindingPolicy::get_TextureShift()
{
return this->TextureShift;
}

int32_t ShaderBindingPolicy::GetSlot(::ShaderResourceType type, int32_t registerIndex)
{
    if (registerIndex < 0)
    {
throw ([&]() {
auto __ctor_arg_000000E4 = "registerIndex";
auto __ctor_arg_000000E5 = "Register index cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_000000E4, __ctor_arg_000000E5);
})();
    }
const int32_t shift = this->GetShift(type);
return shift + registerIndex;}

ShaderBindingPolicy::ShaderBindingPolicy(int32_t defaultSpace, int32_t constantBufferShift, int32_t textureShift, int32_t samplerShift, int32_t storageShift) : ConstantBufferShift(0), DefaultSpace(0), SamplerShift(0), StorageShift(0), TextureShift(0)
{
    if (defaultSpace < 0)
    {
throw ([&]() {
auto __ctor_arg_000000E6 = "defaultSpace";
auto __ctor_arg_000000E7 = "Default space cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_000000E6, __ctor_arg_000000E7);
})();
    }
    if (constantBufferShift < 0)
    {
throw ([&]() {
auto __ctor_arg_000000E8 = "constantBufferShift";
auto __ctor_arg_000000E9 = "Constant buffer shift cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_000000E8, __ctor_arg_000000E9);
})();
    }
    if (textureShift < 0)
    {
throw ([&]() {
auto __ctor_arg_000000EA = "textureShift";
auto __ctor_arg_000000EB = "Texture shift cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_000000EA, __ctor_arg_000000EB);
})();
    }
    if (samplerShift < 0)
    {
throw ([&]() {
auto __ctor_arg_000000EC = "samplerShift";
auto __ctor_arg_000000ED = "Sampler shift cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_000000EC, __ctor_arg_000000ED);
})();
    }
    if (storageShift < 0)
    {
throw ([&]() {
auto __ctor_arg_000000EE = "storageShift";
auto __ctor_arg_000000EF = "Storage shift cannot be negative.";
return new ArgumentOutOfRangeException(__ctor_arg_000000EE, __ctor_arg_000000EF);
})();
    }
this->DefaultSpace = defaultSpace;
this->ConstantBufferShift = constantBufferShift;
this->TextureShift = textureShift;
this->SamplerShift = samplerShift;
this->StorageShift = storageShift;
}

int32_t ShaderBindingPolicy::GetShift(::ShaderResourceType type)
{
switch (type) {
case ShaderResourceType::ConstantBuffer: {
return this->ConstantBufferShift;}
case ShaderResourceType::Texture2D:case ShaderResourceType::TextureCube: {
return this->TextureShift;}
case ShaderResourceType::Sampler: {
return this->SamplerShift;}
case ShaderResourceType::Buffer:case ShaderResourceType::StorageBuffer:case ShaderResourceType::StorageTexture2D: {
return this->StorageShift;}
default:  {
throw ([&]() {
auto __ctor_arg_000000F0 = "type";
auto __ctor_arg_000000F1 = "Unsupported resource type.";
return new ArgumentOutOfRangeException(__ctor_arg_000000F0, __ctor_arg_000000F1);
})();
}
}

}

