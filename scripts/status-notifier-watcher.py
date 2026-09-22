#!/usr/bin/env python3
# Copyright (c) 2023-2026 ktsu-dev contributors
"""A minimal StatusNotifierWatcher, so a Linux tray can be exercised end to end in CI.

Avalonia's Linux tray is a StatusNotifierItem published over D-Bus. Nothing appears - and the tray
backend gives up - unless something owns `org.kde.StatusNotifierWatcher` on the session bus and says
a host is registered. A build agent has no panel, so this stands in for one: it accepts the
registration, prints it, and holds the name for as long as it runs.

    pip install dbus-next
    eval "$(dbus-launch --sh-syntax)"
    python3 scripts/status-notifier-watcher.py &
    xvfb-run -a dotnet run --project examples/TrayApp.Demo -- --tray --for 5s

Seeing `registered item: org.kde.StatusNotifierItem-...` is the proof that the tray icon really came
up, which assertions about a menu model cannot give you.
"""

import argparse
import asyncio
import sys

from dbus_next import BusType, PropertyAccess
from dbus_next.aio import MessageBus
from dbus_next.service import ServiceInterface, dbus_property, method, signal

WELL_KNOWN_NAME = "org.kde.StatusNotifierWatcher"
OBJECT_PATH = "/StatusNotifierWatcher"
PROTOCOL_VERSION = 0


class StatusNotifierWatcher(ServiceInterface):
    """The watcher interface a status-notifier item expects to find on the session bus."""

    def __init__(self):
        super().__init__(WELL_KNOWN_NAME)
        self._items = []
        self._hosts = []

    @method()
    def RegisterStatusNotifierItem(self, service: "s"):  # noqa: N802,F821,ANN201
        """Records an item and announces it."""
        if service not in self._items:
            self._items.append(service)

        print(f"registered item: {service}", flush=True)
        self.StatusNotifierItemRegistered(service)
        self.emit_properties_changed({"RegisteredStatusNotifierItems": self._items})

    @method()
    def RegisterStatusNotifierHost(self, service: "s"):  # noqa: N802,F821,ANN201
        """Records a host and announces it."""
        if service not in self._hosts:
            self._hosts.append(service)

        print(f"registered host: {service}", flush=True)
        self.StatusNotifierHostRegistered()

    @method()
    def UnregisterStatusNotifierItem(self, service: "s"):  # noqa: N802,F821,ANN201
        """Drops an item and announces it."""
        if service in self._items:
            self._items.remove(service)

        print(f"unregistered item: {service}", flush=True)
        self.StatusNotifierItemUnregistered(service)
        self.emit_properties_changed({"RegisteredStatusNotifierItems": self._items})

    @signal()
    def StatusNotifierItemRegistered(self, service) -> "s":  # noqa: N802,F821
        """Announces a newly registered item."""
        return service

    @signal()
    def StatusNotifierItemUnregistered(self, service) -> "s":  # noqa: N802,F821
        """Announces an item that went away."""
        return service

    @signal()
    def StatusNotifierHostRegistered(self):  # noqa: N802,ANN201
        """Announces that a host is available."""

    @dbus_property(access=PropertyAccess.READ)
    def RegisteredStatusNotifierItems(self) -> "as":  # noqa: N802,F821
        """The items registered so far."""
        return self._items

    @dbus_property(access=PropertyAccess.READ)
    def IsStatusNotifierHostRegistered(self) -> "b":  # noqa: N802,F821
        """Always true: an item that reads false here never publishes itself."""
        return True

    @dbus_property(access=PropertyAccess.READ)
    def ProtocolVersion(self) -> "i":  # noqa: N802,F821
        """The protocol version the specification pins at zero."""
        return PROTOCOL_VERSION


async def serve(seconds):
    """Owns the well-known name until the timeout elapses, or forever when there is none."""
    bus = await MessageBus(bus_type=BusType.SESSION).connect()
    bus.export(OBJECT_PATH, StatusNotifierWatcher())
    await bus.request_name(WELL_KNOWN_NAME)

    print(f"watching on {WELL_KNOWN_NAME}{OBJECT_PATH}", flush=True)

    if seconds is None:
        await bus.wait_for_disconnect()
        return 0

    try:
        await asyncio.wait_for(bus.wait_for_disconnect(), timeout=seconds)
    except asyncio.TimeoutError:
        pass

    return 0


def main(argv=None):
    """Parses arguments and runs the watcher."""
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--seconds", type=float, default=None, help="Exit after this long; the default runs until killed.")
    args = parser.parse_args(argv)

    return asyncio.run(serve(args.seconds))


if __name__ == "__main__":
    sys.exit(main())
