#ifdef DrawText
#undef DrawText
#endif
#include "InputSystem.hpp"
#include "InputFrameState.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_dictionary.hpp"
#include "InputActionState.hpp"
#include "InputPointerState.hpp"
#include "InputGamepadState.hpp"
#include "InputTextState.hpp"
#include "int2.hpp"
#include "ButtonState.hpp"
#include "IInputBackend.hpp"
#include "MouseState.hpp"
#include "InputActionId.hpp"
#include "InputBinding.hpp"
#include "KeyboardState.hpp"
#include "InputPointerButton.hpp"
#include "InputDeviceKind.hpp"
#include "InputControlKind.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_string.hpp"
#include "system/math.hpp"

::IInputBackend* InputSystem::get_Backend()
{
return this->Backend;
}

void InputSystem::set_Backend(::IInputBackend* value)
{
this->Backend = value;
}

::InputFrameState InputSystem::get_CurrentFrame()
{
return this->CurrentFrame;
}

void InputSystem::set_CurrentFrame(::InputFrameState value)
{
this->CurrentFrame = value;
}

bool InputSystem::get_IsPointerWrapEnabled()
{
return this->ActivePointerWrapEnabled;}

void InputSystem::ClearBindings(::InputContextId contextId)
{
for (int32_t i = this->Bindings->Count() - 1; i >= 0; i--) {
    if ((*this->Bindings)[i].get_ContextId() != contextId)
    {
continue;
    }
this->Bindings->RemoveAt(i);
}
}

void InputSystem::ClearContexts()
{
this->ActiveContextStack->Clear();
}

void InputSystem::EarlyUpdate()
{
this->ApplyPointerWrapState();
this->EnsureInputStateCaptured();
this->ResolveBindings();
}

::InputActionState InputSystem::GetActionState(::InputActionId actionId)
{
::InputActionState currentState;
    if (this->CurrentActionStates->TryGetValue(actionId.get_Value(), currentState))
    {
return currentState;    }
return ::InputActionState();}

float InputSystem::GetActionValue(::InputActionId actionId)
{
return this->GetActionState(actionId).get_Value();}

int32_t InputSystem::GetGamepadCount()
{
return this->CurrentFrame.get_GamepadCount();}

::InputGamepadState InputSystem::GetGamepadState(int32_t index)
{
    if (this->CurrentFrame.get_Gamepads() == nullptr)
    {
return ::InputGamepadState();    }
    if (index < 0 || index >= this->CurrentFrame.get_GamepadCount() || index >= this->CurrentFrame.get_Gamepads()->Length)
    {
return ::InputGamepadState();    }
return (*this->CurrentFrame.get_Gamepads())[index];}

::int2 InputSystem::GetMouseDelta()
{
return this->mouseDelta;}

int32_t InputSystem::GetMouseDeltaX()
{
return this->mouseDelta.X;}

int32_t InputSystem::GetMouseDeltaY()
{
return this->mouseDelta.Y;}

::ButtonState InputSystem::GetMouseLeftButtonState()
{
return this->mouseState.get_LeftButton();}

::ButtonState InputSystem::GetMouseMiddleButtonState()
{
return this->mouseState.get_MiddleButton();}

::int2 InputSystem::GetMousePosition()
{
return ::int2(this->mouseState.get_X(), this->mouseState.get_Y());}

::ButtonState InputSystem::GetMouseRightButtonState()
{
return this->mouseState.get_RightButton();}

int32_t InputSystem::GetMouseScrollWheelDelta()
{
return this->mouseState.get_ScrollWheelValue() - this->lastMouseState.get_ScrollWheelValue();}

int32_t InputSystem::GetMouseScrollWheelValue()
{
return this->mouseState.get_ScrollWheelValue();}

int32_t InputSystem::GetMouseX()
{
return this->mouseState.get_X();}

::ButtonState InputSystem::GetMouseXButton1State()
{
return this->mouseState.get_XButton1();}

::ButtonState InputSystem::GetMouseXButton2State()
{
return this->mouseState.get_XButton2();}

int32_t InputSystem::GetMouseY()
{
return this->mouseState.get_Y();}

::InputPointerState InputSystem::GetPointerState()
{
return this->CurrentFrame.get_Pointer();}

