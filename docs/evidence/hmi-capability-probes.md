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
ARGS: hmi-delete C:\Users\<user>\Desktop\AI Ladder Project\JOB9002 - Tom White Waste - Scratch Copy\JOB9002 - Tom White Waste\JOB9002 - Tom White Waste_V20\JOB9002 - Tom White Waste_V20.ap20 --kind Tags --name ZZ_AI_TestTag --yes --timeout-connect 1500 --timeout-open 1800
EXIT=0
  deleted Tags 'ZZ_AI_TestTag' [HmiTag] on HMI_1/HMI_RT_1; confirmed absent on re-read

==================== P1.7a read back the screen - are the bindings orphaned? ====================
ARGS: hmi C:\Users\<user>\Desktop\AI Ladder Project\JOB9002 - Tom White Waste - Scratch Copy\JOB9002 - Tom White Waste\JOB9002 - Tom White Waste_V20\JOB9002 - Tom White Waste_V20.ap20 --screen ZZ_AI_TestScreen --timeout-connect 1500 --timeout-open 1800
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
ARGS: hmi-delete C:\Users\<user>\Desktop\AI Ladder Project\JOB9002 - Tom White Waste - Scratch Copy\JOB9002 - Tom White Waste\JOB9002 - Tom White Waste_V20\JOB9002 - Tom White Waste_V20.ap20 --kind Tags --name ZZ_AI_NoSuchTag --yes --timeout-connect 1500 --timeout-open 1800
EXIT=5
  openness-cli hmi-delete failed: HmiObjectNotFoundException: No Tags object named 'ZZ_AI_NoSuchTag' was found. List what exists with `openness-cli hmi-inventory <project> --kind Tags`.

==================== P1.6a delete refused without the ZZ_AI_ prefix (guard) ====================
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
EXIT=5
  openness-cli hmi-delete failed: HmiRefusedToDeleteRealObjectException: Refusing to delete 'MainScreen': this tool only deletes its own probe artifacts, whose names start with 'ZZ_AI_'. Pass --allow-any-name to override, which is never correct for unattended work and must be a deliberate, supervised choice.

==================== P1.2 delete a binding ====================
ARGS: hmi-edit-screen C:\Users\<user>\Desktop\AI Ladder Project\JOB9002 - Tom White Waste - Scratch Copy\JOB9002 - Tom White Waste\JOB9002 - Tom White Waste_V20\JOB9002 - Tom White Waste_V20.ap20 --name ZZ_AI_TestScreen --delete-bind HmiText_2.Visible --yes --timeout-connect 1500 --timeout-open 1800
EXIT=0
  EDITED screen 'ZZ_AI_TestScreen' on HMI_1/HMI_RT_1
    changes applied: 1
      deleted binding HmiText_2.Visible; confirmed absent on re-read
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P1.3 delete an event handler ====================
ARGS: hmi-edit-screen C:\Users\<user>\Desktop\AI Ladder Project\JOB9002 - Tom White Waste - Scratch Copy\JOB9002 - Tom White Waste\JOB9002 - Tom White Waste_V20\JOB9002 - Tom White Waste_V20.ap20 --name ZZ_AI_TestScreen --delete-event HmiButton_3:Tapped --yes --timeout-connect 1500 --timeout-open 1800
EXIT=0
  EDITED screen 'ZZ_AI_TestScreen' on HMI_1/HMI_RT_1
    changes applied: 1
      deleted event HmiButton_3:Tapped; confirmed absent on re-read
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P1.1 delete a screen item ====================
ARGS: hmi-edit-screen C:\Users\<user>\Desktop\AI Ladder Project\JOB9002 - Tom White Waste - Scratch Copy\JOB9002 - Tom White Waste\JOB9002 - Tom White Waste_V20\JOB9002 - Tom White Waste_V20.ap20 --name ZZ_AI_TestScreen --delete-item HmiText_2 --yes --timeout-connect 1500 --timeout-open 1800
EXIT=0
  EDITED screen 'ZZ_AI_TestScreen' on HMI_1/HMI_RT_1
    changes applied: 1
      deleted item 'HmiText_2'; confirmed absent on re-read
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P1.4 delete the screen ====================
ARGS: hmi-delete C:\Users\<user>\Desktop\AI Ladder Project\JOB9002 - Tom White Waste - Scratch Copy\JOB9002 - Tom White Waste\JOB9002 - Tom White Waste_V20\JOB9002 - Tom White Waste_V20.ap20 --kind Screens --name ZZ_AI_TestScreen --yes --timeout-connect 1500 --timeout-open 1800
EXIT=0
  deleted Screens 'ZZ_AI_TestScreen' [HmiScreen] on HMI_1/HMI_RT_1; confirmed absent on re-read

==================== P1.5 delete the tag table ====================
ARGS: hmi-delete C:\Users\<user>\Desktop\AI Ladder Project\JOB9002 - Tom White Waste - Scratch Copy\JOB9002 - Tom White Waste\JOB9002 - Tom White Waste_V20\JOB9002 - Tom White Waste_V20.ap20 --kind TagTables --name ZZ_AI_TestTags --yes --timeout-connect 1500 --timeout-open 1800
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

Headlines: ~~**only 3 of 6 dynamization kinds create**~~ **— WRONG, retracted by P7 below: 5 of 6 create; this run bound every kind to a Boolean property** —; **alarm text cannot be written at all** (`set_Text` throws); bare alarms/logs are useless and the compile names exactly which fields are missing.

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

## P7 -- are the dynamization refusals property-type gating? (2026-08-09, 14 probes)

Headline: **YES -- and P3's "3 of 6 kinds create" was a confounded probe, not an API limit.**
P3 bound all three "refused" kinds to `Visible`, a Boolean. Here `Flashing` creates on colour
properties and `ResourceList` on text properties, with **negative controls in the same session**
(P7.4/P7.5) showing the same kinds refuse again the moment the property type is wrong.
**5 of 6 kinds create.** `TagParameter` refused in every position tried -- probably faceplate-scoped.

