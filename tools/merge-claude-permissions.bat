@echo off
REM ---------------------------------------------------------------------------
REM Merge Claude Code permission rules from the JOB9004-marker-db worktree's
REM settings.local.json into the main checkout's.
REM
REM WHY: .claude/settings.local.json is PER DIRECTORY. A worktree has its own, so
REM leaving the worktree silently loses every permission rule granted there - the
REM tooling still exists and is still approved, but nothing may run it. Measured
REM 2026-08-12: exiting dropped 16 rules in one step and said nothing.
REM
REM SAFE TO RUN, AND SAFE TO RUN TWICE:
REM   - only ADDS rules; never removes, reorders or edits an existing one
REM   - idempotent - a second run adds nothing
REM   - backs the target up with a timestamp before writing
REM   - parses both files first, and RE-PARSES its own output before swapping it
REM     in, because a malformed settings.local.json silently disables EVERY rule
REM     in it with no error at all
REM
REM ASCII only, CRLF. PowerShell 5.1 reads a BOM-less script as ANSI, so a UTF-8
REM em dash becomes a quote delimiter and the parser fails a hundred lines away.
REM Batch is no better. Do not "improve" the punctuation in this file.
REM
REM Optional args: %1 = source settings.local.json, %2 = target
REM ---------------------------------------------------------------------------

setlocal
set "SCRIPT_DIR=%~dp0"

where python >nul 2>&1
if errorlevel 1 (
    echo REFUSED: python was not found on PATH. Nothing was changed.
    exit /b 1
)

echo.
echo Merging Claude Code permission rules...
echo.

python "%SCRIPT_DIR%merge-claude-permissions.py" %1 %2
set "RESULT=%ERRORLEVEL%"

echo.
if "%RESULT%"=="0" (
    echo Done. Exit code 0.
) else (
    echo REFUSED - nothing was written. Exit code %RESULT%.
)

endlocal & exit /b %RESULT%
