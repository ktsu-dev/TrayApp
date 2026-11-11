## v1.5.1 (patch)

Changes since v1.5.0:

- [patch] Read the status line of a console run through StatusReport.Read (#83) (matt-edmondson)
- [patch] Claim the shutdown on Quit and the --for expiry, not only on a signal (#84) (matt-edmondson)
- Indent a multi-line --status value to the value column [patch] (#86) (matt-edmondson)
- Read a long option's value attached with =, as in --for=5s [patch] (#90) (matt-edmondson)
- Keep the tray item's Id the same in every status on Linux [patch] (#96) (matt-edmondson)
- Report no save failure when quitting during a debounced write [patch] (#97) (matt-edmondson)
- Undo remembered tray preferences when falling back to the console [patch] (#98) (matt-edmondson)
- [patch] Keep a gap after a long label in --status and align every value (#76) (matt-edmondson)
- Report an out-of-range number for an option as a usage error [patch] (#70) (matt-edmondson)
- Leave a later toggle alone when the tray icon's first toggle is disabled [patch] (#68) (matt-edmondson)
- Let a repeated Ctrl+C, SIGTERM or SIGHUP end a run whose shutdown hangs [patch] (#66) (matt-edmondson)
- Skip restoring a toggle already in its remembered state [patch] (#65) (matt-edmondson)
- Report a throwing status getter in its own row instead of crashing --status [patch] (#64) (matt-edmondson)
- Refuse a persistAs key another toggle already remembers [patch] (#60) (matt-edmondson)
- Reject a sub-tick --for and report an overflowing one as too long [patch] (#59) (matt-edmondson)
- Leave a gap after the longest option in --help [patch] (#58) (matt-edmondson)

