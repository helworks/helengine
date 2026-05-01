#ifdef DrawText
#undef DrawText
#endif
#include "ObjectManager.hpp"
#include "runtime/native_list.hpp"
#include "IRenderQueue3D.hpp"
#include "ICamera.hpp"
#include "IRenderQueue2D.hpp"
#include "runtime/native_exceptions.hpp"
#include "Entity.hpp"
#include "IUpdateable.hpp"
#include "PendingUpdateOperation.hpp"
#include "IDrawable2D.hpp"
#include "IDrawable3D.hpp"
#include "IInteractable2D.hpp"
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
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"

List<::ICamera*>* ObjectManager::get_Cameras()
{
return this->Cameras;
}

void ObjectManager::set_Cameras(List<::ICamera*>* value)
{
this->Cameras = value;
}

List<::IDrawable2D*>* ObjectManager::get_Drawables2D()
{
return this->Drawables2D;
}

void ObjectManager::set_Drawables2D(List<::IDrawable2D*>* value)
{
this->Drawables2D = value;
}

List<::IDrawable3D*>* ObjectManager::get_Drawables3D()
{
return this->Drawables3D;
}

void ObjectManager::set_Drawables3D(List<::IDrawable3D*>* value)
{
this->Drawables3D = value;
}

List<::Entity*>* ObjectManager::get_Entities()
{
return this->Entities;
}

void ObjectManager::set_Entities(List<::Entity*>* value)
{
this->Entities = value;
}

List<::IInteractable2D*>* ObjectManager::get_Interactables()
{
return this->Interactables;
}

void ObjectManager::set_Interactables(List<::IInteractable2D*>* value)
{
this->Interactables = value;
}

uint8_t ObjectManager::get_RenderOrderLayers3D()
{
return this->RenderOrderLayers3D;
}

void ObjectManager::set_RenderOrderLayers3D(uint8_t value)
{
this->RenderOrderLayers3D = value;
}

uint8_t ObjectManager::get_UpdateOrderLayers()
{
return this->UpdateOrderLayers;
}

void ObjectManager::set_UpdateOrderLayers(uint8_t value)
{
this->UpdateOrderLayers = value;
}

List<::IUpdateable*>* ObjectManager::get_Updateables()
{
return this->Updateables;
}

void ObjectManager::set_Updateables(List<::IUpdateable*>* value)
{
this->Updateables = value;
}

uint8_t ObjectManager::GetRenderOrderForLayer3D(int32_t layerIndex)
{
return this->GetOrderForLayer(layerIndex, this->RenderOrderLayers3D);}

uint8_t ObjectManager::GetUpdateOrderForLayer(int32_t layerIndex)
{
return this->GetOrderForLayer(layerIndex, this->UpdateOrderLayers);}

ObjectManager::ObjectManager(::CoreInitializationOptions* settings) : Cameras(), Drawables2D(), Drawables3D(), Entities(), Interactables(), RenderOrderLayers3D(4), UpdateOrderLayers(4), Updateables(), pendingUpdateOperations(), updateLoopActive()
{
    if (settings == nullptr)
    {
throw new ArgumentNullException("settings");
    }
settings->Normalize();
this->set_UpdateOrderLayers(settings->get_UpdateOrderLayers());
this->set_RenderOrderLayers3D(settings->get_RenderOrderLayers3D());
this->set_Entities(new List<::Entity*>());
this->set_Updateables(new List<::IUpdateable*>(settings->get_UpdateListInitialCapacity()));
this->pendingUpdateOperations = new List<::PendingUpdateOperation*>();
this->set_Drawables2D(new List<::IDrawable2D*>(settings->get_RenderList2DInitialCapacity()));
this->set_Drawables3D(new List<::IDrawable3D*>(settings->get_RenderList3DInitialCapacity()));
this->set_Cameras(new List<::ICamera*>());
this->set_Interactables(new List<::IInteractable2D*>());
}