::InputTextState InputSystem::GetTextState()
{
return this->CurrentFrame.get_Text();}

InputSystem::InputSystem() : Backend(), CurrentFrame(), ActiveContextStack(), ActivePointerWrapEnabled(), Bindings(), CurrentActionStates(), FrameUpdateHandler(), KeyboardIsActive(), MouseClientBounds(), PointerWrapDeltaOffset(), PreviousActionStates(), RequestedPointerWrapEnabled(), SeenActionIds(), hasCapturedInput(), keyboardState(), lastKeyboardState(), lastMouseState(), mouseDelta(), mouseState()
{
this->Bindings = new List<::InputBinding>();
this->ActiveContextStack = new List<int32_t>();
this->CurrentActionStates = new Dictionary<int32_t, ::InputActionState>();
this->PreviousActionStates = new Dictionary<int32_t, ::InputActionState>();
this->SeenActionIds = new List<int32_t>();
this->keyboardState = ::KeyboardState();
this->mouseState = ::MouseState(0, 0, 0, ButtonState::Released, ButtonState::Released, ButtonState::Released, ButtonState::Released, ButtonState::Released);
this->set_CurrentFrame(([&]() {
auto __object_00000000 = ::InputFrameState();
__object_00000000.set_Keyboard(this->keyboardState);
__object_00000000.set_Mouse(this->mouseState);
return __object_00000000;
})());
}

bool InputSystem::IsActionDown(::InputActionId actionId)
{
return this->GetActionState(actionId).get_IsDown();}

bool InputSystem::IsKeyDown(::Keys key)
{
return this->keyboardState.IsKeyDown(key);}

bool InputSystem::IsKeyUp(::Keys key)
{
return this->keyboardState.IsKeyUp(key);}

void InputSystem::PopContext(::InputContextId contextId)
{
for (int32_t i = this->ActiveContextStack->Count() - 1; i >= 0; i--) {
    if ((*this->ActiveContextStack)[i] != contextId.get_Value())
    {
continue;
    }
this->ActiveContextStack->RemoveAt(i);
return;}
}

void InputSystem::PushContext(::InputContextId contextId)
{
this->ActiveContextStack->Add(contextId.get_Value());
}

void InputSystem::RegisterBinding(::InputBinding binding)
{
this->Bindings->Add(binding);
}

void InputSystem::RequestPointerWrapEnabled()
{
this->RequestedPointerWrapEnabled = true;
}

void InputSystem::SetBackend(::IInputBackend* backend)
{
this->set_Backend(backend);
}

void InputSystem::SetFrameUpdateHandler(Action<>* handler)
{
this->FrameUpdateHandler = handler;
}

void InputSystem::SetKeyboardActive(bool isActive)
{
this->KeyboardIsActive = isActive;
}

void InputSystem::SetKeyboardState(::KeyboardState state)
{
this->keyboardState = state;
::InputFrameState currentFrame = this->CurrentFrame;
currentFrame.set_Keyboard(state);
this->set_CurrentFrame(currentFrame);
}

void InputSystem::SetMouseClientBounds(::int2 clientBounds)
{
this->MouseClientBounds = clientBounds;
}

void InputSystem::SetMouseState(::MouseState state)
{
this->mouseState = state;
::InputFrameState currentFrame = this->CurrentFrame;
currentFrame.set_Mouse(state);
this->set_CurrentFrame(currentFrame);
}

void InputSystem::SetPointerWrapEnabled(bool isEnabled)
{
this->ActivePointerWrapEnabled = isEnabled;
this->RequestedPointerWrapEnabled = isEnabled;
}

void InputSystem::Update()
{
this->EnsureInputStateCaptured();
{
auto __finallyGuard_00000001 = he_cpp_make_scope_exit([&]() {
this->CommitPointerWrapState();
this->hasCapturedInput = false;
});
    if (this->FrameUpdateHandler != nullptr)
    {
(*this->FrameUpdateHandler)();
    }
}
}

bool InputSystem::WasActionPressed(::InputActionId actionId)
{
return this->GetActionState(actionId).get_WasPressed();}

bool InputSystem::WasActionReleased(::InputActionId actionId)
{
return this->GetActionState(actionId).get_WasReleased();}

