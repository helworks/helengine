#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class FontTightMetrics
{
public:
    FontTightMetrics();

    float get_Height();

    float MaxBottom;

    float MinTop;

    float Width;

    FontTightMetrics(float width, float minTop, float maxBottom);
};
