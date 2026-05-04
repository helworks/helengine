#ifdef DrawText
#undef DrawText
#endif
#include "InputBinding.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_string.hpp"

InputBinding::InputBinding() : ActionId(), ContextId(), Control(), Scale()
{
}

::InputActionId InputBinding::get_ActionId()
{
return this->ActionId;
}

::InputContextId InputBinding::get_ContextId()
{
return this->ContextId;
}

::InputControlId InputBinding::get_Control()
{
return this->Control;
}

float InputBinding::get_Scale()
{
return this->Scale;
}

InputBinding::InputBinding(::InputContextId contextId, ::InputActionId actionId, ::InputControlId control, float scale) : ActionId(), ContextId(), Control(), Scale()
{
this->ContextId = contextId;
this->ActionId = actionId;
this->Control = control;
this->Scale = scale;
}

