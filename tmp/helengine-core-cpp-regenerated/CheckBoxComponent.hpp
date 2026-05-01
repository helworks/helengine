#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Component;
class RenderOrder2D;
class Entity;
class int2;
class RoundedRectComponent;
class TextComponent;
class FontAsset;
class InteractableComponent;

#include "Component.hpp"
#include "Component.hpp"
#include "RenderOrder2D.hpp"
#include "RenderOrder2D.hpp"
#include "Entity.hpp"
#include "runtime/native_event.hpp"
#include "int2.hpp"
#include "RoundedRectComponent.hpp"
#include "TextComponent.hpp"
#include "Entity.hpp"
#include "FontAsset.hpp"
#include "InteractableComponent.hpp"
#include "PointerInteraction.hpp"

class CheckBoxComponent : public Component
{
public:
    ::Event CheckedChanged;

    bool get_IsChecked();

    void set_IsChecked(bool value);

    ::int2 get_Size();

    void set_Size(::int2 value);

    CheckBoxComponent(::int2 size, ::FontAsset* font, bool isChecked);

    void ComponentAdded(::Entity* entity);

    void ComponentRemoved(::Entity* entity);

    void ParentEnabledChange(bool newEnabled);

    void SetRenderOrders(uint8_t backgroundOrder, uint8_t checkMarkOrder);

    ::Entity* get_Parent();

    void set_Parent(::Entity* value);
private:
    ::RoundedRectComponent* Background;

    uint8_t BackgroundRenderOrder;

    ::TextComponent* CheckMark;

    ::Entity* CheckMarkEntity;

    uint8_t CheckMarkRenderOrder;

    ::FontAsset* Font;

    bool HasRenderOrderOverrides;

    ::InteractableComponent* Interactable;

    bool IsCheckedValue;

    bool IsHovering;

    bool IsPressed;

    ::int2 SizeValue;

    void HandleCursorEvent(::int2 relPos, ::int2 delta, ::PointerInteraction state);

    void SetCheckedState(bool isChecked, bool raiseEvent);

    void UpdateCheckMarkLayout();

    void UpdateVisualState();
};
