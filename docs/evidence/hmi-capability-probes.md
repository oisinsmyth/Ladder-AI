# Evidence -- HMI capability probes, transcripts (P1-P6)

Output of the three live sweeps run against JOB9002's **scratch copy** on 2026-08-08/09. Findings and
verdicts live in `docs/notes/openness-hmi-write-api.md`; the programme's state lives in
`docs/notes/hmi-capability-probe-plan.md`. This file is append-only and is the primary record -- the
working transcripts were in a session temp directory that does not survive.

## Redaction -- read this before reading the transcripts

The raw output **enumerates restricted content**: 311 discrete alarm names, equipment names,
screen names and tag names belonging to a real project. `docs/13-data-boundary.md`'s JOB9002 entry
forbids that verbatim in any committed doc, so what follows has been filtered by a **fail-closed
whitelist**, applied mechanically:

- **Kept:** probe headers, exit codes, compile `STATE`/counts, `[Error]` message lines, validation
  and save lines, and every line naming one of this programme's own invented **`ZZ_AI_*`** artifacts.
- **Elided:** everything else, including all inventory and read-back listings of pre-existing
  objects. Each contiguous run is replaced by a `... [N line(s) elided ...] ...` marker giving the
  count and the reason, so the redaction is visible and quantified.
- The device also carries **156 pre-existing warnings** that repeat verbatim in every compile; these
  are elided under the same mechanism and say nothing about the probes.

**Nothing about a probe's outcome is removed** -- every `EXIT=`, every `[Error]`, every refusal is
here as it was printed. What is removed is the site's data, which the probes did not touch.
The counts in the markers are themselves the evidence that the elided material was inventory listings
rather than results.

Every probe ran with the **worktree** binary, never the shared `bin/Debug` path. Exit codes are the
codes as they were **at the time of the run**; two exit-code defects the sweeps themselves exposed
(`hmi-set`/`hmi-edit-screen` exiting 0 on partial refusal) were fixed afterwards, so re-running P3.2
or P4.4 today would exit 7, not 0. That is a fix, not a discrepancy in the record.

## P1 -- deletion lifecycle (2026-08-08, 13 probes)

Headline: **deletion ORPHANS silently** (P1.7), and only the compile notices (P1.7b).