bool InputSystem::WasKeyPressed(::Keys key)
{
return this->keyboardState.IsKeyDown(key) && this->lastKeyboardState.IsKeyUp(key);}

bool InputSystem::WasKeyReleased(::Keys key)
{
return this->keyboardState.IsKeyUp(key) && this->lastKeyboardState.IsKeyDown(key);}

bool InputSystem::WasMouseLeftButtonPressed()
{
return this->WasMouseButtonPressed(this->mouseState.get_LeftButton(), this->lastMouseState.get_LeftButton());}

bool InputSystem::WasMouseLeftButtonReleased()
{
return this->WasMouseButtonReleased(this->mouseState.get_LeftButton(), this->lastMouseState.get_LeftButton());}

bool InputSystem::WasMouseMiddleButtonPressed()
{
return this->WasMouseButtonPressed(this->mouseState.get_MiddleButton(), this->lastMouseState.get_MiddleButton());}

bool InputSystem::WasMouseMiddleButtonReleased()
{
return this->WasMouseButtonReleased(this->mouseState.get_MiddleButton(), this->lastMouseState.get_MiddleButton());}

bool InputSystem::WasMouseRightButtonPressed()
{
return this->WasMouseButtonPressed(this->mouseState.get_RightButton(), this->lastMouseState.get_RightButton());}

bool InputSystem::WasMouseRightButtonReleased()
{
return this->WasMouseButtonReleased(this->mouseState.get_RightButton(), this->lastMouseState.get_RightButton());}

bool InputSystem::WasMouseXButton1Pressed()
{
return this->WasMouseButtonPressed(this->mouseState.get_XButton1(), this->lastMouseState.get_XButton1());}

bool InputSystem::WasMouseXButton1Released()
{
return this->WasMouseButtonReleased(this->mouseState.get_XButton1(), this->lastMouseState.get_XButton1());}

bool InputSystem::WasMouseXButton2Pressed()
{
return this->WasMouseButtonPressed(this->mouseState.get_XButton2(), this->lastMouseState.get_XButton2());}

bool InputSystem::WasMouseXButton2Released()
{
return this->WasMouseButtonReleased(this->mouseState.get_XButton2(), this->lastMouseState.get_XButton2());}

void InputSystem::ApplyActionTransitions()
{
List<int32_t> *resolvedActionKeys = new List<int32_t>();
for (const auto& actionKey : this->CurrentActionStates->Keys()) {
resolvedActionKeys->Add(actionKey);
}
for (int32_t i = 0; i < resolvedActionKeys->Count(); i++) {
const int32_t actionKey = (*resolvedActionKeys)[i];
::InputActionState currentState = (*this->CurrentActionStates)[actionKey];
::InputActionState previousState;
    if (!this->PreviousActionStates->TryGetValue(actionKey, previousState))
    {
previousState = ::InputActionState();
    }
currentState.set_IsDown(currentState.get_Value() != 0.0f);
currentState.set_WasPressed(currentState.get_IsDown() && !previousState.get_IsDown());
currentState.set_WasReleased(!currentState.get_IsDown() && previousState.get_IsDown());
(*this->CurrentActionStates)[actionKey] = currentState;
}
}

