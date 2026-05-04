#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderModel;

#include "runtime/native_string.hpp"
#include "ShaderModel.hpp"
#include "ShaderStage.hpp"
#include "ShaderCompileTarget.hpp"

class ShaderCompileCacheKey
{
public:
    virtual ~ShaderCompileCacheKey() = default;

    std::string BindingPolicySignature;

    std::string get_BindingPolicySignature();

    std::string DefinesSignature;

    std::string get_DefinesSignature();

    std::string EntryPoint;

    std::string get_EntryPoint();

    std::string ProgramName;

    std::string get_ProgramName();

    ::ShaderModel* ShaderModel;

    ::ShaderModel* get_ShaderModel();

    std::string SourceHash;

    std::string get_SourceHash();

    ::ShaderStage Stage;

    ::ShaderStage get_Stage();

    ::ShaderCompileTarget Target;

    ::ShaderCompileTarget get_Target();

    std::string Variant;

    std::string get_Variant();

    ShaderCompileCacheKey(std::string sourceHash, std::string programName, std::string entryPoint, ::ShaderStage stage, ::ShaderCompileTarget target, ::ShaderModel* shaderModel, std::string variant, std::string definesSignature, std::string bindingPolicySignature);

    std::string ToString();
private:
    std::string key;

    std::string BuildKey();
};
