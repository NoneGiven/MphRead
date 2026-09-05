# CLAUDE Index

CLAUDE.md is the always-loaded top-level file: identity, paths, environment,
commands, and short pointers into the topic files below. The topic files hold
the depth — read the one you need for the area you're touching rather than
loading everything.

- KNOWN-GAPS.md — claims not yet verified, so you don't re-prove or re-claim them
- android/ANDROID-PORT.md — the GL ES renderer, the touch controls, building the APK
- launcher/LAUNCHER-OVERVIEW.md — entries, platforms (incl. macOS/Android), threading
- launcher/LAUNCHER-DESIGN.md — UI components, logo/assets, pitfalls
- launcher/LAUNCHER-SETTINGS.md — settings window layout and toggles
- launcher/LAUNCHER-FIRSTRUN.md — extraction flow and progress bar
- DEBUG-LOGS.md — the launcher's corner switch: what it writes, where, and why it exists
- GAMEPAD.md — controllers on the desktop and Android: the layout, the feel, and how to test one without owning one
- multiplayer/NETWORK-BROWSER.md — server discovery, directory, hosting
- multiplayer/NETWORK-CHAT.md — the in-game chat line: the packet, the relay's rules, the input traps
- multiplayer/NETWORK-DEMOS.md — recording and replaying a match: format, clocking, the gaps
- multiplayer/NETWORK-MATCHEND.md — match end, rotation, the double-counted-kill bug
- multiplayer/NETWORK-DIAGNOSTICS.md — the full damage-bug postmortem, traps, diagnostics
- render/CEL-SHADING.md — flat colours in place of textures, and the depth-kink ink pass
- render/FRAME-PACING.md — 60 Hz of simulation under a picture drawn at the display's rate: the split, interpolation, and how both halves are tested without a 144 Hz monitor
- mapgen/MAP-PIPELINE.md — custom maps: the generator, the Quake 3 importer, the format traps
- testing/TEST-HARNESS.md — netcheck/maptest, map sweeps, the world and affliction probes
- testing/TEST-HARD-CASES.md — disconnects, blackouts, latency, loss, capacity, spectators, the Pi's ceiling
- testing/TEST-METRICS.md — reading results, common traps, last verified status
- build-deploy/BUILD-WORKFLOW.md — CI workflows, tagging and the bump, release notes, binaries, asset guard
- build-deploy/DEPLOY-SERVERS.md — deploy script and publish commands

Usage: these are the token-optimised detail store for CLAUDE.md. Keep them
current as the code changes; when a fact changes, fix it here rather than
letting CLAUDE.md's summary and a topic file disagree.
