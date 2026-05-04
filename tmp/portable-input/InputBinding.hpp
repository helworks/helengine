#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class InputActionId;
class InputContextId;
class InputControlId;

#include "InputActionId.hpp"
#include "InputContextId.hpp"
#include "InputControlId.hpp"

class InputBinding
{
public:
    virtual ~InputBinding() = default;

    InputBinding();

    ::InputActionId ActionId;

    ::InputActionId get_ActionId();

    ::InputContextId ContextId;

    ::InputContextId get_ContextId();

    ::InputControlId Control;

    ::InputControlId get_Control();

    float Scale;

    float get_Scale();

    InputBinding(::InputContextId contextId, ::InputActionId actionId, ::InputControlId control, float scale);
};