```

==================== P7.1 host screen: rectangle, text, IO field, button ====================
EXIT=0
  CREATED screen 'ZZ_AI_P7Screen' on HMI_1/HMI_RT_1
  ... [6 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P7.2 Flashing on a COLOUR property (hypothesis: SUCCEEDS) ====================
EXIT=0
  EDITED screen 'ZZ_AI_P7Screen' on HMI_1/HMI_RT_1
    changes applied: 1
      dynamization FlashingDynamization created on HmiRectangle.BackColor [DynamizationType=Flashing]
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P7.3 ResourceList on a TEXT property (hypothesis: SUCCEEDS) ====================
EXIT=0
  EDITED screen 'ZZ_AI_P7Screen' on HMI_1/HMI_RT_1
    changes applied: 1
      dynamization ResourceListDynamization created on HmiText.Text [DynamizationType=ResourceList]
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P7.4 NEGATIVE CONTROL Flashing on Visible (must still REFUSE) ====================
EXIT=7
  EDITED screen 'ZZ_AI_P7Screen' on HMI_1/HMI_RT_1
    changes applied: 0 (1 REFUSED)
      dynamization FlashingDynamization on HmiText_2.Visible -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Dynamization.DynamizationBaseComposition'.)
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P7.5 NEGATIVE CONTROL ResourceList on a colour property (must REFUSE if it is text-only) ====================
EXIT=7
  EDITED screen 'ZZ_AI_P7Screen' on HMI_1/HMI_RT_1
    changes applied: 0 (1 REFUSED)
      dynamization ResourceListDynamization on HmiRectangle_1.BorderColor -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Dynamization.DynamizationBaseComposition'.)
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P7.6 Flashing on other colour properties ====================
EXIT=0
  EDITED screen 'ZZ_AI_P7Screen' on HMI_1/HMI_RT_1
    changes applied: 2
      dynamization FlashingDynamization created on HmiRectangle.BorderColor [DynamizationType=Flashing]
      dynamization FlashingDynamization created on HmiButton.BackColor [DynamizationType=Flashing]
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P7.7 ResourceList on other text/graphic properties ====================
EXIT=0
  EDITED screen 'ZZ_AI_P7Screen' on HMI_1/HMI_RT_1
    changes applied: 1
      dynamization ResourceListDynamization created on HmiButton.Text [DynamizationType=ResourceList]
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P7.8 TagParameter across several property shapes ====================
EXIT=7
  EDITED screen 'ZZ_AI_P7Screen' on HMI_1/HMI_RT_1
    changes applied: 0 (3 REFUSED)
      dynamization TagParameterDynamization on HmiIOField_3.ProcessValue -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Dynamization.DynamizationBaseComposition'.)
      dynamization TagParameterDynamization on HmiText_2.Text -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Dynamization.DynamizationBaseComposition'.)
      dynamization TagParameterDynamization on HmiRectangle_1.Width -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Dynamization.DynamizationBaseComposition'.)
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P7.9 configure the flashing dynamization ====================
EXIT=7
  Screen 'ZZ_AI_P7Screen' has no item named 'HmiRectangle_1.BackColor'. Use 'Screen' to target the screen itself, or list the item names with `openness-cli hmi <project> --screen ZZ_AI_P7Screen`.

==================== P7.10 read back the screen ====================
EXIT=0
  ... [8 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
    SCREEN  ZZ_AI_P7Screen  #0  1280x615  items=4
  ... [98 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...

==================== P7.11 compile ====================
EXIT=8
  STATE: Error
  ERRORS: 7  WARNINGS: 156
  ... [2 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  [Error] HMI_1: 
  ... [3 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  [Error] Screens: 
  [Error] ZZ_AI_P7Screen: 
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  [Error] No tag is configured for dynamization of the property 'Text'. Select an existing tag.
  [Error] No resource list is selected for dynamization of the property 'Text'. Select an existing resource list.
  ... [156 pre-existing [Warning] line(s) elided -- same 156 device warnings in every compile] ...
  [Error] Compiling finished (errors: 2; warnings: 0)

==================== P7.12 delete the screen ====================
EXIT=0
  deleted Screens 'ZZ_AI_P7Screen' [HmiScreen] on HMI_1/HMI_RT_1; confirmed absent on re-read

==================== P7.13 FINAL compile - expect clean ====================
EXIT=0
  STATE: Success
  ERRORS: 0  WARNINGS: 156
  ... [7 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  ... [156 pre-existing [Warning] line(s) elided -- same 156 device warnings in every compile] ...
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...

==================== P7.14 FINAL inventory - expect zero artifacts ====================
EXIT=0
  ... [628 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  PROBE ARTIFACTS (ZZ_AI_*): 0
```

## P8 -- the MAPPING-TABLE route to flashing (2026-08-09, 47 probe records)

P7 (section 4m) surfaced a second, untouched route to flashing:
`TagDynamization -> ValueConverter -> MappingTable -> Entries`, where each entry carries
`Value`/`AlternateValue`/`Flashing`/`FlashingRate`. Nobody had ever created one.
**Verdict: the hypothesis is TRUE.** Analysis in `openness-hmi-write-api.md` section 4n.

Two harness notes, because they shaped the transcript:

- P8.1/P8.2 exit 7 with "already exists". An earlier attempt of this same sweep was killed
  mid-run and its screen/tag survived -- Openness commits eagerly (section 4h). The create-never-
  overwrite guard refused, correctly, and the sweep continued against those artifacts.
- The probe runner was rewritten mid-sweep to redirect the client's output to a FILE rather
  than pipe it. When `openness-cli` has to LAUNCH its own Portal, that Portal is a child and
  inherits the client's stdout handle, so a pipeline waits for an EOF that never comes while
  Portal lives -- the sweep hung on probes that had already finished. Probes P8.0-P8.8 ran
  under the old runner (they attached to a running Portal, so it never bit).

