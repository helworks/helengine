#ifdef DrawText
#undef DrawText
#endif
#include "InputManager.hpp"
#include "Keyboard.hpp"
#include "Mouse.hpp"
#include "int2.hpp"
#include "ButtonState.hpp"
#include "ObjectManager.hpp"
#include "Core.hpp"
#include "runtime/native_list.hpp"
#include "PointerInteraction.hpp"
#include "ICamera.hpp"
#include "float2.hpp"
#include "MouseState.hpp"
#include "float4.hpp"
#include "IInteractable2D.hpp"
#include "runtime/native_exceptions.hpp"
#include "Entity.hpp"
#include "PointerCursorKind.hpp"
#include "IDrawable2D.hpp"
#include "float3.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_event.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/io/stream.hpp"
#include "system/math.hpp"

::IInteractable2D* InputManager::get_Highlighted()
{
return this->Highlighted;
}

void InputManager::set_Highlighted(::IInteractable2D* value)
{
this->Highlighted = value;
}

::PointerCursorKind InputManager::get_HoverCursor()
{
    if (this->Hovering == nullptr)
    {
return PointerCursorKind::Default;    }
return this->Hovering->get_HoverCursor();}

::IInteractable2D* InputManager::get_Hovering()
{
return this->Hovering;
}

void InputManager::set_Hovering(::IInteractable2D* value)
{
this->Hovering = value;
}

void InputManager::EarlyUpdate()
{
this->ApplyPointerWrapState();
this->EnsureInputStateCaptured();
}

::int2 InputManager::GetMouseDelta()
{
return this->mouseDelta;}

::ButtonState InputManager::GetMouseLeftButtonState()
{
return this->mouseState.get_LeftButton();}

::ButtonState InputManager::GetMouseMiddleButtonState()
{
return this->mouseState.get_MiddleButton();}

::int2 InputManager::GetMousePosition()
{
return ::int2(this->mouseState.get_X(), this->mouseState.get_Y());}

::ButtonState InputManager::GetMouseRightButtonState()
{
return this->mouseState.get_RightButton();}

int32_t InputManager::GetMouseScrollWheelDelta()
{
return this->mouseState.get_ScrollWheelValue() - this->lastMouseState.get_ScrollWheelValue();}

int32_t InputManager::GetMouseScrollWheelValue()
{
return this->mouseState.get_ScrollWheelValue();}

::ButtonState InputManager::GetMouseXButton1State()
{
return this->mouseState.get_XButton1();}

::ButtonState InputManager::GetMouseXButton2State()
{
return this->mouseState.get_XButton2();}

InputManager::InputManager() : Highlighted(), Hovering(), ActivePointerWrapEnabled(), RequestedPointerWrapEnabled(), capturedCamera(), hasCapturedInput(), keyboardState(), lastKeyboardState(), lastMouseState(), mouseDelta(), mouseState(), Keyboard(), Mouse(), core()
{
this->core = Core::get_Instance();
}

bool InputManager::IsKeyDown(::Keys key)
{
return this->keyboardState.IsKeyDown(key);}

bool InputManager::IsKeyUp(::Keys key)
{
return this->keyboardState.IsKeyUp(key);}

void InputManager::RequestPointerWrapEnabled()
{
this->RequestedPointerWrapEnabled = true;
}

void InputManager::SetKeyboardActive(bool isActive)
{
    if (this->Keyboard == nullptr)
    {
return;    }
this->Keyboard->SetActive(isActive);
}

void InputManager::SetPointerWrapEnabled(bool isEnabled)
{
this->ActivePointerWrapEnabled = isEnabled;
this->RequestedPointerWrapEnabled = isEnabled;
    if (this->Mouse == nullptr)
    {
return;    }
this->Mouse->SetPointerWrapEnabled(isEnabled);
}

