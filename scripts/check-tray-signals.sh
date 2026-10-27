#!/usr/bin/env bash
# Copyright (c) 2023-2026 ktsu-dev contributors
#
# Checks that a tray run ends through the tool's own shutdown when it is sent SIGTERM or SIGINT: the
# process exits 0 rather than dying to the signal, and a toggle clicked inside the preference store's
# 500 ms debounce window is still saved. Needs Xvfb, dbus-run-session and `pip install dbus-next`.
#
#     dotnet build examples/TrayApp.Demo
#     dbus-run-session -- scripts/check-tray-signals.sh
#
# Exits non-zero, naming the signal, on the first run that does not shut down cleanly.

set -u

root="$(cd "$(dirname "$0")/.." && pwd)"
demo="$root/examples/TrayApp.Demo/bin/Debug/net10.0/ktsu.TrayApp.Demo.dll"
work="$(mktemp -d)"
trap 'kill $(jobs -p) 2>/dev/null; rm -rf "$work"' EXIT

if [ ! -f "$demo" ]; then
	echo "Build the demo first: dotnet build examples/TrayApp.Demo" >&2
	exit 2
fi

check() {
	local signal="$1"
	local home="$work/$signal"
	mkdir -p "$home"

	python3 -u "$root/scripts/status-notifier-watcher.py" > "$home/watcher.log" 2>&1 &
	local watcher=$!

	# A non-interactive shell starts background jobs with SIGINT ignored, and an ignored signal stays
	# ignored across exec, so it is put back to the default for the run to receive it at all.
	HOME="$home" XDG_CONFIG_HOME="$home/.config" xvfb-run -a \
		env --default-signal=INT DBUS_SESSION_BUS_ADDRESS="$DBUS_SESSION_BUS_ADDRESS" \
		dotnet "$demo" --tray > "$home/demo.log" 2>&1 &
	local run=$!

	local item=""
	for _ in $(seq 1 60); do
		item="$(grep -o 'org.kde.StatusNotifierItem-[0-9-]*' "$home/watcher.log" | head -1)"
		[ -n "$item" ] && break
		sleep 0.5
	done

	if [ -z "$item" ]; then
		echo "SIG$signal: the tray icon never registered" >&2
		cat "$home/demo.log" >&2
		return 1
	fi

	local pid
	pid="$(pgrep -f "^dotnet $demo" | head -1)"

	# Flip the remembered toggle, then send the signal well inside the debounce window.
	python3 -u "$root/scripts/click-tray-menu.py" "$item" Running > "$home/click.log" 2>&1 &
	for _ in $(seq 1 100); do
		grep -q '^clicking' "$home/click.log" && break
		sleep 0.02
	done
	sleep 0.1
	kill "-$signal" "$pid"

	wait "$run"
	local status=$?
	kill "$watcher" 2>/dev/null

	local saved
	saved="$(cat "$home/.config/trayapp-demo/tray.json" 2>/dev/null)"

	if [ "$status" -ne 0 ]; then
		echo "SIG$signal: exited $status instead of shutting down" >&2
		return 1
	fi

	if ! grep -q '"running": false' <<< "$saved"; then
		echo "SIG$signal: the toggle clicked before the signal was not saved: ${saved:-no preference file}" >&2
		return 1
	fi

	echo "SIG$signal: shut down cleanly and saved the toggle"
}

check TERM && check INT
