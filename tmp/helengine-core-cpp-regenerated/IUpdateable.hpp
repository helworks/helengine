#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class IUpdateable
{
public:
    virtual uint8_t get_UpdateOrder() = 0;

    virtual void set_UpdateOrder(uint8_t value) = 0;

    virtual void Update() = 0;
};
