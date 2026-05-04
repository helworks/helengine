#ifdef DrawText
#undef DrawText
#endif
#include "FPSComponent.hpp"
#include "runtime/native_exceptions.hpp"
#include "UpdateComponent.hpp"
#include "Entity.hpp"
#include "runtime/native_list.hpp"
#include "FPSComponent.hpp"
#include "FontAsset.hpp"
#include "TextComponent.hpp"
#include "byte4.hpp"
#include "float3.hpp"
#include "Core.hpp"
#include "runtime/array.hpp"
#include "runtime/finally.hpp"
#include "runtime/native_cast.hpp"
#include "runtime/native_datetime.hpp"
#include "runtime/native_dictionary.hpp"
#include "runtime/native_disposable.hpp"
#include "runtime/native_enum.hpp"
#include "runtime/native_event.hpp"
#include "runtime/native_exceptions.hpp"
#include "runtime/native_list.hpp"
#include "runtime/native_nullable.hpp"
#include "runtime/native_span.hpp"
#include "runtime/native_stack.hpp"
#include "runtime/native_string.hpp"
#include "runtime/native_tuple.hpp"
#include "runtime/native_type.hpp"
#include "system/app_context.hpp"
#include "system/binary_primitives.hpp"
#include "system/bit_converter.hpp"
#include "system/diagnostics/debug.hpp"
#include "system/diagnostics/stopwatch.hpp"
#include "system/guid.hpp"
#include "system/io/file-stream.hpp"
#include "system/io/file.hpp"
#include "system/io/memory-stream.hpp"
#include "system/io/path.hpp"
#include "system/io/stream-reader.hpp"
#include "system/io/stream.hpp"
#include "system/io/string-reader.hpp"
#include "system/math.hpp"
#include "system/number.hpp"
#include "system/security/cryptography/sha256.hpp"
#include "system/string_comparer.hpp"
#include "system/text/encoding.hpp"
#include "system/text/regular_expressions/regex.hpp"
#include "system/text/string-builder.hpp"

::FontAsset* FPSComponent::get_Font()
{
return this->font;}

void FPSComponent::set_Font(::FontAsset* value)
{
    if (value == nullptr)
    {
throw new ArgumentNullException("value");
    }
    if (this->font == value)
    {
return;    }
this->font = value;
this->ApplyFont();
}

int2* FPSComponent::get_Padding()
{
return this->padding;}

void FPSComponent::set_Padding(int2* value)
{
this->padding = value;
this->ApplyPadding();
}

double FPSComponent::get_RefreshIntervalSeconds()
{
return this->refreshIntervalSeconds;}

void FPSComponent::set_RefreshIntervalSeconds(double value)
{
    if (value < 0.0)
    {
throw ([&]() {
auto __ctor_arg_0000017F = "value";
auto __ctor_arg_00000180 = "Refresh interval must be zero or greater.";
return new ArgumentOutOfRangeException(__ctor_arg_0000017F, __ctor_arg_00000180);
})();
    }
this->refreshIntervalSeconds = value;
}

std::string FPSComponent::get_RenderFpsText()
{
return this->RenderFpsText;
}

void FPSComponent::set_RenderFpsText(std::string value)
{
this->RenderFpsText = value;
}

uint8_t FPSComponent::get_RenderOrder2D()
{
return this->renderOrder2D;}

void FPSComponent::set_RenderOrder2D(uint8_t value)
{
    if (this->renderOrder2D == value)
    {
return;    }
this->renderOrder2D = value;
this->ApplyRenderOrder();
}

std::string FPSComponent::get_UpdateFpsText()
{
return this->UpdateFpsText;
}

void FPSComponent::set_UpdateFpsText(std::string value)
{
this->UpdateFpsText = value;
}

