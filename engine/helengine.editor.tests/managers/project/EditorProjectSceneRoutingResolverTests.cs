using helengine.editor;
using helengine.projectfile;
using Xunit;

namespace helengine.editor.tests.managers.project;

/// <summary>Verifies project scene routing is resolved from explicit data and selected-scene catalogs.</summary>
public sealed class EditorProjectSceneRoutingResolverTests {
    [Fact]
    public void Resolve_WhenPlatformIsUnknown_UsesSelectedSceneOrder() {
        ProjectFileDocument project = new ProjectFileDocument();
        ProjectPlatformSceneRoutingDocument result = new EditorProjectSceneRoutingResolver().Resolve(project, "fictional", ["GeneratedBootScene", "Opening"]);
        Assert.Equal("Opening", result.BootSceneId);
        Assert.Empty(result.SceneAliases);
    }

    [Fact]
    public void Resolve_WhenConfigured_UsesAliasesAndBootScene() {
        ProjectFileDocument project = new ProjectFileDocument {
            SceneRouting = new ProjectSceneRoutingDocument {
                Platforms = new Dictionary<string, ProjectPlatformSceneRoutingDocument> {
                    ["handheld-test"] = new ProjectPlatformSceneRoutingDocument {
                        BootSceneId = "SmallMenu",
                        SceneAliases = new Dictionary<string, string> { ["MainMenu"] = "SmallMenu" }
                    }
                }
            }
        };
        ProjectPlatformSceneRoutingDocument result = new EditorProjectSceneRoutingResolver().Resolve(project, "handheld-test", ["GeneratedBootScene", "SmallMenu"]);
        Assert.Equal("SmallMenu", result.BootSceneId);
        Assert.Equal("SmallMenu", result.SceneAliases["MainMenu"]);
    }

    [Fact]
    public void Resolve_WhenAliasDestinationIsNotSelected_Throws() {
        ProjectFileDocument project = new ProjectFileDocument {
            SceneRouting = new ProjectSceneRoutingDocument {
                Platforms = new Dictionary<string, ProjectPlatformSceneRoutingDocument> {
                    ["handheld-test"] = new ProjectPlatformSceneRoutingDocument {
                        BootSceneId = "SmallMenu",
                        SceneAliases = new Dictionary<string, string> { ["MainMenu"] = "Missing" }
                    }
                }
            }
        };
        Assert.Throws<InvalidOperationException>(() => new EditorProjectSceneRoutingResolver().Resolve(project, "handheld-test", ["GeneratedBootScene", "SmallMenu"]));
    }
}