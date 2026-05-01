#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class CameraClearSettings;
class Entity;
class IRenderQueue2D;
class IRenderQueue3D;
class RenderTarget;
class float4;

#include "CameraClearSettings.hpp"
#include "Entity.hpp"
#include "IRenderQueue2D.hpp"
#include "IRenderQueue3D.hpp"
#include "RenderTarget.hpp"
#include "float4.hpp"

class ICamera
{
public:
    virtual uint8_t get_CameraDrawOrder() = 0;

    virtual void set_CameraDrawOrder(uint8_t value) = 0;

    virtual ::CameraClearSettings get_ClearSettings() = 0;

    virtual void set_ClearSettings(::CameraClearSettings value) = 0;

    virtual uint16_t get_LayerMask() = 0;

    virtual void set_LayerMask(uint16_t value) = 0;

    virtual ::Entity* get_Parent() = 0;

    virtual ::IRenderQueue2D* get_RenderQueue2D() = 0;

    virtual ::IRenderQueue3D* get_RenderQueue3D() = 0;

    virtual ::RenderTarget* get_RenderTarget() = 0;

    virtual void set_RenderTarget(::RenderTarget* value) = 0;

    virtual ::float4 get_Viewport() = 0;

    virtual void set_Viewport(::float4 value) = 0;
};