void InputSystem::ApplyPointerWrap()
{
    if (!this->ActivePointerWrapEnabled)
    {
return;    }
    if (this->MouseClientBounds.X <= 1 || this->MouseClientBounds.Y <= 1)
    {
return;    }
int32_t wrappedX = this->mouseState.get_X();
int32_t wrappedY = this->mouseState.get_Y();
int32_t deltaOffsetX = 0;
int32_t deltaOffsetY = 0;
    if (this->mouseState.get_X() <= 0)
    {
wrappedX = this->MouseClientBounds.X - 2;
deltaOffsetX = -(this->MouseClientBounds.X - 2);
    }
else     if (this->mouseState.get_X() >= this->MouseClientBounds.X - 1)
    {
wrappedX = 1;
deltaOffsetX = this->MouseClientBounds.X - 2;
    }
    if (this->mouseState.get_Y() <= 0)
    {
wrappedY = this->MouseClientBounds.Y - 2;
deltaOffsetY = -(this->MouseClientBounds.Y - 2);
    }
else     if (this->mouseState.get_Y() >= this->MouseClientBounds.Y - 1)
    {
wrappedY = 1;
deltaOffsetY = this->MouseClientBounds.Y - 2;
    }
    if (deltaOffsetX == 0 && deltaOffsetY == 0)
    {
return;    }
this->mouseState.set_X(wrappedX);
this->mouseState.set_Y(wrappedY);
this->PointerWrapDeltaOffset = ::int2(deltaOffsetX, deltaOffsetY);
::InputFrameState currentFrame = this->CurrentFrame;
currentFrame.set_Mouse(this->mouseState);
::InputPointerState pointer = currentFrame.get_Pointer();
pointer.set_X(wrappedX);
pointer.set_Y(wrappedY);
const int32_t pointerDeltaX = pointer.get_DeltaX() + deltaOffsetX;
const int32_t pointerDeltaY = pointer.get_DeltaY() + deltaOffsetY;
pointer.set_DeltaX(pointerDeltaX);
pointer.set_DeltaY(pointerDeltaY);
currentFrame.set_Pointer(pointer);
this->set_CurrentFrame(currentFrame);
}

void InputSystem::ApplyPointerWrapState()
{
    if (!this->ActivePointerWrapEnabled)
    {
return;    }
this->ApplyPointerWrap();
}

uint64_t InputSystem::BuildPointerButtonMask(::MouseState state)
{
uint64_t buttons = 0;
    if (state.get_LeftButton() == ButtonState::Pressed)
    {
buttons |= 1UL << static_cast<int32_t>(InputPointerButton::Primary);
    }
    if (state.get_RightButton() == ButtonState::Pressed)
    {
buttons |= 1UL << static_cast<int32_t>(InputPointerButton::Secondary);
    }
    if (state.get_MiddleButton() == ButtonState::Pressed)
    {
buttons |= 1UL << static_cast<int32_t>(InputPointerButton::Middle);
    }
    if (state.get_XButton1() == ButtonState::Pressed)
    {
buttons |= 1UL << static_cast<int32_t>(InputPointerButton::Back);
    }
    if (state.get_XButton2() == ButtonState::Pressed)
    {
buttons |= 1UL << static_cast<int32_t>(InputPointerButton::Forward);
    }
return buttons;}

void InputSystem::CaptureInputState()
{
this->lastMouseState = this->mouseState;
this->lastKeyboardState = this->keyboardState;
    if (this->Backend != nullptr)
    {
::InputFrameState backendFrame = this->Backend->CaptureFrame();
    if (this->KeyboardIsActive)
    {
this->keyboardState = backendFrame.get_Keyboard();
    }
this->mouseState = backendFrame.get_Mouse();
this->set_CurrentFrame(backendFrame);
    }
else {
::InputFrameState currentFrame = this->CurrentFrame;
currentFrame.set_Keyboard(this->keyboardState);
currentFrame.set_Mouse(this->mouseState);
this->set_CurrentFrame(currentFrame);
}
::int2 pointerWrapDeltaOffset = this->ConsumePointerWrapDeltaOffset();
this->mouseDelta = ::int2(this->mouseState.get_X() - this->lastMouseState.get_X() + pointerWrapDeltaOffset.X, this->mouseState.get_Y() - this->lastMouseState.get_Y() + pointerWrapDeltaOffset.Y);
::InputFrameState updatedFrame = this->CurrentFrame;
updatedFrame.set_Keyboard(this->keyboardState);
updatedFrame.set_Mouse(this->mouseState);
updatedFrame.set_Pointer(([&]() {
auto __object_00000002 = ::InputPointerState();
__object_00000002.set_Connected(true);
__object_00000002.set_X(this->mouseState.get_X());
__object_00000002.set_Y(this->mouseState.get_Y());
__object_00000002.set_DeltaX(this->mouseDelta.X);
__object_00000002.set_DeltaY(this->mouseDelta.Y);
__object_00000002.set_ScrollDelta(this->GetMouseScrollWheelDelta());
__object_00000002.set_Buttons(this->BuildPointerButtonMask(this->mouseState));
return __object_00000002;
})());
this->set_CurrentFrame(updatedFrame);
this->hasCapturedInput = true;
}

