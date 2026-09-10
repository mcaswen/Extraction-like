# Agent reproduction runner

Run from PowerShell: `./tools/agent-repro/Invoke-AgentRepro.ps1 -Mode Regression -Suite Smoke`.

The runner uses the exact project Unity version, an owned copy on D: and a separate persistent-data identity. It never opens or stops the user's editor. Output is under `Logs/AgentReproduction/<run-id>/`: source manifest, Unity logs, NUnit XML, per-case evidence, JSON summary and Markdown report.

`-Group`, `-TestFilter`, `-Repeat`, `-UnityPath`, `-WorkspaceRoot`, `-TimeoutSeconds` and `-IncludeGraphics` support targeted diagnosis. Diagnose exit 0 means valid execution, not correct gameplay. Regression fails on any non-passing test. Exit 2 means missing tests/results, timeout or an environment error. Every repeat is retained.

Only the runner-owned workspace is mirrored. Its ownership marker and exclusive file lock protect other projects. Successful or failed workspaces are retained for investigation and import-cache reuse; no source saves or PlayerPrefs are cleared. The coding agent is responsible for launching runs, examining evidence and fixing failures.

Infrastructure fault checks: `-Suite Smoke -FaultProbe Assertion` intentionally fails the smoke assertion; `-Suite Smoke -FaultProbe Timeout -Repeat 2 -TimeoutSeconds 30` hangs only the first repeat, so the runner must recover its process and continue the second. These switches never run in normal gameplay regression.
