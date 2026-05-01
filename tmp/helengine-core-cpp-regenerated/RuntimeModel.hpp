#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class RuntimeData;

#include "RuntimeData.hpp"

class RuntimeModel : public RuntimeData
{
public:
    std::string get_Id();

    void set_Id(std::string value);
};