```

==================== P1.0 baseline inventory ====================
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
EXIT=0
  ... [371 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
      ZZ_AI_TestScreen  [HmiScreen]
  ... [22 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
      ZZ_AI_TestTags  [HmiTagTable]
  ... [234 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
      ZZ_AI_TestTag  [HmiTag]
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  PROBE ARTIFACTS (ZZ_AI_*): 3 — these are this tool's own and must be zero at end of programme:
      Screens / ZZ_AI_TestScreen
      TagTables / ZZ_AI_TestTags
      Tags / ZZ_AI_TestTag

==================== P1.7 delete a tag that two live bindings still reference ====================
ARGS: hmi-delete C:\Users\User\Desktop\AI Ladder Project\JOB9002 - Tom White Waste - Scratch Copy\JOB9002 - Tom White Waste\JOB9002 - Tom White Waste_V20\JOB9002 - Tom White Waste_V20.ap20 --kind Tags --name ZZ_AI_TestTag --yes --timeout-connect 1500 --timeout-open 1800
EXIT=0
  deleted Tags 'ZZ_AI_TestTag' [HmiTag] on HMI_1/HMI_RT_1; confirmed absent on re-read

==================== P1.7a read back the screen - are the bindings orphaned? ====================
ARGS: hmi C:\Users\User\Desktop\AI Ladder Project\JOB9002 - Tom White Waste - Scratch Copy\JOB9002 - Tom White Waste\JOB9002 - Tom White Waste_V20\JOB9002 - Tom White Waste_V20.ap20 --screen ZZ_AI_TestScreen --timeout-connect 1500 --timeout-open 1800
EXIT=0
  ... [8 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
    SCREEN  ZZ_AI_TestScreen  #0  1000x615  items=3
  ... [2 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
        Visible <- Tag  tag=ZZ_AI_TestTag
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
        Visible <- Tag  tag=ZZ_AI_TestTag
  ... [92 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...

==================== P1.7b compile - does it catch the now-dangling bindings? ====================
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
EXIT=8
  STATE: Error
  ERRORS: 8  WARNINGS: 156
  ... [2 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  [Error] HMI_1: 
  ... [3 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  [Error] Screens: 
  [Error] ZZ_AI_TestScreen: 
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  [Error] The tag 'ZZ_AI_TestTag' for dynamization of the property 'Visibility' does not exist. Select an existing tag.
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  [Error] The tag 'ZZ_AI_TestTag' for dynamization of the property 'Visibility' does not exist. Select an existing tag.
  ... [156 pre-existing [Warning] line(s) elided -- same 156 device warnings in every compile] ...
  [Error] Compiling finished (errors: 2; warnings: 0)

==================== P1.6 delete a nonexistent object ====================
ARGS: hmi-delete C:\Users\User\Desktop\AI Ladder Project\JOB9002 - Tom White Waste - Scratch Copy\JOB9002 - Tom White Waste\JOB9002 - Tom White Waste_V20\JOB9002 - Tom White Waste_V20.ap20 --kind Tags --name ZZ_AI_NoSuchTag --yes --timeout-connect 1500 --timeout-open 1800
EXIT=5
  openness-cli hmi-delete failed: HmiObjectNotFoundException: No Tags object named 'ZZ_AI_NoSuchTag' was found. List what exists with `openness-cli hmi-inventory <project> --kind Tags`.

==================== P1.6a delete refused without the ZZ_AI_ prefix (guard) ====================
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
EXIT=5
  openness-cli hmi-delete failed: HmiRefusedToDeleteRealObjectException: Refusing to delete 'MainScreen': this tool only deletes its own probe artifacts, whose names start with 'ZZ_AI_'. Pass --allow-any-name to override, which is never correct for unattended work and must be a deliberate, supervised choice.

==================== P1.2 delete a binding ====================
ARGS: hmi-edit-screen C:\Users\User\Desktop\AI Ladder Project\JOB9002 - Tom White Waste - Scratch Copy\JOB9002 - Tom White Waste\JOB9002 - Tom White Waste_V20\JOB9002 - Tom White Waste_V20.ap20 --name ZZ_AI_TestScreen --delete-bind HmiText_2.Visible --yes --timeout-connect 1500 --timeout-open 1800
EXIT=0
  EDITED screen 'ZZ_AI_TestScreen' on HMI_1/HMI_RT_1
    changes applied: 1
      deleted binding HmiText_2.Visible; confirmed absent on re-read
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P1.3 delete an event handler ====================
ARGS: hmi-edit-screen C:\Users\User\Desktop\AI Ladder Project\JOB9002 - Tom White Waste - Scratch Copy\JOB9002 - Tom White Waste\JOB9002 - Tom White Waste_V20\JOB9002 - Tom White Waste_V20.ap20 --name ZZ_AI_TestScreen --delete-event HmiButton_3:Tapped --yes --timeout-connect 1500 --timeout-open 1800
EXIT=0
  EDITED screen 'ZZ_AI_TestScreen' on HMI_1/HMI_RT_1
    changes applied: 1
      deleted event HmiButton_3:Tapped; confirmed absent on re-read
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P1.1 delete a screen item ====================
ARGS: hmi-edit-screen C:\Users\User\Desktop\AI Ladder Project\JOB9002 - Tom White Waste - Scratch Copy\JOB9002 - Tom White Waste\JOB9002 - Tom White Waste_V20\JOB9002 - Tom White Waste_V20.ap20 --name ZZ_AI_TestScreen --delete-item HmiText_2 --yes --timeout-connect 1500 --timeout-open 1800
EXIT=0
  EDITED screen 'ZZ_AI_TestScreen' on HMI_1/HMI_RT_1
    changes applied: 1
      deleted item 'HmiText_2'; confirmed absent on re-read
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P1.4 delete the screen ====================
ARGS: hmi-delete C:\Users\User\Desktop\AI Ladder Project\JOB9002 - Tom White Waste - Scratch Copy\JOB9002 - Tom White Waste\JOB9002 - Tom White Waste_V20\JOB9002 - Tom White Waste_V20.ap20 --kind Screens --name ZZ_AI_TestScreen --yes --timeout-connect 1500 --timeout-open 1800
EXIT=0
  deleted Screens 'ZZ_AI_TestScreen' [HmiScreen] on HMI_1/HMI_RT_1; confirmed absent on re-read

==================== P1.5 delete the tag table ====================
ARGS: hmi-delete C:\Users\User\Desktop\AI Ladder Project\JOB9002 - Tom White Waste - Scratch Copy\JOB9002 - Tom White Waste\JOB9002 - Tom White Waste_V20\JOB9002 - Tom White Waste_V20.ap20 --kind TagTables --name ZZ_AI_TestTags --yes --timeout-connect 1500 --timeout-open 1800
EXIT=0
  deleted TagTables 'ZZ_AI_TestTags' [HmiTagTable] on HMI_1/HMI_RT_1; confirmed absent on re-read

==================== P1.9 final inventory - expect ZERO probe artifacts ====================
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
EXIT=0
  ... [628 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  PROBE ARTIFACTS (ZZ_AI_*): 0

==================== P1.10 final compile - expect clean ====================
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
EXIT=0
  STATE: Success
  ERRORS: 0  WARNINGS: 156
  ... [7 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  ... [156 pre-existing [Warning] line(s) elided -- same 156 device warnings in every compile] ...
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
```

