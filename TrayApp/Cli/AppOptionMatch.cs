// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.TrayApp.Cli;

/// <summary>
/// One application-specific option the parser found, with the value it was given.
/// </summary>
/// <param name="Option">The option that matched.</param>
/// <param name="Value">The value read from the command line, or <see langword="null"/> for a switch.</param>
public sealed record AppOptionMatch(AppOption Option, string? Value);
