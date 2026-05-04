#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderProgramDefinition;
class ShaderBindingAsset;
class ShaderVertexElementAsset;
class ShaderVariantAsset;
class ShaderBinding;
class ShaderVertexElement;
class ShaderVariant;

#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "ShaderProgramDefinition.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "runtime/native_list.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "runtime/native_list.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "runtime/native_list.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "ShaderBindingAsset.hpp"
#include "runtime/array.hpp"
#include "ShaderVertexElementAsset.hpp"
#include "ShaderStage.hpp"
#include "runtime/array.hpp"
#include "ShaderVariantAsset.hpp"
#include "ShaderProgramDefinition.hpp"
#include "runtime/array.hpp"
#include "ShaderBinding.hpp"
#include "runtime/array.hpp"
#include "ShaderVertexElement.hpp"
#include "runtime/array.hpp"
#include "ShaderVariant.hpp"

class ShaderProgramAsset
{
public:
    virtual ~ShaderProgramAsset() = default;

    Array<::ShaderBindingAsset*>* Bindings;

    std::string EntryPoint;

    Array<::ShaderVertexElementAsset*>* Inputs;

    std::string Name;

    Array<::ShaderVertexElementAsset*>* Outputs;

    ::ShaderStage Stage;

    Array<::ShaderVariantAsset*>* Variants;

    static ::ShaderProgramAsset* FromDefinition(::ShaderProgramDefinition* definition);

    ShaderProgramAsset();

    ::ShaderProgramDefinition* ToDefinition();
private:
    static Array<::ShaderBindingAsset*>* BuildBindingAssets(::ShaderProgramDefinition* definition);

    Array<::ShaderBinding*>* BuildBindings();

    static Array<::ShaderVertexElementAsset*>* BuildInputAssets(::ShaderProgramDefinition* definition);

    Array<::ShaderVertexElement*>* BuildInputs();

    static Array<::ShaderVertexElementAsset*>* BuildOutputAssets(::ShaderProgramDefinition* definition);

    Array<::ShaderVertexElement*>* BuildOutputs();

    static Array<::ShaderVariantAsset*>* BuildVariantAssets(::ShaderProgramDefinition* definition);

    Array<::ShaderVariant*>* BuildVariants();

    void Validate();
};
