#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ObjectManager;
class Core;
class ICamera;
class float4;
class IInteractable2D;
class float2;
class float3;
class Entity;
class IDrawable2D;

#include "ObjectManager.hpp"
#include "Core.hpp"
#include "runtime/native_list.hpp"
#include "ObjectManager.hpp"
#include "runtime/native_list.hpp"
#include "PointerInteraction.hpp"
#include "PointerInteraction.hpp"
#include "InputSystem.hpp"
#include "ICamera.hpp"
#include "float4.hpp"
#include "ICamera.hpp"
#include "int2.hpp"
#include "IInteractable2D.hpp"
#include "IInteractable2D.hpp"
#include "runtime/native_list.hpp"
#include "float2.hpp"
#include "float3.hpp"
#include "Entity.hpp"
#include "Core.hpp"
#include "PointerCursorKind.hpp"
#include "InputSystem.hpp"
#include "IDrawable2D.hpp"

class PointerInteractionSystem
{
public:
    virtual ~PointerInteractionSystem() = default;

    ::Core* Core;

    ::Core* get_Core();
    void set_Core(::Core* value);

    ::IInteractable2D* Highlighted;

    ::IInteractable2D* get_Highlighted();
    void set_Highlighted(::IInteractable2D* value);

    ::PointerCursorKind get_HoverCursor();

    ::IInteractable2D* Hovering;

    ::IInteractable2D* get_Hovering();
    void set_Hovering(::IInteractable2D* value);

    InputSystem* Input;

    InputSystem* get_Input();
    void set_Input(InputSystem* value);

    PointerInteractionSystem(::Core* core, InputSystem* inputSystem);

    void Update();
private:
    ::ICamera* capturedCamera;

    bool CandidateIsInFront(uint8_t candidateRenderOrder, int32_t candidateDrawableIndex, int32_t candidateInteractableIndex, uint8_t currentRenderOrder, int32_t currentDrawableIndex, int32_t currentInteractableIndex);

    ::ICamera* FindCameraForInteractableAt(::IInteractable2D* interactable, int32_t x, int32_t y);

    void GetRelativePointerForInteractable(::IInteractable2D* interactable, int32_t x, int32_t y, ::ICamera* camera, int32_t& relativeX, int32_t& relativeY);

    uint8_t GetTopDrawableRenderOrder(List<::IDrawable2D*>* drawables2D, ::IInteractable2D* interactable, uint16_t camMask, int32_t& candidateDrawableIndex);

    ::ICamera* GetTopmostCameraAt(int32_t x, int32_t y);
};
