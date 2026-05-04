#include "feature_manifest.hpp"

static const HEFeatureEntry kFeatureEntries[] = {
    { HEFeature::DebugOverlay, false, HEFeatureDecisionOrigin::NotIncluded, "DebugOverlay" },
    { HEFeature::Render2D, false, HEFeatureDecisionOrigin::NotIncluded, "Render2D" },
    { HEFeature::Shaders, false, HEFeatureDecisionOrigin::NotIncluded, "Shaders" },
    { HEFeature::Sprites, false, HEFeatureDecisionOrigin::NotIncluded, "Sprites" },
    { HEFeature::Text2D, false, HEFeatureDecisionOrigin::NotIncluded, "Text2D" },
};

bool he_feature_enabled(HEFeature feature) {
    for (const HEFeatureEntry& entry : kFeatureEntries) {
        if (entry.Feature == feature) {
            return entry.Enabled;
        }
    }

    return false;
}

const HEFeatureEntry* he_get_feature_entries(std::size_t* count) {
    if (count != nullptr) {
        *count = sizeof(kFeatureEntries) / sizeof(kFeatureEntries[0]);
    }

    return kFeatureEntries;
}
