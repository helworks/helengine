#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderDefine;
class ShaderModel;

#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_list.hpp"
#include "runtime/array.hpp"
#include "runtime/native_string.hpp"
#include "runtime/array.hpp"
#include "ShaderDefine.hpp"
#include "ShaderCompileTarget.hpp"
#include "ShaderModel.hpp"
#include "runtime/native_list.hpp"

class ShaderPlatformDefines
{
public:
    virtual ~ShaderPlatformDefines() = default;

    static Array<::ShaderDefine*>* BuildDefines(::ShaderCompileTarget target, ::ShaderModel* shaderModel, List<::ShaderDefine*>* additionalDefines);
private:
    static void AddTargetDefine(::ShaderCompileTarget target, List<::ShaderDefine*>* defines);

    static std::string GetTargetDefineName(::ShaderCompileTarget target);
};
