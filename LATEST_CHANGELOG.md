## v1.5.0 (minor)

Changes since v1.4.0:

- Take SIGHUP in tray mode so closing the terminal still runs OnStop (Claude)
- Move the tray's signal handling into ShutdownSignals so it is unit tested (Claude)
- [patch] Shut a tray run down through OnStop on SIGTERM and Ctrl+C (Claude)
- Reject a preference debounce a timer cannot wait for when it is configured [patch] (Claude)
- Fail a value option followed by a switch instead of taking the switch as its value [patch] (Claude)

