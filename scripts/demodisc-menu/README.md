# DemoDisc menu source tools

This directory preserves the source changes used by the coordinated Dreamcast,
PlayStation 1, PlayStation 3, Xbox 360, Xbox, Nintendo 64 and Windows menu builds.
The original DemoDisc project remains unchanged. These files are source templates
and validation tools; compiled games, generated C++, emulator profiles and build
history are kept in the workspace build directories.

`availability` contains the reviewed runtime navigation sources that hide demos
whose scenes are absent from the native package. `title-layout` contains the final
menu hook and heading layout. The two headings share one reducing font/effect
ratio, align using visible glyph widths, and end at the same painted right margin.
The layout preserves button styles and borrows entity-owned text components.

The source manifests retain the accepted original and earlier staging hashes so
the preparer can reject unexpected edits before writing any source. They describe
the existing fixed staging paths used for this DemoDisc run.

To restore the tools into those same paths:

```powershell
powershell -NoProfile -File .\scripts\demodisc-menu\restore-tools.ps1
```

Then run the existing platform build scripts. Their preparation step invokes
`prepare-staged-menu.ps1 -Platform <platform> -StagedProjectRoot <stage>` before
code generation. The script accepts all seven platforms and is idempotent.

`run-tests.ps1` runs 22 regressions against the real engine and the built DemoDisc
managed module. It requires the editor and module dependencies from the current
coordinated build paths listed in `TitleFitTests.csproj`. These checks cover
visible bounds, shared font scale, native origin rounding, viewport resizing,
effect proportions, unchanged buttons, borrowed component lifetime and zero
managed allocations per menu update. `test-combined-preparation.ps1` verifies
accepted source states, idempotence and rejection before writes.

PlayStation 1 font-size compensation is maintained in that platform repository's
`scripts/normalize-staged-font-size.ps1`. Its emulator launcher also requires
`Display/CropMode=Borders` and `Display/FineCropMode=None` to display the programmed
320x240 framebuffer without removing the eight upper and lower overscan lines.
