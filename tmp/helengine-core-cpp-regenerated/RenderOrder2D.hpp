#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class RenderOrder2D
{
public:
    virtual ~RenderOrder2D() = default;

    static uint8_t FloatingPanelBias;

    static uint8_t ModalBackground;

    static uint8_t ModalForeground;

    static uint8_t ModalInput;

    static uint8_t OverlayBackground;

    static uint8_t OverlayForeground;

    static uint8_t OverlayInput;

    static uint8_t PanelBackground;

    static uint8_t PanelForeground;

    static uint8_t PanelInteractive;

    static uint8_t PanelSurface;
};
