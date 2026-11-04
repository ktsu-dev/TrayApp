## v1.5.1 (patch)

Changes since v1.5.0:

- [patch] Keep a gap after a long label in --status and align every value (#76) (matt-edmondson)
- Report an out-of-range number for an option as a usage error [patch] (#70) (matt-edmondson)
- Leave a later toggle alone when the tray icon's first toggle is disabled [patch] (#68) (matt-edmondson)
- Let a repeated Ctrl+C, SIGTERM or SIGHUP end a run whose shutdown hangs [patch] (#66) (matt-edmondson)
- Skip restoring a toggle already in its remembered state [patch] (#65) (matt-edmondson)
- Report a throwing status getter in its own row instead of crashing --status [patch] (#64) (matt-edmondson)
- Refuse a persistAs key another toggle already remembers [patch] (#60) (matt-edmondson)
- Reject a sub-tick --for and report an overflowing one as too long [patch] (#59) (matt-edmondson)
- Leave a gap after the longest option in --help [patch] (#58) (matt-edmondson)

