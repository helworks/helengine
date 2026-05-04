#ifdef DrawText
#undef DrawText
#endif
#include "RuntimeComponentRegistry.hpp"
#include "RuntimeComponentRegistry.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_dictionary.hpp"
#include "IRuntimeComponentDeserializer.hpp"
#include "RuntimeMeshComponentDeserializer.hpp"
#include "RuntimeCameraComponentDeserializer.hpp"
#include "RuntimeFPSComponentDeserializer.hpp"
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

::RuntimeComponentRegistry* RuntimeComponentRegistry::CreateDefault()
{
::RuntimeComponentRegistry *registry = new ::RuntimeComponentRegistry();
registry->Register(new ::RuntimeMeshComponentDeserializer());
registry->Register(new ::RuntimeCameraComponentDeserializer());
registry->Register(new ::RuntimeFPSComponentDeserializer());
return registry;}

::IRuntimeComponentDeserializer* RuntimeComponentRegistry::GetDeserializer(std::string componentTypeId)
{
    if (String::IsNullOrWhiteSpace(componentTypeId))
    {
throw ([&]() {
auto __ctor_arg_000000C0 = "Component type id must be provided.";
auto __ctor_arg_000000C1 = "componentTypeId";
return new ArgumentException(__ctor_arg_000000C0, __ctor_arg_000000C1);
})();
    }
::IRuntimeComponentDeserializer* deserializer;
    if (!this->DeserializersByTypeId->TryGetValue(componentTypeId, deserializer))
    {
throw new InvalidOperationException(std::string("Player builds do not support serialized component type '") + componentTypeId + std::string("' yet."));
    }
return deserializer;}

void RuntimeComponentRegistry::Register(::IRuntimeComponentDeserializer* deserializer)
{
    if (deserializer == nullptr)
    {
throw new ArgumentNullException("deserializer");
    }
    if (String::IsNullOrWhiteSpace(deserializer->get_ComponentTypeId()))
    {
throw new InvalidOperationException("Runtime component deserializers must expose a serialized type id.");
    }
    if (this->DeserializersByTypeId->ContainsKey(deserializer->get_ComponentTypeId()))
    {
throw new InvalidOperationException(std::string("A runtime component deserializer is already registered for '") + deserializer->get_ComponentTypeId() + std::string("'."));
    }
this->DeserializersByTypeId->Add(deserializer->get_ComponentTypeId(), deserializer);
}

RuntimeComponentRegistry::RuntimeComponentRegistry() : DeserializersByTypeId()
{
this->DeserializersByTypeId = new Dictionary<std::string, ::IRuntimeComponentDeserializer*>(StringComparer::OrdinalIgnoreCase);
}

bool RuntimeComponentRegistry::TryGet(std::string componentTypeId, ::IRuntimeComponentDeserializer*& deserializer)
{
    if (String::IsNullOrWhiteSpace(componentTypeId))
    {
deserializer = nullptr;
return false;    }
return this->DeserializersByTypeId->TryGetValue(componentTypeId, deserializer);}

