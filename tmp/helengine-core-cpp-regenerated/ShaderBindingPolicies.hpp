#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderBindingPolicy;

#include "ShaderBindingPolicy.hpp"

class ShaderBindingPolicies
{
public:
    virtual ~ShaderBindingPolicies() = default;

    static ::ShaderBindingPolicy* get_Default();
};
