#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class InputFrameState;
class InputActionState;
class InputPointerState;
class InputGamepadState;
class InputTextState;
class int2;
class IInputBackend;
class MouseState;
class InputActionId;
class InputBinding;
class KeyboardState;
class InputContextId;

#include "InputFrameState.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_dictionary.hpp"
#include "InputActionState.hpp"
#include "InputPointerState.hpp"
#include "InputGamepadState.hpp"
#include "InputTextState.hpp"
#include "int2.hpp"
#include "ButtonState.hpp"
#include "IInputBackend.hpp"
#include "int2.hpp"
#include "MouseState.hpp"
#include "InputPointerState.hpp"
#include "InputFrameState.hpp"
#include "runtime/native_list.hpp"
#include "InputActionId.hpp"
#include "IInputBackend.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_list.hpp"
#include "InputBinding.hpp"
#include "runtime/native_dictionary.hpp"
#include "InputActionState.hpp"
#include "system/action.hpp"
#include "KeyboardState.hpp"
#include "MouseState.hpp"
#include "InputContextId.hpp"
#include "InputActionId.hpp"
#include "InputGamepadState.hpp"
#include "ButtonState.hpp"
#include "InputTextState.hpp"
#include "Keys.hpp"

class InputSystem
{
public:
    virtual ~InputSystem() = default;

    ::IInputBackend* Backend;

    ::IInputBackend* get_Backend();
    void set_Backend(::IInputBackend* value);

    ::InputFrameState CurrentFrame;

    ::InputFrameState get_CurrentFrame();
    void set_CurrentFrame(::InputFrameState value);

    bool get_IsPointerWrapEnabled();

    void ClearBindings(::InputContextId contextId);

    void ClearContexts();

    void EarlyUpdate();

    ::InputActionState GetActionState(::InputActionId actionId);

    float GetActionValue(::InputActionId actionId);

    int32_t GetGamepadCount();

    ::InputGamepadState GetGamepadState(int32_t index);

    ::int2 GetMouseDelta();

    int32_t GetMouseDeltaX();

    int32_t GetMouseDeltaY();

    ::ButtonState GetMouseLeftButtonState();

    ::ButtonState GetMouseMiddleButtonState();

    ::int2 GetMousePosition();

    ::ButtonState GetMouseRightButtonState();

    int32_t GetMouseScrollWheelDelta();

    int32_t GetMouseScrollWheelValue();

    int32_t GetMouseX();

    ::ButtonState GetMouseXButton1State();

    ::ButtonState GetMouseXButton2State();

    int32_t GetMouseY();

    ::InputPointerState GetPointerState();

    ::InputTextState GetTextState();

    InputSystem();

    bool IsActionDown(::InputActionId actionId);

    bool IsKeyDown(::Keys key);

    bool IsKeyUp(::Keys key);

    void PopContext(::InputContextId contextId);

    void PushContext(::InputContextId contextId);

    void RegisterBinding(::InputBinding binding);

    void RequestPointerWrapEnabled();

    void SetBackend(::IInputBackend* backend);

    void SetFrameUpdateHandler(Action<>* handler);

    void SetKeyboardActive(bool isActive);

    void SetKeyboardState(::KeyboardState state);

    void SetMouseClientBounds(::int2 clientBounds);

    void SetMouseState(::MouseState state);

    void SetPointerWrapEnabled(bool isEnabled);

    void Update();

    bool WasActionPressed(::InputActionId actionId);

    bool WasActionReleased(::InputActionId actionId);

    bool WasKeyPressed(::Keys key);

    bool WasKeyReleased(::Keys key);

    bool WasMouseLeftButtonPressed();

    bool WasMouseLeftButtonReleased();

    bool WasMouseMiddleButtonPressed();

    bool WasMouseMiddleButtonReleased();

    bool WasMouseRightButtonPressed();

    bool WasMouseRightButtonReleased();

    bool WasMouseXButton1Pressed();

    bool WasMouseXButton1Released();

    bool WasMouseXButton2Pressed();

    bool WasMouseXButton2Released();
private:
    List<int32_t>* ActiveContextStack;

    bool ActivePointerWrapEnabled;

    List<::InputBinding>* Bindings;

    Dictionary<int32_t, ::InputActionState>* CurrentActionStates;

    Action<>* FrameUpdateHandler;

    bool KeyboardIsActive;

    ::int2 MouseClientBounds;

    ::int2 PointerWrapDeltaOffset;

    Dictionary<int32_t, ::InputActionState>* PreviousActionStates;

    bool RequestedPointerWrapEnabled;

    List<int32_t>* SeenActionIds;

    bool hasCapturedInput;

    ::KeyboardState keyboardState;

    ::KeyboardState lastKeyboardState;

    ::MouseState lastMouseState;

    ::int2 mouseDelta;

    ::MouseState mouseState;

    void ApplyActionTransitions();

    void ApplyPointerWrap();

    void ApplyPointerWrapState();

    uint64_t BuildPointerButtonMask(::MouseState state);

    void CaptureInputState();

    void CommitPointerWrapState();

    ::int2 ConsumePointerWrapDeltaOffset();

    void EnsureInputStateCaptured();

    float GetGamepadAxisValue(::InputGamepadState gamepad, int32_t axisIndex);

    bool IsGamepadButtonDown(::InputGamepadState gamepad, int32_t buttonIndex);

    bool IsPointerButtonDown(::InputPointerState pointer, int32_t buttonIndex);

    float ResolveBindingValue(::InputBinding binding);

    void ResolveBindings();

    float ResolveGamepadBindingValue(::InputBinding binding);

    float ResolvePointerBindingValue(::InputBinding binding);

    void StoreActionValue(::InputActionId actionId, float value);

    bool TryGetGamepad(int32_t deviceIndex, ::InputGamepadState& gamepad);

    bool WasMouseButtonPressed(::ButtonState currentState, ::ButtonState previousState);

    bool WasMouseButtonReleased(::ButtonState currentState, ::ButtonState previousState);
};
