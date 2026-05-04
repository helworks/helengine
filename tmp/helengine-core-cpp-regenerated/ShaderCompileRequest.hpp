#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderDefine;
class ShaderCompileOptions;
class ShaderModel;
class ShaderSourceInfo;

#include "runtime/native_list.hpp"
#include "ShaderDefine.hpp"
#include "runtime/native_string.hpp"
#include "ShaderCompileOptions.hpp"
#include "ShaderModel.hpp"
#include "ShaderSourceInfo.hpp"
#include "ShaderStage.hpp"
#include "ShaderCompileTarget.hpp"

class ShaderCompileRequest
{
public:
    virtual ~ShaderCompileRequest() = default;

    List<::ShaderDefine*>* Defines;

    List<::ShaderDefine*>* get_Defines();

    std::string EntryPoint;

    std::string get_EntryPoint();

    ::ShaderCompileOptions* Options;

    ::ShaderCompileOptions* get_Options();

    std::string ProgramName;

    std::string get_ProgramName();

    ::ShaderModel* ShaderModel;

    ::ShaderModel* get_ShaderModel();

    ::ShaderSourceInfo* Source;

    ::ShaderSourceInfo* get_Source();

    ::ShaderStage Stage;

    ::ShaderStage get_Stage();

    ::ShaderCompileTarget Target;

    ::ShaderCompileTarget get_Target();

    std::string Variant;

    std::string get_Variant();

    ShaderCompileRequest(::ShaderSourceInfo* source, std::string programName, std::string entryPoint, ::ShaderStage stage, ::ShaderCompileTarget target, ::ShaderModel* shaderModel, std::string variant, List<::ShaderDefine*>* defines, ::ShaderCompileOptions* options);
};
