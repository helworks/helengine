#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class IRenderQueue3D;
class ICamera;
class IRenderQueue2D;
class IDrawable2D;
class IDrawable3D;
class Entity;
class IInteractable2D;
class IUpdateable;
class PendingUpdateOperation;
class CoreInitializationOptions;

#include "runtime/native_list.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_list.hpp"
#include "IRenderQueue3D.hpp"
#include "ICamera.hpp"
#include "IRenderQueue2D.hpp"
#include "runtime/native_list.hpp"
#include "IRenderQueue3D.hpp"
#include "IRenderQueue2D.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "ICamera.hpp"
#include "runtime/native_list.hpp"
#include "IDrawable2D.hpp"
#include "runtime/native_list.hpp"
#include "IDrawable3D.hpp"
#include "runtime/native_list.hpp"
#include "Entity.hpp"
#include "runtime/native_list.hpp"
#include "IInteractable2D.hpp"
#include "runtime/native_list.hpp"
#include "IUpdateable.hpp"
#include "runtime/native_list.hpp"
#include "PendingUpdateOperation.hpp"
#include "CoreInitializationOptions.hpp"
#include "runtime/native_list.hpp"

class ObjectManager
{
public:
    virtual ~ObjectManager() = default;

    List<::ICamera*>* Cameras;

    List<::ICamera*>* get_Cameras();
    void set_Cameras(List<::ICamera*>* value);

    List<::IDrawable2D*>* Drawables2D;

    List<::IDrawable2D*>* get_Drawables2D();
    void set_Drawables2D(List<::IDrawable2D*>* value);

    List<::IDrawable3D*>* Drawables3D;

    List<::IDrawable3D*>* get_Drawables3D();
    void set_Drawables3D(List<::IDrawable3D*>* value);

    List<::Entity*>* Entities;

    List<::Entity*>* get_Entities();
    void set_Entities(List<::Entity*>* value);

    List<::IInteractable2D*>* Interactables;

    List<::IInteractable2D*>* get_Interactables();
    void set_Interactables(List<::IInteractable2D*>* value);

    uint8_t RenderOrderLayers3D;

    uint8_t get_RenderOrderLayers3D();
    void set_RenderOrderLayers3D(uint8_t value);

    uint8_t UpdateOrderLayers;

    uint8_t get_UpdateOrderLayers();
    void set_UpdateOrderLayers(uint8_t value);

    List<::IUpdateable*>* Updateables;

    List<::IUpdateable*>* get_Updateables();
    void set_Updateables(List<::IUpdateable*>* value);

    uint8_t GetRenderOrderForLayer3D(int32_t layerIndex);

    uint8_t GetUpdateOrderForLayer(int32_t layerIndex);

    ObjectManager(::CoreInitializationOptions* settings);

    void RegisterCamera(::ICamera* camera);

    virtual void RegisterEntity(::Entity* entity);

    void RegisterForRender2D(::IDrawable2D* drawable);

    void RegisterForRender3D(::IDrawable3D* drawable);

    virtual void RegisterForUpdate(::IUpdateable* entity);

    virtual void RegisterInteractable(::IInteractable2D* entity);

    virtual void RemoveCamera(::ICamera* camera);

    virtual void RemoveEntity(::Entity* entity);

    void RemoveFromRender2D(::IDrawable2D* drawable);

    void RemoveFromRender3D(::IDrawable3D* drawable);

    virtual void RemoveFromUpdate(::IUpdateable* entity, uint8_t lastUpdateOrder);

    virtual void RemoveInteractable(::IInteractable2D* entity);

    void ReserveRender2DCapacity(uint8_t renderOrder, int32_t additional, uint16_t layerMask);

    void ReserveRender3DCapacity(uint8_t renderOrder, int32_t additional, uint16_t layerMask);

    void ReserveUpdateCapacity(uint8_t updateOrder, int32_t additional);

    virtual void Update();
private:
    List<::PendingUpdateOperation*>* pendingUpdateOperations;

    bool updateLoopActive;

    void AddUpdateableToList(::IUpdateable* entity);

    void ApplyPendingUpdateOperations();

    int32_t FindUpdateInsertIndex(uint8_t updateOrder);

    static uint8_t GetOrderForLayer(int32_t layerIndex, int32_t layerCount);

    void InsertCameraByDrawOrder(::ICamera* camera);

    void QueueUpdateOperation(::IUpdateable* entity, bool isAdd);

    template <typename T>
    static bool RemoveByReference(List<T>* list, T item);

    void RemoveUpdateableFromList(::IUpdateable* entity);
};
