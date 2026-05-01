#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class ShaderCompiledBinary;
class ShaderCompileDiagnostic;
class ShaderProgramDefinition;
class ShaderCompileRequest;

#include "ShaderCompiledBinary.hpp"
#include "runtime/native_list.hpp"
#include "ShaderCompileDiagnostic.hpp"
#include "ShaderProgramDefinition.hpp"
#include "ShaderCompileRequest.hpp"

class ShaderCompileResult
{
public:
    ::ShaderCompiledBinary* Binary;

    ::ShaderCompiledBinary* get_Binary();

    List<::ShaderCompileDiagnostic*>* Diagnostics;

    List<::ShaderCompileDiagnostic*>* get_Diagnostics();

    ::ShaderProgramDefinition* ProgramDefinition;

    ::ShaderProgramDefinition* get_ProgramDefinition();

    ::ShaderCompileRequest* Request;

    ::ShaderCompileRequest* get_Request();

    bool Success;

    bool get_Success();

    ShaderCompileResult(::ShaderCompileRequest* request, ::ShaderProgramDefinition* programDefinition, ::ShaderCompiledBinary* binary, List<::ShaderCompileDiagnostic*>* diagnostics, bool success);
};
