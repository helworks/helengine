#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderProgramBinary;
class ShaderProgramDefinition;

#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_exceptions.hpp"
#include "ShaderProgramBinary.hpp"
#include "ShaderProgramBinary.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_list.hpp"
#include "ShaderProgramDefinition.hpp"
#include "runtime/array.hpp"
#include "runtime/array.hpp"

class ShaderModuleDefinition
{
public:
    List<::ShaderProgramBinary*>* get_Binaries();

    std::string Name;

    std::string get_Name();

    List<::ShaderProgramDefinition*>* get_Programs();

    ::ShaderProgramBinary* GetBinary(std::string programName, std::string target, std::string variant);

    ::ShaderProgramDefinition* GetProgram(std::string programName);

    ShaderModuleDefinition(std::string name, Array<::ShaderProgramDefinition*>* programs, Array<::ShaderProgramBinary*>* binaries);

    bool TryGetBinary(std::string programName, std::string target, std::string variant, ::ShaderProgramBinary*& binary);

    bool TryGetProgram(std::string programName, ::ShaderProgramDefinition*& program);
private:
    Array<::ShaderProgramBinary*>* binaries;

    Array<::ShaderProgramDefinition*>* programs;
};