void InputSystem::CommitPointerWrapState()
{
this->ActivePointerWrapEnabled = this->RequestedPointerWrapEnabled;
this->RequestedPointerWrapEnabled = false;
}

::int2 InputSystem::ConsumePointerWrapDeltaOffset()
{
::int2 pointerWrapDeltaOffset = this->PointerWrapDeltaOffset;
this->PointerWrapDeltaOffset = ::int2(0, 0);
return pointerWrapDeltaOffset;}

void InputSystem::EnsureInputStateCaptured()
{
    if (this->hasCapturedInput)
    {
return;    }
this->CaptureInputState();
}

float InputSystem::GetGamepadAxisValue(::InputGamepadState gamepad, int32_t axisIndex)
{
switch (axisIndex) {
case 0: {
return gamepad.get_LeftStickX();}
case 1: {
return gamepad.get_LeftStickY();}
case 2: {
return gamepad.get_RightStickX();}
case 3: {
return gamepad.get_RightStickY();}
case 4: {
return gamepad.get_LeftTrigger();}
case 5: {
return gamepad.get_RightTrigger();}
default:  {
return 0.0f;}
}

}

bool InputSystem::IsGamepadButtonDown(::InputGamepadState gamepad, int32_t buttonIndex)
{
    if (buttonIndex < 0 || buttonIndex >= 64)
    {
return false;    }
return (gamepad.get_Buttons() & (1UL << buttonIndex)) != 0;}

bool InputSystem::IsPointerButtonDown(::InputPointerState pointer, int32_t buttonIndex)
{
    if (buttonIndex < 0 || buttonIndex >= 64)
    {
return false;    }
return (pointer.get_Buttons() & (1UL << buttonIndex)) != 0;}

float InputSystem::ResolveBindingValue(::InputBinding binding)
{
switch (binding.get_Control().get_DeviceKind()) {
case InputDeviceKind::Gamepad: {
return this->ResolveGamepadBindingValue(binding);}
case InputDeviceKind::Pointer: {
return this->ResolvePointerBindingValue(binding);}
default:  {
return 0.0f;}
}

}

void InputSystem::ResolveBindings()
{
List<int32_t> *previousActionKeys = new List<int32_t>();
for (const auto& actionKey : this->PreviousActionStates->Keys()) {
previousActionKeys->Add(actionKey);
}
for (int32_t index = 0; index < previousActionKeys->Count(); index++) {
const int32_t actionKey = (*previousActionKeys)[index];
this->PreviousActionStates->Remove(actionKey);
}
List<int32_t> *currentActionKeys = new List<int32_t>();
for (const auto& actionKey : this->CurrentActionStates->Keys()) {
currentActionKeys->Add(actionKey);
}
for (int32_t index = 0; index < currentActionKeys->Count(); index++) {
const int32_t actionKey = (*currentActionKeys)[index];
(*this->PreviousActionStates)[actionKey] = (*this->CurrentActionStates)[actionKey];
this->CurrentActionStates->Remove(actionKey);
}
this->SeenActionIds->Clear();
for (int32_t contextIndex = this->ActiveContextStack->Count() - 1; contextIndex >= 0; contextIndex--) {
const int32_t activeContextValue = (*this->ActiveContextStack)[contextIndex];
Dictionary<int32_t, float> *contextActionValues = new Dictionary<int32_t, float>();
List<int32_t> *contextActionKeys = new List<int32_t>();
for (int32_t bindingIndex = 0; bindingIndex < this->Bindings->Count(); bindingIndex++) {
::InputBinding binding = (*this->Bindings)[bindingIndex];
    if (binding.get_ContextId().get_Value() != activeContextValue)
    {
continue;
    }
const float value = this->ResolveBindingValue(binding);
    if (!this->SeenActionIds->Contains(binding.get_ActionId().get_Value()))
    {
this->SeenActionIds->Add(binding.get_ActionId().get_Value());
    }
    if (value == 0.0f)
    {
continue;
    }
const int32_t actionKey = binding.get_ActionId().get_Value();
float currentContextValue;
    if (contextActionValues->TryGetValue(actionKey, currentContextValue) && Math::Abs(value) <= Math::Abs(currentContextValue))
    {
continue;
    }
(*contextActionValues)[actionKey] = value;
    if (!contextActionKeys->Contains(actionKey))
    {
contextActionKeys->Add(actionKey);
    }
}
for (const auto& actionKey : *contextActionKeys) {
    if (CurrentActionStates->ContainsKey(actionKey))
    {
continue;
    }
StoreActionValue(::InputActionId(actionKey), (*contextActionValues)[actionKey]);
}
}
for (const auto& actionKey : *this->SeenActionIds) {
    if (CurrentActionStates->ContainsKey(actionKey))
    {
continue;
    }
::InputActionState previousState;
    if (!PreviousActionStates->TryGetValue(actionKey, previousState))
    {
previousState = ::InputActionState();
    }
(*CurrentActionStates)[actionKey] = ([&]() {
auto __object_00000003 = ::InputActionState();
__object_00000003.set_Value(0.0f);
__object_00000003.set_IsDown(false);
__object_00000003.set_WasPressed(false);
__object_00000003.set_WasReleased(previousState.get_IsDown());
return __object_00000003;
})();
}
this->ApplyActionTransitions();
}

