#include "vulkan_render_manager_3d.hpp"
#include "Core.hpp"
#include "ObjectManager.hpp"
#include "Entity.hpp"
#include "Component.hpp"
#include "LightComponent.hpp"
#include "PointLightComponent.hpp"
#include "SpotLightComponent.hpp"
#include "LightDirectionUtility.hpp"
#include "ICamera.hpp"
#include <algorithm>
#include <array>
#include <cmath>
#include <cstring>
#include <numbers>
#include <stdexcept>

namespace helengine::vulkan {
void VulkanRenderManager3D::PrepareCameraLights(::ICamera* camera) {
    CurrentLights.clear(); std::array<float,72> values {}; ::float3 ambient(0.0f,0.0f,0.0f); uint32_t count=0;
    const uint32_t budget=static_cast<uint32_t>(std::clamp(get_MaximumVisibleLights(),0,4));
    auto* entities=get_OwnerCore()->get_ObjectManager()->get_Entities();
    for (int32_t entityIndex=0;entities && entityIndex<entities->get_Count();++entityIndex) {
        auto* entity=(*entities)[entityIndex];
        if (!entity || !entity->get_IsHierarchyEnabled() || !(entity->get_LayerMask()&camera->get_LayerMask())) { continue; }
        auto* components=entity->get_Components();
        for (int32_t index=0;components && index<components->get_Count();++index) {
            auto* light=dynamic_cast<::LightComponent*>((*components)[index]); if (!light) { continue; }
            const auto color=light->get_Color(); const float intensity=light->get_Intensity();
            if (light->get_LightType()==::LightType::Ambient) { ambient.X+=color.X*intensity; ambient.Y+=color.Y*intensity; ambient.Z+=color.Z*intensity; continue; }
            if (count>=budget) { continue; }
            CurrentLights.push_back(light);
            const size_t offset=8+count*16; const auto direction=::LightDirectionUtility::GetLightDirection(light);
            values[offset]=static_cast<float>(color.X*intensity); values[offset+1]=static_cast<float>(color.Y*intensity); values[offset+2]=static_cast<float>(color.Z*intensity); values[offset+3]=static_cast<float>(light->get_LightType());
            values[offset+4]=direction.X.ToSingle(); values[offset+5]=direction.Y.ToSingle(); values[offset+6]=direction.Z.ToSingle(); values[offset+7]=light->get_ShadowStrength();
            float range=0;
            if (auto* point=dynamic_cast<::PointLightComponent*>(light)) { range=point->get_Range(); }
            else if (auto* spot=dynamic_cast<::SpotLightComponent*>(light)) {
                range=spot->get_Range(); values[offset+12]=std::cos(spot->get_InnerConeAngleDegrees()*std::numbers::pi_v<float>/180.0f); values[offset+13]=std::cos(spot->get_OuterConeAngleDegrees()*std::numbers::pi_v<float>/180.0f);
            }
            if (light->get_LightType()!=::LightType::Directional) { const auto position=entity->get_Position(); values[offset+8]=position.X.ToSingle(); values[offset+9]=position.Y.ToSingle(); values[offset+10]=position.Z.ToSingle(); values[offset+11]=range; }
            ++count;
        }
    }
    values[0]=ambient.X.ToSingle(); values[1]=ambient.Y.ToSingle(); values[2]=ambient.Z.ToSingle(); values[4]=static_cast<float>(count);
    CurrentForwardLights.resize(sizeof(values)); std::memcpy(CurrentForwardLights.data(),values.data(),sizeof(values));
}
}
