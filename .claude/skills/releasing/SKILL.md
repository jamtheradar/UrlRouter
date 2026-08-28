---
name: releasing
description: Cut and publish a UrlRouter release - version.txt stamping, the version.json update feed, the in-app update check, and the guards that keep the assembly, the feed and the tag agreeing. Use when publishing or debugging a release, or changing release.yml, publish-release.ps1, version.txt or UpdateService.
---

# Releasing

A release is one file — `UrlRouter.exe`, published single-file — plus the `version.json` the
in-app update check polls and a `.sha256` sidecar. There is no installer.

**Push a tag and `.github/workflows/release.yml` does everything.** `Tools/publish-release.ps1` is
the local equivalent for when Actions is unavailable, and `-NoPublish` is the dry run that writes
`artifacts\` without touching GitHub. The two must stay in step: same publish flags, same three
assets, same guards. Neither uses `FolderProfile.pubxml`, which exists to install to the fixed
path the Windows registration points at, not to stage a release.

**This project can build on a hosted runner, unlike the sibling SSMS extension.** It has no
`PackageReference` and no dependency on locally-installed software, so CI builds exactly what a
release ships — `ci.yml` runs the full publish path, not just `dotnet build`, so the single-file
and ReadyToRun settings are covered on every PR. Don't "simplify" CI down to a plain build.

**`version.txt` at the repo root is the only place the version is written**, because three
consumers have to agree: the assembly's `FileVersion` (what `UpdateService.CurrentVersion` reads
off the *running exe*, via `Environment.ProcessPath` — `Assembly.Location` is empty in a
single-file publish), `version.json`, and the tag. A build reporting `1.0.0.0` against a feed
advertising a date version reads as out of date forever: notify, install, notify again, with no
way out but turning the check off. `src/UrlRouter.csproj` reads the file during evaluation under
`Condition="'$(Version)' == ''"`, so `-p:Version=` from a release still wins.

**No leading zero in any component.** `yyyy.M.d.HHmm` before 10:00 gives `0834`, which
`System.Version` normalises to `834` in the assembly while a JSON string keeps it verbatim — one
release, two versions. Three places reject it independently (`ValidateVersion` in the csproj, the
script's pre-flight, the workflow's resolve step) because each can be the only guard on some
route to a release. And both publish paths *verify the built exe reports the version being
published* before uploading anything. That check is the point of the arrangement, not a
formality.

**The feed is `releases/latest/download/version.json`, deliberately not the GitHub API.** That
path redirects to the newest published non-prerelease release's asset of that name: no API call,
no 60-per-hour-per-IP limit (an office behind one NAT shares it), and a download host less often
proxy-blocked than `api.github.com`. Drafts and prereleases are invisible to it, which is what
makes `-Draft` (and the workflow's draft input) a safe dry run against real GitHub. The
manifest's `url` names *that release's* asset rather than the alias, so an installed copy is
offered exactly the executable the manifest hashed, even if a newer release lands mid-check.

**`version.json` must not carry a BOM.** `System.Text.Json` reads a leading U+FEFF as an
unexpected character and throws, and the only symptom is the update check silently never finding
anything again. Both paths write UTF-8 without one (`Set-Content -Encoding utf8` on PowerShell
5.1 writes one — hence `[System.IO.File]::WriteAllText` with an explicit encoding everywhere,
including for `version.txt`), and `UpdateService.CheckAsync` strips it defensively.

**`Get-FileHash` is deliberately not used in the local script.** It lives in a module that is not
always loadable in a constrained or `-NoProfile` host, and it failed exactly that way on first
run; the script uses `System.Security.Cryptography.SHA256` directly. The workflow keeps
`Get-FileHash`, which is fine on a GitHub runner.

**Nothing installs without the user confirming.** The agent's background check raises a tray
balloon; `UpdateFlow` is the single confirm-download-install conversation shared by the balloon
and the Updates tab. If you are tempted to make it silent, that was considered and rejected:
the app replaces its own executable under a link handler people depend on.

Release notes default to `Build <version>` from the script and to `--generate-notes` from the
workflow. Whatever ends up in `notes` is shown verbatim in the confirmation prompt and the
Updates tab, so keep it short — pass `-Notes` or `-NotesFile` for anything that matters.

**`-MinRequiredVersion` removes the user's "skip this version" option** for anyone below it. For
a fix that must not be skippable, nothing else.

## Repository rules

`main` and the `v*` release tags are protected by GitHub rulesets, matching the sibling
SQLExtended repo: no deletion and no force-push on the default branch, and tags additionally
cannot be *updated* — a published release's tag is immutable, which is what stops the feed's
`releases/download/<tag>/UrlRouter.exe` ever pointing at different bytes than the ones
`version.json` hashed.

`branch-rule.json` and `tag-rule.json` at the repo root are the payloads, and they are
**gitignored on purpose** — local scaffolding, not product. Applied with:

```bash
gh api repos/JamTheRadar/UrlRouter/rulesets --input branch-rule.json
gh api repos/JamTheRadar/UrlRouter/rulesets --input tag-rule.json
gh api repos/JamTheRadar/UrlRouter/rulesets   # verify
```

If the files are missing, they are four lines each; copy them from the sibling repo rather than
guessing at the schema.

## Publishing

```bash
# Dry run: build, verify, write artifacts\version.json, touch nothing
./Tools/publish-release.ps1 -NoPublish

# Real release
git tag v2026.8.28.1537 && git push origin v2026.8.28.1537
```

Remember to commit the `version.txt` bump: both publish paths write it, and a tag pushed without
it leaves the committed file behind the release.
