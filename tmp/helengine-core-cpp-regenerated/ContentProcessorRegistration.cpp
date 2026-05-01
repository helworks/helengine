#ifdef DrawText
#undef DrawText
#endif
#include "ContentProcessorRegistration.hpp"
#include "runtime/array.hpp"
#include "runtime/native_string.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_cast.hpp"
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
#include "system/bit_converter.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"

Array<std::string>* ContentProcessorRegistration::get_Extensions()
{
return this->ExtensionsValue;
}

Type* ContentProcessorRegistration::get_OutputType()
{
return this->ProcessorValue->get_OutputType();
}

::IContentProcessor* ContentProcessorRegistration::get_Processor()
{
return this->ProcessorValue;
}

std::string ContentProcessorRegistration::get_ProcessorId()
{
return this->ProcessorIdValue;
}

ContentProcessorRegistration::ContentProcessorRegistration(std::string processorId, ::IContentProcessor* processor, Array<std::string>* extensions) : ExtensionsValue(), ProcessorIdValue(), ProcessorValue()
{
    if (String::IsNullOrWhiteSpace(processorId))
    {
throw ([&]() {
auto __ctor_arg_4f54f902 = "Processor id must be provided.";
auto __ctor_arg_545fc779 = "processorId";
return new ArgumentException(__ctor_arg_4f54f902, __ctor_arg_545fc779);
})();
    }
    if (processor == nullptr)
    {
throw new ArgumentNullException("processor");
    }
this->ProcessorIdValue = processorId;
this->ProcessorValue = processor;
this->ExtensionsValue = extensions == nullptr ? Array<std::string>::Empty() : this->NormalizeExtensions(extensions);
}

std::string ContentProcessorRegistration::NormalizeExtension(std::string extension)
{
    if (String::Equals(extension, "*", StringComparison::Ordinal))
    {
return extension;    }
    if (!String::StartsWith(extension, "."))
    {
extension = String::Concat(".", extension);
    }
return String::ToLowerInvariant(extension);}

Array<std::string>* ContentProcessorRegistration::NormalizeExtensions(Array<std::string>* sourceExtensions)
{
Array<std::string> *normalized = new Array<std::string>(sourceExtensions->Length);
for (int32_t extensionIndex = 0; extensionIndex < sourceExtensions->Length; extensionIndex++) {
const std::string extension = (*sourceExtensions)[extensionIndex];
    if (String::IsNullOrWhiteSpace(extension))
    {
throw ([&]() {
auto __ctor_arg_33607f82 = "Extension values must be non-empty.";
auto __ctor_arg_f681837a = "sourceExtensions";
return new ArgumentException(__ctor_arg_33607f82, __ctor_arg_f681837a);
})();
    }
(*normalized)[extensionIndex] = this->NormalizeExtension(extension);
}
return normalized;}

