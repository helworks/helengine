#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/array.hpp"
#include "runtime/native_string.hpp"
#include "ShaderStage.hpp"

class ShaderProgramBinary
{
public:
    Array<uint8_t>* get_Bytecode();

    std::string Path;

    std::string get_Path();

    std::string ProgramName;

    std::string get_ProgramName();

    ::ShaderStage Stage;

    ::ShaderStage get_Stage();

    std::string Target;

    std::string get_Target();

    std::string Variant;

    std::string get_Variant();

    ShaderProgramBinary(std::string programName, ::ShaderStage stage, std::string target, std::string variant, std::string path);

    ShaderProgramBinary(std::string programName, ::ShaderStage stage, std::string target, std::string variant, Array<uint8_t>* bytecode);
private:
    Array<uint8_t>* bytecode;
};
