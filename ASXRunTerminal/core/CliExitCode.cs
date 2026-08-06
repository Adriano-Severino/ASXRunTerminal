namespace ASXRunTerminal.Core;

internal enum CliExitCode
{
    Success = 0,
    RuntimeError = 1,
    InvalidArguments = 2,
    GeneralError = 3,
    Cancelled = 130
}
