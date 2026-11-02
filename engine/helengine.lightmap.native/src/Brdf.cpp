#include "Brdf.h"
namespace hellm::detail {
namespace {
/// <summary>Evaluate the normalized GGX distribution using perceptual roughness clamped away from a delta lobe.</summary>
double Distribution(double nh, double roughness) {
    const double alpha = std::pow(std::max(0.04, roughness), 2), a2 = alpha * alpha;
    const double denominator = nh * nh * (a2 - 1) + 1;
    return a2 / (Pi * denominator * denominator);
}
/// <summary>Evaluate exact isotropic Smith masking for one GGX direction.</summary>
double Masking(double cosine, double roughness) {
    const double alpha = std::pow(std::max(0.04, roughness), 2);
    return 2 * cosine / (cosine + std::sqrt(alpha * alpha + (1 - alpha * alpha) * cosine * cosine));
}
/// <summary>Reserve a fixed dielectric specular proposal while exclusively sampling GGX for a conductor.</summary>
double SpecularProbability(double metallic) {
    return 0.25 + 0.75 * metallic;
}
}
Vec3 GgxHalfVector(Vec3 n, double roughness, double first, double second) {
    const double alpha = std::pow(std::max(0.04, roughness), 2);
    const double cosine = std::sqrt((1 - first) / (1 + (alpha * alpha - 1) * first));
    const double sine = std::sqrt(std::max(0.0, 1 - cosine * cosine)), angle = 2 * Pi * second;
    const Vec3 tangent = Unit(Cross(std::abs(n.Z) < 0.9 ? Vec3{0,0,1} : Vec3{0,1,0}, n));
    return Unit(Add(Add(Mul(tangent, sine * std::cos(angle)), Mul(Cross(n, tangent), sine * std::sin(angle))), Mul(n, cosine)));
}
Vec3 Brdf(Vec3 base, double metallic, double roughness, Vec3 n, Vec3 outgoing, Vec3 incoming) {
    const double nv = Dot(n, outgoing), nl = Dot(n, incoming);
    if (nv <= 0 || nl <= 0) return {};
    const Vec3 h = Unit(Add(outgoing, incoming));
    const double nh = std::clamp(Dot(n, h), 0.0, 1.0), vh = std::clamp(Dot(outgoing, h), 0.0, 1.0);
    const Vec3 f0 = Add(Mul(Vec3{0.04f,0.04f,0.04f}, 1 - metallic), Mul(base, metallic));
    const double schlick = std::pow(1 - vh, 5);
    const Vec3 fresnel = Add(f0, Mul(Sub(Vec3{1,1,1}, f0), schlick));
    const double specular = Distribution(nh, roughness) * Masking(nv, roughness) * Masking(nl, roughness) / (4 * nv * nl);
    // Reciprocal view/light interface transmission bounds diffuse energy at grazing;
    // half-vector-only attenuation otherwise gains energy in a white furnace.
    const Vec3 transmissionView = Mul(Sub(Vec3{1,1,1}, f0), 1 - std::pow(1 - std::clamp(nv,0.0,1.0),5));
    const Vec3 transmissionLight = Mul(Sub(Vec3{1,1,1}, f0), 1 - std::pow(1 - std::clamp(nl,0.0,1.0),5));
    return Add(Mul(Product(Product(transmissionView, transmissionLight), base), (1 - metallic) / Pi), Mul(fresnel, specular));
}
Vec3 SampleBrdf(Vec3 base, double metallic, double roughness, Vec3 n, Vec3 outgoing, Random &random, Vec3 &weight) {
    weight = {};
    const double probability = SpecularProbability(metallic);
    const bool specular = random.Next() < probability;
    Vec3 incoming;
    if (specular) {
        const double first = random.Next(), second = random.Next();
        const Vec3 h = GgxHalfVector(n, roughness, first, second);
        incoming = Sub(Mul(h, 2 * Dot(outgoing, h)), outgoing);
    } else incoming = CosineDirection(n, random);
    const double nl = Dot(n, incoming), nv = Dot(n, outgoing);
    if (nl <= 0 || nv <= 0) return incoming;
    const Vec3 h = Unit(Add(outgoing, incoming));
    const double nh = std::clamp(Dot(n, h), 0.0, 1.0), vh = std::abs(Dot(outgoing, h));
    if (vh <= 0) return incoming;
    const double pdf = probability * Distribution(nh, roughness) * nh / (4 * vh) + (1 - probability) * nl / Pi;
    if (pdf > 0) weight = Mul(Brdf(base, metallic, roughness, n, outgoing, incoming), nl / pdf);
    return incoming;
}
} // namespace hellm::detail
