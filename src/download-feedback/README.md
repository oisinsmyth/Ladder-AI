# download-feedback — the download feedback parser

Turns a TIA Openness `DownloadResult` message tree into an answer to the question a download is
actually asked: **what reached the device?**

Spec: `docs/notes/PC-Client-Modbus-Spec-Draft-final.txt` §9a (the load manifest), §9b (the three
download options, measured) and §9c (this component).

Built from **fifteen recorded probe logs**, with no device — which is the point. Those runs cost a
live rig and a CPU restart each; the evidence already existed, so the parser could be built and
proved before the next download rather than after it.

## What it is, and what it is not

| | |
|---|---|
| `Ladder.Download` | `netstandard2.0`, references **nothing**. |
| `Ladder.Download.Tests` | `net8.0`, xunit, fixtures from the real logs. |

`netstandard2.0` is deliberate: `openness-cli` is pinned to `net48` by `Siemens.Engineering`, and a
`net8.0` library would be unreferenceable from the one tool that most needs it.

**It does not reference `Siemens.Engineering`, and it must not start.** The caller adapts its
`DownloadResult` in through `DownloadResultAdapter` (six small projections). That is what lets the
whole component be exercised against recorded text with no Portal in the room, and it means a change
here can never trigger the TIA Openness `(Path, FileHash)` re-approval cycle that a rebuild of
`openness-cli` does.

**Wiring it into `openness-cli` is a separate, deliberate step and has not been done.** This
solution builds and tests entirely on its own.

## Using it

Live, from a download:

```csharp
var summary = DownloadResultAdapter.Adapt<DownloadResultMessage>(
    result.State.ToString(), result.ErrorCount, result.WarningCount, result.Messages,
    m => m.Message,
    m => m.State.ToString(),
    m => (int)m.ErrorCount,
    m => (int)m.WarningCount,
    m => m.DateTime,
    m => m.Messages);

var feedback = DownloadFeedbackParser.Parse(summary);
Console.Write(feedback.ToReport());
```

When the download **threw or aborted**, pass `DownloadResultSummary.Absent` (or `null`). That is a
distinct input producing a distinct answer — `Undetermined`, never `NothingTransferred`.

Retrospectively, from a log that already exists:

```csharp
var feedback = DownloadFeedbackParser.Parse(ProbeLogReader.Read(File.ReadAllText(path)));
```

`ProbeLogReader` is for fixtures and forensics only. Never route a live download through a log file:
the log is the probe's *rendering* of the result, and whatever the renderer drops is gone before the
reader sees it.

## The verdict

Three values. `Undetermined` is not a courtesy — it is the difference between "the device still
holds what it held" and "nothing whatever is known, go and read it".

```
no result object at all      -> Undetermined
>= 1 item reported loaded    -> Transferred        (with the COUNT)
up-to-date stated, 0 loaded  -> NothingTransferred
otherwise                    -> Undetermined
```

**The verdict is keyed on the load manifest and on nothing else.** Two readings are excluded by
construction, because the probe this replaces committed both at once and fixing only one would have
left the other standing:

1. **Never inferred from the absence of an up-to-date phrase.** Absence of a denial is not evidence
   of an act. The run that prompted this had 99 positive load messages going unread while the
   verdict was derived from a phrase that was not there.
2. **Never read off `state=Success`.** The first run that ever returned `Success` transferred
   nothing. The result state is reported and never consulted — `VerdictTests` asserts each of these
   separately, and both were negative-tested by reintroducing the defect and watching them fail.

The count is reported, not a boolean: "1 object" versus "99 objects" is what makes the change
tracker falsifiable against TIA's own online/offline comparison on every download.

## The message vocabularies

Recognised by **shape**, anchored, never by substring:

| Kind | Example | Notes |
|---|---|---|
| `ObjectLoad` | `'DB_Sample' was loaded successfully.` | quoted name → the manifest |
| `NonObjectLoad` | `Hardware configuration was loaded successfully.` | unquoted subject |
| | `Connection configuration was downloaded successfully.` | **different verb, same run** |
| | `Routing configuration was loaded successfully.` | |
| `CpuStopped` / `CpuStarted` | `PLC_1 stopped.` / `PLC_1 started.` | order preserved |
| `UpToDate` | `The software has not been loaded, because it is up-to-date.` | success, nothing moved |
| `Container` | `PLC_1`, `Hardware configuration` | a group header — has children, is not a sentence |
| `Unrecognised` | anything else | **carried in full, counted, reported** |

`Hardware configuration` appears **twice with two different meanings** on the hardware download —
once as the group node that *contains* the loads, once as the sentence that *is* one. A substring
match on those two words counts the container as a transfer. Hence anchored patterns, and hence
`Container` being recognised structurally (children **and** no terminating period) rather than by
name.

Unrecognised messages are the guard that matters most, because its absence is invisible: a parser
that classifies only what it knows still returns a confident answer when the vocabulary changes, it
just quietly answers about a subset. Every download option measured so far produced a vocabulary
nobody predicted.

## Run-state

`RunStateDisclosed == false` means **the download said nothing about run state**. It does not mean
the CPU kept running. Nothing in this library asks a controller anything; these are TIA's words
about what the download did.

## Tests

`dotnet test src/download-feedback/download-feedback.sln` — 37 tests, all passing.

Fixtures are excerpts of the real probe logs, copied into `DownloadFeedback.Tests/Fixtures/` rather
than read from the job directory they were produced in (that directory is disposable; a test that
reaches into one stops existing when somebody tidies up). **Object names are invented; the message
shape is verbatim** — the shape is what is under test, the names are not, and the logs came from a
live engineering job.

Pinned facts, each measured on a real S7-1200:

- differential download → **1** object
- full download → **99** objects = 66 blocks + 33 PLC data types, agreeing exactly with
  `openness-cli download-plan`
- hardware download → hardware / connection / routing, in **three different wordings**, and **zero**
  program objects
- up-to-date run → **nothing** transferred while the result reported `Success`
- aborted run → no result object, verdict `Undetermined`
