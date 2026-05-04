#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class IUpdateable;

#include "IUpdateable.hpp"

class PendingUpdateOperation
{
public:
    virtual ~PendingUpdateOperation() = default;

    ::IUpdateable* Entity;

    ::IUpdateable* get_Entity();
    void set_Entity(::IUpdateable* value);

    bool IsAdd;

    bool get_IsAdd();
    void set_IsAdd(bool value);

    PendingUpdateOperation(::IUpdateable* entity, bool isAdd);
};
