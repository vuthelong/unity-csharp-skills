# CI upload with steamcmd

Sources: https://partner.steamgames.com/doc/sdk/uploading, https://github.com/game-ci/steam-deploy, Valve Developer Community SteamCMD page (https://developer.valvesoftware.com/wiki/SteamCMD).

## Builder account

- Create a separate Steam account for uploads and add it to the Steamworks partner group with only the "Edit App Metadata" and "Publish App Changes To Steam" permissions, as Valve recommends.
- Steam Guard must be on: Valve requires either a phone number or the Steam Mobile App attached to the account.
- After any security change on the account (email, phone), Valve says to expect a 3-day wait before it can build.
- One builder account per studio is enough; do not reuse a developer's personal account.

## Getting past Steam Guard

steamcmd caches a login token in `config/config.vdf` under its Steam folder. Valve's CI procedure:

1. On the build machine, run `steamcmd +login <username>` and enter the password and Steam Guard code once.
2. Type `info` to confirm the account is connected, then `quit`.
3. Every later run uses `steamcmd +login <username>` with no password.
4. Preserve `config.vdf` between runs; steamcmd may rewrite it after a successful login.
5. Logging in again with a password issues a new Steam Guard token, which then needs a fresh code.

For ephemeral CI runners, do steps 1-2 on a trusted machine, base64-encode the resulting `config.vdf`, store it as a CI secret, and restore it before upload. `upload.sh` / `upload.ps1` do this when `STEAM_CONFIG_VDF_BASE64` is set.

Where `config.vdf` lands depends on how steamcmd was installed. On Windows it is `config\config.vdf` next to `steamcmd.exe` (Valve: `<Steam>\config\config.vdf`). game-ci's Linux script uses `$HOME/Steam/config/config.vdf`, and its README gives `~/Library/Application Support/Steam/config/config.vdf` on macOS. Check on your runner after the first login (`find / -name config.vdf 2>/dev/null`) and set `STEAM_CONFIG_DIR` if it differs.

When Steam asks for a new code, run `steamcmd +set_steam_guard_code <code>` (Valve troubleshooting), log in once, then re-export `config.vdf` and update the secret.

Alternative: game-ci's `steam-deploy` accepts a `totp` input instead of `configVdf`, recommended there "if you have access to the shared secret". Generating a TOTP from a Steam shared secret needs third-party tooling that Valve does not document; evaluate that before adopting it, because the shared secret is equivalent to the authenticator itself.

## Secrets

| Variable | Used by | Notes |
|---|---|---|
| `STEAM_USERNAME` | both scripts | Required. |
| `STEAM_CONFIG_VDF_BASE64` | both scripts | Base64 of `config.vdf`. Never commit the raw or encoded file. |
| `STEAM_CONFIG_DIR` | both scripts | Folder to restore `config.vdf` into when the default guess is wrong. |
| `STEAM_PASSWORD` | both scripts | Only for the first interactive login. Arguments are visible in the process list and Valve says re-entering it issues a new token, so leave it unset in CI. |
| `STEAM_GUARD_CODE` | both scripts | Passed as `+set_steam_guard_code`. |
| `STEAMCMD` | both scripts | Full path to steamcmd if it is not on `PATH`. |

Mask all of these in CI logs. Restrict the upload job to protected branches or tags so a pull request from a fork cannot read them.

## Installing steamcmd on a runner

- Windows: `https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip`, or `tools\ContentBuilder\builder\steamcmd.exe` from the Steamworks SDK.
- Linux: `https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz`, then run `steamcmd.sh`. It is a 32-bit binary; on Ubuntu runners you may need the distribution's 32-bit runtime packages (for example `lib32gcc-s1`). Check the package name for your image.
- The first run self-updates; run `steamcmd +quit` once before the real upload so the update is not mixed into the upload step.

## steamcmd flags used by the scripts

```
steamcmd +@ShutdownOnFailedCommand 1 +@NoPromptForPassword 1 [+set_steam_guard_code <code>] +login <user> +run_app_build <abs path to app_build.vdf> +quit
```

- `+login`, `+run_app_build`, `+quit` are the ones Valve documents for SteamPipe.
- `@ShutdownOnFailedCommand` and `@NoPromptForPassword` come from the SteamCMD script examples on the Valve Developer Community wiki; they stop steamcmd from waiting on a prompt that CI cannot answer. Verify their behaviour on your steamcmd version with a deliberately bad password before relying on them.
- Pass an absolute path to `+run_app_build`; relative paths inside the VDF resolve from the VDF's own folder.
- The scripts fail on a nonzero steamcmd exit code. Valve does not document steamcmd's exit codes for build failures, so also check the `*.log` files in `BuildOutput` and the SteamPipe Builds page after the first CI upload, and test a deliberately broken depot path to see whether your steamcmd version exits nonzero.

## GitHub Actions example

Build with the Unity job of your choice (see `unity-cli` for `unity build` and licensing on runners), upload the content folder as an artifact, then:

```yaml
upload-steam:
  needs: build-windows
  runs-on: ubuntu-latest
  if: github.ref_type == 'tag'
  environment: steam
  steps:
    - uses: actions/checkout@v4
    - uses: actions/download-artifact@v4
      with:
        name: windows-content
        path: steam/content/windows
    - name: Install steamcmd
      run: |
        mkdir -p "$HOME/steamcmd"
        curl -sSL https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz | tar -xz -C "$HOME/steamcmd"
        "$HOME/steamcmd/steamcmd.sh" +quit
    - name: Set build description
      run: sed -i "s|\"Desc\" \".*\"|\"Desc\" \"${GITHUB_REF_NAME} ${GITHUB_SHA::7}\"|" steam/scripts/app_build_1000.vdf
    - name: Upload
      env:
        STEAM_USERNAME: ${{ secrets.STEAM_USERNAME }}
        STEAM_CONFIG_VDF_BASE64: ${{ secrets.STEAM_CONFIG_VDF }}
      run: STEAMCMD="$HOME/steamcmd/steamcmd.sh" bash tools/steam/upload.sh steam/scripts/app_build_1000.vdf steam/content/windows
```

`tools/steam/upload.sh` is wherever you copied this skill's `scripts/upload.sh` in your repo. Pin action versions to what your organisation allows. The example uploads only the Windows depot, so its app script must list only depot 1001 or the job must also download the Linux content.

Using `game-ci/steam-deploy@v3` instead is fine for simple projects: it generates the app and depot VDFs itself (inputs `appId`, `buildDescription`, `rootPath`, `depot1Path`..`depot9Path`, `releaseBranch`, `configVdf` or `totp`). Its README notes that the `default` branch does not work as `releaseBranch`, matching Valve's `SetLive` rule.

## Local or self-hosted Windows runner

```powershell
$env:STEAM_USERNAME = 'studio_builder'
.\tools\steam\upload.ps1 -AppBuildVdf steam\scripts\app_build_1000.vdf -ContentDir steam\content\windows
```

A persistent self-hosted runner keeps `config.vdf` next to `steamcmd.exe`, so after one interactive login no secret beyond the username is needed.
