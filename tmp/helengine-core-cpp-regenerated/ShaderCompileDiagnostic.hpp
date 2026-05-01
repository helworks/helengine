#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_string.hpp"
#include "ShaderDiagnosticSeverity.hpp"

class ShaderCompileDiagnostic
{
public:
    int32_t Column;

    int32_t get_Column();

    std::string FilePath;

    std::string get_FilePath();

    int32_t Line;

    int32_t get_Line();

    std::string Message;

    std::string get_Message();

    ::ShaderDiagnosticSeverity Severity;

    ::ShaderDiagnosticSeverity get_Severity();

    ShaderCompileDiagnostic(::ShaderDiagnosticSeverity severity, std::string message, std::string filePath, int32_t line, int32_t column);
};
