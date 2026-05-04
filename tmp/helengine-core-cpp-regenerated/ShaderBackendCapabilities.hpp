#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderModel;

#include "ShaderModel.hpp"
#include "runtime/native_list.hpp"
#include "ShaderStage.hpp"

class ShaderBackendCapabilities
{
public:
    virtual ~ShaderBackendCapabilities() = default;

    ::ShaderModel* MaximumShaderModel;

    ::ShaderModel* get_MaximumShaderModel();

    ::ShaderModel* MinimumShaderModel;

    ::ShaderModel* get_MinimumShaderModel();

    List<::ShaderStage>* SupportedStages;

    List<::ShaderStage>* get_SupportedStages();

    bool SupportsRayTracing;

    bool get_SupportsRayTracing();

    ShaderBackendCapabilities(::ShaderModel* minimumShaderModel, ::ShaderModel* maximumShaderModel, List<::ShaderStage>* supportedStages, bool supportsRayTracing);
};