```


==================== P8.0 baseline inventory - expect zero probe artifacts ====================
ARGS: hmi-inventory <scratch project> --timeout-connect 1800 --timeout-open 3600
EXIT=0
  ... [371 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
      ZZ_AI_P8Screen  [HmiScreen]
  ... [22 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
      ZZ_AI_MapTags  [HmiTagTable]
  ... [234 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
      ZZ_AI_MapTag  [HmiTag]
  PROBE ARTIFACTS (ZZ_AI_*): 3 — these are this tool's own and must be zero at end of programme:
      Screens / ZZ_AI_P8Screen
      TagTables / ZZ_AI_MapTags
      Tags / ZZ_AI_MapTag

==================== P8.1 create the probe screen with five items ====================
ARGS: hmi-create-screen <scratch project> --name ZZ_AI_P8Screen --item HmiRectangle --item HmiText --item HmiButton --item HmiIOField --item HmiRectangle --yes --timeout-connect 1800 --timeout-open 3600
EXIT=7
  openness-cli.exe : A screen named 'ZZ_AI_P8Screen' already exists. This command creates screens and never overwrites one — choose a different name, or 
  ... [6 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...

==================== P8.2 create the bind-target tag ====================
ARGS: hmi-create-tag <scratch project> --name ZZ_AI_MapTag --table ZZ_AI_MapTags --datatype Int --yes --timeout-connect 1800 --timeout-open 3600
EXIT=7
  openness-cli.exe : An HMI tag named 'ZZ_AI_MapTag' already exists. This command creates tags and never modifies an existing one — choose a different name.
  ... [5 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...

==================== P8.3 bind six properties to that tag (TagDynamization on COLOUR properties) ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8Screen --bind HmiRectangle_1.BackColor=ZZ_AI_MapTag --bind HmiRectangle_5.BackColor=ZZ_AI_MapTag --bind HmiButton_3.BackColor=ZZ_AI_MapTag --bind HmiRectangle_1.BorderColor=ZZ_AI_MapTag --bind HmiText_2.Visible=ZZ_AI_MapTag --bind HmiIOField_4.ProcessValue=ZZ_AI_MapTag --yes --timeout-connect 1800 --timeout-open 3600
EXIT=0
  EDITED screen 'ZZ_AI_P8Screen' on HMI_1/HMI_RT_1
    changes applied: 6
      bind replaced HmiRectangle.BackColor <- tag 'ZZ_AI_MapTag' [resolved: plcTag='' dataType='Int']
      bind replaced HmiRectangle.BackColor <- tag 'ZZ_AI_MapTag' [resolved: plcTag='' dataType='Int']
      bind replaced HmiButton.BackColor <- tag 'ZZ_AI_MapTag' [resolved: plcTag='' dataType='Int']
      bind replaced HmiRectangle.BorderColor <- tag 'ZZ_AI_MapTag' [resolved: plcTag='' dataType='Int']
      bind replaced HmiText.Visible <- tag 'ZZ_AI_MapTag' [resolved: plcTag='' dataType='Int']
      bind replaced HmiIOField.ProcessValue <- tag 'ZZ_AI_MapTag' [resolved: plcTag='' dataType='Int']
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P8.4 NESTED --set: reach the dynamization, ValueConverter and MappingTable ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8Screen --set HmiRectangle_1.BackColor.ValueConverter.IsFormulaSelected=False --set HmiRectangle_1.BackColor.ValueConverter.MappingTable.ConditionType=Range --yes --timeout-connect 1800 --timeout-open 3600
EXIT=0
  EDITED screen 'ZZ_AI_P8Screen' on HMI_1/HMI_RT_1
    changes applied: 2
      set HmiRectangle_1.BackColor.ValueConverter.IsFormulaSelected = False (Boolean)
      set HmiRectangle_1.BackColor.ValueConverter.MappingTable.ConditionType = Range (ConditionType)
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P8.5 ORDER A: Range entry AFTER ConditionType=Range ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8Screen --map HmiRectangle_1.BackColor=Range;From=int:1;To=int:5;Value=color:#FF0000;AlternateValue=color:#0000FF;Flashing=True;FlashingRate=Fast --yes --timeout-connect 1800 --timeout-open 3600
EXIT=0
  EDITED screen 'ZZ_AI_P8Screen' on HMI_1/HMI_RT_1
    changes applied: 1
      map-entry Create<MappingTableEntryRange>() on HmiRectangle.BackColor [ConditionType Range -> Range, entries=1] set: From=1 (Int32), To=5 (Int32), Value=#FFFF0000 (Color), AlternateValue=#FF0000FF (Color), Flashing=True (Boolean), FlashingRate=Fast (FlashingRate) | read back: MappingTableEntryRange From=1<Int32> To=5<Int32> RangeType=Range<RangeType> Value=#FFFF0000<Color> AlternateValue=#FF0000FF<Color> Flashing=True<Boolean> FlashingRate=Fast<FlashingRate>
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P8.6 ORDER B: Range entry with ConditionType still at its default ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8Screen --map HmiRectangle_5.BackColor=Range;From=int:10;To=int:20;Value=color:#00FF00;Flashing=True;FlashingRate=Slow --yes --timeout-connect 1800 --timeout-open 3600
EXIT=0
  EDITED screen 'ZZ_AI_P8Screen' on HMI_1/HMI_RT_1
    changes applied: 1
      map-entry Create<MappingTableEntryRange>() on HmiRectangle.BackColor [ConditionType None -> None, entries=1] set: From=10 (Int32), To=20 (Int32), Value=#FF00FF00 (Color), Flashing=True (Boolean), FlashingRate=Slow (FlashingRate) | read back: MappingTableEntryRange From=10<Int32> To=20<Int32> RangeType=Range<RangeType> Value=#FF00FF00<Color> AlternateValue=#FFFF0000<Color> Flashing=True<Boolean> FlashingRate=Slow<FlashingRate>
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P8.7 ORDER B part 2: set ConditionType=Range afterwards and read the entry back ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8Screen --set HmiRectangle_5.BackColor.ValueConverter.MappingTable.ConditionType=Range --set HmiRectangle_5.BackColor.ValueConverter.MappingTable.Entries[0].FlashingRate=Medium --yes --timeout-connect 1800 --timeout-open 3600
EXIT=0
  EDITED screen 'ZZ_AI_P8Screen' on HMI_1/HMI_RT_1
    changes applied: 2
      set HmiRectangle_5.BackColor.ValueConverter.MappingTable.ConditionType = Range (ConditionType)
      set HmiRectangle_5.BackColor.ValueConverter.MappingTable.Entries[0].FlashingRate = Medium (FlashingRate)
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P8.8 Q1: MappingTableEntrySimple, ConditionType untouched ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8Screen --map HmiButton_3.BackColor=Simple;Condition=1;Value=color:#FFFF00;Flashing=True;FlashingRate=Medium --yes --timeout-connect 1800 --timeout-open 3600
EXIT=5
  ... [7 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  TIA Portal has either been disposed or stopped running.


==================== P8.8 Q1: MappingTableEntrySimple, ConditionType untouched ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8Screen --map HmiButton_3.BackColor=Simple;Condition=1;Value=color:#FFFF00;Flashing=True;FlashingRate=Medium --yes --timeout-connect 1800 --timeout-open 3600
EXIT=5
  ... [7 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  TIA Portal has either been disposed or stopped running.

==================== P8.9 Q1: MappingTableEntryBitmask, ConditionType=Bitmask set first ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8Screen --set HmiRectangle_1.BorderColor.ValueConverter.MappingTable.ConditionType=Bitmask --map HmiRectangle_1.BorderColor=Bitmask;Condition=ulong:3;Value=color:#FF00FF;Flashing=True --yes --timeout-connect 1800 --timeout-open 3600
EXIT=7
  EDITED screen 'ZZ_AI_P8Screen' on HMI_1/HMI_RT_1
    changes applied: 1 (1 REFUSED)
      set HmiRectangle_1.BorderColor.ValueConverter.MappingTable.ConditionType = Bitmask (ConditionType)
      map-entry 'Bitmask;Condition=ulong:3;Value=color:#FF00FF;Flashing=True' on HmiRectangle_1.BorderColor -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Dynamization.Tag.MappingTableEntryBaseComposition'.)
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P8.9b ISOLATION: MappingTableEntrySimple created BARE (no attributes) - does the create alone kill Portal? ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8Screen --map HmiButton_3.BackColor=Simple --yes --timeout-connect 1800 --timeout-open 3600
EXIT=5
  openness-cli hmi-edit-screen failed: EngineeringObjectDisposedException: Access to a disposed object of type 'Siemens.Engineering.Project' is not possible.
  TIA Portal has either been disposed or stopped running.

==================== P8.10 Q5: the non-generic Create(BitDynamizationType.SingleBit) ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8Screen --map-clear HmiRectangle_1.BorderColor --map HmiRectangle_1.BorderColor=bits:SingleBit;Flashing=True;FlashingRate=Fast --yes --timeout-connect 1800 --timeout-open 3600
EXIT=0

==================== P8.11 Q5: the non-generic Create(BitDynamizationType.MultiBit) ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8Screen --map-clear HmiRectangle_1.BorderColor --map HmiRectangle_1.BorderColor=bits:MultiBit --yes --timeout-connect 1800 --timeout-open 3600
EXIT=7
  EDITED screen 'ZZ_AI_P8Screen' on HMI_1/HMI_RT_1
    changes applied: 1 (1 REFUSED)
      map-clear HmiRectangle_1.BorderColor -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Delete' of type 'Siemens.Engineering.HmiUnified.UI.Dynamization.Tag.MappingTableEntryBitmask'.)
      map-entry Create(BitDynamizationType.MultiBit) -> 1 entry(ies) on HmiRectangle.BorderColor [ConditionType Bitmask -> Bitmask, entries=1] set: no attributes set | read back: MappingTableEntryBitmask Condition=1<UInt64> BitDynamizationType=MultiBit<BitDynamizationType> Relevant=1<UInt64> Value=#FF7D7D85<Color> AlternateValue=#FFFF0000<Color> Flashing=False<Boolean> FlashingRate=Medium<FlashingRate>
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P8.12 Q1: the non-abstract MappingTableEntryBase itself ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8Screen --map HmiIOField_4.ProcessValue=Base;Value=int:1;Flashing=True;FlashingRate=Slow --yes --timeout-connect 1800 --timeout-open 3600
EXIT=7
  EDITED screen 'ZZ_AI_P8Screen' on HMI_1/HMI_RT_1
    changes applied: 0 (1 REFUSED)
      map-entry 'Base;Value=int:1;Flashing=True;FlashingRate=Slow' on HmiIOField_4.ProcessValue -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Dynamization.Tag.MappingTableEntryBaseComposition'.)
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P8.13 Q3: Value as Color, then String, then Int32, then Color again, on ONE entry ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8Screen --set HmiRectangle_1.BackColor.ValueConverter.MappingTable.Entries[0].Value=color:#00FFFF --set HmiRectangle_1.BackColor.ValueConverter.MappingTable.Entries[0].Value=str:Lime --set HmiRectangle_1.BackColor.ValueConverter.MappingTable.Entries[0].Value=int:65280 --set HmiRectangle_1.BackColor.ValueConverter.MappingTable.Entries[0].Value=color:#FF0000 --yes --timeout-connect 1800 --timeout-open 3600
EXIT=5
  openness-cli hmi-edit-screen failed: EngineeringTargetInvocationException: Error when calling method 'set_Value' of type 'Siemens.Engineering.HmiUnified.UI.Dynamization.Tag.MappingTableEntryRange'.
  -Invalid value type.

==================== P8.14 Q7: mapping-table flashing on Visible, a BOOLEAN property ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8Screen --map HmiText_2.Visible=Range;From=int:1;To=int:1;Value=bool:True;Flashing=True;FlashingRate=Fast --yes --timeout-connect 1800 --timeout-open 3600
EXIT=0
  EDITED screen 'ZZ_AI_P8Screen' on HMI_1/HMI_RT_1
    changes applied: 1
      map-entry Create<MappingTableEntryRange>() on HmiText.Visible [ConditionType None -> None, entries=1] set: From=1 (Int32), To=1 (Int32), Value=True (Boolean), Flashing=True (Boolean), FlashingRate=Fast (FlashingRate) | read back: MappingTableEntryRange From=1<Int32> To=1<Int32> RangeType=Range<RangeType> Value=True<Boolean> AlternateValue=(null)<null> Flashing=True<Boolean> FlashingRate=Fast<FlashingRate>
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P8.14b Q7 comparison: FlashingDynamization on a Boolean property (the Â§4m refusal) ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8Screen --bind-kind HmiIOField_4.Visible=FlashingDynamization --yes --timeout-connect 1800 --timeout-open 3600
EXIT=7
  EDITED screen 'ZZ_AI_P8Screen' on HMI_1/HMI_RT_1
    changes applied: 0 (1 REFUSED)
      dynamization FlashingDynamization on HmiIOField_4.Visible -> REFUSED (EngineeringTargetInvocationException: Error when calling method 'Create' of type 'Siemens.Engineering.HmiUnified.UI.Dynamization.DynamizationBaseComposition'.)
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P8.15 NEGATIVE CONTROL: --map onto a FlashingDynamization (no ValueConverter) ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8Screen --bind-kind HmiRectangle_5.BorderColor=FlashingDynamization --map HmiRectangle_5.BorderColor=Range;From=int:1;To=int:2;Value=color:#123456 --yes --timeout-connect 1800 --timeout-open 3600
EXIT=7
  EDITED screen 'ZZ_AI_P8Screen' on HMI_1/HMI_RT_1
    changes applied: 1 (1 REFUSED)
      dynamization FlashingDynamization created on HmiRectangle.BorderColor [DynamizationType=Flashing]
      map-entry 'Range;From=int:1;To=int:2;Value=color:#123456' on HmiRectangle_5.BorderColor -> REFUSED (HmiMappingTableNotAvailableException: No mapping table is reachable on HmiRectangle.BorderColor: the dynamization there is a FlashingDynamization; only a TagDynamization carries a ValueConverter. A mapping table hangs off a TagDynamization's ValueConverter, so bind the property to a tag first (--bind).)
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P8.16 NEGATIVE CONTROL: --map onto a property with NO dynamization at all ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8Screen --map HmiButton_3.BorderColor=Range;From=int:1;To=int:2;Value=color:#123456 --yes --timeout-connect 1800 --timeout-open 3600
EXIT=7
  EDITED screen 'ZZ_AI_P8Screen' on HMI_1/HMI_RT_1
    changes applied: 0 (1 REFUSED)
      map-entry 'Range;From=int:1;To=int:2;Value=color:#123456' on HmiButton_3.BorderColor -> REFUSED (HmiMappingTableNotAvailableException: No mapping table is reachable on HmiButton.BorderColor: there is no dynamization on that property — create one with --bind first. A mapping table hangs off a TagDynamization's ValueConverter, so bind the property to a tag first (--bind).)
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P8.17 NEGATIVE CONTROL: a nested --set path that does not resolve ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8Screen --set HmiRectangle_1.BackColor.ValueConverter.MappingTable.Entries[99].Flashing=True --yes --timeout-connect 1800 --timeout-open 3600
EXIT=7
  Cannot resolve 'Entries[99]' in target path 'HmiRectangle_1.BackColor.ValueConverter.MappingTable.Entries[99]': index 99 is out of range — the composition holds 1 element(s). A nested target steps through dynamizations and engineering objects, e.g. 'HmiRectangle_1.BackColor.ValueConverter.MappingTable.Entries[0]'.

==================== P8.18 re-runnable: --map-clear then --map (run 1) ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8Screen --map-clear HmiRectangle_1.BackColor --map HmiRectangle_1.BackColor=Range;From=int:1;To=int:5;Value=color:#FF0000;AlternateValue=color:#0000FF;Flashing=True;FlashingRate=Fast --yes --timeout-connect 1800 --timeout-open 3600
EXIT=0
  EDITED screen 'ZZ_AI_P8Screen' on HMI_1/HMI_RT_1
    changes applied: 2
      map-clear HmiRectangle.BackColor: deleted 1 entry(ies); 0 remain on re-read
      map-entry Create<MappingTableEntryRange>() on HmiRectangle.BackColor [ConditionType Range -> Range, entries=1] set: From=1 (Int32), To=5 (Int32), Value=#FFFF0000 (Color), AlternateValue=#FF0000FF (Color), Flashing=True (Boolean), FlashingRate=Fast (FlashingRate) | read back: MappingTableEntryRange From=1<Int32> To=5<Int32> RangeType=Range<RangeType> Value=#FFFF0000<Color> AlternateValue=#FF0000FF<Color> Flashing=True<Boolean> FlashingRate=Fast<FlashingRate>
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P8.19 re-runnable: the identical command again (run 2) ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8Screen --map-clear HmiRectangle_1.BackColor --map HmiRectangle_1.BackColor=Range;From=int:1;To=int:5;Value=color:#FF0000;AlternateValue=color:#0000FF;Flashing=True;FlashingRate=Fast --yes --timeout-connect 1800 --timeout-open 3600
EXIT=0
  EDITED screen 'ZZ_AI_P8Screen' on HMI_1/HMI_RT_1
    changes applied: 2
      map-clear HmiRectangle.BackColor: deleted 1 entry(ies); 0 remain on re-read
      map-entry Create<MappingTableEntryRange>() on HmiRectangle.BackColor [ConditionType Range -> Range, entries=1] set: From=1 (Int32), To=5 (Int32), Value=#FFFF0000 (Color), AlternateValue=#FF0000FF (Color), Flashing=True (Boolean), FlashingRate=Fast (FlashingRate) | read back: MappingTableEntryRange From=1<Int32> To=5<Int32> RangeType=Range<RangeType> Value=#FFFF0000<Color> AlternateValue=#FF0000FF<Color> Flashing=True<Boolean> FlashingRate=Fast<FlashingRate>
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P8.20 Q4: FRESH read-back of the whole screen ====================
ARGS: hmi <scratch project> --screen ZZ_AI_P8Screen --timeout-connect 1800 --timeout-open 3600
EXIT=0
  HMI DEVICE  HMI_1/HMI_RT_1  [Unified]
  ... [7 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
    SCREEN  ZZ_AI_P8Screen  #0  1280x615  items=5
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
        BackColor <- Tag  tag=ZZ_AI_MapTag
          mapping: ConditionType=Range entries=1 { MappingTableEntryRange From=1<Int32> To=5<Int32> RangeType=Range<RangeType> Value=#FFFF0000<Color> AlternateValue=#FF0000FF<Color> Flashing=True<Boolean> FlashingRate=Fast<FlashingRate> }
        BorderColor <- Tag  tag=ZZ_AI_MapTag
          mapping: ConditionType=Bitmask entries=1 { MappingTableEntryBitmask Condition=1<UInt64> BitDynamizationType=MultiBit<BitDynamizationType> Relevant=1<UInt64> Value=#FF7D7D85<Color> AlternateValue=#FFFF0000<Color> Flashing=False<Boolean> FlashingRate=Medium<FlashingRate> }
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
        Visible <- Tag  tag=ZZ_AI_MapTag
          mapping: ConditionType=None entries=1 { MappingTableEntryRange From=1<Int32> To=1<Int32> RangeType=Range<RangeType> Value=True<Boolean> AlternateValue=(null)<null> Flashing=True<Boolean> FlashingRate=Fast<FlashingRate> }
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
        BackColor <- Tag  tag=ZZ_AI_MapTag
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
        ProcessValue <- Tag  tag=ZZ_AI_MapTag
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
        BackColor <- Tag  tag=ZZ_AI_MapTag
          mapping: ConditionType=Range entries=1 { MappingTableEntryRange From=10<Int32> To=20<Int32> RangeType=Range<RangeType> Value=#FF00FF00<Color> AlternateValue=#FFFF0000<Color> Flashing=True<Boolean> FlashingRate=Medium<FlashingRate> }
  ... [91 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...

==================== P8.21 Q6: compile the HMI device with the mapping tables present ====================
ARGS: hmi-compile <scratch project> --timeout-connect 1800 --timeout-open 3600
EXIT=0
  STATE: Success
  ERRORS: 0  WARNINGS: 156
  NOTE: compiler reported ErrorCount=0, WarningCount=0 — these disagree with the messages above and are not reliable; counts shown are from the message tree.
  ... [5 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  ... [156 pre-existing [Warning] line(s) elided -- same device warnings in every compile] ...
  [Success] Compiling finished (errors: 0; warnings: 0)

==================== P8.22 cleanup: delete the probe screen ====================
ARGS: hmi-delete <scratch project> --kind Screens --name ZZ_AI_P8Screen --yes --timeout-connect 1800 --timeout-open 3600
EXIT=0
  deleted Screens 'ZZ_AI_P8Screen' [HmiScreen] on HMI_1/HMI_RT_1; confirmed absent on re-read

==================== P8.23 cleanup: delete the probe tag ====================
ARGS: hmi-delete <scratch project> --kind Tags --name ZZ_AI_MapTag --yes --timeout-connect 1800 --timeout-open 3600
EXIT=0
  deleted Tags 'ZZ_AI_MapTag' [HmiTag] on HMI_1/HMI_RT_1; confirmed absent on re-read

==================== P8.24 cleanup: delete the probe tag table ====================
ARGS: hmi-delete <scratch project> --kind TagTables --name ZZ_AI_MapTags --yes --timeout-connect 1800 --timeout-open 3600
EXIT=0
  deleted TagTables 'ZZ_AI_MapTags' [HmiTagTable] on HMI_1/HMI_RT_1; confirmed absent on re-read

==================== P8.25 FINAL compile - expect clean ====================
ARGS: hmi-compile <scratch project> --timeout-connect 1800 --timeout-open 3600
EXIT=0
  STATE: Success
  ERRORS: 0  WARNINGS: 156
  NOTE: compiler reported ErrorCount=0, WarningCount=0 — these disagree with the messages above and are not reliable; counts shown are from the message tree.
  ... [5 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  ... [156 pre-existing [Warning] line(s) elided -- same device warnings in every compile] ...
  [Success] Compiling finished (errors: 0; warnings: 0)

==================== P8.26 FINAL inventory - expect PROBE ARTIFACTS: 0 ====================
ARGS: hmi-inventory <scratch project> --timeout-connect 1800 --timeout-open 3600
EXIT=0
  ... [627 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  PROBE ARTIFACTS (ZZ_AI_*): 0
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...

==================== P8.27 set up: fresh screen with two rectangles ====================
ARGS: hmi-create-screen <scratch project> --name ZZ_AI_P8bScreen --item HmiRectangle --item HmiRectangle --yes --timeout-connect 1800 --timeout-open 3600
EXIT=0
  CREATED screen 'ZZ_AI_P8bScreen' on HMI_1/HMI_RT_1
  ... [4 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P8.28 set up: bind-target tag ====================
ARGS: hmi-create-tag <scratch project> --name ZZ_AI_MapTag2 --table ZZ_AI_MapTags2 --datatype Int --yes --timeout-connect 1800 --timeout-open 3600
EXIT=0
  created tag 'ZZ_AI_MapTag2' in table 'ZZ_AI_MapTags2' (table created) on HMI_1/HMI_RT_1; requested type 'Int' -> HmiDataType REFUSED (Error when calling method 'set_HmiDataType' of type 'Siemens.Engineering.HmiUnified.HmiTags.HmiTag'.); HmiDataType now 'Int'

==================== P8.29 set up: bind BackColor and BorderColor, and one Range entry ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8bScreen --bind HmiRectangle_1.BackColor=ZZ_AI_MapTag2 --bind HmiRectangle_1.BorderColor=ZZ_AI_MapTag2 --bind HmiRectangle_2.BackColor=ZZ_AI_MapTag2 --map HmiRectangle_1.BackColor=Range;From=int:1;To=int:5;Value=color:#FF0000;Flashing=True;FlashingRate=Fast --yes --timeout-connect 1800 --timeout-open 3600
EXIT=0
  EDITED screen 'ZZ_AI_P8bScreen' on HMI_1/HMI_RT_1
    changes applied: 4
      bind created HmiRectangle.BackColor <- tag 'ZZ_AI_MapTag2' [resolved: plcTag='' dataType='Int']
      bind created HmiRectangle.BorderColor <- tag 'ZZ_AI_MapTag2' [resolved: plcTag='' dataType='Int']
      bind created HmiRectangle.BackColor <- tag 'ZZ_AI_MapTag2' [resolved: plcTag='' dataType='Int']
      map-entry Create<MappingTableEntryRange>() on HmiRectangle.BackColor [ConditionType None -> None, entries=1] set: From=1 (Int32), To=5 (Int32), Value=#FFFF0000 (Color), Flashing=True (Boolean), FlashingRate=Fast (FlashingRate) | read back: MappingTableEntryRange From=1<Int32> To=5<Int32> RangeType=Range<RangeType> Value=#FFFF0000<Color> AlternateValue=#FFFF0000<Color> Flashing=True<Boolean> FlashingRate=Fast<FlashingRate>
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P8.30 Q3a: Value as a plain String, alone ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8bScreen --set HmiRectangle_1.BackColor.ValueConverter.MappingTable.Entries[0].Value=str:Lime --yes --timeout-connect 1800 --timeout-open 3600
EXIT=5
  openness-cli hmi-edit-screen failed: EngineeringTargetInvocationException: Error when calling method 'set_Value' of type 'Siemens.Engineering.HmiUnified.UI.Dynamization.Tag.MappingTableEntryRange'.
  -Invalid value type.

==================== P8.31 Q3b: Value as an Int32, alone ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8bScreen --set HmiRectangle_1.BackColor.ValueConverter.MappingTable.Entries[0].Value=int:65280 --yes --timeout-connect 1800 --timeout-open 3600
EXIT=5
  openness-cli hmi-edit-screen failed: EngineeringTargetInvocationException: Error when calling method 'set_Value' of type 'Siemens.Engineering.HmiUnified.UI.Dynamization.Tag.MappingTableEntryRange'.
  -Invalid value type.

==================== P8.32 Q3c: Value as a Color, alone (positive control for the setter) ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8bScreen --set HmiRectangle_1.BackColor.ValueConverter.MappingTable.Entries[0].Value=color:#00FFFF --yes --timeout-connect 1800 --timeout-open 3600
EXIT=0
  EDITED screen 'ZZ_AI_P8bScreen' on HMI_1/HMI_RT_1
    changes applied: 1
      set HmiRectangle_1.BackColor.ValueConverter.MappingTable.Entries[0].Value = #FF00FFFF (Color)
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P8.33 Q3d: AlternateValue as a Color, alone ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8bScreen --set HmiRectangle_1.BackColor.ValueConverter.MappingTable.Entries[0].AlternateValue=color:#0000FF --yes --timeout-connect 1800 --timeout-open 3600
EXIT=0
  EDITED screen 'ZZ_AI_P8bScreen' on HMI_1/HMI_RT_1
    changes applied: 1
      set HmiRectangle_1.BackColor.ValueConverter.MappingTable.Entries[0].AlternateValue = #FF0000FF (Color)
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P8.34 Q3e: Flashing and FlashingRate set post-create, alone ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8bScreen --set HmiRectangle_1.BackColor.ValueConverter.MappingTable.Entries[0].Flashing=True --set HmiRectangle_1.BackColor.ValueConverter.MappingTable.Entries[0].FlashingRate=Slow --yes --timeout-connect 1800 --timeout-open 3600
EXIT=0
  EDITED screen 'ZZ_AI_P8bScreen' on HMI_1/HMI_RT_1
    changes applied: 2
      set HmiRectangle_1.BackColor.ValueConverter.MappingTable.Entries[0].Flashing = True (Boolean)
      set HmiRectangle_1.BackColor.ValueConverter.MappingTable.Entries[0].FlashingRate = Slow (FlashingRate)
    Validate(): ran, returned no errors and no warnings
    project saved: yes

==================== P8.35 CRASH ISOLATION: Simple entry on a DIFFERENT item and property ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8bScreen --map HmiRectangle_2.BackColor=Simple --yes --timeout-connect 1800 --timeout-open 3600
EXIT=5
  openness-cli hmi-edit-screen failed: EngineeringObjectDisposedException: Access to a disposed object of type 'Siemens.Engineering.Project' is not possible.
  TIA Portal has either been disposed or stopped running.

==================== P8.36 CRASH ISOLATION: a Range entry on that same target (did the target break, or the type?) ====================
ARGS: hmi-edit-screen <scratch project> --name ZZ_AI_P8bScreen --map HmiRectangle_2.BackColor=Range;From=int:7;To=int:9;Value=color:#00FF00;Flashing=True --yes --timeout-connect 1800 --timeout-open 3600
EXIT=0

==================== P8.37 FRESH read-back ====================
ARGS: hmi <scratch project> --screen ZZ_AI_P8bScreen --timeout-connect 1800 --timeout-open 3600
EXIT=0
  HMI DEVICE  HMI_1/HMI_RT_1  [Unified]
  ... [7 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
    SCREEN  ZZ_AI_P8bScreen  #0  1280x615  items=2
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
        BackColor <- Tag  tag=ZZ_AI_MapTag2
          mapping: ConditionType=None entries=1 { MappingTableEntryRange From=1<Int32> To=5<Int32> RangeType=Range<RangeType> Value=#FF00FFFF<Color> AlternateValue=#FF0000FF<Color> Flashing=True<Boolean> FlashingRate=Slow<FlashingRate> }
        BorderColor <- Tag  tag=ZZ_AI_MapTag2
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
        BackColor <- Tag  tag=ZZ_AI_MapTag2
          mapping: ConditionType=None entries=1 { MappingTableEntryRange From=7<Int32> To=9<Int32> RangeType=Range<RangeType> Value=#FF00FF00<Color> AlternateValue=#FFFF0000<Color> Flashing=True<Boolean> FlashingRate=Medium<FlashingRate> }
  ... [90 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...

==================== P8.38 compile ====================
ARGS: hmi-compile <scratch project> --timeout-connect 1800 --timeout-open 3600
EXIT=0
  STATE: Success
  ERRORS: 0  WARNINGS: 156
  NOTE: compiler reported ErrorCount=0, WarningCount=0 — these disagree with the messages above and are not reliable; counts shown are from the message tree.
  ... [5 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  ... [156 pre-existing [Warning] line(s) elided -- same device warnings in every compile] ...
  [Success] Compiling finished (errors: 0; warnings: 0)

==================== P8.39 cleanup: delete screen ====================
ARGS: hmi-delete <scratch project> --kind Screens --name ZZ_AI_P8bScreen --yes --timeout-connect 1800 --timeout-open 3600
EXIT=0
  deleted Screens 'ZZ_AI_P8bScreen' [HmiScreen] on HMI_1/HMI_RT_1; confirmed absent on re-read

==================== P8.40 cleanup: delete tag ====================
ARGS: hmi-delete <scratch project> --kind Tags --name ZZ_AI_MapTag2 --yes --timeout-connect 1800 --timeout-open 3600
EXIT=0
  deleted Tags 'ZZ_AI_MapTag2' [HmiTag] on HMI_1/HMI_RT_1; confirmed absent on re-read

==================== P8.41 cleanup: delete tag table ====================
ARGS: hmi-delete <scratch project> --kind TagTables --name ZZ_AI_MapTags2 --yes --timeout-connect 1800 --timeout-open 3600
EXIT=0
  deleted TagTables 'ZZ_AI_MapTags2' [HmiTagTable] on HMI_1/HMI_RT_1; confirmed absent on re-read

==================== P8.42 FINAL compile - expect clean ====================
ARGS: hmi-compile <scratch project> --timeout-connect 1800 --timeout-open 3600
EXIT=0
  STATE: Success
  ERRORS: 0  WARNINGS: 156
  NOTE: compiler reported ErrorCount=0, WarningCount=0 — these disagree with the messages above and are not reliable; counts shown are from the message tree.
  ... [5 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  ... [156 pre-existing [Warning] line(s) elided -- same device warnings in every compile] ...
  [Success] Compiling finished (errors: 0; warnings: 0)

==================== P8.43 FINAL inventory - expect PROBE ARTIFACTS: 0 ====================
ARGS: hmi-inventory <scratch project> --timeout-connect 1800 --timeout-open 3600
EXIT=0
  ... [627 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
  PROBE ARTIFACTS (ZZ_AI_*): 0
  ... [1 line(s) elided -- listings of pre-existing project objects, redacted per docs/13] ...
```

