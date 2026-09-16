HELENGINE EDITOR - PORTABLE WINDOWS X64 - ALL 10 PLATFORMS

Included targets: Windows, PlayStation 2, PSP, PS Vita, Nintendo DS,
Nintendo 3DS, GameCube, Wii, Wii U and Switch.

PREREQUISITES
1. Install Docker Desktop and start it in Linux containers mode.
   Complete Docker's WSL 2/virtualization setup if requested.
2. Install Visual Studio 2022 or newer, or its Build Tools, with the "Desktop
   development with C++" workload: MSVC x64/x86 tools, a Windows SDK,
   and C++ CMake tools for Windows (including Ninja).
3. Internet access may be needed for NuGet packages and Dockerfile
   base images used by platform builders. This is not an offline kit.

No separate .NET installation or Helengine source checkout is required.
The .NET SDK/runtime, code generator, engine sources, platform builders,
player sources and the nine console toolchain images are included.
Allow tens of GB of free disk space for extraction, Docker images and builds.

FIRST USE
1. Download all three release ZIPs (each is under 2,000,000,000 bytes):
     helengine-editor-windows-x64.zip
     helengine-toolchains-01.zip
     helengine-toolchains-02.zip
   Extract ALL THREE into the SAME short writable destination, for example
   C:\, so their Helengine folders merge into C:\Helengine.
   If Explorer suggests a separate folder named after each ZIP, change it
   to the same destination. These are ordinary ZIPs; no 7-Zip is required.
   Do not run the editor inside the ZIP or place it in Program Files.
2. After starting Docker, double-click Setup-Toolchains.cmd.
   This imports the included SDK images into Docker and can take several minutes.
   Setup first joins and verifies the two payload parts automatically, using
   about 2.8 GB of additional disk space. Missing/corrupt parts stop setup.
   It assigns the helengine-* image tags used by the platform builders.
3. Double-click Start-Helengine.cmd and choose your project's .heproj file.
   Or drag a .heproj file onto Start-Helengine.cmd.
4. Use the editor's project platform/build settings to select a supported target.

Always launch through Start-Helengine.cmd or Start-Helengine.ps1: the launcher
selects the bundled SDK, sources and relative platform manifest. Keep all
folders together when moving this portable installation.
SHA256SUMS.txt beside the downloads provides archive checksums.

COMMAND LINE
Check bundled payload paths and SDK:
  powershell -NoProfile -ExecutionPolicy Bypass -File .\Start-Helengine.ps1 -Check
Open an existing project:
  .\Start-Helengine.cmd "C:\Projects\MyGame\project.heproj"
Build a target:
  powershell -NoProfile -ExecutionPolicy Bypass -File .\Start-Helengine.ps1 -Project "C:\Projects\MyGame\project.heproj" -Build windows -Output "C:\Builds\MyGame"

PROJECTS AND VERSIONS
This is a development snapshot of the local source trees, including current
working changes. source-revisions.json records the commits and changed paths;
it is not a clean, immutable release. The platform manifest retains the source
installation's engine-version identifiers. A project's requiredEngineVersion
must match an entry in user_settings\platforms.json. Existing project settings
still determine its supported platforms and build options.

Files under user_settings, cache and tools\nuget-packages are writable local
state. Project edits and builds also write to the selected project/output.
Docker stores imported images in its own storage outside this folder.

Build support is provided for all listed targets; hardware/emulator behavior
and individual project compatibility depend on the corresponding backend.
See verification.txt for the checks performed on this particular package.
