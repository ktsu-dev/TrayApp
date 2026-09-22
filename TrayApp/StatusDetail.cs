// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.TrayApp;

using System;

/// <summary>
/// One extra row in the <c>--status</c> report.
/// </summary>
/// <param name="Label">The row's label, without a trailing colon.</param>
/// <param name="Value">Produces the row's value when the report is built.</param>
public sealed record StatusDetail(string Label, Func<string> Value);
