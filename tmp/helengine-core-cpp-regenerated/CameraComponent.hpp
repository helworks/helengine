#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Component;
class ICamera;
class CoreInitializationOptions;
class Core;
class Entity;
class ObjectManager;
class CameraClearSettings;
class IRenderQueue2D;
class IRenderQueue3D;
class RenderTarget;
class float4;
class RenderList2D;
class RenderList3D;

#include "Component.hpp"
#include "ICamera.hpp"
#include "runtime/native_exceptions.hpp"
#include "CoreInitializationOptions.hpp"
#include "Core.hpp"
#include "Core.hpp"
#include "CoreInitializationOptions.hpp"
#include "Component.hpp"
#include "Entity.hpp"
#include "ObjectManager.hpp"
#include "CameraClearSettings.hpp"
#include "IRenderQueue2D.hpp"
#include "IRenderQueue3D.hpp"
#include "RenderTarget.hpp"
#include "float4.hpp"
#include "RenderList2D.hpp"
#include "RenderList3D.hpp"
#include "Entity.hpp"

class CameraComponent : public Component, public ICamera
{
public:
    virtual ~CameraComponent() = default;

    uint8_t get_CameraDrawOrder();

    void set_CameraDrawOrder(uint8_t value);

    ::CameraClearSettings ClearSettings;

    ::CameraClearSettings get_ClearSettings();
    void set_ClearSettings(::CameraClearSettings value);

    uint16_t get_LayerMask();

    void set_LayerMask(uint16_t value);

    ::IRenderQueue2D* get_RenderQueue2D();

    ::IRenderQueue3D* get_RenderQueue3D();

    ::RenderTarget* RenderTarget;

    ::RenderTarget* get_RenderTarget();
    void set_RenderTarget(::RenderTarget* value);

    ::float4 Viewport;

    ::float4 get_Viewport();
    void set_Viewport(::float4 value);

    CameraComponent();

    void ComponentAdded(::Entity* entity);

    void ParentEnabledChange(bool newEnabled);

    ::Entity* get_Parent();

    void set_Parent(::Entity* value);
private:
    uint8_t cameraDrawOrder;

    uint16_t layerMask;

    ::RenderList2D* renderList2D;

    ::RenderList3D* renderList3D;

    void InitializeLists();
};
