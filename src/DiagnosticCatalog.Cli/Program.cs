using DiagnosticCatalog.Cli;

// The command tree, its exception handling and its exit codes live in CliApplication, so a test can
// reach them without launching a process. Spectre handles argument parsing and --help; a run's own
// failures are handled inside the command, so the tool reports them as a terse "error: …" line
// rather than a stack trace.
return await CliApplication.RunAsync(args);
