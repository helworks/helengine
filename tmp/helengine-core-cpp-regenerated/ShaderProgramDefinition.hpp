#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderBinding;
class ShaderVertexElement;
class ShaderVariant;

#include "runtime/native_list.hpp"
#include "ShaderBinding.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_list.hpp"
#include "ShaderVertexElement.hpp"
#include "ShaderStage.hpp"
#include "runtime/native_list.hpp"
#include "ShaderVariant.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"

class ShaderProgramDefinition
{
public:
    List<::ShaderBinding*>* get_Bindings();

    std::string EntryPoint;

    std::string get_EntryPoint();

    List<::ShaderVertexElement*>* get_Inputs();

    std::string Name;

    std::string get_Name();

    List<::ShaderVertexElement*>* get_Outputs();

    ::ShaderStage Stage;

    ::ShaderStage get_Stage();

    List<::ShaderVariant*>* get_Variants();

    ShaderProgramDefinition(std::string name, ::ShaderStage stage, std::string entryPoint, Array<::ShaderBinding*>* bindings, Array<::ShaderVertexElement*>* inputs, Array<::ShaderVertexElement*>* outputs, Array<::ShaderVariant*>* variants);
private:
    Array<::ShaderBinding*>* bindings;

    Array<::ShaderVertexElement*>* inputs;

    Array<::ShaderVertexElement*>* outputs;

    Array<::ShaderVariant*>* variants;
};
