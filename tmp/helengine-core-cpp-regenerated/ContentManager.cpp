#ifdef DrawText
#undef DrawText
#endif
#include "ContentManager.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/array.hpp"
#include "ContentProcessorRegistration.hpp"
#include "IContentProcessor_1.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/path.hpp"
#include "IContentProcessor.hpp"
#include "TextContentProcessor.hpp"
#include "RawByteContentProcessor.hpp"
#include "runtime/array.hpp"
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
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"

std::string ContentManager::get_RootDirectory()
{
return this->RootDirectoryPath;
}

ContentManager::ContentManager(std::string rootDirectory) : DefaultProcessorsByTypeAndExtension(), ProcessorRegistrationsById(), RootDirectoryPath()
{
    if (String::IsNullOrWhiteSpace(rootDirectory))
    {
throw ([&]() {
auto __ctor_arg_00000022 = "Root directory must be provided.";
auto __ctor_arg_00000023 = "rootDirectory";
return new ArgumentException(__ctor_arg_00000022, __ctor_arg_00000023);
})();
    }
this->RootDirectoryPath = Path::GetFullPath(rootDirectory);
this->ProcessorRegistrationsById = new Dictionary<std::string, ::ContentProcessorRegistration*>(StringComparer::OrdinalIgnoreCase);
this->DefaultProcessorsByTypeAndExtension = new Dictionary<Type*, Dictionary<std::string, ::ContentProcessorRegistration*>*>();
this->RegisterBuiltInProcessors();
}

bool ContentManager::IsProcessorRegistered(std::string processorId)
{
    if (String::IsNullOrWhiteSpace(processorId))
    {
throw ([&]() {
auto __ctor_arg_00000024 = "Processor id must be provided.";
auto __ctor_arg_00000025 = "processorId";
return new ArgumentException(__ctor_arg_00000024, __ctor_arg_00000025);
})();
    }
return this->ProcessorRegistrationsById->ContainsKey(processorId);}

template <typename T>
T ContentManager::Load(std::string assetPath, std::string processorId)
{
const std::string fullPath = this->ResolveContentPath(assetPath);
::IContentProcessor_1<T> *processor = ResolveProcessor<T>(fullPath, processorId);
return this->LoadProcessedContent<T>(fullPath, processor);}

template <typename T>
T ContentManager::Load(std::string assetPath, ::IContentProcessor_1<T>* processor)
{
    if (processor == nullptr)
    {
throw new ArgumentNullException("processor");
    }
const std::string fullPath = this->ResolveContentPath(assetPath);
return this->LoadProcessedContent<T>(fullPath, processor);}

void ContentManager::RegisterProcessor(::ContentProcessorRegistration* registration)
{
    if (registration == nullptr)
    {
throw new ArgumentNullException("registration");
    }
    if (this->ProcessorRegistrationsById->ContainsKey(registration->get_ProcessorId()))
    {
throw new InvalidOperationException(std::string("Content processor '") + registration->get_ProcessorId() + std::string("' is already registered."));
    }
Array<std::string> *extensions = registration->get_Extensions();
    if (extensions->Length > 0)
    {
Dictionary<std::string, ::ContentProcessorRegistration*> *registrationsByExtension = this->GetOrCreateTypeRegistrationMap(registration->get_OutputType());
for (int32_t extensionIndex = 0; extensionIndex < extensions->Length; extensionIndex++) {
const std::string extension = this->NormalizeExtension((*extensions)[extensionIndex]);
    if (registrationsByExtension->ContainsKey(extension))
    {
throw new InvalidOperationException(std::string("A default content processor is already registered for type '") + registration->get_OutputType()->get_Name() + std::string("' and extension '") + extension + std::string("'."));
    }
registrationsByExtension->Add(extension, registration);
}
    }
this->ProcessorRegistrationsById->Add(registration->get_ProcessorId(), registration);
}

template <typename T>
void ContentManager::RegisterProcessor(std::string processorId, ::IContentProcessor_1<T>* processor, Array<std::string>* extensions)
{
    if (processor == nullptr)
    {
throw new ArgumentNullException("processor");
    }
this->RegisterProcessor(new ::ContentProcessorRegistration(processorId, processor, extensions));
}