## P2 -- item-type breadth (2026-08-09, all 56 item types)

Headline: **`GetCreationInfos` overstates by 21 of 56** -- the creatable list must be established by trial.

```

==================== P2.1 create the sweep screen ====================
EXIT=0
  created Screens 'ZZ_AI_P2Screen' on HMI_1/HMI_RT_1 [HmiScreen]

==================== P2.2 attempt all 56 item types ====================
EXIT=0
  EDITED screen 'ZZ_AI_P2Screen' on HMI_1/HMI_RT_1
    changes applied: 56
  ... [3 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
      add-item HmiCentricShapeBase -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition'.)
  ... [4 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
      add-item HmiCircularShapeBase -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition'.)
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
      add-item HmiCompanionBase -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition'.)
      add-item HmiContainerBase -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition'.)
      add-item HmiControlWindowBase -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition'.)
      add-item HmiCustomWebControlContainer -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition'.)
      add-item HmiCustomWidgetContainer -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition'.)
  ... [4 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
      add-item HmiEllipticalShapeBase -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition'.)
  ... [5 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
      add-item HmiLabel -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition'.)
  ... [3 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
      add-item HmiPointBasedShapeBase -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition'.)
  ... [2 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
      add-item HmiProcessControl -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition'.)
  ... [2 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
      add-item HmiScaleWidgetBase -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition'.)
      add-item HmiScreenItemBase -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition'.)
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
      add-item HmiSelectionGroupBase -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition'.)
      add-item HmiShapeBase -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition'.)
      add-item HmiSimpleScreenItemBase -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition'.)
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
      add-item HmiSurfaceShapeBase -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition'.)
  ... [4 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
      add-item HmiTextWidgetBase -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition'.)
  ... [4 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
      add-item HmiTrendControlBase -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition'.)
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
      add-item HmiWidgetBase -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition'.)
      add-item HmiWindowBase -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition'.)
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P2.3 read back - which items actually exist ====================
EXIT=0
  ... [8 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
    SCREEN  ZZ_AI_P2Screen  #0  1280x800  items=35
  ... [125 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...

==================== P2.4 compile with every creatable type present ====================
EXIT=8
  STATE: Error
  ERRORS: 6  WARNINGS: 156
  ... [2 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  [Error] HMI_1: 
  ... [3 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  [Error] Screens: 
  [Error] ZZ_AI_P2Screen: 
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  [Error] The referenced faceplate type does not exist. Select a valid faceplate type.
  ... [156 pre-existing [Warning] line(s) elided -- same 156 device warnings in every compile] ...
  [Error] Compiling finished (errors: 1; warnings: 0)

==================== P2.5 delete the sweep screen ====================
EXIT=0
  deleted Screens 'ZZ_AI_P2Screen' [HmiScreen] on HMI_1/HMI_RT_1; confirmed absent on re-read

==================== P2.6 final compile - expect clean ====================
EXIT=0
  STATE: Success
  ERRORS: 0  WARNINGS: 156
  ... [7 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  ... [156 pre-existing [Warning] line(s) elided -- same 156 device warnings in every compile] ...
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...

==================== P2.7 final inventory - expect zero artifacts ====================
EXIT=0
  ... [628 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  PROBE ARTIFACTS (ZZ_AI_*): 0
```

## P3-P6 -- dynamizations, alarms, logs, structure (2026-08-09, 30 probes)

Headlines: **only 3 of 6 dynamization kinds create**; **alarm text cannot be written at all** (`set_Text` throws); bare alarms/logs are useless and the compile names exactly which fields are missing.

