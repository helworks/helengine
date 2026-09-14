# Project scene-routing migration

This directory documents the manual migration from legacy game-name inference to explicit `.heproj` scene routing. The migration is intentionally reviewable and does not edit external game projects automatically.

For each project, make a backup of the `.heproj` file and record the selected scene catalog before editing. Add only aliases whose destination scene exists in that project. Run the project-file reader and the generated boot-scene tests after the change.

The reviewed DemoDisc compatibility record is:

```json
{
  "sceneRouting": {
    "platforms": {
      "handheld-test": {
        "bootSceneId": "DemoDiscMainMenuHandheld",
        "sceneAliases": {
          "DemoDiscMainMenu": "DemoDiscMainMenuHandheld"
        }
      }
    }
  }
}
```

Legacy `_ds` scene pairs must be listed from the actual project scene catalog before adding any alias. The engine no longer derives those pairs from a platform identifier or suffix. A dry run should print the proposed platform record, boot scene, aliases, and missing destinations; only a separately reviewed edit should write the project file.

The engine-owned generated helper remains `GeneratedBootScene`; that identifier is not project routing data and is defined in `EngineSceneIdentifiers`.