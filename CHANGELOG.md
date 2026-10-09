## v1.4.3-pre.1 (prerelease)

Changes since v1.4.2:

- Bump Avalonia and Avalonia.Native ([@dependabot[bot]](https://github.com/dependabot[bot]))
- Bump the ktsu group with 5 updates ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.4.2 (patch)

Changes since v1.4.1:

- Fail a value option followed by a switch instead of taking the switch as its value [patch] ([@Claude](https://github.com/Claude))

## v1.4.1 (patch)

Changes since v1.4.0:

- Reject a preference debounce a timer cannot wait for when it is configured [patch] ([@Claude](https://github.com/Claude))

## v1.4.1-pre.2 (prerelease)

Changes since v1.4.1-pre.1:

- Bump the ktsu group with 5 updates ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.4.1-pre.1 (prerelease)

Changes since v1.4.0:

- Bump MSTest.Sdk from 4.4.1 to 4.5.1 ([@dependabot[bot]](https://github.com/dependabot[bot]))
- Bump the ktsu group with 5 updates ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.4.0 (minor)

Changes since v1.3.0:

- Keep --status read-only by not restoring preferences for it ([@Claude](https://github.com/Claude))
- Refuse an option alias that a standard switch or another option already owns ([@Claude](https://github.com/Claude))
- Run a flag's handler once however many times it was given ([@Claude](https://github.com/Claude))

## v1.3.1-pre.1 (prerelease)

Changes since v1.3.0:

- Bump Polyfill from 11.4.1 to 11.4.3 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.3.0 (minor)

Changes since v1.2.0:

- Cover the no-start-action, no-host and debug-output recovery paths ([@Claude](https://github.com/Claude))
- Keep the restore error and stop the tool once when tray start-up fails ([@Claude](https://github.com/Claude))

## v1.2.1-pre.1 (prerelease)

Changes since v1.2.0:

- Bump Polyfill from 11.4.0 to 11.4.1 ([@dependabot[bot]](https://github.com/dependabot[bot]))
- Bump the ktsu group with 5 updates ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.2.0 (minor)

Changes since v1.1.0:

- Reject --for durations longer than a wait can take ([@Claude](https://github.com/Claude))
- test: cover the disabled-item backstop in Activate [patch] ([@Claude](https://github.com/Claude))
- fix: do not activate a disabled toggle from a tray icon click [patch] ([@Claude](https://github.com/Claude))
- Never absorb a fatal exception from the tool's own code ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Do not let a refused preference restore kill the tray ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.1.6 (patch)

Changes since v1.1.5:

- Bump the ktsu group with 5 updates ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.1.5 (patch)

Changes since v1.1.4:

- Bump the ktsu group with 5 updates ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.1.4 (patch)

Changes since v1.1.3:

- test: cover the disabled-item backstop in Activate [patch] ([@Claude](https://github.com/Claude))
- fix: do not activate a disabled toggle from a tray icon click [patch] ([@Claude](https://github.com/Claude))

## v1.1.3 (patch)

Changes since v1.1.2:

- Bump the ktsu group with 14 updates ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.1.2 (patch)

Changes since v1.1.1:

- Never absorb a fatal exception from the tool's own code ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Do not let a refused preference restore kill the tray ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.1.1 (patch)

Changes since v1.1.0:

- Bump Avalonia, Avalonia.Desktop and Avalonia.Native ([@dependabot[bot]](https://github.com/dependabot[bot]))
- Bump the ktsu group with 9 updates ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.1.0 (major)

- Give the idempotent-dispose test something to assert ([@matt-edmondson](https://github.com/matt-edmondson))
- Clear the Sonar findings and cover what was untested ([@matt-edmondson](https://github.com/matt-edmondson))
- Add a tray menu click probe, so the menu is verified and not just the icon ([@matt-edmondson](https://github.com/matt-edmondson))
- Fix the file header text, and ship the packaging props at all ([@matt-edmondson](https://github.com/matt-edmondson))
- [minor] Add ktsu.TrayApp, the skeleton behind cross-platform tray tools ([@matt-edmondson](https://github.com/matt-edmondson))
- Initial commit ([@matt-edmondson](https://github.com/matt-edmondson))

