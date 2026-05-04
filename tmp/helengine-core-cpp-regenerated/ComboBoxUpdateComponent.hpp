#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class UpdateComponent;
class IUpdateable;
class ComboBoxComponent;

#include "UpdateComponent.hpp"
#include "IUpdateable.hpp"
#include "ComboBoxComponent.hpp"
#include "ComboBoxComponent.hpp"

class ComboBoxUpdateComponent : public UpdateComponent
{
public:
    virtual ~ComboBoxUpdateComponent() = default;

    ComboBoxUpdateComponent(::ComboBoxComponent* comboBox);

    void Update();

    uint8_t get_UpdateOrder();

    void set_UpdateOrder(uint8_t value);

    ::Entity* get_Parent();

    void set_Parent(::Entity* value);
private:
    ::ComboBoxComponent* comboBox;
};
