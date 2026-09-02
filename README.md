# osu! patcher

Windows launcher and runtime patcher for starting `osu!.exe` to make relax better.

## ⚠️ Warning

**Use at your own risk.** This patcher modifies the osu! client at runtime. Depending on the server's anti-cheat detection, using this tool may result in account restrictions, bans, or other penalties. The developers are not responsible for any consequences resulting from the use of this software.

## Projects

- `OsuPatcher.Launcher/` - Tauri desktop app and installer bundle.
- `OsuPatcher.Cli/` - Windows CLI that starts osu! and injects the runtime patcher.
- `OsuPatcher.Runtime/` - .NET Framework runtime patcher DLL injected into osu!.
- `OsuPatcher.PP/` - Rust FFI library used by the runtime patcher for performance calculation.

## Requirements

- Windows
- .NET SDK with .NET Framework 4.7.2 targeting support
- Rust toolchain with the `i686-pc-windows-msvc` target
- Node.js and npm
- NuGet packages restored into `packages/`

Install the Rust target if needed:

```powershell
rustup target add i686-pc-windows-msvc
```

Install launcher dependencies:

```powershell
cd OsuPatcher.Launcher
npm.cmd install
```

## Build

From the repository root:

```powershell
dotnet build OsuPatcher.Runtime.sln -c Release
dotnet build OsuPatcher.Cli.sln -c Release
```

Build the FFI library:

```powershell
cd OsuPatcher.PP
cargo build --release --target i686-pc-windows-msvc
```

Build the launcher installer:

```powershell
cd OsuPatcher.Launcher
npm.cmd run tauri:build
```

The NSIS installer is written to:

```text
OsuPatcher.Launcher/src-tauri/target/release/bundle/nsis/
```

## Development

Run the launcher in development mode:

```powershell
cd OsuPatcher.Launcher
npm.cmd run tauri:dev
```

The Tauri bundle expects these release artifacts to exist:

- `OsuPatcher.Cli/bin/Release/patcher-cli.exe`
- `OsuPatcher.Runtime/bin/Release/OsuPatcher.Runtime.dll`
- `OsuPatcher.Runtime/bin/Release/0Harmony.dll`
- `OsuPatcher.PP/target/i686-pc-windows-msvc/release/refx_ffi.dll`

## Release

1. Build all release artifacts.
2. Run `npm.cmd run tauri:build` from `OsuPatcher.Launcher/`.
3. Tag the commit, for example `v0.1.0`.
4. Upload the generated NSIS installer to the GitHub release.
