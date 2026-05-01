#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class UpdateComponent;
class IUpdateable;
class TextBoxComponent;
class InputManager;
class Core;
class int2;

#include "UpdateComponent.hpp"
#include "IUpdateable.hpp"
#include "TextBoxComponent.hpp"
#include "InputManager.hpp"
#include "Core.hpp"
#include "Core.hpp"
#include "int2.hpp"
#include "InputManager.hpp"
#include "TextBoxComponent.hpp"

class TextBoxUpdateComponent : public UpdateComponent
{
public:
    TextBoxUpdateComponent(::TextBoxComponent* textBox);

    void Update();

    uint8_t get_UpdateOrder();

    void set_UpdateOrder(uint8_t value);

    ::Entity* get_Parent();

    void set_Parent(::Entity* value);
private:
    ::TextBoxComponent* textBox;
};
