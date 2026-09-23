// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.TrayApp;

using System;

/// <summary>
/// Tells a failure the library is meant to absorb from one it must not.
/// </summary>
/// <remarks>
/// <para>
/// The library catches broadly on purpose wherever it runs the consuming tool's own code: a toggle setter,
/// a command, a start action. There is no exception type to filter on there - the tool's platform layer
/// throws whatever it throws - and the whole point of this library is that a refusal ends in a message
/// rather than in a dead process.
/// </para>
/// <para>
/// That reasoning stops at an exception which says the process is already lost. Reporting one in a menu and
/// carrying on presents a tray that cannot work, and buries the real cause under whatever fails next.
/// <see cref="StackOverflowException"/> is absent because .NET does not let managed code catch it, and
/// <see cref="InsufficientMemoryException"/> is deliberately not fatal: it means one allocation was refused
/// before being attempted, not that the process is out of memory.
/// </para>
/// </remarks>
internal static class FatalError
{
	/// <summary>
	/// Determines whether an exception is one the library may absorb.
	/// </summary>
	/// <param name="exception">The exception the tool's code threw.</param>
	/// <returns><see langword="true"/> when it may be reported and survived.</returns>
	internal static bool IsNotFatal(Exception exception) => exception switch
	{
		InsufficientMemoryException => true,
		OutOfMemoryException or AccessViolationException => false,
		_ => true,
	};
}
