#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderBindingPolicy;

#include "ShaderBindingPolicy.hpp"

class ShaderCompileOptions
{
public:
    virtual ~ShaderCompileOptions() = default;

    ::ShaderBindingPolicy* BindingPolicy;

    ::ShaderBindingPolicy* get_BindingPolicy();

    bool GenerateDebugInfo;

    bool get_GenerateDebugInfo();

    bool Optimize;

    bool get_Optimize();

    bool TreatWarningsAsErrors;

    bool get_TreatWarningsAsErrors();

    ShaderCompileOptions(::ShaderBindingPolicy* bindingPolicy, bool generateDebugInfo, bool optimize, bool treatWarningsAsErrors);
};
