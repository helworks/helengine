#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderProgramBinary;
class ContentManager;
class RawByteContent;
class ShaderModuleDefinition;

#include "ShaderProgramBinary.hpp"
#include "ShaderProgramBinary.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"
#include "system/io/path.hpp"
#include "system/io/path.hpp"
#include "runtime/array.hpp"
#include "ContentManager.hpp"
#include "RawByteContent.hpp"
#include "ContentManager.hpp"
#include "ShaderModuleDefinition.hpp"
#include "ShaderCompileTarget.hpp"
#include "runtime/array.hpp"

class ShaderModulePackage
{
public:
    virtual ~ShaderModulePackage() = default;

    ::ShaderModuleDefinition* Definition;

    ::ShaderModuleDefinition* get_Definition();

    std::string RootPath;

    std::string get_RootPath();

    ::ShaderCompileTarget Target;

    ::ShaderCompileTarget get_Target();

    ::ShaderProgramBinary* GetBinary(std::string programName, std::string variant);

    Array<uint8_t>* GetBinaryData(std::string programName, std::string variant);

    std::string GetBinaryPath(std::string programName, std::string variant);

    ShaderModulePackage(::ShaderCompileTarget target, std::string rootPath, ::ShaderModuleDefinition* definition);
private:
    std::string targetName;
};
