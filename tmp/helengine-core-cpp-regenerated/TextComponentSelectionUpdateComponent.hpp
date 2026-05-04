#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class UpdateComponent;
class IUpdateable;
class TextComponent;

#include "UpdateComponent.hpp"
#include "IUpdateable.hpp"
#include "TextComponent.hpp"
#include "TextComponent.hpp"

class TextComponentSelectionUpdateComponent : public UpdateComponent
{
public:
    virtual ~TextComponentSelectionUpdateComponent() = default;

    ::TextComponent* TextComponent;

    ::TextComponent* get_TextComponent();

    TextComponentSelectionUpdateComponent(::TextComponent* textComponent);

    void Update();

    uint8_t get_UpdateOrder();

    void set_UpdateOrder(uint8_t value);

    ::Entity* get_Parent();

    void set_Parent(::Entity* value);
};
