#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class UpdateComponent;
class IUpdateable;
class TextBoxComponent;
class Core;

#include "UpdateComponent.hpp"
#include "IUpdateable.hpp"
#include "TextBoxComponent.hpp"
#include "InputSystem.hpp"
#include "Core.hpp"
#include "Core.hpp"
#include "InputSystem.hpp"
#include "TextBoxComponent.hpp"

class TextBoxUpdateComponent : public UpdateComponent
{
public:
    virtual ~TextBoxUpdateComponent() = default;

    TextBoxUpdateComponent(::TextBoxComponent* textBox);

    void Update();

    uint8_t get_UpdateOrder();

    void set_UpdateOrder(uint8_t value);

    ::Entity* get_Parent();

    void set_Parent(::Entity* value);
private:
    ::TextBoxComponent* textBox;
};
