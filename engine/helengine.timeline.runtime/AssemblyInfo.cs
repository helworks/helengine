using System.Runtime.CompilerServices;
using helengine;
using helengine.timeline.runtime;

[assembly: InternalsVisibleTo("helengine.timeline.runtime.tests")]
[assembly: GeneratedRuntimeModuleManifest(
    "timeline-runtime-module",
    typeof(TimelineRuntimeRegistration),
    nameof(TimelineRuntimeRegistration.Register),
    typeof(TimelinePlayerComponent))]
