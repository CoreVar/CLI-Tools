# Launcher smoke fixture

The release workflow copies the Native-AOT command-line smoke executable into `versions/1.0.0/smoke.exe`, sets `COREVAR_CLI_HOME` to this directory, and executes it through the Native-AOT launcher. The binary is generated and is not committed.
