#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Component;
class IInteractable2D;
class Entity;
class Core;
class ObjectManager;

#include "Component.hpp"
#include "IInteractable2D.hpp"
#include "Component.hpp"
#include "Entity.hpp"
#include "Core.hpp"
#include "Core.hpp"
#include "ObjectManager.hpp"
#include "runtime/native_event.hpp"
#include "PointerCursorKind.hpp"
#include "int2.hpp"
#include "Entity.hpp"
#include "PointerInteraction.hpp"

class InteractableComponent : public Component, public IInteractable2D
{
public:
    virtual ~InteractableComponent() = default;

    InteractableComponent();

    ::Event CursorEvent;

    ::PointerCursorKind HoverCursor;

    ::PointerCursorKind get_HoverCursor();
    void set_HoverCursor(::PointerCursorKind value);

    int2* Size;

    int2* get_Size();
    void set_Size(int2* value);

    void ComponentAdded(::Entity* entity);

    virtual void OnCursor(int2* relPos, int2* delta, ::PointerInteraction state);

    void ParentEnabledChange(bool newEnabled);

    ::Entity* get_Parent();

    void set_Parent(::Entity* value);
};
