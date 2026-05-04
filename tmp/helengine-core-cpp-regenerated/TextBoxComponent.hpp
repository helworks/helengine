#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Component;
class IFocusTarget;
class RenderOrder2D;
class Entity;
class TextBoxUpdateComponent;
class TextBoxEditState;
class Core;
class FontChar;
class FontTightMetrics;
class FontAsset;
class float3;
class IFocusGroup;
class RoundedRectComponent;
class InteractableComponent;
class TextComponent;

#include "Component.hpp"
#include "IFocusTarget.hpp"
#include "runtime/native_exceptions.hpp"
#include "Component.hpp"
#include "RenderOrder2D.hpp"
#include "RenderOrder2D.hpp"
#include "Entity.hpp"
#include "TextBoxUpdateComponent.hpp"
#include "TextBoxUpdateComponent.hpp"
#include "TextBoxEditState.hpp"
#include "InputSystem.hpp"
#include "Core.hpp"
#include "Core.hpp"
#include "runtime/native_string.hpp"
#include "system/math.hpp"
#include "system/math.hpp"
#include "FontChar.hpp"
#include "FontTightMetrics.hpp"
#include "FontAsset.hpp"
#include "float3.hpp"
#include "runtime/native_event.hpp"
#include "IFocusGroup.hpp"
#include "FontAsset.hpp"
#include "int2.hpp"
#include "TextBoxEditState.hpp"
#include "RoundedRectComponent.hpp"
#include "InteractableComponent.hpp"
#include "runtime/native_datetime.hpp"
#include "Entity.hpp"
#include "TextComponent.hpp"
#include "Keys.hpp"
#include "PointerInteraction.hpp"

class TextBoxComponent : public Component, public IFocusTarget
{
public:
    virtual ~TextBoxComponent() = default;

    bool get_CanReceiveFocus();

    float get_CurrentShakeOffsetX();

    ::Event FocusChanged;

    ::IFocusGroup* FocusGroup;

    ::IFocusGroup* get_FocusGroup();
    void set_FocusGroup(::IFocusGroup* value);

    ::FontAsset* get_Font();

    void set_Font(::FontAsset* value);

    bool IsDefaultTarget;

    bool get_IsDefaultTarget();
    void set_IsDefaultTarget(bool value);

    bool get_IsFocused();

    void set_IsFocused(bool value);

    std::string get_Placeholder();

    void set_Placeholder(std::string value);

    int2* get_Size();

    void set_Size(int2* value);

    ::Event Submitted;

    int32_t TabIndex;

    int32_t get_TabIndex();
    void set_TabIndex(int32_t value);

    std::string get_Text();

    void set_Text(std::string value);

    ::Event TextChanged;

    void ActivateFromKey(Keys key);

    bool CanActivateWithKey(Keys key);

    void ComponentAdded(::Entity* entity);

    void ComponentRemoved(::Entity* entity);

    bool ContainsScreenPoint(int32_t x, int32_t y);

    void ParentEnabledChange(bool newEnabled);

    void SetInvalidState(bool isInvalid);

    void SetRenderOrders(uint8_t backgroundOrder, uint8_t textOrder);

    void SetTargetFocused(bool isFocused);

    TextBoxComponent(int2* size, ::FontAsset* font, std::string placeholder);

    void TriggerInvalidShake();

    void Update();

    ::Entity* get_Parent();

    void set_Parent(::Entity* value);
private:
    static float EffectFrameDeltaSeconds;

    static float ShakeAmplitudePixels;

    static float ShakeDurationSeconds;

    static float ShakeFrequencyHz;

    static int32_t TextPaddingX;

    static ::TextBoxComponent* focusedTextBox;

    ::TextBoxEditState* EditState;

    uint8_t backgroundRenderOrder;

    ::RoundedRectComponent* backgroundSprite;

    float currentShakeOffsetX;

    bool cursorVisible;

    ::FontAsset* font;

    bool hasRenderOrderOverrides;

    ::InteractableComponent* interactableComponent;

    bool isFocused;

    bool isInvalid;

    bool isSelectingText;

    bool isShakeActive;

    DateTime lastCursorBlink;

    std::string placeholder;

    ::Entity* selectionEntity;

    ::RoundedRectComponent* selectionSprite;

    ::float3 shakeBaseLocalPosition;

    float shakeElapsedSeconds;

    int2* size;

    ::TextComponent* textComponent;

    ::Entity* textEntity;

    uint8_t textRenderOrder;

    void HandleKeyPress(Keys key, bool isShiftPressed);

    char KeyToChar(Keys key, bool isShiftPressed);

    void OnCursorEvent(int2* relPos, int2* delta, ::PointerInteraction state);

    double ResolveCharacterAdvance(char character);

    int32_t ResolveCursorPositionFromClick(int32_t clickX);

    double ResolveTextWidth(int32_t startIndex, int32_t endIndex);

    void SetFocusedState(bool value, bool submitOnBlur);

    void UpdateFocusVisual();

    void UpdateSelectionVisual();

    void UpdateSelectionVisual(double textY, double lineHeight);

    void UpdateShakeAnimation();

    void UpdateTextDisplay();

    void UpdateTextLayout();
};