## P10 -- the project library, and the faceplate question settled (2026-08-09)

Headline: **hypothesis FALSE.** Unified faceplates are plain `LibraryType`, NOT
`FaceplateLibraryType`, and `GetSupportedExportFormats()` is EMPTY for every one -- so there is no
document round trip for them. The negative control is in the same read: PLC types and code blocks
in the SAME library return full format lists, so the mechanism works and is simply not offered for
HMI content. Also visible: every faceplate type reports `DefaultVersionInconsistent` while every
PLC type reports `Consistent`.

**ANONYMISED, not whitelisted** -- type and folder names are restricted content and are
replaced by stable invented labels grouped by CLR class (docs/13). Every structural field that
carries the finding -- CLR class, status, export formats, version numbers and states -- is verbatim.

```
PROJECT LIBRARY — types: 23  masterCopies: 0
  Folder_1  HmiType_1  [LibraryType]  status=DefaultVersionInconsistent
      exportFormats: (NONE — no document round trip for this type)
      v0.0.1  Committed  [LibraryTypeVersion]
      v0.0.2  Committed  [LibraryTypeVersion]
      v0.0.3  Committed  [LibraryTypeVersion]
      v0.0.4  Committed  (default)  [LibraryTypeVersion]
      v0.0.5  InWork  [LibraryTypeVersion]
  Folder_1  HmiType_2  [LibraryType]  status=DefaultVersionInconsistent
      exportFormats: (NONE — no document round trip for this type)
      v0.0.1  Committed  [LibraryTypeVersion]
      v0.0.2  Committed  [LibraryTypeVersion]
      v0.0.3  Committed  [LibraryTypeVersion]
      v0.0.4  Committed  [LibraryTypeVersion]
      v0.0.5  Committed  [LibraryTypeVersion]
      v0.0.6  Committed  [LibraryTypeVersion]
      v0.0.7  Committed  [LibraryTypeVersion]
      v0.0.8  Committed  [LibraryTypeVersion]
      v0.0.9  Committed  [LibraryTypeVersion]
      v0.0.10  Committed  [LibraryTypeVersion]
      v0.0.11  Committed  [LibraryTypeVersion]
      v0.0.12  Committed  [LibraryTypeVersion]
      v0.0.13  Committed  [LibraryTypeVersion]
      v0.0.14  Committed  [LibraryTypeVersion]
      v0.0.15  Committed  (default)  [LibraryTypeVersion]
  Folder_1  HmiType_3  [LibraryType]  status=DefaultVersionInconsistent
      exportFormats: (NONE — no document round trip for this type)
      v0.0.1  Committed  [LibraryTypeVersion]
      v0.0.2  Committed  [LibraryTypeVersion]
      v0.0.3  Committed  [LibraryTypeVersion]
      v0.0.4  Committed  (default)  [LibraryTypeVersion]
      v0.0.5  InWork  [LibraryTypeVersion]
  Folder_1  HmiType_4  [LibraryType]  status=DefaultVersionInconsistent
      exportFormats: (NONE — no document round trip for this type)
      v0.0.1  Committed  [LibraryTypeVersion]
      v0.0.2  Committed  [LibraryTypeVersion]
      v0.0.3  Committed  [LibraryTypeVersion]
      v0.0.4  Committed  [LibraryTypeVersion]
      v0.0.5  Committed  [LibraryTypeVersion]
      v0.0.6  Committed  [LibraryTypeVersion]
      v0.0.7  Committed  [LibraryTypeVersion]
      v0.0.8  Committed  [LibraryTypeVersion]
      v0.0.9  Committed  [LibraryTypeVersion]
      v0.0.10  Committed  [LibraryTypeVersion]
      v0.0.11  Committed  (default)  [LibraryTypeVersion]
  Folder_2  HmiType_5  [LibraryType]  status=Consistent
      exportFormats: (NONE — no document round trip for this type)
      v0.0.1  Committed  (default)  [LibraryTypeVersion]
  Folder_2  HmiType_6  [LibraryType]  status=Consistent
      exportFormats: (NONE — no document round trip for this type)
      v0.0.1  Committed  (default)  [LibraryTypeVersion]
  Folder_2  HmiType_7  [LibraryType]  status=Consistent
      exportFormats: (NONE — no document round trip for this type)
      v0.0.1  Committed  (default)  [LibraryTypeVersion]
  Folder_3  PlcTypeType_1  [PlcTypeLibraryType]  status=Consistent
      exportFormats: SimaticMLWithExportOptionsNone, SimaticMLWithExportOptionsWithDefaults, SimaticMLWithExportOptionsWithReadOnly, SimaticMLWithExportOptionsWithoutDocumentInfo, UDT, SimaticSD
      v0.0.4  Committed  (default)  [PlcTypeLibraryTypeVersion]
  Folder_3  PlcTypeType_2  [PlcTypeLibraryType]  status=Consistent
      exportFormats: SimaticMLWithExportOptionsNone, SimaticMLWithExportOptionsWithDefaults, SimaticMLWithExportOptionsWithReadOnly, SimaticMLWithExportOptionsWithoutDocumentInfo, UDT, SimaticSD
      v0.0.1  Committed  [PlcTypeLibraryTypeVersion]
      v0.0.2  Committed  (default)  [PlcTypeLibraryTypeVersion]
  Folder_3  PlcTypeType_3  [PlcTypeLibraryType]  status=Consistent
      exportFormats: SimaticMLWithExportOptionsNone, SimaticMLWithExportOptionsWithDefaults, SimaticMLWithExportOptionsWithReadOnly, SimaticMLWithExportOptionsWithoutDocumentInfo, UDT, SimaticSD
      v0.0.1  Committed  [PlcTypeLibraryTypeVersion]
      v0.0.2  Committed  [PlcTypeLibraryTypeVersion]
      v0.0.3  Committed  [PlcTypeLibraryTypeVersion]
      v0.0.4  Committed  [PlcTypeLibraryTypeVersion]
      v0.0.5  Committed  (default)  [PlcTypeLibraryTypeVersion]
  Folder_3  PlcTypeType_4  [PlcTypeLibraryType]  status=Consistent
      exportFormats: SimaticMLWithExportOptionsNone, SimaticMLWithExportOptionsWithDefaults, SimaticMLWithExportOptionsWithReadOnly, SimaticMLWithExportOptionsWithoutDocumentInfo, UDT, SimaticSD
      v0.0.1  Committed  [PlcTypeLibraryTypeVersion]
      v0.0.2  Committed  (default)  [PlcTypeLibraryTypeVersion]
  Folder_3  PlcTypeType_5  [PlcTypeLibraryType]  status=Consistent
      exportFormats: SimaticMLWithExportOptionsNone, SimaticMLWithExportOptionsWithDefaults, SimaticMLWithExportOptionsWithReadOnly, SimaticMLWithExportOptionsWithoutDocumentInfo, UDT, SimaticSD
      v0.0.1  Committed  [PlcTypeLibraryTypeVersion]
      v0.0.2  Committed  (default)  [PlcTypeLibraryTypeVersion]
  Folder_4  HmiType_8  [LibraryType]  status=DefaultVersionInconsistent
      exportFormats: (NONE — no document round trip for this type)
      v0.0.1  Committed  [LibraryTypeVersion]
      v0.0.2  Committed  [LibraryTypeVersion]
      v0.0.3  Committed  [LibraryTypeVersion]
      v0.0.4  Committed  [LibraryTypeVersion]
      v0.0.5  Committed  [LibraryTypeVersion]
      v0.0.6  Committed  [LibraryTypeVersion]
      v0.0.7  Committed  (default)  [LibraryTypeVersion]
      v0.0.8  InWork  [LibraryTypeVersion]
  Folder_4  HmiType_9  [LibraryType]  status=DefaultVersionInconsistent
      exportFormats: (NONE — no document round trip for this type)
      v0.0.1  Committed  [LibraryTypeVersion]
      v0.0.2  Committed  [LibraryTypeVersion]
      v0.0.3  Committed  (default)  [LibraryTypeVersion]
      v0.0.4  InWork  [LibraryTypeVersion]
  Folder_4  HmiType_10  [LibraryType]  status=DefaultVersionInconsistent
      exportFormats: (NONE — no document round trip for this type)
      v0.0.1  Committed  [LibraryTypeVersion]
      v0.0.2  Committed  [LibraryTypeVersion]
      v0.0.3  Committed  [LibraryTypeVersion]
      v0.0.4  Committed  [LibraryTypeVersion]
      v0.0.5  Committed  [LibraryTypeVersion]
      v0.0.6  Committed  [LibraryTypeVersion]
      v0.0.7  Committed  [LibraryTypeVersion]
      v0.0.8  Committed  [LibraryTypeVersion]
      v0.0.9  Committed  [LibraryTypeVersion]
      v0.0.10  Committed  [LibraryTypeVersion]
      v0.0.11  Committed  [LibraryTypeVersion]
      v0.0.12  Committed  [LibraryTypeVersion]
      v0.0.13  Committed  [LibraryTypeVersion]
      v0.0.14  Committed  (default)  [LibraryTypeVersion]
  Folder_4  HmiType_11  [LibraryType]  status=DefaultVersionInconsistent
      exportFormats: (NONE — no document round trip for this type)
      v0.0.1  Committed  [LibraryTypeVersion]
      v0.0.2  Committed  [LibraryTypeVersion]
      v0.0.3  Committed  [LibraryTypeVersion]
      v0.0.4  Committed  (default)  [LibraryTypeVersion]
  Folder_5  HmiType_12  [LibraryType]  status=DefaultVersionInconsistent
      exportFormats: (NONE — no document round trip for this type)
      v0.0.1  Committed  [LibraryTypeVersion]
      v0.0.2  Committed  (default)  [LibraryTypeVersion]
      v0.0.3  InWork  [LibraryTypeVersion]
  Folder_5  HmiType_13  [LibraryType]  status=DefaultVersionInconsistent
      exportFormats: (NONE — no document round trip for this type)
      v0.0.1  Committed  [LibraryTypeVersion]
      v0.0.2  Committed  [LibraryTypeVersion]
      v0.0.3  Committed  [LibraryTypeVersion]
      v0.0.4  Committed  [LibraryTypeVersion]
      v0.0.5  Committed  [LibraryTypeVersion]
      v0.0.6  Committed  [LibraryTypeVersion]
      v0.0.7  Committed  [LibraryTypeVersion]
      v0.0.8  Committed  (default)  [LibraryTypeVersion]
  Folder_5  HmiType_14  [LibraryType]  status=DefaultVersionInconsistent
      exportFormats: (NONE — no document round trip for this type)
      v0.0.1  Committed  [LibraryTypeVersion]
      v0.0.2  Committed  [LibraryTypeVersion]
      v0.0.3  Committed  [LibraryTypeVersion]
      v0.0.4  Committed  [LibraryTypeVersion]
      v0.0.5  Committed  (default)  [LibraryTypeVersion]
  Folder_5  HmiType_15  [LibraryType]  status=DefaultVersionInconsistent
      exportFormats: (NONE — no document round trip for this type)
      v0.0.1  Committed  [LibraryTypeVersion]
      v0.0.2  Committed  (default)  [LibraryTypeVersion]
      v0.0.3  InWork  [LibraryTypeVersion]
  Folder_6  CodeBlockType_1  [CodeBlockLibraryType]  status=Consistent
      exportFormats: SimaticMLWithExportOptionsNone, SimaticMLWithExportOptionsWithDefaults, SimaticMLWithExportOptionsWithReadOnly, SimaticMLWithExportOptionsWithoutDocumentInfo, SimaticSD, SCL
      v3.0.2  Committed  (default)  [CodeBlockLibraryTypeVersion]
  Folder_7  PlcTypeType_6  [PlcTypeLibraryType]  status=Consistent
      exportFormats: SimaticMLWithExportOptionsNone, SimaticMLWithExportOptionsWithDefaults, SimaticMLWithExportOptionsWithReadOnly, SimaticMLWithExportOptionsWithoutDocumentInfo, UDT, SimaticSD
      v1.0.0  Committed  (default)  [PlcTypeLibraryTypeVersion]
  Folder_7  PlcTypeType_7  [PlcTypeLibraryType]  status=Consistent
      exportFormats: SimaticMLWithExportOptionsNone, SimaticMLWithExportOptionsWithDefaults, SimaticMLWithExportOptionsWithReadOnly, SimaticMLWithExportOptionsWithoutDocumentInfo, UDT, SimaticSD
      v1.0.0  Committed  (default)  [PlcTypeLibraryTypeVersion]
```

