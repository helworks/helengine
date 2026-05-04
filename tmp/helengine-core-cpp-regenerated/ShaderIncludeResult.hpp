#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_string.hpp"

class ShaderIncludeResult
{
public:
    virtual ~ShaderIncludeResult() = default;

    std::string Path;

    std::string get_Path();

    std::string Source;

    std::string get_Source();

    ShaderIncludeResult(std::string path, std::string source);
};
