# Prebuilt personal-fork distribution

`win-x64/` contains the Windows x64 Native AOT build of this fork. It runs on a
new Windows x64 machine without .NET, Visual Studio, C++ Build Tools, or Git LFS.
Install Codex separately. The bridge and updater must stay together.

## Set up a new machine

From the cloned repository in PowerShell:

```powershell
git fetch origin
git switch codex/local-customizations
git pull --ff-only

$runtime = 'C:\Runtime\copilot-bridge\v2'
New-Item -ItemType Directory -Path $runtime -Force | Out-Null
Copy-Item .\dist\win-x64\copilot-bridge.exe, .\dist\win-x64\copilot-updater.exe -Destination $runtime -Force
if (-not (Test-Path (Join-Path $runtime 'appsettings.json'))) {
    Copy-Item .\dist\win-x64\appsettings.json -Destination $runtime
}

Set-Location $runtime
.\copilot-bridge.exe auth login
.\copilot-bridge.exe config codex
.\copilot-bridge.exe serve
```

Complete the device-code login using an account with Copilot access. Keep the
bridge console running, fully restart Codex, then select GPT-6.2 Astra/Sol or
another available model. Credentials must be created on each machine; Windows
DPAPI credentials from another machine cannot be reused. GitHub CLI login is
not required to run the bridge. Configuring Codex from the final runtime path
ensures its command-auth helper points at that machine's installed executable.

For later updates, stop the installed bridge yourself before copying new
executables. The copy commands preserve an existing appsettings.json and all
credentials. Rerun `config codex` if you move the installation directory.

You can inspect `win-x64/build-info.json` for the packaged source revision and
file hashes. `win-x64/SHA256SUMS` records SHA-256 for both executables, the
configuration template and the build record. Git preserves these bytes on
checkout. Only those reviewed payload files are allowed into this directory;
credentials, logs, traces and debugging symbols are excluded.

The initial 2026-11-24 payload was verified with real Codex
0.171.0-alpha.13 against the exact executables in this directory. GPT-6.2
Sol/Astra, Sol Fast, and the packaged direct-Sol default were visibly selectable
and completed real tool tasks. Their isolated client logs contained no dispatch
fatals, errors, or retries. Archive/extraction and staged Git hashes were also
checked. Source and file hashes are recorded in the accompanying build record.

## Refresh on the build machine

Commit runtime source changes first, then run:

```powershell
.\scripts\update-dist.ps1
```

This builds both Native AOT executables using the installed .NET SDK and Visual
Studio x64 C++ toolchain, and stages the minimal payload into `dist/win-x64`.
Review the changed hashes and run `real-client-verify` with
`COPILOT_BRIDGE_TEST_NATIVE_BINARY_DIRECTORY` pointing at that directory. Commit
the refreshed distribution on `codex/local-customizations` and push it to the
personal fork only after verification. Consumers then pull and copy the new
executables. Do not copy entire runtime/build folders into Git.

This distribution currently supports Windows x64 only. Native arm64, Linux and
macOS artifacts require their own platform-specific builds. The development
version does not self-update from upstream releases, preserving this fork's
customizations.
