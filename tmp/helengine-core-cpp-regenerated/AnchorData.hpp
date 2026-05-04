#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

#include "runtime/native_nullable.hpp"

class AnchorData
{
public:
    virtual ~AnchorData() = default;

    AnchorData();

    Nullable<float> BottomDistance;

    Nullable<float> get_BottomDistance();
    void set_BottomDistance(Nullable<float> value);

    Nullable<float> LeftDistance;

    Nullable<float> get_LeftDistance();
    void set_LeftDistance(Nullable<float> value);

    Nullable<float> RightDistance;

    Nullable<float> get_RightDistance();
    void set_RightDistance(Nullable<float> value);

    Nullable<float> TopDistance;

    Nullable<float> get_TopDistance();
    void set_TopDistance(Nullable<float> value);
};