void ObjectManager::RegisterCamera(::ICamera* camera)
{
this->InsertCameraByDrawOrder(camera);
::IRenderQueue3D *list3D = camera->get_RenderQueue3D();
for (int32_t i = 0; i < this->Drawables3D->Count(); i++) {
::IDrawable3D *drawable = (*this->Drawables3D)[i];
    if ((drawable->get_Parent()->get_LayerMask() & camera->get_LayerMask()) != 0)
    {
list3D->Add(drawable);
    }
}
::IRenderQueue2D *list2D = camera->get_RenderQueue2D();
for (int32_t i = 0; i < this->Drawables2D->Count(); i++) {
::IDrawable2D *drawable2D = (*this->Drawables2D)[i];
    if ((drawable2D->get_Parent()->get_LayerMask() & camera->get_LayerMask()) != 0)
    {
list2D->Add(drawable2D);
    }
}
}

void ObjectManager::RegisterEntity(::Entity* entity)
{
this->Entities->Add(entity);
}

void ObjectManager::RegisterForRender2D(::IDrawable2D* drawable)
{
this->Drawables2D->Add(drawable);
for (int32_t i = 0; i < this->Cameras->Count(); i++) {
::ICamera *camera = (*this->Cameras)[i];
    if ((drawable->get_Parent()->get_LayerMask() & camera->get_LayerMask()) == 0)
    {
continue;
    }
::IRenderQueue2D *list = camera->get_RenderQueue2D();
list->Add(drawable);
}
}

void ObjectManager::RegisterForRender3D(::IDrawable3D* drawable)
{
this->Drawables3D->Add(drawable);
for (int32_t i = 0; i < this->Cameras->Count(); i++) {
::ICamera *camera = (*this->Cameras)[i];
    if ((drawable->get_Parent()->get_LayerMask() & camera->get_LayerMask()) == 0)
    {
continue;
    }
::IRenderQueue3D *list = camera->get_RenderQueue3D();
list->Add(drawable);
}
}

void ObjectManager::RegisterForUpdate(::IUpdateable* entity)
{
    if (entity == nullptr)
    {
return;    }
    if (this->updateLoopActive)
    {
this->QueueUpdateOperation(entity, true);
return;    }
this->AddUpdateableToList(entity);
}

void ObjectManager::RegisterInteractable(::IInteractable2D* entity)
{
this->Interactables->Add(entity);
}

void ObjectManager::RemoveCamera(::ICamera* camera)
{
    if (camera == nullptr)
    {
return;    }
this->Cameras->Remove(camera);
camera->get_RenderQueue3D()->Clear();
camera->get_RenderQueue2D()->Clear();
}

void ObjectManager::RemoveEntity(::Entity* entity)
{
this->Entities->Remove(entity);
}

void ObjectManager::RemoveFromRender2D(::IDrawable2D* drawable)
{
this->RemoveByReference<IDrawable2D*>(this->Drawables2D, drawable);
for (int32_t i = 0; i < this->Cameras->Count(); i++) {
::ICamera *camera = (*this->Cameras)[i];
::IRenderQueue2D *list = camera->get_RenderQueue2D();
list->Remove(drawable);
}
}

void ObjectManager::RemoveFromRender3D(::IDrawable3D* drawable)
{
this->RemoveByReference<IDrawable3D*>(this->Drawables3D, drawable);
for (int32_t i = 0; i < this->Cameras->Count(); i++) {
::ICamera *camera = (*this->Cameras)[i];
::IRenderQueue3D *list = camera->get_RenderQueue3D();
list->Remove(drawable);
}
}

void ObjectManager::RemoveFromUpdate(::IUpdateable* entity, uint8_t lastUpdateOrder)
{
    if (entity == nullptr)
    {
return;    }
    if (this->updateLoopActive)
    {
this->QueueUpdateOperation(entity, false);
return;    }
this->RemoveUpdateableFromList(entity);
}

void ObjectManager::RemoveInteractable(::IInteractable2D* entity)
{
this->Interactables->Remove(entity);
}

void ObjectManager::ReserveRender2DCapacity(uint8_t renderOrder, int32_t additional, uint16_t layerMask)
{
    if (additional < 1)
    {
return;    }
for (int32_t i = 0; i < this->Cameras->Count(); i++) {
::ICamera *camera = (*this->Cameras)[i];
    if ((layerMask & camera->get_LayerMask()) == 0)
    {
continue;
    }
::IRenderQueue2D *list = camera->get_RenderQueue2D();
list->EnsureCapacity(list->get_Count() + additional);
}
}

