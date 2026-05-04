#ifdef DrawText
#undef DrawText
#endif
#include "PointerInteractionSystem.hpp"
#include "ObjectManager.hpp"
#include "Core.hpp"
#include "runtime/native_list.hpp"
#include "PointerInteraction.hpp"
#include "InputSystem.hpp"
#include "ICamera.hpp"
#include "float4.hpp"
#include "int2.hpp"
#include "IInteractable2D.hpp"
#include "float2.hpp"
#include "float3.hpp"
#include "Entity.hpp"
#include "PointerCursorKind.hpp"
#include "IDrawable2D.hpp"
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
#include "system/io/stream.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"

::Core* PointerInteractionSystem::get_Core()
{
return this->Core;
}

void PointerInteractionSystem::set_Core(::Core* value)
{
this->Core = value;
}

::IInteractable2D* PointerInteractionSystem::get_Highlighted()
{
return this->Highlighted;
}

void PointerInteractionSystem::set_Highlighted(::IInteractable2D* value)
{
this->Highlighted = value;
}

::PointerCursorKind PointerInteractionSystem::get_HoverCursor()
{
    if (this->Hovering == nullptr)
    {
return PointerCursorKind::Default;    }
return this->Hovering->get_HoverCursor();}

::IInteractable2D* PointerInteractionSystem::get_Hovering()
{
return this->Hovering;
}

void PointerInteractionSystem::set_Hovering(::IInteractable2D* value)
{
this->Hovering = value;
}

InputSystem* PointerInteractionSystem::get_Input()
{
return this->Input;
}

void PointerInteractionSystem::set_Input(InputSystem* value)
{
this->Input = value;
}

PointerInteractionSystem::PointerInteractionSystem(::Core* core, InputSystem* inputSystem) : Core(), Highlighted(), Hovering(), Input(), capturedCamera()
{
this->set_Core((core != nullptr ? core : throw new ArgumentNullException("core")));
this->set_Input((inputSystem != nullptr ? inputSystem : throw new ArgumentNullException("inputSystem")));
}

void PointerInteractionSystem::Update()
{
::ObjectManager *objectManager = this->Core->get_ObjectManager();
List<::IInteractable2D*> *interactables = objectManager->get_Interactables();
List<::IDrawable2D*> *drawables2D = objectManager->get_Drawables2D();
::PointerInteraction interaction = PointerInteraction::None;
    if (this->Input->WasMouseLeftButtonReleased())
    {
interaction = PointerInteraction::Release;
    }
else     if (this->Input->WasMouseLeftButtonPressed())
    {
interaction = PointerInteraction::Press;
    }
int32_t mouseX = this->Input->GetMouseX();
int32_t mouseY = this->Input->GetMouseY();
::ICamera *topCamera = this->GetTopmostCameraAt(mouseX, mouseY);
    if (topCamera != nullptr)
    {
::float4 viewport = topCamera->get_Viewport();
mouseX -= static_cast<int32_t>(viewport.X);
mouseY -= static_cast<int32_t>(viewport.Y);
    }
    if (this->Highlighted != nullptr)
    {
int32_t pointerX;
int32_t pointerY;
this->GetRelativePointerForInteractable(this->Highlighted, this->Input->GetMouseX(), this->Input->GetMouseY(), this->capturedCamera, pointerX, pointerY);
const int32_t deltaX = this->Input->GetMouseDeltaX();
const int32_t deltaY = this->Input->GetMouseDeltaY();
    if (interaction == PointerInteraction::None && (deltaX != 0 || deltaY != 0))
    {
interaction = PointerInteraction::Hover;
    }
int2 *pointer = new int2(pointerX, pointerY);
int2 *delta = new int2(deltaX, deltaY);
this->Highlighted->OnCursor(pointer, delta, interaction);
    if (interaction == PointerInteraction::Release)
    {
this->set_Highlighted(nullptr);
this->capturedCamera = nullptr;
    }
return;    }
::IInteractable2D *hit = nullptr;
uint8_t hitRenderOrder = 0;
int32_t hitDrawableIndex = -1;
int32_t hitInteractableIndex = -1;
    if (topCamera != nullptr)
    {
const uint16_t camMask = topCamera->get_LayerMask();
for (int32_t i = 0; i < interactables->Count(); i++) {
::IInteractable2D *interactable = (*interactables)[i];
    if ((interactable->get_Parent()->get_LayerMask() & camMask) == 0)
    {
continue;
    }
::float3 position = interactable->get_Parent()->get_Position();
::float4 rect = ::float4(position.X, position.Y, interactable->get_Size()->X, interactable->get_Size()->Y);
    if (!rect.Contains(mouseX, mouseY))
    {
continue;
    }
int32_t candidateDrawableIndex;
const uint8_t candidateRenderOrder = this->GetTopDrawableRenderOrder(drawables2D, interactable, camMask, candidateDrawableIndex);
    if (hit == nullptr || this->CandidateIsInFront(candidateRenderOrder, candidateDrawableIndex, i, hitRenderOrder, hitDrawableIndex, hitInteractableIndex))
    {
hit = interactable;
hitRenderOrder = candidateRenderOrder;
hitDrawableIndex = candidateDrawableIndex;
hitInteractableIndex = i;
    }
}
    }
const bool hoveringChanged = hit != this->Hovering;
    if (hoveringChanged && this->Hovering != nullptr)
    {
int32_t prevPointerX;
int32_t prevPointerY;
::ICamera *hoverCamera = this->FindCameraForInteractableAt(this->Hovering, this->Input->GetMouseX(), this->Input->GetMouseY());
this->GetRelativePointerForInteractable(this->Hovering, this->Input->GetMouseX(), this->Input->GetMouseY(), hoverCamera, prevPointerX, prevPointerY);
int2 *previousPointer = new int2(prevPointerX, prevPointerY);
int2 *zeroDelta = new int2(0, 0);
this->Hovering->OnCursor(previousPointer, zeroDelta, PointerInteraction::Leave);
    }
this->set_Hovering(hit);
    if (this->Hovering == nullptr)
    {
return;    }
int32_t currentPointerX;
int32_t currentPointerY;
this->GetRelativePointerForInteractable(this->Hovering, this->Input->GetMouseX(), this->Input->GetMouseY(), topCamera, currentPointerX, currentPointerY);
const int32_t currentDeltaX = this->Input->GetMouseDeltaX();
const int32_t currentDeltaY = this->Input->GetMouseDeltaY();
    if (interaction == PointerInteraction::Press)
    {
    if (hoveringChanged)
    {
int2 *hoverPointer = new int2(currentPointerX, currentPointerY);
int2 *hoverDelta = new int2(currentDeltaX, currentDeltaY);
this->Hovering->OnCursor(hoverPointer, hoverDelta, PointerInteraction::Hover);
    }
this->set_Highlighted(this->Hovering);
this->capturedCamera = topCamera;
int2 *pressPointer = new int2(currentPointerX, currentPointerY);
int2 *pressDelta = new int2(currentDeltaX, currentDeltaY);
this->Hovering->OnCursor(pressPointer, pressDelta, PointerInteraction::Press);
    }
else     if (hoveringChanged || currentDeltaX != 0 || currentDeltaY != 0)
    {
int2 *hoverPointer = new int2(currentPointerX, currentPointerY);
int2 *hoverDelta = new int2(currentDeltaX, currentDeltaY);
this->Hovering->OnCursor(hoverPointer, hoverDelta, PointerInteraction::Hover);
    }
}

