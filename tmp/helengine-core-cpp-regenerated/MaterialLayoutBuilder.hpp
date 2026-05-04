#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderProgramAsset;
class MaterialRenderState;
class MaterialLayout;
class MaterialAsset;
class ShaderAsset;
class MaterialLayoutBinding;
class ShaderBindingAsset;

#include "runtime/native_exceptions.hpp"
#include "ShaderProgramAsset.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_dictionary.hpp"
#include "system/string_comparer.hpp"
#include "system/string_comparer.hpp"
#include "MaterialRenderState.hpp"
#include "MaterialLayout.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_list.hpp"
#include "MaterialLayout.hpp"
#include "MaterialAsset.hpp"
#include "ShaderAsset.hpp"
#include "MaterialLayoutBinding.hpp"
#include "ShaderStage.hpp"
#include "ShaderBindingAsset.hpp"

class MaterialLayoutBuilder
{
public:
    virtual ~MaterialLayoutBuilder() = default;

    static ::MaterialLayout* Build(::MaterialAsset* materialAsset, ::ShaderAsset* shaderAsset);
private:
    static std::string TransformBufferName;

    static void AddBindingToCategory(::MaterialLayoutBinding* binding, List<::MaterialLayoutBinding*>* textureBindings, List<::MaterialLayoutBinding*>* constantBufferBindings, List<::MaterialLayoutBinding*>* samplerBindings);

    static void AddBindings(::ShaderProgramAsset* program, List<::MaterialLayoutBinding*>* textureBindings, List<::MaterialLayoutBinding*>* constantBufferBindings, List<::MaterialLayoutBinding*>* samplerBindings, Dictionary<std::string, ::MaterialLayoutBinding*>* bindingByKey);

    static std::string BuildBindingKey(::MaterialLayoutBinding* binding);

    static ::ShaderProgramAsset* FindProgram(::ShaderAsset* shaderAsset, std::string programName, ::ShaderStage stage);

    static bool IsEngineManagedBinding(::ShaderBindingAsset* binding);

    static void ValidateMatchingBinding(::MaterialLayoutBinding* existingBinding, ::MaterialLayoutBinding* newBinding);
};
