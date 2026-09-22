// Copyright (c) 2026 ktsu-dev contributors

namespace ktsu.TrayApp.Tray;

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ktsu.TrayApp.Menu;

/// <summary>
/// The tray host: one icon, one menu, no windows.
/// </summary>
/// <remarks>
/// <para>
/// Avalonia gives all three desktop platforms the same tray API - <c>Shell_NotifyIcon</c> on Windows,
/// <c>NSStatusItem</c> on macOS, and a StatusNotifierItem over D-Bus on Linux - which is the whole reason it
/// is here. Nothing else in this library needs a UI framework.
/// </para>
/// <para>
/// There is no XAML: the menu is built in code and <c>AvaloniaXamlLoader</c> is never called, so
/// <see cref="Initialize"/> has nothing to do. A tray icon and a native menu need no styles or theme, and
/// skipping XAML keeps a tool's payload and its startup cost down.
/// </para>
/// </remarks>
internal sealed class TrayApplication : Application, IDisposable
{
	private readonly TrayAppDefinition app;
	private readonly TrayMenu menu;
	private readonly TimeSpan? duration;
	private readonly Action<TrayToggleItem>? onToggled;

	// The native items exist from construction so the refresh path has nothing to null-check; only the tray
	// icon itself has to wait, because its constructor reaches for a platform handle that does not exist
	// until Avalonia has finished starting.
	private readonly List<(TrayMenuItem Model, NativeMenuItem Native)> entries = [];
	private readonly NativeMenu nativeMenu = [];

	private TrayIcon? trayIcon;
	private DispatcherTimer? expiryTimer;
	private bool disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="TrayApplication"/> class.
	/// </summary>
	/// <param name="app">The tool being run.</param>
	/// <param name="menu">The menu model to show.</param>
	/// <param name="duration">How long to run before quitting, or <see langword="null"/> to run until asked to stop.</param>
	/// <param name="onToggled">Called after a toggle changes, so the caller can remember it.</param>
	internal TrayApplication(TrayAppDefinition app, TrayMenu menu, TimeSpan? duration, Action<TrayToggleItem>? onToggled)
	{
		Ensure.NotNull(app);
		Ensure.NotNull(menu);

		this.app = app;
		this.menu = menu;
		this.duration = duration;
		this.onToggled = onToggled;

		BuildNativeMenu();
	}

	/// <summary>
	/// Gets a value indicating whether the tray icon came up.
	/// </summary>
	/// <remarks>
	/// Avalonia's Linux tray backend watches the D-Bus status-notifier host from an <c>async void</c> method
	/// whose exception filter stops applying once the icon is disposed, so a perfectly ordinary shutdown can
	/// throw a <see cref="System.Threading.Tasks.TaskCanceledException"/> out of the run loop after the tray
	/// has already done its job. This is how the caller tells that apart from a tray that never started.
	/// </remarks>
	internal bool HasStarted { get; private set; }

	/// <inheritdoc/>
	public override void Initialize() => Name = app.DisplayName;

	/// <inheritdoc/>
	public override void OnFrameworkInitializationCompleted()
	{
		menu.QuitRequested += OnQuitRequested;

		trayIcon = new TrayIcon { Menu = nativeMenu, IsVisible = true };

		// A left click is the fastest way to flip the first switch on Windows. Linux status-notifier hosts
		// and macOS mostly open the menu instead, which is why the menu carries the same toggle.
		trayIcon.Clicked += OnTrayIconClicked;

		// Registering the icon with the application is what disposes it on shutdown; a bare TrayIcon left
		// behind outlives the process on some Linux panels.
		TrayIcon.SetIcons(this, [trayIcon]);

		// Started here rather than before the run so that a tool whose start action throws still gets a tray
		// icon, with the refusal in its status line, instead of a process that exits before the user sees
		// anything. Run() puts the message where RefreshMenu will pick it up.
		if (app.OnStart is not null)
		{
			menu.Run(app.OnStart);
		}

		// The tool's own state can change with nobody having clicked anything - a timer expiring, a device
		// going away - and the menu only ever reads through getters, so all it needs is a nudge.
		app.RefreshSubscription?.Invoke(() => Dispatcher.UIThread.Post(RefreshMenu));

		ApplyDuration();
		RefreshMenu();

		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.Exit += OnExit;
		}

		HasStarted = true;

		base.OnFrameworkInitializationCompleted();
	}

	/// <summary>
	/// Stops the tray icon and runs the tool's stop action.
	/// </summary>
	public void Dispose()
	{
		if (disposed)
		{
			return;
		}

		disposed = true;

		expiryTimer?.Stop();
		menu.QuitRequested -= OnQuitRequested;

		if (HasStarted)
		{
			app.OnStop?.Invoke();
		}

		trayIcon?.Dispose();
		trayIcon = null;
	}

	private void BuildNativeMenu()
	{
		foreach (TrayMenuItem item in menu.Items)
		{
			if (item is TraySeparatorItem)
			{
				nativeMenu.Add(new NativeMenuItemSeparator());
				continue;
			}

			NativeMenuItem native = new(item.Header);

			if (item is TrayToggleItem)
			{
				native.ToggleType = MenuItemToggleType.CheckBox;
			}

			if (item is not TrayStatusItem)
			{
				TrayMenuItem model = item;
				native.Click += (_, _) => Activate(model);
			}

			nativeMenu.Add(native);
			entries.Add((item, native));
		}
	}

	private void OnTrayIconClicked(object? sender, EventArgs e)
	{
		foreach ((TrayMenuItem model, _) in entries)
		{
			if (model is TrayToggleItem)
			{
				Activate(model);
				return;
			}
		}
	}

	private void OnQuitRequested(object? sender, EventArgs e) => Shutdown();

	private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e) => Dispose();

	private void Activate(TrayMenuItem item)
	{
		bool succeeded = menu.Activate(item);

		if (succeeded && item is TrayToggleItem toggle)
		{
			onToggled?.Invoke(toggle);
		}

		RefreshMenu();
	}

	private void ApplyDuration()
	{
		if (duration is not TimeSpan window)
		{
			return;
		}

		expiryTimer = new DispatcherTimer { Interval = window };
		expiryTimer.Tick += (_, _) =>
		{
			expiryTimer.Stop();
			Shutdown();
		};

		expiryTimer.Start();
	}

	private void Shutdown()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.Shutdown();
			return;
		}

		// Nothing else can end a tray-only run, so fall back to ending the dispatcher loop directly rather
		// than leaving a process with no way out.
		Dispatcher.UIThread.InvokeShutdown();
	}

	private void RefreshMenu()
	{
		menu.Refresh();

		foreach ((TrayMenuItem model, NativeMenuItem native) in entries)
		{
			native.Header = model.Header;
			native.IsEnabled = model.IsEnabled;

			if (model is TrayToggleItem toggle)
			{
				native.IsChecked = toggle.IsChecked;
			}
		}

		if (trayIcon is not null)
		{
			bool active = app.IsActive?.Invoke() ?? false;

			if (app.Icons is not null)
			{
				trayIcon.Icon = app.Icons.For(active);
			}

			trayIcon.ToolTipText = menu.StatusItem?.Header ?? app.DisplayName;
		}
	}
}