float InputSystem::ResolveGamepadBindingValue(::InputBinding binding)
{
::InputGamepadState gamepad;
    if (!this->TryGetGamepad(binding.get_Control().get_DeviceIndex(), gamepad))
    {
return 0.0f;    }
switch (binding.get_Control().get_ControlKind()) {
case InputControlKind::Button: {
return this->IsGamepadButtonDown(gamepad, binding.get_Control().get_ControlIndex()) ? binding.get_Scale() : 0.0f;}
case InputControlKind::Axis: {
return this->GetGamepadAxisValue(gamepad, binding.get_Control().get_ControlIndex()) * binding.get_Scale();}
default:  {
return 0.0f;}
}

}

float InputSystem::ResolvePointerBindingValue(::InputBinding binding)
{
switch (binding.get_Control().get_ControlKind()) {
case InputControlKind::Button: {
return this->IsPointerButtonDown(this->CurrentFrame.get_Pointer(), binding.get_Control().get_ControlIndex()) ? binding.get_Scale() : 0.0f;}
case InputControlKind::PointerDelta: {
    if (binding.get_Control().get_ControlIndex() == 0)
    {
return this->CurrentFrame.get_Pointer().get_DeltaX() * binding.get_Scale();    }
return this->CurrentFrame.get_Pointer().get_DeltaY() * binding.get_Scale();}
case InputControlKind::ScrollWheel: {
return this->CurrentFrame.get_Pointer().get_ScrollDelta() * binding.get_Scale();}
default:  {
return 0.0f;}
}

}

void InputSystem::StoreActionValue(::InputActionId actionId, float value)
{
const int32_t key = actionId.get_Value();
::InputActionState currentState;
    if (!this->CurrentActionStates->TryGetValue(key, currentState))
    {
currentState = ::InputActionState();
    }
    if (Math::Abs(value) <= Math::Abs(currentState.get_Value()))
    {
return;    }
currentState.set_Value(value);
currentState.set_IsDown(value != 0.0f);
(*this->CurrentActionStates)[key] = currentState;
}

bool InputSystem::TryGetGamepad(int32_t deviceIndex, ::InputGamepadState& gamepad)
{
gamepad = ::InputGamepadState();
    if (this->CurrentFrame.get_Gamepads() == nullptr)
    {
return false;    }
    if (deviceIndex < 0 || deviceIndex >= this->CurrentFrame.get_GamepadCount() || deviceIndex >= this->CurrentFrame.get_Gamepads()->Length)
    {
return false;    }
gamepad = (*this->CurrentFrame.get_Gamepads())[deviceIndex];
return gamepad.get_Connected();}

bool InputSystem::WasMouseButtonPressed(::ButtonState currentState, ::ButtonState previousState)
{
return currentState == ButtonState::Pressed && previousState == ButtonState::Released;}

bool InputSystem::WasMouseButtonReleased(::ButtonState currentState, ::ButtonState previousState)
{
return currentState == ButtonState::Released && previousState == ButtonState::Pressed;}

