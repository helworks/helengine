#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_string.hpp"
#include "runtime/native_exceptions.hpp"

class RuntimeData
{
public:
    virtual ~RuntimeData() = default;

    RuntimeData();

    std::string Id;

    std::string get_Id();
    void set_Id(std::string value);

    void SetId(std::string id);
};