std::string ContentManager::RawByteContentProcessorId = "core.raw-byte-content";

std::string ContentManager::TextContentProcessorId = "core.text-content";

std::string ContentManager::WildcardExtension = "*";

Dictionary<std::string, ::ContentProcessorRegistration*>* ContentManager::GetOrCreateTypeRegistrationMap(Type* outputType)
{
    if (outputType == nullptr)
    {
throw new ArgumentNullException("outputType");
    }
Dictionary<std::string, ::ContentProcessorRegistration*>* registrationsByExtension;
    if (this->DefaultProcessorsByTypeAndExtension->TryGetValue(outputType, registrationsByExtension))
    {
return registrationsByExtension;    }
registrationsByExtension = new Dictionary<std::string, ::ContentProcessorRegistration*>(StringComparer::OrdinalIgnoreCase);
this->DefaultProcessorsByTypeAndExtension->Add(outputType, registrationsByExtension);
return registrationsByExtension;}

template <typename T>
T ContentManager::LoadProcessedContent(std::string fullPath, ::IContentProcessor_1<T>* processor)
{
    if (String::IsNullOrWhiteSpace(fullPath))
    {
throw ([&]() {
auto __ctor_arg_00000026 = "Content path must be provided.";
auto __ctor_arg_00000027 = "fullPath";
return new ArgumentException(__ctor_arg_00000026, __ctor_arg_00000027);
})();
    }
    if (processor == nullptr)
    {
throw new ArgumentNullException("processor");
    }
{
::FileStream *stream = File::OpenRead(fullPath);
return processor->Read(stream);}
}

std::string ContentManager::NormalizeExtension(std::string extension)
{
    if (String::IsNullOrWhiteSpace(extension))
    {
throw ([&]() {
auto __ctor_arg_00000028 = "Extension must be provided.";
auto __ctor_arg_00000029 = "extension";
return new ArgumentException(__ctor_arg_00000028, __ctor_arg_00000029);
})();
    }
    if (String::Equals(extension, WildcardExtension, StringComparison::Ordinal))
    {
return extension;    }
    if (!String::StartsWith(extension, "."))
    {
extension = String::Concat(".", extension);
    }
return String::ToLowerInvariant(extension);}

void ContentManager::RegisterBuiltInProcessors()
{
this->RegisterProcessor<TextContent*>(TextContentProcessorId, new ::TextContentProcessor(), new Array<std::string>({ WildcardExtension }));
this->RegisterProcessor<RawByteContent*>(RawByteContentProcessorId, new ::RawByteContentProcessor(), new Array<std::string>({ WildcardExtension }));
}

std::string ContentManager::ResolveContentPath(std::string assetPath)
{
    if (String::IsNullOrWhiteSpace(assetPath))
    {
throw ([&]() {
auto __ctor_arg_0000002A = "Asset path must be provided.";
auto __ctor_arg_0000002B = "assetPath";
return new ArgumentException(__ctor_arg_0000002A, __ctor_arg_0000002B);
})();
    }
    if (Path::IsPathRooted(assetPath))
    {
return Path::GetFullPath(assetPath);    }
return Path::GetFullPath(Path::Combine(this->RootDirectoryPath, assetPath));}

::ContentProcessorRegistration* ContentManager::ResolveDefaultProcessorRegistration(Type* requestedType, std::string fullPath)
{
    if (requestedType == nullptr)
    {
throw new ArgumentNullException("requestedType");
    }
    if (String::IsNullOrWhiteSpace(fullPath))
    {
throw ([&]() {
auto __ctor_arg_0000002C = "Content path must be provided.";
auto __ctor_arg_0000002D = "fullPath";
return new ArgumentException(__ctor_arg_0000002C, __ctor_arg_0000002D);
})();
    }
Dictionary<std::string, ::ContentProcessorRegistration*>* registrationsByExtension;
    if (!this->DefaultProcessorsByTypeAndExtension->TryGetValue(requestedType, registrationsByExtension))
    {
throw new InvalidOperationException(std::string("No content processors are registered for type '") + requestedType->get_Name() + std::string("'."));
    }
const std::string fileName = Path::GetFileName(fullPath);
    if (String::IsNullOrWhiteSpace(fileName))
    {
throw new InvalidOperationException(std::string("Unable to resolve a content processor for '") + requestedType->get_Name() + std::string("' because '") + fullPath + std::string("' does not contain a file name."));
    }
std::string extension;
    if (!this->TryResolveRegisteredExtension(fileName, registrationsByExtension, extension))
    {
throw new InvalidOperationException(std::string("No content processor is registered for type '") + requestedType->get_Name() + std::string("' and file '") + fileName + std::string("'."));
    }
::ContentProcessorRegistration* registration;
    if (registrationsByExtension->TryGetValue(extension, registration))
    {
return registration;    }
throw new InvalidOperationException(std::string("No content processor is registered for type '") + requestedType->get_Name() + std::string("' and extension '") + extension + std::string("'."));
}

