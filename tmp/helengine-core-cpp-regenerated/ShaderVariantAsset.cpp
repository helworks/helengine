#ifdef DrawText
#undef DrawText
#endif
#include "ShaderVariantAsset.hpp"
#include "ShaderVariant.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/array.hpp"
#include "ShaderVariantAsset.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_list.hpp"
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

ShaderVariantAsset::ShaderVariantAsset() : Defines(), Name()
{
}

::ShaderVariantAsset* ShaderVariantAsset::FromVariant(::ShaderVariant* variant)
{
    if (variant == nullptr)
    {
throw new ArgumentNullException("variant");
    }
Array<std::string> *defines = BuildDefines(variant);
::ShaderVariantAsset *asset = ([&]() {
auto __object_0000016F = new ::ShaderVariantAsset();
__object_0000016F->Name = variant->get_Name();
__object_0000016F->Defines = defines;
return __object_0000016F;
})();
return asset;}

::ShaderVariant* ShaderVariantAsset::ToVariant()
{
this->Validate();
return new ::ShaderVariant(this->Name, this->Defines);}

Array<std::string>* ShaderVariantAsset::BuildDefines(::ShaderVariant* variant)
{
List<std::string> *defines = variant->get_Defines();
Array<std::string> *result = new Array<std::string>(defines->get_Count());
for (int32_t i = 0; i < defines->get_Count(); i++) {
(*result)[i] = (*defines)[i];
}
return result;}

void ShaderVariantAsset::Validate()
{
    if (String::IsNullOrWhiteSpace(this->Name))
    {
throw new InvalidOperationException("Variant name must be provided.");
    }
else     if (this->Defines == nullptr)
    {
throw new InvalidOperationException("Variant defines must be provided.");
    }
}