Anonymised: 23 type names, 7 folder names.

> ⚠️ **This anonymisation was later found to be LOSSY in a way that misled a reader.** It erased the
> distinction between a **faceplate** type and an **image-file** type, both rendered `HmiType_n`. A
> follow-up agent read this transcript, saw three `Consistent` `HmiType_n` entries, and recommended
> probing "a `Consistent` faceplate" — which does not exist in this project; the three `Consistent`
> entries are images, and **every** faceplate is `DefaultVersionInconsistent`. **When anonymising,
> preserve the KIND even while erasing the NAME.** See P11 below.

---

## P11 — faceplate instantiation, live (2026-08-09)

**Scope:** reference project's **scratch copy**, invented `ZZ_AI_*` names, under
`docs/13-data-boundary.md`'s write extension. Library type and screen names below are
**genericized** — the real ones identify a production plant. Structure, sizes, error text and
sequence are verbatim.

**Q1 — the load-bearing question — answered YES.** Full analysis in
`docs/notes/hmi-faceplate-gap-probe.md`'s verdict box; this is the transcript.

```
# 1. create — a container plus a Custom Web Control container in one call
$ openness-cli hmi-create-screen <project> --name ZZ_AI_FP_Probe --width 1920 --height 1080 \
      --item HmiFaceplateContainer --item HmiCustomWebControlContainer --yes

openness-cli hmi-create-screen failed: TargetInvocationException: ... --->
  EngineeringTargetInvocationException: Error when calling method 'Create' of type
  'Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition'.
'ContainedTypeValue' parameter is missing. Please use correct method for object creation.
```

