#ifndef HEL_SHADER_COMMON_HLSL
#define HEL_SHADER_COMMON_HLSL 1

// Target defines set by the build pipeline:
// HEL_DX9, HEL_DX11, HEL_DX12, HEL_VULKAN, HEL_METAL

#if defined(HEL_DX11) || defined(HEL_DX12) || defined(HEL_VULKAN) || defined(HEL_METAL)
    #define HEL_DX11_PLUS 1
#else
    #define HEL_DX11_PLUS 0
#endif

#if defined(HEL_VULKAN)
    #define HEL_VK_BINDING(set, slot) [[vk::binding(slot, set)]]
#else
    #define HEL_VK_BINDING(set, slot)
#endif

#if defined(HEL_DX12)
    #define HEL_DX_REGISTER(reg, set) register(reg, space##set)
#else
    #define HEL_DX_REGISTER(reg, set) register(reg)
#endif

#if defined(HEL_DX9)
    #define HEL_POSITION_SEMANTIC POSITION
    #define HEL_TARGET_SEMANTIC COLOR0
#else
    #define HEL_POSITION_SEMANTIC SV_POSITION
    #define HEL_TARGET_SEMANTIC SV_Target
#endif

#if defined(HEL_DX9)
    #define HEL_CBUFFER_BEGIN(name, set, slot) struct name##_Type {
    #define HEL_CBUFFER_END(name, set, slot) } name : HEL_DX_REGISTER(c##slot, set)
    #define HEL_TEXTURE2D(name, set, slot) sampler2D name : HEL_DX_REGISTER(s##slot, set)
    #define HEL_SAMPLER(name, set, slot)
    #define HEL_TEXTURE2D_SAMPLE(tex, samp, uv) tex2D(tex, uv)
#else
    #define HEL_CBUFFER_BEGIN(name, set, slot) HEL_VK_BINDING(set, slot) cbuffer name : HEL_DX_REGISTER(b##slot, set) {
    #define HEL_CBUFFER_END(name, set, slot) }
    #define HEL_TEXTURE2D(name, set, slot) HEL_VK_BINDING(set, slot) Texture2D name : HEL_DX_REGISTER(t##slot, set)
    #define HEL_SAMPLER(name, set, slot) HEL_VK_BINDING(set, slot) SamplerState name : HEL_DX_REGISTER(s##slot, set)
    #define HEL_TEXTURE2D_SAMPLE(tex, samp, uv) tex.Sample(samp, uv)
#endif

#endif
