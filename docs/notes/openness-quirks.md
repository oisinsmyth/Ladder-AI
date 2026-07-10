# Openness quirks log

Working notes on TIA Openness friction (risk R-06). Record here as encountered.

## First-connect approval dialog
Per Portal version/binary, the first Openness connect triggers a manual approval dialog inside TIA Portal. If a connect hangs, check Portal for the dialog.
TODO: paste exact dialog text/screenshot on first connect (S0 exit item).

## Known constraints
- User must be in the "Siemens TIA Openness" Windows group (log off/on to take effect).
- One Portal instance/session — no parallel Openness sessions.
- Project open is slow; don't kill and retry.
