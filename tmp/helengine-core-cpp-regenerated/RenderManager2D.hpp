#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class RuntimeTexture;
class TextureAsset;
class IRoundedRectDrawable2D;
class ISpriteDrawable2D;
class ITextDrawable2D;

#include "runtime/native_disposable.hpp"
#include "RuntimeTexture.hpp"
#include "TextureAsset.hpp"
#include "IRoundedRectDrawable2D.hpp"
#include "ISpriteDrawable2D.hpp"
#include "ITextDrawable2D.hpp"

class RenderManager2D : public IDisposable
{
public:
    virtual ::RuntimeTexture* BuildTextureFromRaw(::TextureAsset* data) = 0;

    virtual void Dispose();

    virtual void Draw();

    virtual void DrawRoundedRect(::IRoundedRectDrawable2D* shape) = 0;

    virtual void DrawSprite(::ISpriteDrawable2D* sprite) = 0;

    virtual void DrawText(::ITextDrawable2D* text) = 0;

    virtual void Update();
};