void InputManager::Update()
{
this->EnsureInputStateCaptured();
{
auto __finallyGuard_3082c3a9 = he_cpp_make_scope_exit([&]() {
this->CommitPointerWrapState();
this->hasCapturedInput = false;
});
::ObjectManager *objectManager = this->core->get_ObjectManager();
List<::IInteractable2D*> *interactables = objectManager->get_Interactables();
List<::IDrawable2D*> *drawables2D = objectManager->get_Drawables2D();
::PointerInteraction interaction = PointerInteraction::None;
    if (this->mouseState.get_LeftButton() == ButtonState::Released && this->lastMouseState.get_LeftButton() == ButtonState::Pressed)
    {
interaction = PointerInteraction::Release;
    }
else     if (this->mouseState.get_LeftButton() == ButtonState::Pressed && this->lastMouseState.get_LeftButton() == ButtonState::Released)
    {
interaction = PointerInteraction::Press;
    }
::ICamera *topCamera = this->GetTopmostCameraAt(this->mouseState.get_X(), this->mouseState.get_Y());
::float2 localMouse = ::float2(this->mouseState.get_X(), this->mouseState.get_Y());
    if (topCamera != nullptr)
    {
::float4 vp = topCamera->get_Viewport();
localMouse.X -= vp.X;
localMouse.Y -= vp.Y;
    }
    if (this->Highlighted != nullptr)
    {
::float2 capturedLocal = ::float2(this->mouseState.get_X(), this->mouseState.get_Y());
    if (this->capturedCamera != nullptr)
    {
::float4 cvp = this->capturedCamera->get_Viewport();
capturedLocal.X -= cvp.X;
capturedLocal.Y -= cvp.Y;
    }
::int2 rel = ([&]() {
auto __ctor_arg_0711e9b2 = static_cast<int32_t>(MathF::Round(capturedLocal.X - this->Highlighted->get_Parent()->get_Position().X));
auto __ctor_arg_6e80e88e = static_cast<int32_t>(MathF::Round(capturedLocal.Y - this->Highlighted->get_Parent()->get_Position().Y));
return ::int2(__ctor_arg_0711e9b2, __ctor_arg_6e80e88e);
})();
const int32_t deltaX = this->mouseDelta.X;
const int32_t deltaY = this->mouseDelta.Y;
    if (interaction == PointerInteraction::None && (deltaX != 0 || deltaY != 0))
    {
interaction = PointerInteraction::Hover;
    }
this->Highlighted->OnCursor(rel, ::int2(deltaX, deltaY), interaction);
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
::float3 pos = interactable->get_Parent()->get_Position();
::int2 size = interactable->get_Size();
::float4 rect = ::float4(pos.X, pos.Y, size.X, size.Y);
    if (rect.Contains(localMouse.X, localMouse.Y))
    {
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
    }
const bool hoveringChanged = hit != this->Hovering;
    if (hoveringChanged)
    {
    if (this->Hovering != nullptr)
    {
::float2 prevLocal = ::float2(this->mouseState.get_X(), this->mouseState.get_Y());
::ICamera *prevCam = this->FindCameraForInteractableAt(this->Hovering, this->mouseState.get_X(), this->mouseState.get_Y());
    if (prevCam != nullptr)
    {
::float4 vp = prevCam->get_Viewport();
prevLocal.X -= vp.X;
prevLocal.Y -= vp.Y;
    }
::int2 prevRel = ([&]() {
auto __ctor_arg_66564ac0 = static_cast<int32_t>(MathF::Round(prevLocal.X - this->Hovering->get_Parent()->get_Position().X));
auto __ctor_arg_f88da3a9 = static_cast<int32_t>(MathF::Round(prevLocal.Y - this->Hovering->get_Parent()->get_Position().Y));
return ::int2(__ctor_arg_66564ac0, __ctor_arg_f88da3a9);
})();
this->Hovering->OnCursor(prevRel, ::int2(0, 0), PointerInteraction::Leave);
    }
this->set_Hovering(hit);
    }
    if (this->Hovering != nullptr)
    {
::int2 rel = ([&]() {
auto __ctor_arg_cec8bca8 = static_cast<int32_t>(MathF::Round(localMouse.X - this->Hovering->get_Parent()->get_Position().X));
auto __ctor_arg_5ac81e0e = static_cast<int32_t>(MathF::Round(localMouse.Y - this->Hovering->get_Parent()->get_Position().Y));
return ::int2(__ctor_arg_cec8bca8, __ctor_arg_5ac81e0e);
})();
const int32_t deltaX = this->mouseDelta.X;
const int32_t deltaY = this->mouseDelta.Y;
    if (interaction == PointerInteraction::Press)
    {
    if (hoveringChanged)
    {
this->Hovering->OnCursor(rel, ::int2(deltaX, deltaY), PointerInteraction::Hover);
    }
this->set_Highlighted(this->Hovering);
this->capturedCamera = topCamera;
this->Hovering->OnCursor(rel, ::int2(deltaX, deltaY), PointerInteraction::Press);
    }
else {
    if (hoveringChanged || deltaX != 0 || deltaY != 0)
    {
this->Hovering->OnCursor(rel, ::int2(deltaX, deltaY), PointerInteraction::Hover);
    }
}
    }
else {
    if (this->Hovering != nullptr)
    {
this->Hovering->OnCursor(::int2(0, 0), ::int2(0, 0), PointerInteraction::Leave);
this->set_Hovering(nullptr);
    }
}
}
}

bool InputManager::WasKeyPressed(::Keys key)
{
return this->keyboardState.IsKeyDown(key) && this->lastKeyboardState.IsKeyUp(key);}

bool InputManager::WasKeyReleased(::Keys key)
{
return this->keyboardState.IsKeyUp(key) && this->lastKeyboardState.IsKeyDown(key);}

bool InputManager::WasMouseLeftButtonPressed()
{
return this->WasMouseButtonPressed(this->mouseState.get_LeftButton(), this->lastMouseState.get_LeftButton());}

bool InputManager::WasMouseLeftButtonReleased()
{
return this->WasMouseButtonReleased(this->mouseState.get_LeftButton(), this->lastMouseState.get_LeftButton());}

bool InputManager::WasMouseMiddleButtonPressed()
{
return this->WasMouseButtonPressed(this->mouseState.get_MiddleButton(), this->lastMouseState.get_MiddleButton());}

bool InputManager::WasMouseMiddleButtonReleased()
{
return this->WasMouseButtonReleased(this->mouseState.get_MiddleButton(), this->lastMouseState.get_MiddleButton());}

bool InputManager::WasMouseRightButtonPressed()
{
return this->WasMouseButtonPressed(this->mouseState.get_RightButton(), this->lastMouseState.get_RightButton());}

bool InputManager::WasMouseRightButtonReleased()
{
return this->WasMouseButtonReleased(this->mouseState.get_RightButton(), this->lastMouseState.get_RightButton());}

bool InputManager::WasMouseXButton1Pressed()
{
return this->WasMouseButtonPressed(this->mouseState.get_XButton1(), this->lastMouseState.get_XButton1());}

bool InputManager::WasMouseXButton1Released()
{
return this->WasMouseButtonReleased(this->mouseState.get_XButton1(), this->lastMouseState.get_XButton1());}

bool InputManager::WasMouseXButton2Pressed()
{
return this->WasMouseButtonPressed(this->mouseState.get_XButton2(), this->lastMouseState.get_XButton2());}

bool InputManager::WasMouseXButton2Released()
{
return this->WasMouseButtonReleased(this->mouseState.get_XButton2(), this->lastMouseState.get_XButton2());}

::Keyboard* InputManager::get_Keyboard()
{
return this->Keyboard;
}

void InputManager::set_Keyboard(::Keyboard* value)
{
this->Keyboard = value;
}

::Mouse* InputManager::get_Mouse()
{
return this->Mouse;
}

void InputManager::set_Mouse(::Mouse* value)
{
this->Mouse = value;
}

void InputManager::ApplyPointerWrapState()
{
    if (this->Mouse == nullptr)
    {
return;    }
this->Mouse->SetPointerWrapEnabled(this->ActivePointerWrapEnabled);
}

bool InputManager::CandidateIsInFront(uint8_t candidateRenderOrder, int32_t candidateDrawableIndex, int32_t candidateInteractableIndex, uint8_t currentRenderOrder, int32_t currentDrawableIndex, int32_t currentInteractableIndex)
{
    if (candidateRenderOrder != currentRenderOrder)
    {
return candidateRenderOrder > currentRenderOrder;    }
    if (candidateDrawableIndex != currentDrawableIndex)
    {
return candidateDrawableIndex > currentDrawableIndex;    }
return candidateInteractableIndex > currentInteractableIndex;}

void InputManager::CaptureInputState()
{
this->lastMouseState = this->mouseState;
this->mouseState = this->Mouse->GetState();
::int2 pointerWrapDeltaOffset = this->Mouse->ConsumePointerWrapDeltaOffset();
this->mouseDelta = ::int2(this->mouseState.get_X() - this->lastMouseState.get_X() + pointerWrapDeltaOffset.X, this->mouseState.get_Y() - this->lastMouseState.get_Y() + pointerWrapDeltaOffset.Y);
this->lastKeyboardState = this->keyboardState;
this->keyboardState = this->Keyboard->GetState();
this->hasCapturedInput = true;
}

void InputManager::CommitPointerWrapState()
{
this->ActivePointerWrapEnabled = this->RequestedPointerWrapEnabled;
this->RequestedPointerWrapEnabled = false;
    if (this->Mouse == nullptr)
    {
return;    }
this->Mouse->SetPointerWrapEnabled(this->ActivePointerWrapEnabled);
}

void InputManager::EnsureInputStateCaptured()
{
    if (this->hasCapturedInput)
    {
return;    }
this->CaptureInputState();
}

::ICamera* InputManager::FindCameraForInteractableAt(::IInteractable2D* interactable, int32_t mouseX, int32_t mouseY)
{
List<::ICamera*> *cameras = this->core->get_ObjectManager()->get_Cameras();
for (int32_t i = cameras->Count() - 1; i >= 0; i--) {
::ICamera *cam = (*cameras)[i];
    if ((interactable->get_Parent()->get_LayerMask() & cam->get_LayerMask()) == 0)
    {
continue;
    }
::float4 vp = cam->get_Viewport();
    if (mouseX >= vp.X && mouseX < vp.X + vp.Z && mouseY >= vp.Y && mouseY < vp.Y + vp.W)
    {
return cam;    }
}
return nullptr;}

uint8_t InputManager::GetTopDrawableRenderOrder(List<::IDrawable2D*>* drawables, ::IInteractable2D* interactable, uint16_t cameraLayerMask, int32_t& highestDrawableIndex)
{
    if (drawables == nullptr)
    {
throw new ArgumentNullException("drawables");
    }
    if (interactable == nullptr)
    {
throw new ArgumentNullException("interactable");
    }
uint8_t highestRenderOrder = 0;
highestDrawableIndex = -1;
::Entity *interactableRoot = interactable->get_Parent();
for (int32_t i = 0; i < drawables->Count(); i++) {
::IDrawable2D *drawable = (*drawables)[i];
    if (drawable == nullptr || drawable->get_Parent() == nullptr)
    {
continue;
    }
    if ((drawable->get_Parent()->get_LayerMask() & cameraLayerMask) == 0)
    {
continue;
    }
    if (!this->IsSameEntityOrDescendant(drawable->get_Parent(), interactableRoot))
    {
continue;
    }
    if (highestDrawableIndex < 0 || drawable->get_RenderOrder2D() > highestRenderOrder || (drawable->get_RenderOrder2D() == highestRenderOrder && i > highestDrawableIndex))
    {
highestRenderOrder = drawable->get_RenderOrder2D();
highestDrawableIndex = i;
    }
}
return highestRenderOrder;}

::ICamera* InputManager::GetTopmostCameraAt(int32_t x, int32_t y)
{
List<::ICamera*> *cameras = this->core->get_ObjectManager()->get_Cameras();
for (int32_t i = cameras->Count() - 1; i >= 0; i--) {
::ICamera *cam = (*cameras)[i];
::float4 vp = cam->get_Viewport();
    if (x >= vp.X && x < vp.X + vp.Z && y >= vp.Y && y < vp.Y + vp.W)
    {
return cam;    }
}
return nullptr;}

bool InputManager::IsSameEntityOrDescendant(::Entity* candidate, ::Entity* root)
{
    if (candidate == nullptr)
    {
throw new ArgumentNullException("candidate");
    }
    if (root == nullptr)
    {
throw new ArgumentNullException("root");
    }
::Entity *current = candidate;
while (current != nullptr) {
    if ((current == root))
    {
return true;    }
current = current->get_Parent();
}
return false;}

bool InputManager::WasMouseButtonPressed(::ButtonState current, ::ButtonState previous)
{
return current == ButtonState::Pressed && previous == ButtonState::Released;}

bool InputManager::WasMouseButtonReleased(::ButtonState current, ::ButtonState previous)
{
return current == ButtonState::Released && previous == ButtonState::Pressed;}

