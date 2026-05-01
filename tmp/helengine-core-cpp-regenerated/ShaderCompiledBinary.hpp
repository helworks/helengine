#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/array.hpp"
#include "runtime/native_string.hpp"
#include "ShaderStage.hpp"
#include "ShaderCompileTarget.hpp"

class ShaderCompiledBinary
{
public:
    Array<uint8_t>* Bytecode;

    Array<uint8_t>* get_Bytecode();

    std::string EntryPoint;

    std::string get_EntryPoint();

    ::ShaderStage Stage;

    ::ShaderStage get_Stage();

    ::ShaderCompileTarget Target;

    ::ShaderCompileTarget get_Target();

    std::string Variant;

    std::string get_Variant();

    ShaderCompiledBinary(::ShaderCompileTarget target, ::ShaderStage stage, std::string entryPoint, std::string variant, Array<uint8_t>* bytecode);
};
