#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_string.hpp"

class SceneEntityReference
{
public:
    virtual ~SceneEntityReference() = default;

    SceneEntityReference();

    std::string EntityId;

    std::string get_EntityId();
    void set_EntityId(std::string value);
};