```

==================== P3.1 create host screen + items ====================
EXIT=0
  CREATED screen 'ZZ_AI_P3Screen' on HMI_1/HMI_RT_1
  ... [7 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P3.2 create all six dynamization kinds on different properties ====================
EXIT=0
  EDITED screen 'ZZ_AI_P3Screen' on HMI_1/HMI_RT_1
    changes applied: 5
      dynamization FlashingDynamization on HmiRectangle_1.Visible -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Dynamization.DynamizationBaseComposition'.)
  ... [2 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
      dynamization ResourceListDynamization on HmiButton_4.Visible -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Dynamization.DynamizationBaseComposition'.)
      dynamization TagParameterDynamization on HmiCircle_5.Visible -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Dynamization.DynamizationBaseComposition'.)
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P3.3 read back the dynamizations ====================
EXIT=0
  ... [8 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
    SCREEN  ZZ_AI_P3Screen  #0  1280x615  items=5
  ... [97 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...

==================== P3.4 compile with all dynamization kinds present ====================
EXIT=8
  STATE: Error
  ERRORS: 6  WARNINGS: 156
  ... [2 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  [Error] HMI_1: 
  ... [3 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  [Error] Screens: 
  [Error] ZZ_AI_P3Screen: 
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  [Error] The configured tag is invalid.
  ... [156 pre-existing [Warning] line(s) elided -- same 156 device warnings in every compile] ...
  [Error] Compiling finished (errors: 1; warnings: 0)

==================== P3.5 delete the P3 screen ====================
EXIT=0
  deleted Screens 'ZZ_AI_P3Screen' [HmiScreen] on HMI_1/HMI_RT_1; confirmed absent on re-read

==================== P4.1 create an alarm class ====================
EXIT=0
  created AlarmClasses 'ZZ_AI_AlarmClass' on HMI_1/HMI_RT_1 [HmiAlarmClass]

==================== P4.2 create a discrete alarm ====================
EXIT=0
  created DiscreteAlarms 'ZZ_AI_DiscAlarm' on HMI_1/HMI_RT_1 [HmiDiscreteAlarm]

==================== P4.3 create an analog alarm ====================
EXIT=0
  created AnalogAlarms 'ZZ_AI_AnalogAlarm' on HMI_1/HMI_RT_1 [HmiAnalogAlarm]

==================== P4.4 configure the discrete alarm + write its TEXT ====================
EXIT=0
  DiscreteAlarms 'ZZ_AI_DiscAlarm': 3 change(s)
    set ZZ_AI_AlarmClass (String)  -> AlarmClass
    set RaisedStateTagBitNumber -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'set_RaisedStateTagBitNumber' of type 'Siemens.Engineering.HmiUnified.HmiAlarm.HmiDiscreteAlarm'.)
    text EventText -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'set_Text' of type 'Siemens.Engineering.MultilingualTextItem'.)

==================== P4.5 set alarm-class severity + state machine ====================
EXIT=0
  AlarmClasses 'ZZ_AI_AlarmClass': 2 change(s)
    set 7 (Byte)  -> Priority
    set RaiseClearRequiresAcknowledgement (HmiAlarmStateMachine)  -> StateMachine

==================== P4.6 compile with alarms present ====================
EXIT=8
  STATE: Error
  ERRORS: 8  WARNINGS: 156
  ... [2 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  [Error] HMI_1: 
  ... [3 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  [Error] HMI alarms: 
  [Error] ZZ_AI_DiscAlarm: 
  [Error] Trigger tag: No trigger tag is configured.
  [Error] ZZ_AI_AnalogAlarm: 
  [Error] Trigger tag: No trigger tag is configured.
  [Error] The trigger value is invalid.
  ... [156 pre-existing [Warning] line(s) elided -- same 156 device warnings in every compile] ...
  [Error] Compiling finished (errors: 3; warnings: 0)

==================== P4.7 delete the alarms ====================
EXIT=0
  deleted DiscreteAlarms 'ZZ_AI_DiscAlarm' [HmiDiscreteAlarm] on HMI_1/HMI_RT_1; confirmed absent on re-read

==================== P4.7b delete the analog alarm ====================
EXIT=0
  deleted AnalogAlarms 'ZZ_AI_AnalogAlarm' [HmiAnalogAlarm] on HMI_1/HMI_RT_1; confirmed absent on re-read

==================== P4.7c delete the alarm class ====================
EXIT=0
  deleted AlarmClasses 'ZZ_AI_AlarmClass' [HmiAlarmClass] on HMI_1/HMI_RT_1; confirmed absent on re-read

==================== P5.1 create a data log ====================
EXIT=0
  created DataLogs 'ZZ_AI_DataLog' on HMI_1/HMI_RT_1 [HmiDataLog]

==================== P5.2 create an alarm log ====================
EXIT=0
  created AlarmLogs 'ZZ_AI_AlarmLog' on HMI_1/HMI_RT_1 [HmiAlarmLog]

==================== P5.3 create a connection ====================
EXIT=0
  created Connections 'ZZ_AI_Conn' on HMI_1/HMI_RT_1 [HmiConnection]

==================== P5.4 compile with logs + connection present ====================
EXIT=8
  STATE: Error
  ERRORS: 8  WARNINGS: 156
  ... [2 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  [Error] HMI_1: 
  ... [3 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  [Error] Logs: 
  [Error] ZZ_AI_AlarmLog: 
  [Error] Database of the log must be on the same medium as the main database for alarm logging.
  [Error] No alarm class is configured for the alarm log. Assign at least one alarm class to the log.
  [Error] ZZ_AI_DataLog: 
  [Error] Database of the log must be on the same medium as the main database for tag logging.
  ... [156 pre-existing [Warning] line(s) elided -- same 156 device warnings in every compile] ...
  [Error] Compiling finished (errors: 3; warnings: 0)

==================== P5.5 delete the data log ====================
EXIT=0
  deleted DataLogs 'ZZ_AI_DataLog' [HmiDataLog] on HMI_1/HMI_RT_1; confirmed absent on re-read

==================== P5.6 delete the alarm log ====================
EXIT=0
  deleted AlarmLogs 'ZZ_AI_AlarmLog' [HmiAlarmLog] on HMI_1/HMI_RT_1; confirmed absent on re-read

==================== P5.7 delete the connection ====================
EXIT=0
  deleted Connections 'ZZ_AI_Conn' [HmiConnection] on HMI_1/HMI_RT_1; confirmed absent on re-read

==================== P6.1 create a screen group ====================
EXIT=0
  created ScreenGroups 'ZZ_AI_Group' on HMI_1/HMI_RT_1 [HmiScreenGroup]

==================== P6.2 create a screen (does it land in the group or at root?) ====================
EXIT=0
  created Screens 'ZZ_AI_GroupedScreen' in 'ZZ_AI_Group' on HMI_1/HMI_RT_1 [HmiScreen]

==================== P6.3 create a screen window pointing at a screen ====================
EXIT=0
  CREATED screen 'ZZ_AI_P6Screen' on HMI_1/HMI_RT_1
  ... [3 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P6.4 point the window at a screen ====================
EXIT=0
  EDITED screen 'ZZ_AI_P6Screen' on HMI_1/HMI_RT_1
    changes applied: 1
      set HmiScreenWindow_1.Screen = ZZ_AI_GroupedScreen (String)
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P6.5 compile with group + window present ====================
EXIT=0
  STATE: Success
  ERRORS: 0  WARNINGS: 156
  ... [7 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  ... [156 pre-existing [Warning] line(s) elided -- same 156 device warnings in every compile] ...
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...

==================== P6.6 delete the windowed screen ====================
EXIT=0
  deleted Screens 'ZZ_AI_P6Screen' [HmiScreen] on HMI_1/HMI_RT_1; confirmed absent on re-read

==================== P6.7 delete the grouped screen ====================
EXIT=0
  deleted Screens 'ZZ_AI_GroupedScreen' [HmiScreen] on HMI_1/HMI_RT_1; confirmed absent on re-read

==================== P6.8 delete the screen group ====================
EXIT=0
  deleted ScreenGroups 'ZZ_AI_Group' [HmiScreenGroup] on HMI_1/HMI_RT_1; confirmed absent on re-read

==================== FINAL inventory - expect ZERO probe artifacts ====================
EXIT=0
  ... [628 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  PROBE ARTIFACTS (ZZ_AI_*): 0

==================== FINAL compile - expect clean ====================
EXIT=0
  STATE: Success
  ERRORS: 0  WARNINGS: 156
  ... [7 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  ... [156 pre-existing [Warning] line(s) elided -- same 156 device warnings in every compile] ...
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
```

