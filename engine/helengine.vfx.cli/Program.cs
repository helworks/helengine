using helengine.vfx;
using helengine.vfx.cli;
using helengine.vfx.effects;

// Entry point only: every effect that should be selectable is registered here, and the rest of the
// invocation is handled by VfxCliRunner.
VfxEffectRegistry.Register(new RainbowExpandEffect());
VfxEffectRegistry.Register(new RainbowAuraEffect());
VfxEffectRegistry.Register(new DepthCompositeEffect());

return VfxCliRunner.Run(args);
