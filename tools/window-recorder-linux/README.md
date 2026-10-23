# Linux window recorder

Records only an explicitly supplied X11/Xwayland client window whose declared
owner PID matches `--pid`, on the current `DISPLAY`. It does not capture the
desktop or change input or focus. Replace the example window ID and PID with
those of the owned application:

```sh
/home/helena/.dotnet/dotnet run --project /home/helena/dev/helworks/helengine/tools/window-recorder-linux/helengine.window-recorder-linux.csproj -c Release --artifacts-path /tmp/helengine/linux -- --window 0x123456 --pid 12345 --output /tmp/helengine/linux/menu.mp4 --duration 30 --ffmpeg /usr/bin/ffmpeg --encoder hevc_nvenc
```

`--encoder` accepts software `libx265` (the default) or explicit NVIDIA
`hevc_nvenc`. Both consume CPU RGBA captures and require successful actual
encoder initialization. NVIDIA mode requires a working NVIDIA driver. There
is no automatic fallback.

Output is silent Main 8-bit HEVC MP4, hvc1, SDR BT.709, CFR60, target 40 Mbps,
maximum 60 Mbps, GOP120 and no B frames. Thirty seconds produces 1,800 output
frames. The absolute output path must end in `menu.mp4`; an existing final or
fixed `menu.mp4.partial` is refused. Ctrl+C cancels the owned encoding process
and removes its unverified partial.

Sequential writes keep memory bounded. Missed capture intervals repeat the
preceding capture at their original output slots. The success JSON reports
actual capture sample count, repeated output slots and elapsed time separately;
samples can contain unchanged images, and CFR60 does not measure game FPS.
Success is reported only after finalization, complete-file decode verification
and exclusive publication of the final MP4.