::ContentProcessorRegistration* ContentManager::ResolveExplicitProcessorRegistration(Type* requestedType, std::string processorId)
{
    if (requestedType == nullptr)
    {
throw new ArgumentNullException("requestedType");
    }
    if (String::IsNullOrWhiteSpace(processorId))
    {
throw ([&]() {
auto __ctor_arg_0000002E = "Processor id must be provided.";
auto __ctor_arg_0000002F = "processorId";
return new ArgumentException(__ctor_arg_0000002E, __ctor_arg_0000002F);
})();
    }
::ContentProcessorRegistration* registration;
    if (!this->ProcessorRegistrationsById->TryGetValue(processorId, registration))
    {
throw new InvalidOperationException(std::string("Content processor '") + processorId + std::string("' is not registered."));
    }
    if (registration->get_OutputType() != requestedType)
    {
throw new InvalidOperationException(std::string("Content processor '") + processorId + std::string("' produces '") + registration->get_OutputType()->get_Name() + std::string("', not '") + requestedType->get_Name() + std::string("'."));
    }
return registration;}

template <typename T>
::IContentProcessor_1<T>* ContentManager::ResolveProcessor(std::string fullPath, std::string processorId)
{
::ContentProcessorRegistration *registration = String::IsNullOrWhiteSpace(processorId) ? this->ResolveDefaultProcessorRegistration(he_cpp_type_of<T>("T"), fullPath) : this->ResolveExplicitProcessorRegistration(he_cpp_type_of<T>("T"), processorId);
    IContentProcessor_1<T>* typedProcessor = he_cpp_try_cast<IContentProcessor_1<T>>(registration->get_Processor());
    if (typedProcessor != nullptr)
    {
return typedProcessor;    }
throw new InvalidOperationException(std::string("Registered processor '") + registration->get_ProcessorId() + std::string("' does not implement the expected processor interface for type '") + he_cpp_type_of<T>("T")->get_Name() + std::string("'."));
}

bool ContentManager::TryResolveRegisteredExtension(std::string fileName, Dictionary<std::string, ::ContentProcessorRegistration*>* registrationsByExtension, std::string& matchedExtension)
{
    if (String::IsNullOrWhiteSpace(fileName))
    {
throw ([&]() {
auto __ctor_arg_00000030 = "File name must be provided.";
auto __ctor_arg_00000031 = "fileName";
return new ArgumentException(__ctor_arg_00000030, __ctor_arg_00000031);
})();
    }
    if (registrationsByExtension == nullptr)
    {
throw new ArgumentNullException("registrationsByExtension");
    }
const std::string normalizedFileName = String::ToLowerInvariant(fileName);
matchedExtension = String::Empty;
for (const auto& extension : registrationsByExtension->Keys()) {
    if (String::Equals(extension, WildcardExtension, StringComparison::Ordinal))
    {
    if (String::IsNullOrWhiteSpace(matchedExtension))
    {
matchedExtension = extension;
    }
continue;
    }
    if (!String::EndsWith(normalizedFileName, extension, StringComparison::OrdinalIgnoreCase))
    {
continue;
    }
    if (static_cast<int32_t>(matchedExtension.size()) >= static_cast<int32_t>(extension.size()))
    {
continue;
    }
matchedExtension = extension;
}
return !String::IsNullOrWhiteSpace(matchedExtension);}

