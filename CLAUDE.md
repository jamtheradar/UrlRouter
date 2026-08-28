# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

UrlRouter registers itself with Windows as a browser. Windows hands it every clicked
http/https link, it matches the URL against an ordered rule list, and re-launches the *real*
browser with the right `--profile-directory`. The problem it solves: links clicked in Outlook
and Teams all land in the single default browser, so two tenants' ticket systems (two Atlassian
sites, say) cannot both open in the profile that is already signed in to them.

**This repository is public.** Keep it that way in what you write here and in code comments: the
mechanisms are the interesting part and belong in the open, but employer names, tenant hostnames
and a named organisation's security configuration do not. Use placeholder examples
(`example.com`, `tickets.example.com`).

Unmatched URLs raise a picker rather than being guessed at, and the picker can write a rule
for the host it just asked about — so the rule set grows out of normal use.

The solution is `UrlRouter.slnx`; the project lives in `src/`.

## Build Commands

```bash
dotnet build src/UrlRouter.csproj
dotnet build src/UrlRouter.csproj -c Release

# Diagnose routing without opening anything
UrlRouter --test "https://tickets.example.com/browse/ABC-123"

# Install to the fixed path the registration points at
dotnet publish src/UrlRouter.csproj -p:PublishProfile=FolderProfile

# Stage a release locally without touching GitHub
./Tools/publish-release.ps1 -NoPublish
```

## Architecture

- **Target**: .NET 10, Windows Forms (`net10.0-windows7.0`)
- **Output**: `UrlRouter.exe` (WinExe — a console subsystem app would flash a window on every click)
- **No PackageReferences, deliberately.** This process is spawned on every link click, so the
  hot path must not pay to load extra assemblies. `System.Text.Json` and
  `Microsoft.Win32.Registry` are in-box for the `-windows` TFM. Do not add dependencies
  without weighing cold-start cost.
- **Config**: `%APPDATA%\UrlRouter\config.json` (seeded from detected browsers on first run)
- **Log**: `%LOCALAPPDATA%\UrlRouter\router.log` (size-capped, rolls to `.1`)

`Program.Main` dispatches on arguments and only calls `ApplicationConfiguration.Initialize()`
on paths that actually show a window, so a matched route never pays WinForms init cost.

## Why there is a resident agent (read this before "simplifying" it)

Managed Windows environments commonly enable the Defender ASR rule
**"Block Office communication application from creating child processes"**
(`26190899-1602-49e8-8b27-eb1d0a1ce869`, action `1` = Block, set via
`HKLM\SOFTWARE\Policies\...\Windows Defender Exploit Guard\ASR\Rules`). Where it is on,
Windows refuses to let Outlook — classic *and* new — start `UrlRouter.exe` at all. The click dies
with *"Windows cannot access the specified device, path, or file"* and **nothing reaches our
code**. The policy is centrally managed and needs local admin to except, so the deployment this
was built for could not simply add an exclusion — assume that is the case for anyone else too.

The way around it is the shell's pre-Chromium `ddeexec` path: if the ProgId carries a
`shell\open\ddeexec` key and a DDE server with the matching service name is **already
running**, `ShellExecute` delivers the URL over a DDE conversation and never creates a
process. Nothing is spawned, so the ASR rule has nothing to block. The browser is then
launched by the agent, which is not an Office process and is unrestricted.

Consequences that follow from this, none of them optional:

- `--agent` must be running or Outlook links fail. Registration adds an
  `HKCU\...\CurrentVersion\Run` entry, and `RegistrationStatus.AgentRunning` reports it.
- The `shell\open\command` line is now only a **fallback** for callers permitted to launch us.
- DDE callbacks must return promptly. `AgentContext` posts the routing work via
  `BeginInvoke` rather than doing it inline, because routing can raise the modal picker.
- The agent is single-instance via a named mutex (`RegistrationService.AgentMutexName`);
  two DDE servers cannot own one service name.
- Verified by observing that the UrlRouter process count does **not** change when a URL is
  shell-executed, and the log records the route with an `[dde]` prefix. That is the test to
  repeat if this ever regresses.

## Things that will bite you

