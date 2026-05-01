#ifdef DrawText
#undef DrawText
#endif
#include "ShaderVertexElementAsset.hpp"
#include "ShaderVertexElement.hpp"
#include "runtime/native_exceptions.hpp"
#include "ShaderVertexElementAsset.hpp"
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
#include "system/security/cryptography/sha256.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

ShaderVertexElementAsset::ShaderVertexElementAsset() : Format(), Index(0), Semantic()
{
}

::ShaderVertexElementAsset* ShaderVertexElementAsset::FromVertexElement(::ShaderVertexElement* element)
{
    if (element == nullptr)
    {
throw new ArgumentNullException("element");
    }
::ShaderVertexElementAsset *asset = ([&]() {
auto __object_98b5882b = new ::ShaderVertexElementAsset();
__object_98b5882b->Semantic = element->get_Semantic();
__object_98b5882b->Index = element->get_Index();
__object_98b5882b->Format = element->get_Format();
return __object_98b5882b;
})();
return asset;}

::ShaderVertexElement* ShaderVertexElementAsset::ToVertexElement()
{
this->Validate();
return new ::ShaderVertexElement(this->Semantic, this->Index, this->Format);}

void ShaderVertexElementAsset::Validate()
{
    if (String::IsNullOrWhiteSpace(this->Semantic))
    {
throw new InvalidOperationException("Vertex element semantic must be provided.");
    }
else     if (String::IsNullOrWhiteSpace(this->Format))
    {
throw new InvalidOperationException("Vertex element format must be provided.");
    }
else     if (this->Index < 0)
    {
throw new InvalidOperationException("Vertex element index cannot be negative.");
    }
}

