#pragma once
#ifdef DrawText
#undef DrawText
#endif
#include <cstdint>

class Component;
class ITextDrawable2D;
class IDrawable2D;
class Entity;
class Core;
class ObjectManager;
class FontChar;
class float3;
class float2;
class FontAsset;
class RenderManager2D;
class byte4;
class float4;
class RuntimeTexture;
class RoundedRectComponent;
class TextComponentSelectionUpdateComponent;

#include "Component.hpp"
#include "ITextDrawable2D.hpp"
#include "IDrawable2D.hpp"
#include "Component.hpp"
#include "Entity.hpp"
#include "Core.hpp"
#include "Core.hpp"
#include "ObjectManager.hpp"
#include "runtime/native_string.hpp"
#include "system/math.hpp"
#include "system/math.hpp"
#include "FontChar.hpp"
#include "float3.hpp"
#include "float2.hpp"
#include "FontAsset.hpp"
#include "float2.hpp"
#include "InputSystem.hpp"
#include "InputSystem.hpp"
#include "RenderManager2D.hpp"
#include "byte4.hpp"
#include "FontAsset.hpp"
#include "int2.hpp"
#include "float4.hpp"
#include "RuntimeTexture.hpp"
#include "Entity.hpp"
#include "RoundedRectComponent.hpp"
#include "TextComponentSelectionUpdateComponent.hpp"

class TextComponent : public Component, public ITextDrawable2D
{
public:
    virtual ~TextComponent() = default;

    ::byte4 Color;

    ::byte4 get_Color();
    void set_Color(::byte4 value);

    ::FontAsset* Font;

    ::FontAsset* get_Font();
    void set_Font(::FontAsset* value);

    bool get_HasSelection();

    uint8_t LayerMask;

    uint8_t get_LayerMask();
    void set_LayerMask(uint8_t value);

    uint8_t get_RenderOrder2D();

    void set_RenderOrder2D(uint8_t value);

    float Rotation;

    float get_Rotation();
    void set_Rotation(float value);

    bool get_SelectionEnabled();

    void set_SelectionEnabled(bool value);

    int32_t get_SelectionEnd();

    int32_t get_SelectionStart();

    int2* Size;

    int2* get_Size();
    void set_Size(int2* value);

    ::float4 SourceRect;

    ::float4 get_SourceRect();
    void set_SourceRect(::float4 value);

    std::string get_Text();

    void set_Text(std::string value);

    ::RuntimeTexture* Texture;

    ::RuntimeTexture* get_Texture();
    void set_Texture(::RuntimeTexture* value);

    bool WrapText;

    bool get_WrapText();
    void set_WrapText(bool value);

    void ClearSelection();

    void ComponentAdded(::Entity* entity);

    void ComponentRemoved(::Entity* entity);

    virtual void Draw();

    void ParentEnabledChange(bool newEnabled);

    void SelectAll();

    TextComponent();

    void UpdateSelectionInput();

    ::Entity* get_Parent();

    void set_Parent(::Entity* value);
private:
    int32_t CursorPositionValue;

    bool IsFocusedValue;

    bool IsSelectingTextValue;

    uint8_t RenderOrder2DValue;

    int32_t SelectionAnchorPositionValue;

    bool SelectionEnabledValue;

    ::Entity* SelectionEntityValue;

    ::RoundedRectComponent* SelectionSpriteValue;

    ::TextComponentSelectionUpdateComponent* SelectionUpdateComponentValue;

    std::string TextValue;

    void ClampSelectionToTextLength();

    bool ContainsScreenPoint(int32_t x, int32_t y);

    void EnsureSelectionInfrastructure(::Entity* entity);

    void HandleSelectionDrag(int32_t pointerX, int32_t pointerY);

    void HandleSelectionKeyboardInput();

    void HandleSelectionPress(int32_t pointerX, int32_t pointerY);

    void HandleSelectionRelease();

    void MoveCursorLeft(bool extendSelection);

    void MoveCursorRight(bool extendSelection);

    void MoveCursorToEnd(bool extendSelection);

    void MoveCursorToStart(bool extendSelection);

    double ResolveCharacterAdvance(char character);

    int32_t ResolveCursorPositionFromClick(int32_t pointerX, int32_t pointerY);

    int32_t ResolveLineCount();

    int32_t ResolveLineEndIndex(int32_t lineIndex);

    int32_t ResolveLineIndexFromLocalY(double localY);

    int32_t ResolveLineIndexFromTextIndex(int32_t textIndex);

    int32_t ResolveLineStartIndex(int32_t lineIndex);

    uint8_t ResolveSelectionRenderOrder();

    double ResolveTextWidth(int32_t startIndex, int32_t endIndex);

    double ResolveTextWidthInLine(int32_t lineIndex, int32_t startIndex, int32_t endIndex);

    void SetFocusedState(bool newFocused);

    void UpdateSelectionRenderOrder();

    void UpdateSelectionVisual();
};
