#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class RoundedRectComponent;
class InteractableComponent;
class TextComponent;
class Entity;
class FontAsset;

#include "RoundedRectComponent.hpp"
#include "runtime/native_event.hpp"
#include "InteractableComponent.hpp"
#include "TextComponent.hpp"
#include "Entity.hpp"
#include "FontAsset.hpp"
#include "int2.hpp"
#include "PointerInteraction.hpp"

class ComboBoxItemVisual
{
public:
    virtual ~ComboBoxItemVisual() = default;

    ::RoundedRectComponent* Background;

    ::RoundedRectComponent* get_Background();

    ::Event CursorEvent;

    int32_t Index;

    int32_t get_Index();
    void set_Index(int32_t value);

    ::InteractableComponent* Interactable;

    ::InteractableComponent* get_Interactable();

    bool IsHovering;

    bool get_IsHovering();
    void set_IsHovering(bool value);

    bool IsPressed;

    bool get_IsPressed();
    void set_IsPressed(bool value);

    ::TextComponent* Label;

    ::TextComponent* get_Label();

    ::Entity* LabelHost;

    ::Entity* get_LabelHost();

    ::Entity* Root;

    ::Entity* get_Root();

    ComboBoxItemVisual(::FontAsset* font, uint16_t layerMask, uint8_t backgroundOrder, uint8_t textOrder);
private:
    void HandleCursorEvent(int2* relPos, int2* delta, ::PointerInteraction state);
};