**Read carefully — this refusal came from the SECOND item, not the first.** No transaction exists,
so the screen and the faceplate container survived; the read-back below shows `items=1`. The
`ContainedTypeValue` requirement is a fact about **`HmiCustomWebControlContainer`**, and the
one-argument `Create<T>` is sufficient for `HmiFaceplateContainer`. *(This was initially attributed
to the faceplate container — wrong, and corrected by one cheap read.)*

```
# 2. read back — the container survived
  SCREEN  ZZ_AI_FP_Probe  #0  1920x1080  items=1
    HmiFaceplateContainer  HmiFaceplateContainer_1  @0,0  120x80

# 3. point it at a real, human-authored library faceplate type
$ openness-cli hmi-edit-screen <project> --name ZZ_AI_FP_Probe \
      --set HmiFaceplateContainer_1.ContainedType=<FP_MotorMain> --yes
EDITED screen 'ZZ_AI_FP_Probe'
  changes applied: 1
    set HmiFaceplateContainer_1.ContainedType = <FP_MotorMain> (String)
  Validate(): ran, returned no errors and no warnings
  project saved: yes

# 4. THE GATE
$ openness-cli hmi-compile <project>
STATE: Success
ERRORS: 0  WARNINGS: 156
# and NO occurrence of "faceplate", "ZZ_AI" or "does not exist" anywhere in the 166-line tree.
# P2's "The referenced faceplate type does not exist" is GONE.

# 5. read back from a FRESH process — the item RESIZED ITSELF
    HmiFaceplateContainer  HmiFaceplateContainer_1  @0,0  300x430
# 120x80 -> 300x430, which is exactly the size of every *Main screen in this project.
# The type's geometry was adopted. Physical confirmation, independent of the compile.

# 6. the parameter list populates once a type resolves
$ ... --set "HmiFaceplateContainer_1.Interface[0].Value=str:ZZ_AI_TEST" --yes
    set HmiFaceplateContainer_1.Interface[0].Value = ZZ_AI_TEST (String)
  Validate(): ran, returned no errors and no warnings      <-- Validate() is BLIND to this. See 8.

# 7. P7's missing negative control, finally runnable, plus an arity probe
$ ... --bind-kind "HmiFaceplateContainer_1.Interface[0].Value=TagParameterDynamization" \
      --bind-kind "HmiFaceplateContainer_1.Interface[1].Value=FlashingDynamization" --yes
  changes applied: 0 (2 REFUSED)
    TagParameterDynamization on ...Interface[0].Value -> REFUSED (EngineeringTargetInvocationException:
      Error when calling method 'Create' of type '...Dynamization.DynamizationBaseComposition'.)
    FlashingDynamization on ...Interface[1].Value -> REFUSED (HmiTargetPathNotResolvableException:
      index 1 is out of range - the composition holds 1 element(s).)
# TagParameter refuses even INSIDE a faceplate parameter - P7's conclusion survives its first real
# test, and still names no reason. The second refusal is the useful one: ARITY = 1.

# 8. does the compile check the parameter VALUE, or only the type reference?
$ openness-cli hmi-compile <project>
STATE: Error
ERRORS: 6  WARNINGS: 156
[Error] ZZ_AI_FP_Probe:
[Error] HmiFaceplateContainer_1:
[Error] Interface.IO: The object "ZZ_AI_TEST" at the property "IO" does not exist.
        Please select an existing object.
# IT CHECKS. The single parameter is named IO and wants an EXISTING OBJECT, not a literal.
# Located screen -> item -> property. This is a specification a generator can consume.

# 9. cleanup, and the mandatory post-delete compile
$ openness-cli hmi-delete <project> --kind Screens --name ZZ_AI_FP_Probe --yes
deleted Screens 'ZZ_AI_FP_Probe' [HmiScreen] on <device>; confirmed absent on re-read

$ openness-cli hmi-compile <project>
STATE: Success
ERRORS: 0  WARNINGS: 156        <-- identical to the pre-probe baseline; 0 ZZ_AI residue
```

**Counts lied again, in both directions:** step 4 self-reported `ErrorCount=0, WarningCount=0`
against 156 warnings in the tree; step 8 reported `ErrorCount=1` against 6. Gate on `State`, count
from the message tree.

Genericized: 1 library type name, screen-name family. Sizes, error strings and sequence verbatim.

