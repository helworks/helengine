#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class UpdateComponent;
class IUpdateable;
class Entity;
class FontAsset;
class TextComponent;

#include "UpdateComponent.hpp"
#include "IUpdateable.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_exceptions.hpp"
#include "UpdateComponent.hpp"
#include "Entity.hpp"
#include "runtime/native_list.hpp"
#include "FontAsset.hpp"
#include "runtime/native_list.hpp"
#include "FontAsset.hpp"
#include "int2.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_datetime.hpp"
#include "Entity.hpp"
#include "TextComponent.hpp"

class FPSComponent : public UpdateComponent
{
public:
    virtual ~FPSComponent() = default;

    ::FontAsset* get_Font();

    void set_Font(::FontAsset* value);

    int2* get_Padding();

    void set_Padding(int2* value);

    double get_RefreshIntervalSeconds();

    void set_RefreshIntervalSeconds(double value);

    std::string RenderFpsText;

    std::string get_RenderFpsText();
    void set_RenderFpsText(std::string value);

    uint8_t get_RenderOrder2D();

    void set_RenderOrder2D(uint8_t value);

    std::string UpdateFpsText;

    std::string get_UpdateFpsText();
    void set_UpdateFpsText(std::string value);

    void ComponentAdded(::Entity* entity);

    void ComponentRemoved(::Entity* entity);

    FPSComponent();

    void ParentEnabledChange(bool newEnabled);

    static void RecordRenderFrame();

    static void RecordUpdateFrame();

    void Update();

    uint8_t get_UpdateOrder();

    void set_UpdateOrder(uint8_t value);

    ::Entity* get_Parent();

    void set_Parent(::Entity* value);
private:
    static List<::FPSComponent*>* ActiveComponents;

    bool Initialized;

    DateTime LastSampleUtc;

    ::Entity* OverlayHost;

    int32_t RenderFrameCount;

    ::Entity* RenderRowHost;

    ::TextComponent* RenderTextComponent;

    int32_t UpdateFrameCount;

    ::Entity* UpdateRowHost;

    ::TextComponent* UpdateTextComponent;

    ::FontAsset* font;

    int2* padding;

    double refreshIntervalSeconds;

    uint8_t renderOrder2D;

    void ApplyFont();

    void ApplyPadding();

    void ApplyRenderOrder();

    void ResetSamplingWindow();

    static ::FontAsset* ResolveDefaultFont();

    void TryRefreshOverlay();
};