void ObjectManager::ReserveRender3DCapacity(uint8_t renderOrder, int32_t additional, uint16_t layerMask)
{
    if (additional < 1)
    {
return;    }
for (int32_t i = 0; i < this->Cameras->Count(); i++) {
::ICamera *camera = (*this->Cameras)[i];
    if ((layerMask & camera->get_LayerMask()) == 0)
    {
continue;
    }
::IRenderQueue3D *list = camera->get_RenderQueue3D();
list->EnsureCapacity(list->get_Count() + additional);
}
}

void ObjectManager::ReserveUpdateCapacity(uint8_t updateOrder, int32_t additional)
{
    if (additional < 1)
    {
return;    }
const int32_t desired = this->Updateables->Count() + additional;
    if (this->Updateables->Capacity() < desired)
    {
this->Updateables->SetCapacity(desired);
    }
}

void ObjectManager::Update()
{
{
auto __finallyGuard_882531ec = he_cpp_make_scope_exit([&]() {
this->updateLoopActive = false;
});
this->updateLoopActive = true;
for (int32_t i = 0; i < this->Updateables->Count(); i++) {
::IUpdateable *item = (*this->Updateables)[i];
item->Update();
}
}
this->ApplyPendingUpdateOperations();
}

void ObjectManager::AddUpdateableToList(::IUpdateable* entity)
{
const int32_t insertIndex = this->FindUpdateInsertIndex(entity->get_UpdateOrder());
this->Updateables->Insert(insertIndex, entity);
}

void ObjectManager::ApplyPendingUpdateOperations()
{
    if (this->pendingUpdateOperations->Count() == 0)
    {
return;    }
for (int32_t i = 0; i < this->pendingUpdateOperations->Count(); i++) {
::PendingUpdateOperation *op = (*this->pendingUpdateOperations)[i];
    if (op->get_IsAdd())
    {
this->AddUpdateableToList(op->get_Entity());
    }
else {
this->RemoveUpdateableFromList(op->get_Entity());
}
}
this->pendingUpdateOperations->Clear();
}

int32_t ObjectManager::FindUpdateInsertIndex(uint8_t updateOrder)
{
for (int32_t i = 0; i < this->Updateables->Count(); i++) {
    if (updateOrder < (*this->Updateables)[i]->get_UpdateOrder())
    {
return i;    }
}
return this->Updateables->Count();}

uint8_t ObjectManager::GetOrderForLayer(int32_t layerIndex, int32_t layerCount)
{
    if (layerCount < 1)
    {
throw new ArgumentOutOfRangeException("layerCount");
    }
int32_t clamped = layerIndex;
    if (clamped < 0)
    {
clamped = 0;
    }
else     if (clamped >= layerCount)
    {
clamped = layerCount - 1;
    }
    if (layerCount == 1)
    {
return 0;    }
const double step = 255.0 / (layerCount - 1);
const int32_t value = static_cast<int32_t>(Math::Round(clamped * step, MidpointRounding::AwayFromZero));
    if (value < 0)
    {
return 0;    }
    if (value > 255)
    {
return 255;    }
return static_cast<uint8_t>(value);}

void ObjectManager::InsertCameraByDrawOrder(::ICamera* camera)
{
int32_t insertIndex = this->Cameras->Count();
const uint8_t order = camera->get_CameraDrawOrder();
for (int32_t i = 0; i < this->Cameras->Count(); i++) {
    if (order < (*this->Cameras)[i]->get_CameraDrawOrder())
    {
insertIndex = i;
break;
    }
}
this->Cameras->Insert(insertIndex, camera);
}

void ObjectManager::QueueUpdateOperation(::IUpdateable* entity, bool isAdd)
{
this->pendingUpdateOperations->Add(new ::PendingUpdateOperation(entity, isAdd));
}

template <typename T>
bool ObjectManager::RemoveByReference(List<T>* list, T item)
{
for (int32_t i = 0; i < list->Count(); i++) {
    if (((*list)[i] == item))
    {
list->RemoveAt(i);
return true;    }
}
return false;}

void ObjectManager::RemoveUpdateableFromList(::IUpdateable* entity)
{
this->RemoveByReference<IUpdateable*>(this->Updateables, entity);
}

