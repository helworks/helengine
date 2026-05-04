#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_string.hpp"
#include "ShaderStage.hpp"

class ShaderModel
{
public:
    virtual ~ShaderModel() = default;

    int32_t Major;

    int32_t get_Major();

    int32_t Minor;

    int32_t get_Minor();

    std::string GetProfile(::ShaderStage stage);

    ShaderModel(int32_t major, int32_t minor);

    std::string ToString();
private:
    std::string GetStagePrefix(::ShaderStage stage);
};
