# Steam release checklist

Sources: https://partner.steamgames.com/doc/sdk/uploading, https://partner.steamgames.com/doc/store/application/branches, https://partner.steamgames.com/doc/sdk/api. Items marked (check) are practice, not quoted Valve requirements; confirm against the current Steamworks docs and your App Admin pages.

## One-time App Admin setup

- [ ] Depots created under SteamPipe > Depots, renamed to something recognisable (Valve's setup step).
- [ ] Depot OS left as All OSes unless the depot is OS-specific; a Windows-only and a Linux-only depot each set their OS. Language left as All languages unless the depot is language-specific.
- [ ] At least one launch option on the Installation tab: executable path relative to the depot root (for example `MyGame.exe`), OS set to match. Add a Linux launch option (`MyGame.x86_64`) only if you ship a native Linux depot.
- [ ] Changes published from the Publish page after defining depots (Valve's setup step). Do the same after editing launch options and other App Admin settings. (check)
- [ ] Builder account has only "Edit App Metadata" and "Publish App Changes To Steam".
- [ ] Beta branches created with "Create new app branch" on the Builds page; names without spaces (players see them). Password-protect internal branches.

## Every build

- [ ] Release build: Development Build off, script debugging off. `SteamBuildScript` fails a release build that reports `BuildOptions.Development`.
- [ ] Version shown in game (`Application.version`) matches `Desc` in the app build script.
- [ ] No `steam_appid.txt` in the content folder. Valve: "Make sure to remove the steam_appid.txt file when uploading the game to your Steam depot!" With the file present, `SteamAPI_RestartAppIfNecessary` always returns false, so a copy shipped to players hides launch-outside-Steam problems.
- [ ] No `*.pdb`, `*_BurstDebugInformation_DoNotShip`, `*_BackUpThisFolder_ButDontShipItWithYourGame` in the depot. Archive the IL2CPP backup folder (moved to `<content>_symbols` by the build script) per release; you need it to symbolicate crash dumps from that exact build. (check)
- [ ] First upload of a new depot layout done with `"Preview" "1"`; manifest in `BuildOutput` reviewed.
- [ ] Upload with `SetLive` set to a beta branch, never `default` (Valve: the default branch cannot be set live automatically).

## Test from the Steam client

- [ ] On a clean machine or second account, opt into the beta: Library > right-click game > Properties > Game Versions & Betas.
- [ ] Install and launch through Steam, not from the build folder. Confirm the overlay opens (Shift+Tab), achievements and cloud saves work, and the game does not restart itself through Steam in a loop. (check)
- [ ] Launch the exe directly from the install folder once: with `RestartAppIfNecessary` wired, it should relaunch through Steam. (check)
- [ ] Uninstall and reinstall once to confirm nothing depends on files outside the depot.
- [ ] Steam Deck, if targeted: test the same beta build on a Deck (see `steam-deck-controller`).

## Going live

- [ ] On SteamPipe > Builds, select the tested build, choose `default` in "Set build live for branch...", preview and confirm. This is a manual step in App Admin by design.
- [ ] Store page, release date and pricing are separate review processes from builds; follow the Steamworks release process docs for your app. (check)
- [ ] Keep the previous default build ID noted so you can roll back by setting it live again from the Builds page. (check)
