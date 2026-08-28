# URL Router

Sends every link you click to the *right browser profile*, automatically.

Windows only lets you have one default browser, and one profile inside it. If you are signed in
to two tenants — two Atlassian sites, two Microsoft 365 accounts, a work and a client Google
Workspace — every link from Outlook or Teams lands in whichever profile happens to be default,
and you spend your day copying URLs between windows.

URL Router registers itself with Windows as a browser. Windows hands it every clicked http/https
link, it matches the URL against an ordered rule list, and re-launches the *real* browser with
the right `--profile-directory`.

```
tickets.example.com      →  Chrome, "Work" profile
*.atlassian.net          →  Edge, "Client" profile
everything else          →  ask me
```

Unmatched URLs raise a picker rather than being guessed at, and the picker can write a rule for
the host it just asked about — so the rule set grows out of normal use rather than up front.

## Install

1. Download `UrlRouter.exe` from the [latest release](../../releases/latest).
2. Put it somewhere permanent — `%LOCALAPPDATA%\UrlRouter` is what the publish profile uses. The
   path is baked into the Windows registration, so moving it later means registering again.
3. Run `UrlRouter --register`. This registers the app, starts the background agent, sets it to
   start at sign-in, and opens Windows Settings.
4. In **Settings → Apps → Default apps → URL Router**, set it for both **HTTP** and **HTTPS**.

Windows does not allow an application to make itself the default browser — the `UserChoice`
registry value is hash-protected — so step 4 has to be done by hand. The Setup tab reports which
state you are actually in rather than assuming the registration succeeded.

**Restart Outlook and Teams afterwards.** Both cache the link handler at startup.

## Using it

Run `UrlRouter` with no arguments, or double-click the tray icon, for the settings window.

| Command | What it does |
| --- | --- |
| `UrlRouter` | Open the settings window |
| `UrlRouter --register` | Register as a selectable browser, then open Windows settings |
| `UrlRouter --unregister` | Remove the registration |
| `UrlRouter --test <url>` | Show which browser a URL *would* open in, without opening it |
| `UrlRouter --agent` | Run the resident agent (started automatically at sign-in) |
| `UrlRouter --version` | Print the installed version |

`--test` is the first thing to reach for when a link goes somewhere unexpected: it prints the
normalised URL, the rule that matched, and the exact command line that would be run.

### Rules

- `*` is the only wildcard. `?` is literal, because it is meaningful in a URL.
- Patterns are anchored, which is what stops `*.example.com` matching
  `example.com.attacker.test`.
- A leading `*.` in a host pattern also matches the bare domain, so `*.example.com` covers
  `example.com` as well as `sso.example.com`.
- Path patterns only see the query string when the pattern itself contains `?`.
- Rules are evaluated in list order and the first enabled match wins — order is meaningful.

Outlook and Teams rewrite links to `https://{tenant}.safelinks.protection.outlook.com/?url=...`.
URL Router unwraps those before matching, which is not cosmetic: without it every emailed link
presents the same host and no rule could tell one from another.

## Updates

The background agent checks for new releases about once a day and offers them through the tray
icon. **Nothing is downloaded or installed without you saying yes.** You can turn the check off,
point it at a different feed, or check on demand, on the Updates tab.

Installing replaces the running executable in place: the new build is downloaded beside the old
one, its SHA-256 is checked against the published manifest, the running image is renamed aside,
and the agent restarts on the new version. The registered path never changes, so the Windows
registration survives the update untouched.

The feed is `releases/latest/download/version.json` on this repository, which resolves to the
newest published, non-prerelease release.

### Security note

Auto-update is a channel for running new code on your machine. The published SHA-256 proves the
download arrived intact; it does not prove authenticity, since the hash travels in the same
manifest from the same host. What you are trusting is this repository's releases. The
executable is **not code-signed**, so SmartScreen will warn on first run.

If that is not a trust you want to extend, turn the check off on the Updates tab — nothing else
in the app depends on it.

## Why there is a resident background agent

Some managed Windows environments enable the Microsoft Defender Attack Surface Reduction rule
**"Block Office communication application from creating child processes"**
(`26190899-1602-49e8-8b27-eb1d0a1ce869`). Where that is on, Windows refuses to let Outlook —
classic *and* new — start `UrlRouter.exe` at all. The click dies with *"Windows cannot access the
specified device, path, or file"* and nothing reaches the application.

The way around it is the shell's pre-Chromium `ddeexec` path: if the ProgId carries a
`shell\open\ddeexec` key and a DDE server with the matching service name is **already running**,
`ShellExecute` delivers the URL over a DDE conversation instead of creating a process. Nothing is
spawned, so the ASR rule has nothing to block, and the browser is launched by the agent — which
is not an Office process and is unrestricted.

So the agent is not a convenience. If it is not running, links clicked in Outlook stop working on
any machine with that rule enabled. `UrlRouter --register` adds a `Run` entry so it starts at
sign-in, and the Setup tab reports whether it is alive.

## Building

Requires the .NET 10 SDK. There are no NuGet dependencies, deliberately — this process is
spawned on every link click, so the hot path must not pay to load extra assemblies.

```bash
dotnet build src/UrlRouter.csproj
dotnet publish src/UrlRouter.csproj -c Release -r win-x64 --self-contained false \
  -p:PublishSingleFile=true -p:PublishReadyToRun=true
```

`version.txt` at the repo root is the only place the version is written; the build reads it and
stamps the assembly, and CI fails if the two disagree.

## Releasing

Push a tag and GitHub Actions does the rest:

```bash
git tag v2026.8.28.1537 && git push origin v2026.8.28.1537
```

The workflow builds, verifies the stamp, computes the hash, writes `version.json`, and publishes
the release. `Tools/publish-release.ps1` is the local equivalent — use `-NoPublish` to see exactly
what would be shipped without touching GitHub, or `-Draft` for a real dry run against GitHub that
no installed copy will be offered.

No version component may have a leading zero. `yyyy.M.d.HHmm` before 10:00 gives `0834`, which
`System.Version` normalises to `834` in the assembly while a JSON string keeps it verbatim — one
release, two versions, and an update that installs and then offers itself again forever. Both the
build and both release paths reject it.

## Licence

MIT — see [LICENSE](LICENSE).