void FPSComponent::ComponentAdded(::Entity* entity)
{
    if (entity == nullptr)
    {
throw new ArgumentNullException("entity");
    }
    if (this->get_Font() == nullptr)
    {
throw new InvalidOperationException("FPSComponent requires a font asset before it can be attached.");
    }
UpdateComponent::ComponentAdded(entity);
    if (this->Initialized)
    {
return;    }
    if (entity->get_Children() == nullptr)
    {
entity->InitChildren();
    }
this->OverlayHost = new ::Entity();
this->OverlayHost->set_LayerMask(entity->get_LayerMask());
this->OverlayHost->InitChildren();
this->OverlayHost->InitComponents();
entity->AddChild(this->OverlayHost);
this->UpdateRowHost = new ::Entity();
this->UpdateRowHost->set_LayerMask(entity->get_LayerMask());
this->UpdateRowHost->InitChildren();
this->UpdateRowHost->InitComponents();
this->OverlayHost->AddChild(this->UpdateRowHost);
this->UpdateTextComponent = new ::TextComponent();
this->UpdateTextComponent->set_Font(this->get_Font());
this->UpdateTextComponent->set_Color(::byte4(255, 255, 255, 255));
this->UpdateTextComponent->set_RenderOrder2D(this->get_RenderOrder2D());
this->UpdateRowHost->AddComponent(this->UpdateTextComponent);
this->RenderRowHost = new ::Entity();
this->RenderRowHost->set_LayerMask(entity->get_LayerMask());
this->RenderRowHost->InitChildren();
this->RenderRowHost->InitComponents();
this->RenderRowHost->set_LocalPosition(::float3(0.0f, this->get_Font()->get_LineHeight(), 0.1f));
this->OverlayHost->AddChild(this->RenderRowHost);
this->RenderTextComponent = new ::TextComponent();
this->RenderTextComponent->set_Color(::byte4(255, 255, 255, 255));
this->RenderTextComponent->set_RenderOrder2D(this->get_RenderOrder2D());
this->RenderRowHost->AddComponent(this->RenderTextComponent);
this->ApplyFont();
this->ResetSamplingWindow();
this->ApplyPadding();
this->Initialized = true;
ActiveComponents->Add(this);
}

void FPSComponent::ComponentRemoved(::Entity* entity)
{
const bool wasInitialized = this->Initialized;
this->Initialized = false;
ActiveComponents->Remove(this);
    if (wasInitialized && Parent != nullptr && this->OverlayHost != nullptr && this->OverlayHost->get_Parent() == Parent)
    {
Parent->RemoveChild(this->OverlayHost);
    }
this->OverlayHost = nullptr;
this->UpdateRowHost = nullptr;
this->RenderRowHost = nullptr;
this->UpdateTextComponent = nullptr;
this->RenderTextComponent = nullptr;
this->Initialized = false;
UpdateComponent::ComponentRemoved(entity);
}

FPSComponent::FPSComponent() : RenderFpsText(), UpdateFpsText(), Initialized(), LastSampleUtc(), OverlayHost(), RenderFrameCount(0), RenderRowHost(), RenderTextComponent(), UpdateFrameCount(0), UpdateRowHost(), UpdateTextComponent(), font(), padding(new int2(8, 6)), refreshIntervalSeconds(0.5), renderOrder2D(250)
{
::FontAsset *defaultFont = this->ResolveDefaultFont();
    if (defaultFont != nullptr)
    {
this->set_Font(defaultFont);
    }
}

void FPSComponent::ParentEnabledChange(bool newEnabled)
{
UpdateComponent::ParentEnabledChange(newEnabled);
this->ResetSamplingWindow();
}

void FPSComponent::RecordRenderFrame()
{
for (int32_t i = ActiveComponents->Count() - 1; i >= 0; i--) {
::FPSComponent *component = (*ActiveComponents)[i];
    if (component->Initialized && component->Parent != nullptr && component->Parent->get_IsHierarchyEnabled())
    {
component->RenderFrameCount++;
    }
}
}

void FPSComponent::RecordUpdateFrame()
{
for (int32_t i = ActiveComponents->Count() - 1; i >= 0; i--) {
::FPSComponent *component = (*ActiveComponents)[i];
    if (component->Initialized && component->Parent != nullptr && component->Parent->get_IsHierarchyEnabled())
    {
component->UpdateFrameCount++;
    }
}
}

