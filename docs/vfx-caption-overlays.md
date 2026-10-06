# Transparent caption overlays

The VFX tool can render SRT or Whisper JSON into a full-canvas PNG RGBA sequence. The caption renderer runs on Windows and does not require a GPU, a source video, or an alpha mask.

Use the `captions` subcommand of `helengine.vfx.cli`. Width, height, FPS and duration control the exported canvas. Omit `--duration` or set it to `0` to export through the final caption; set the video duration to include trailing transparent frames.

Six built-in presets cover bold yellow emphasis, cyan bounce, pink sticker, two-frame comic lettering, a clean lower third, and one-word pop. Select one with `--preset 0..5`, or provide a JSON style with `--style`. `CenterX`, `CenterY` and `MaxWidth` are fractions of the canvas; font size, outline width and shadow offset are output pixels. Colors accept `#RRGGBB` or `#AARRGGBB`.

## Command-line export

Run from the helengine repository directory:

```powershell
rtk proxy dotnet run --project engine/helengine.vfx.cli -- captions --help
rtk proxy dotnet run --project engine/helengine.vfx.cli -- captions --input transcript.srt --out captions --preset 0 --width 1080 --height 1920 --fps 30
rtk proxy dotnet run --project engine/helengine.vfx.cli -- captions --input whisper.json --out captions-custom --style caption-style.json --font custom.ttf --duration 45.5
rtk proxy dotnet run --project engine/helengine.vfx.cli -- captions --input whisper.json --out captions-texture --atlas font-atlas.json
```

Existing shader effect commands still use their original arguments. Captions are a separate `captions` command because they generate images from text instead of processing source/mask EXR sequences.

The destination must be new or empty. Export writes `caption.000000.png`, `caption.000001.png`, etc. Every frame starts at `frameIndex / FPS` relative to time zero of the video, including transparent frames before the first caption and during silence. `sequence.json` records the frame pattern, FPS, canvas, frame count, duration, straight alpha, and style. Fractional FPS is supported. Export rounds duration up to the next frame boundary.

An interrupted or failed export keeps completed frames and `export-in-progress.json`; a successful run replaces that status with the final `sequence.json`. A folder with partial output must not be treated as a finished sequence.

## Transcription timing

SRT supplies timing for complete phrases. It supports multiline text, UTF-8 accents, CRLF/LF, optional numbering, and basic SubRip formatting tags whose appearance is replaced by the chosen preset. Phrase animations work with SRT. The renderer never estimates word timestamps: one-word grouping and spoken-word colors need aligned Whisper JSON.

Accepted JSON shapes are standard Whisper `segments`, a segment array, `segments` with root-level `words`, or a root-level `words` list. Word entries can use `word` or `text`:

```json
{
  "segments": [
    {
      "start": 1.0,
      "end": 3.0,
      "text": "Olá mundo!",
      "words": [
        { "word": "Olá", "start": 1.1, "end": 1.7 },
        { "word": "mundo!", "start": 1.9, "end": 2.8 }
      ]
    }
  ]
}
```

Words retain their supplied start/end times. Zero-duration words such as punctuation remain in the text without being highlighted. `WordsPerGroup` changes the displayed group at the next group's first real word time. `WordsPerLine` and `MaxWidth` wrap text; the renderer reduces lettering size to fit longer text. SRT text is preserved as a whole phrase, with explicit line breaks, regardless of `WordsPerGroup`. Overlapping cues display the latest-starting active cue.

This feature imports files produced by Cortex/Whisper; it does not change Cortex or invoke transcription.

## Custom fonts and texture animation

Select a local TTF/OTF with `--font`, or set `FontFamily` to an installed family in the style JSON. Font files are loaded privately; no system font installation is needed. Omit `FontFile` and `AtlasFile` in the style to use an installed family.

For texture lettering, select an atlas JSON with `--atlas`. Each Unicode glyph maps to a source rectangle plus a horizontal advance and optional offsets. Atlas textures are PNGs with alpha. Multiple frames share the same glyph rectangles and metrics, and cycle at `TextureFps` relative to the start of the caption. For a two-frame effect, list two textures with the same layout but differently drawn glyphs:

```json
{
  "frames": ["font-frame-a.png", "font-frame-b.png"],
  "lineHeight": 64,
  "spaceAdvance": 20,
  "glyphs": {
    "A": { "x": 0, "y": 0, "width": 40, "height": 60, "advance": 44 },
    "B": { "x": 40, "y": 0, "width": 40, "height": 60, "advance": 44 },
    "É": { "x": 80, "y": 0, "width": 40, "height": 64, "advance": 44, "offsetY": -4 }
  }
}
```

Include every character used in the captions, including punctuation and accents. Disable `Uppercase` for lowercase atlases, or provide uppercase glyphs. Missing glyphs and invalid rectangles produce explicit errors. White atlas glyphs receive the preset's text/highlight tint; colored glyph textures retain their authored colors with that tint multiplied in. Texture cycling works with every animation setting. `TwoFrame` motion additionally alternates two fixed positions, including with vector fonts.

Font/atlas paths in saved style files and texture paths in atlas files resolve relative to their owning JSON file. Output currently uses PNG sequences; animated WebP export is not implemented.

## Validation

The dedicated `helengine.vfx.captions.tests` project covers import errors, cue boundaries and overlaps, real word highlighting and silence, deterministic two-frame motion, accented private fonts, animated Unicode atlases, PNG alpha round trips, manifests, cancellation, overwrite protection and the caption command. Set `HELWORKS_CAPTION_TEST_ARTIFACTS` to a workspace-owned folder before running tests.
