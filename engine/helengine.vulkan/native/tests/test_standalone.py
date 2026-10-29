"""Build and link the actual Vulkan module without platform window packages."""
import argparse
import pathlib
import subprocess


def main():
    """Require an explicit external build root and pass toolchain arguments without composing shell commands."""
    parser = argparse.ArgumentParser()
    parser.add_argument("--build-root", type=pathlib.Path, required=True)
    parser.add_argument("--cmake-argument", action="append", default=[])
    args = parser.parse_args()
    source = pathlib.Path(__file__).resolve().parents[1]
    build = args.build_root.resolve()
    if not args.build_root.is_absolute() or build == source or source in build.parents:
        parser.error("--build-root must be absolute and outside the module")
    subprocess.run(["cmake", "-S", str(source), "-B", str(build), "-G", "Ninja",
                    "-DCMAKE_DISABLE_FIND_PACKAGE_SDL3=TRUE", "-DCMAKE_DISABLE_FIND_PACKAGE_Wayland=TRUE",
                    "-DHELENGINE_VULKAN_BUILD_TESTS=ON", *args.cmake_argument], check=True)
    subprocess.run(["cmake", "--build", str(build), "--parallel", "4"], check=True)
    print("PASS standalone renderer and linked context consumer without SDL/Wayland")


if __name__ == "__main__":
    main()