void FPSComponent::Update()
{
    if (!this->Initialized)
    {
throw new InvalidOperationException("FPSComponent must be attached before it can sample frames.");
    }
this->TryRefreshOverlay();
}

uint8_t FPSComponent::get_UpdateOrder()
{
return this->UpdateComponent::get_UpdateOrder();
}

void FPSComponent::set_UpdateOrder(uint8_t value)
{
this->UpdateComponent::set_UpdateOrder(value);
}

::Entity* FPSComponent::get_Parent()
{
return this->Component::get_Parent();
}

void FPSComponent::set_Parent(::Entity* value)
{
this->Component::set_Parent(value);
}

List<::FPSComponent*>* FPSComponent::ActiveComponents = new List<::FPSComponent*>();

void FPSComponent::ApplyFont()
{
    if (this->UpdateTextComponent != nullptr)
    {
this->UpdateTextComponent->set_Font(this->get_Font());
    }
    if (this->RenderTextComponent != nullptr)
    {
this->RenderTextComponent->set_Font(this->get_Font());
    }
    if (this->RenderRowHost != nullptr)
    {
this->RenderRowHost->set_LocalPosition(::float3(0.0f, this->get_Font()->get_LineHeight(), 0.1f));
    }
}

void FPSComponent::ApplyPadding()
{
    if (this->OverlayHost == nullptr)
    {
return;    }
this->OverlayHost->set_LocalPosition(::float3(this->padding->X, this->padding->Y, 0.0f));
}

void FPSComponent::ApplyRenderOrder()
{
    if (this->UpdateTextComponent != nullptr)
    {
this->UpdateTextComponent->set_RenderOrder2D(this->get_RenderOrder2D());
    }
    if (this->RenderTextComponent != nullptr)
    {
this->RenderTextComponent->set_RenderOrder2D(this->get_RenderOrder2D());
    }
}

void FPSComponent::ResetSamplingWindow()
{
this->UpdateFrameCount = 0;
this->RenderFrameCount = 0;
this->LastSampleUtc = DateTime::UtcNow();
this->set_UpdateFpsText("Update FPS: --");
this->set_RenderFpsText("Render FPS: --");
    if (this->UpdateTextComponent != nullptr)
    {
this->UpdateTextComponent->set_Text(this->UpdateFpsText);
    }
    if (this->RenderTextComponent != nullptr)
    {
this->RenderTextComponent->set_Text(this->RenderFpsText);
    }
}

::FontAsset* FPSComponent::ResolveDefaultFont()
{
    if (Core::get_Instance() == nullptr)
    {
return nullptr;    }
return Core::get_Instance()->get_DefaultFontAsset();}

void FPSComponent::TryRefreshOverlay()
{
const double elapsedSeconds = (DateTime::UtcNow() - this->LastSampleUtc).TotalMilliseconds / 1000.0;
    if (this->refreshIntervalSeconds > 0.0 && elapsedSeconds < this->refreshIntervalSeconds)
    {
return;    }
const double safeElapsedSeconds = elapsedSeconds <= 0.0 ? 1.0 : elapsedSeconds;
const double updateFps = this->UpdateFrameCount / safeElapsedSeconds;
const double renderFps = this->RenderFrameCount / safeElapsedSeconds;
this->set_UpdateFpsText(String::Concat("Update FPS: ", std::to_string(updateFps)));
this->set_RenderFpsText(String::Concat("Render FPS: ", std::to_string(renderFps)));
    if (this->UpdateTextComponent != nullptr)
    {
this->UpdateTextComponent->set_Text(this->UpdateFpsText);
    }
    if (this->RenderTextComponent != nullptr)
    {
this->RenderTextComponent->set_Text(this->RenderFpsText);
    }
this->UpdateFrameCount = 0;
this->RenderFrameCount = 0;
this->LastSampleUtc = DateTime::UtcNow();
}