- **`--single-argument` is not optional.** Windows invokes us as
  `UrlRouter.exe --single-argument %1` with `%1` *unquoted* — copied verbatim from how Edge
  and Brave register themselves. `Program.TryGetSingleArgument` reads
  `Environment.CommandLine` rather than the parsed `args[]`, because a URL containing `&` or
  a space arrives split across several elements otherwise.
- **Never use `UseShellExecute = true` on a routed URL.** The shell resolves http(s) to this
  very app, so it would loop forever. `BrowserLauncher` starts the browser exe by full path
  and refuses any target resolving to UrlRouter itself.
- **Safe Links unwrapping is load-bearing, not cosmetic.** Outlook and Teams rewrite links to
  `https://{tenant}.safelinks.protection.outlook.com/?url=...`. Without `UrlNormalizer`,
  every link clicked in either app presents the same host and no rule could distinguish them.
- **Windows will not let an app set itself as the default browser.** The `UserChoice` value is
  hash-protected. `RegistrationService` can only make the app *choosable*; the user picks it in
  Settings. `GetStatus()` reports which state you are in — never claim success without checking it.
- **Packaged apps need explicit ACEs, or links silently fail.** The new Outlook and Teams are
  MSIX packages running in an AppContainer, and such a process can only launch a file whose ACL
  grants `ALL APPLICATION PACKAGES` (`S-1-15-2-1`) or `ALL RESTRICTED APPLICATION PACKAGES`
  (`S-1-15-2-2`). Everything under `C:\Program Files` carries those ACEs by default — which is
  why Edge and Brave work as handlers — but a folder created under `%LOCALAPPDATA%` does not.
  Without them Windows shows *"Windows cannot access the specified device, path, or file"* with
  the URL as the dialog title, and **nothing appears in router.log because our process never
  starts** — so an empty log is the diagnostic signature of this problem, not of a routing bug.
  `RegistrationService.GrantAppContainerAccess` applies the ACEs to the install directory with
  inheritance (so a re-publish stays working) and `HasAppContainerAccess` re-checks them for
  the Setup tab. Use the SIDs, never the account names, which are localised.
- **The published path is fixed on purpose.** Registration bakes in an absolute exe path, so
  `FolderProfile.pubxml` targets `%LOCALAPPDATA%\DataByte\UrlRouter`. Do not switch this
  project to the ClickOnce profile the sibling tools use — version-stamped directories would
  break the handler on every publish. `RegistrationStatus.PathIsStale` detects this happening.
- **Outlook and Teams cache the handler at startup.** Changing the default requires restarting
  them, not just the browser.

## Versioning and updates

`version.txt` at the repo root is **the only place the version is written**, and the reason it
exists is that three consumers have to agree: the assembly's `FileVersion` (what
`UpdateService.CurrentVersion` reads off the *running exe*), `version.json` in the published
release, and the tag it was cut from. A build reporting `1.0.0.0` while the feed advertises a
date version reads as out of date forever — notify, install, notify again, with no way out but
turning the check off. `src/UrlRouter.csproj` reads the file during evaluation, guarded by
`Condition="'$(Version)' == ''"` so a release passing `-p:Version=` still wins.

**No leading zero in any component.** `yyyy.M.d.HHmm` before 10:00 gives `0834`, which
`System.Version` normalises to `834` in the assembly while a JSON string keeps it verbatim: one
release, two versions. The `ValidateVersion` target, `Tools/publish-release.ps1` and
`release.yml` each reject it independently, because each of them can be the only thing in the
path on some route to a release.

**The feed is `releases/latest/download/version.json`, deliberately not the GitHub API.** That
path redirects to the newest published non-prerelease release's asset of that name, so there is
no API call and no 60-per-hour-per-IP limit (an office behind one NAT shares it), and the
download host is less often proxy-blocked than `api.github.com`. Drafts and prereleases are
invisible to it, which is what makes `-Draft` a safe dry run. The manifest's `url` names *that
release's* asset rather than the `latest/download` alias, so a copy is always offered exactly the
executable the manifest hashed.

**`version.json` must not carry a BOM.** `System.Text.Json` reads a leading U+FEFF as an
unexpected character and throws, and the only symptom is the update check silently never finding
anything again. Both publish paths write UTF-8 without one, and `UpdateService.CheckAsync` strips
it defensively anyway.

