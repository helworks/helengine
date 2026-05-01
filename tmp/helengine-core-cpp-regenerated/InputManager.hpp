#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Keyboard;
class Mouse;
class int2;
class ObjectManager;
class Core;
class ICamera;
class float2;
class MouseState;
class float4;
class IInteractable2D;
class Entity;
class KeyboardState;
class IDrawable2D;

#include "Keyboard.hpp"
#include "Mouse.hpp"
#include "int2.hpp"
#include "ButtonState.hpp"
#include "ObjectManager.hpp"
#include "Core.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_list.hpp"
#include "PointerInteraction.hpp"
#include "PointerInteraction.hpp"
#include "ICamera.hpp"
#include "float2.hpp"
#include "MouseState.hpp"
#include "float4.hpp"
#include "ICamera.hpp"
#include "int2.hpp"
#include "IInteractable2D.hpp"
#include "IInteractable2D.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_exceptions.hpp"
#include "Entity.hpp"
#include "PointerCursorKind.hpp"
#include "KeyboardState.hpp"
#include "MouseState.hpp"
#include "Keyboard.hpp"
#include "Mouse.hpp"
#include "Core.hpp"
#include "ButtonState.hpp"
#include "Keys.hpp"
#include "IDrawable2D.hpp"

class InputManager
{
public:
    ::IInteractable2D* Highlighted;

    ::IInteractable2D* get_Highlighted();
    void set_Highlighted(::IInteractable2D* value);

    ::PointerCursorKind get_HoverCursor();

    ::IInteractable2D* Hovering;

    ::IInteractable2D* get_Hovering();
    void set_Hovering(::IInteractable2D* value);

    virtual void EarlyUpdate();

    ::int2 GetMouseDelta();

    ::ButtonState GetMouseLeftButtonState();

    ::ButtonState GetMouseMiddleButtonState();

    ::int2 GetMousePosition();

    ::ButtonState GetMouseRightButtonState();

    int32_t GetMouseScrollWheelDelta();

    int32_t GetMouseScrollWheelValue();

    ::ButtonState GetMouseXButton1State();

    ::ButtonState GetMouseXButton2State();

    InputManager();

    bool IsKeyDown(::Keys key);

    bool IsKeyUp(::Keys key);

    void RequestPointerWrapEnabled();

    void SetKeyboardActive(bool isActive);

    void SetPointerWrapEnabled(bool isEnabled);

    virtual void Update();

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
protected:
    ::Keyboard* Keyboard;

    ::Keyboard* get_Keyboard();
    void set_Keyboard(::Keyboard* value);

    ::Mouse* Mouse;

    ::Mouse* get_Mouse();
    void set_Mouse(::Mouse* value);

    ::Core* core;
private:
    bool ActivePointerWrapEnabled;

    bool RequestedPointerWrapEnabled;

    ::ICamera* capturedCamera;

    bool hasCapturedInput;

    ::KeyboardState keyboardState;

    ::KeyboardState lastKeyboardState;

    ::MouseState lastMouseState;

    ::int2 mouseDelta;

    ::MouseState mouseState;

    void ApplyPointerWrapState();

    bool CandidateIsInFront(uint8_t candidateRenderOrder, int32_t candidateDrawableIndex, int32_t candidateInteractableIndex, uint8_t currentRenderOrder, int32_t currentDrawableIndex, int32_t currentInteractableIndex);

    void CaptureInputState();

    void CommitPointerWrapState();

    void EnsureInputStateCaptured();

    ::ICamera* FindCameraForInteractableAt(::IInteractable2D* interactable, int32_t mouseX, int32_t mouseY);

    uint8_t GetTopDrawableRenderOrder(List<::IDrawable2D*>* drawables, ::IInteractable2D* interactable, uint16_t cameraLayerMask, int32_t& highestDrawableIndex);

    ::ICamera* GetTopmostCameraAt(int32_t x, int32_t y);

    bool IsSameEntityOrDescendant(::Entity* candidate, ::Entity* root);

    bool WasMouseButtonPressed(::ButtonState current, ::ButtonState previous);

    bool WasMouseButtonReleased(::ButtonState current, ::ButtonState previous);
};
