#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderProgramBinary;

#include "ShaderProgramBinary.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_string.hpp"
#include "runtime/array.hpp"
#include "ShaderStage.hpp"
#include "ShaderProgramBinary.hpp"

class ShaderBinaryAsset
{
public:
    ShaderBinaryAsset();

    Array<uint8_t>* Bytecode;

    std::string ProgramName;

    ::ShaderStage Stage;

    std::string TargetName;

    std::string Variant;

    static ::ShaderBinaryAsset* FromBinary(::ShaderProgramBinary* binary);

    ::ShaderProgramBinary* ToBinary();
private:
    void Validate();
};
