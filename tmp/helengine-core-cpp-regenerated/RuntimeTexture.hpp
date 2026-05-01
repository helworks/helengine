#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class RuntimeData;

#include "RuntimeData.hpp"

class RuntimeTexture : public RuntimeData
{
public:
    RuntimeTexture();

    int32_t Height;

    int32_t get_Height();
    void set_Height(int32_t value);

    int32_t Width;

    int32_t get_Width();
    void set_Width(int32_t value);

    std::string get_Id();

    void set_Id(std::string value);
};
