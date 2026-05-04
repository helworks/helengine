#ifdef DrawText
#undef DrawText
#endif
#include "RuntimeSceneLoadService.hpp"
#include "runtime/native_exceptions.hpp"
#include "Logger.hpp"
#include "system/diagnostics/stopwatch.hpp"
#include "runtime/array.hpp"
#include "runtime/native_list.hpp"
#include "Entity.hpp"
#include "Component.hpp"
#include "SceneEntityAsset.hpp"
#include "RuntimeComponentRegistry.hpp"
#include "SceneComponentAssetRecord.hpp"
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

List<::Entity*>* RuntimeSceneLoadService::Load(::SceneAsset* sceneAsset)
{
    if (sceneAsset == nullptr)
    {
throw new ArgumentNullException("sceneAsset");
    }
Logger::WriteLine("Loading packaged scene assets.");
Stopwatch *loadStopwatch = System::Diagnostics::Stopwatch::StartNew();
Array<::SceneEntityAsset*> *rootEntityAssets = ([&]() {
Array<::SceneEntityAsset*>* __coalesce_000000D0 = sceneAsset->get_RootEntities();
return __coalesce_000000D0 != nullptr ? __coalesce_000000D0 : Array<SceneEntityAsset*>::Empty();
})();
List<::Entity*> *rootEntities = new List<::Entity*>(rootEntityAssets->Length);
for (int32_t index = 0; index < rootEntityAssets->Length; index++) {
rootEntities->Add(this->LoadEntity((*rootEntityAssets)[index]));
}
loadStopwatch->Stop();
Logger::WriteLine(std::string("Loaded packaged scene assets in ") + std::to_string(loadStopwatch->Elapsed.TotalMilliseconds) + std::string(" ms (") + std::to_string(rootEntities->Count()) + std::string(" root entities)."));
return rootEntities;}

RuntimeSceneLoadService::RuntimeSceneLoadService(::RuntimeSceneAssetReferenceResolver* referenceResolver) : ComponentRegistry(), ReferenceResolver()
{
this->ReferenceResolver = (referenceResolver != nullptr ? referenceResolver : throw new ArgumentNullException("referenceResolver"));
this->ComponentRegistry = RuntimeComponentRegistry::CreateDefault();
}

RuntimeSceneLoadService::RuntimeSceneLoadService(::RuntimeSceneAssetReferenceResolver* referenceResolver, ::RuntimeComponentRegistry* componentRegistry) : ComponentRegistry(), ReferenceResolver()
{
this->ReferenceResolver = (referenceResolver != nullptr ? referenceResolver : throw new ArgumentNullException("referenceResolver"));
this->ComponentRegistry = (componentRegistry != nullptr ? componentRegistry : throw new ArgumentNullException("componentRegistry"));
}

::Component* RuntimeSceneLoadService::LoadComponent(::SceneComponentAssetRecord* record)
{
    if (record == nullptr)
    {
throw new ArgumentNullException("record");
    }
return this->ComponentRegistry->GetDeserializer(record->get_ComponentTypeId())->Deserialize(record, this->ReferenceResolver);}

::Entity* RuntimeSceneLoadService::LoadEntity(::SceneEntityAsset* entityAsset)
{
    if (entityAsset == nullptr)
    {
throw new ArgumentNullException("entityAsset");
    }
::Entity *entity = ([&]() {
auto __object_000000D1 = new ::Entity();
__object_000000D1->set_LocalPosition(entityAsset->get_LocalPosition());
__object_000000D1->set_LocalScale(entityAsset->get_LocalScale());
__object_000000D1->set_LocalOrientation(entityAsset->get_LocalOrientation());
return __object_000000D1;
})();
entity->InitComponents();
entity->InitChildren();
Array<::SceneComponentAssetRecord*> *componentRecords = ([&]() {
Array<::SceneComponentAssetRecord*>* __coalesce_000000D2 = entityAsset->get_Components();
return __coalesce_000000D2 != nullptr ? __coalesce_000000D2 : Array<SceneComponentAssetRecord*>::Empty();
})();
for (int32_t index = 0; index < componentRecords->Length; index++) {
entity->AddComponent(this->LoadComponent((*componentRecords)[index]));
}
Array<::SceneEntityAsset*> *childEntityAssets = ([&]() {
Array<::SceneEntityAsset*>* __coalesce_000000D3 = entityAsset->get_Children();
return __coalesce_000000D3 != nullptr ? __coalesce_000000D3 : Array<SceneEntityAsset*>::Empty();
})();
for (int32_t index = 0; index < childEntityAssets->Length; index++) {
entity->AddChild(this->LoadEntity((*childEntityAssets)[index]));
}
return entity;}

