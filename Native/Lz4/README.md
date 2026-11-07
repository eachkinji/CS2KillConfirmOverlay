LZ4 1.10.0 (BSD-2-Clause), from https://github.com/lz4/lz4/tree/v1.10.0/lib.

The source is vendored unchanged. `Build-Lz4.ps1` builds an optimized Windows
AppContainer-compatible native library with the installed MSVC C compiler.
Both hosts use the same codec source. Separate desktop and UWP binaries use
their respective CRT import libraries: the UWP build uses `vcvarsall uwp` and
`WindowsApp.lib`, and imports `VCRUNTIME140_APP.dll` from the widget's existing
VCLibs framework dependency. `Lz4.targets` selects the runtime and architecture,
and copies the matching binary and its license.
No download or package installation is performed during the build.