bool PointerInteractionSystem::CandidateIsInFront(uint8_t candidateRenderOrder, int32_t candidateDrawableIndex, int32_t candidateInteractableIndex, uint8_t currentRenderOrder, int32_t currentDrawableIndex, int32_t currentInteractableIndex)
{
    if (candidateRenderOrder != currentRenderOrder)
    {
return candidateRenderOrder > currentRenderOrder;    }
    if (candidateDrawableIndex != currentDrawableIndex)
    {
return candidateDrawableIndex > currentDrawableIndex;    }
return candidateInteractableIndex > currentInteractableIndex;}

::ICamera* PointerInteractionSystem::FindCameraForInteractableAt(::IInteractable2D* interactable, int32_t x, int32_t y)
{
    if (interactable == nullptr)
    {
return nullptr;    }
return this->GetTopmostCameraAt(x, y);}

void PointerInteractionSystem::GetRelativePointerForInteractable(::IInteractable2D* interactable, int32_t x, int32_t y, ::ICamera* camera, int32_t& relativeX, int32_t& relativeY)
{
::float2 local = ::float2(x, y);
    if (camera != nullptr)
    {
::float4 viewport = camera->get_Viewport();
local.X -= viewport.X;
local.Y -= viewport.Y;
    }
::float3 position = interactable->get_Parent()->get_Position();
relativeX = static_cast<int32_t>(Math::Round(local.X - position.X));
relativeY = static_cast<int32_t>(Math::Round(local.Y - position.Y));
}

uint8_t PointerInteractionSystem::GetTopDrawableRenderOrder(List<::IDrawable2D*>* drawables2D, ::IInteractable2D* interactable, uint16_t camMask, int32_t& candidateDrawableIndex)
{
candidateDrawableIndex = -1;
uint8_t renderOrder = 0;
    if (drawables2D == nullptr || interactable == nullptr)
    {
return renderOrder;    }
for (int32_t i = 0; i < drawables2D->Count(); i++) {
::IDrawable2D *drawable = (*drawables2D)[i];
    if (drawable->get_Parent() != interactable->get_Parent())
    {
continue;
    }
    if ((drawable->get_Parent()->get_LayerMask() & camMask) == 0)
    {
continue;
    }
    if (candidateDrawableIndex < 0 || drawable->get_RenderOrder2D() >= renderOrder)
    {
renderOrder = drawable->get_RenderOrder2D();
candidateDrawableIndex = i;
    }
}
return renderOrder;}

::ICamera* PointerInteractionSystem::GetTopmostCameraAt(int32_t x, int32_t y)
{
List<::ICamera*> *cameras = this->Core->get_ObjectManager()->get_Cameras();
for (int32_t i = cameras->Count() - 1; i >= 0; i--) {
::ICamera *camera = (*cameras)[i];
    if (camera->get_Viewport().Contains(x, y))
    {
return camera;    }
}
return nullptr;}