**The update replaces the running executable in place, and that only works because of choices
made elsewhere.** `PublishSingleFile` means there is one file to swap; the fixed publish path
means the Windows registration does not have to be rewritten. Windows will not let a running
image be overwritten but will let it be *renamed*, so `UpdateService.ApplyStaged` is two renames
(`UrlRouter.exe` → `.old.exe`, `.new.exe` → `UrlRouter.exe`) and never leaves the registered path
pointing at nothing. The download is *created in* the install directory rather than moved in from
`%TEMP%`, because a moved file keeps the ACL it was created with and would arrive without the
`ALL APPLICATION PACKAGES` ACEs the new Outlook needs.

**`ApplyStaged` has three outcomes and the distinction is load-bearing.** Once the two renames
succeed the new build *is* the registered handler, so a later failure (the successor not
starting) is a restart problem, not an install problem — rolling the files back would throw away
a good build to fix a symptom. Only `Failed` means the old version is genuinely still in place,
and only that outcome may be described to the user that way. `InstalledPendingRestart` must not
make the agent exit: it is the only agent there is, and exiting would leave the machine with
dead Outlook links until the next sign-in. It also re-checks the staged file's SHA-256
immediately before the swap, because the staging path is a fixed name in a shared directory and
that is the last moment those bytes are still just a file.

**One install at a time, across processes.** The agent and a standalone `--config` window can
both reach the install path independently and share one staging filename, so `UpdateFlow` holds
`UpdateService.TryAcquireInstallLock` for the whole download-and-swap. It is a lock file
(`FileShare.None`, `DeleteOnClose`) rather than a named mutex because a `Mutex` is thread-affine
and this lock is held across awaits.

**Nothing restarts the agent when it exits.** `ExitAgent` just calls `ExitThread`; the only ways
back are the `Run` entry at sign-in and the Setup tab's "Start agent" button. Any message telling
the user to exit the tray icon has to say that, which is what the `ConfigForm` install path now
does — the successor started by `ApplyStaged` only waits 20 seconds for the mutex.

**The handover is the agent mutex, not a pid.** The successor is started as `--agent --wait`,
which retries `RegistrationService.AgentMutexName` for 20 seconds while the outgoing agent exits.
Note `AcquireAgentMutex` reads `createdNew`, not ownership: the kernel object outlives the
owner's exit only until the last handle closes, which is exactly when the successor may start.
`AgentContext.IsRunningInThisProcess` is what lets `ConfigForm` tell whether installing means
ending the process (opened from the tray) or just closing a window (opened via `--config`).

**Nothing installs without confirmation.** `UpdateFlow` is the single confirm-download-install
conversation, shared by the tray balloon and the Updates tab so the two cannot drift. The
background check only ever raises a balloon — a modal dialog thrown in front of someone who just
clicked a link would be worse than the problem it reports.

The update check is never touched by the routing hot path. Only `AgentContext` and `ConfigForm`
call into `UpdateService`, so a link click still does not pay to load `HttpClient`.

## Matching semantics

- `*` is the only wildcard; `?` is literal because it is meaningful in a URL.
- Patterns are anchored, which is what stops `*.example.com` matching `example.com.attacker.test`.
- A leading `*.` in a host pattern also matches the bare domain, so `*.example.com` covers
  `example.com` as well as `sso.example.com`.
- Path patterns only see the query string when the pattern itself contains `?`.
- Rules are evaluated in list order and the first enabled match wins — order is meaningful.

## UI Notes

- `*.Designer.cs` files are hand-written but treated as generated; grid columns are built in
  code (`ConfigForm.BuildRulesGrid` / `BuildTargetsGrid`) because the target combo column
  needs a live data source.
- `BindingList<T>` wraps the same `List<T>` instances `RouterConfig` holds, so grid edits land
  directly on the object that gets serialised. After mutating `_config.Targets` outside the
  grid (e.g. Re-detect), call `ResetBindings()`.
- Both grids set `DataError` to `ThrowException = false`: a rule pointing at a deleted target
  would otherwise raise a modal error on every paint.
