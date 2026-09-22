#!/usr/bin/env python3
# Copyright (c) 2023-2026 ktsu-dev contributors
"""Clicks an entry in a running tray tool's menu, so the tray can be exercised without a panel.

`status-notifier-watcher.py` proves the tray icon registered. This proves the menu behind it
actually works: a StatusNotifierItem exports its menu over `com.canonical.dbusmenu`, so the whole
chain a user's click goes through - the native menu item, the menu model's activation and failure
handling, the tool's setter, and the refresh that re-reads every entry - can be driven from the
session bus and checked from the outside.

    pip install dbus-next
    eval "$(dbus-launch --sh-syntax)"
    python3 scripts/status-notifier-watcher.py > watcher.log 2>&1 &
    xvfb-run -a env DBUS_SESSION_BUS_ADDRESS="$DBUS_SESSION_BUS_ADDRESS" \
      dotnet run --project examples/TrayApp.Demo -- --tray --for 12s &
    sleep 4
    python3 scripts/click-tray-menu.py "$(grep -o 'org.kde.StatusNotifierItem-[0-9-]*' watcher.log | head -1)" Running

The menu is printed before and after, so a toggle that flipped, a status line that re-read itself,
and an entry that stayed greyed out are all visible in the output.
"""

import argparse
import asyncio
import sys
import time

from dbus_next import BusType, Variant
from dbus_next.aio import MessageBus

ITEM_INTERFACE = "org.kde.StatusNotifierItem"
MENU_INTERFACE = "com.canonical.dbusmenu"

# Avalonia publishes its item at /StatusNotifierItem; the Ayatana path is here so this script is
# useful against tools built on other toolkits too.
ITEM_PATHS = ("/StatusNotifierItem", "/org/ayatana/NotificationItem/1")


async def open_menu(bus, service):
    """Finds the item's exported menu and returns a proxy for it."""
    failure = None

    for path in ITEM_PATHS:
        try:
            introspection = await bus.introspect(service, path)
            item = bus.get_proxy_object(service, path, introspection).get_interface(ITEM_INTERFACE)
            menu_path = await item.get_menu()
            break
        except Exception as error:  # noqa: BLE001 - any failure here just means "try the next path"
            failure = error
    else:
        raise RuntimeError(f"could not reach a status notifier item on {service}: {failure}")

    introspection = await bus.introspect(service, menu_path)
    return menu_path, bus.get_proxy_object(service, menu_path, introspection).get_interface(MENU_INTERFACE)


def flatten(node, into):
    """Walks the nested layout dbusmenu returns into a flat list of (id, properties)."""
    identifier, properties, children = node
    into.append((identifier, properties))

    for child in children:
        flatten(child.value, into)

    return into


async def read_layout(menu):
    """Reads the whole menu."""
    _revision, root = await menu.call_get_layout(0, -1, [])
    return flatten(root, [])


def describe(entries, heading):
    """Prints every labelled entry with its checked state."""
    print(heading)

    for identifier, properties in entries:
        if "label" not in properties:
            continue

        state = properties["toggle-state"].value if "toggle-state" in properties else None
        enabled = properties["enabled"].value if "enabled" in properties else True
        print(f"  id={identifier} label={properties['label'].value!r} checked={state} enabled={enabled}")


async def run(service, label):
    """Clicks one entry and reports the menu either side of it."""
    bus = await MessageBus(bus_type=BusType.SESSION).connect()
    menu_path, menu = await open_menu(bus, service)
    print(f"menu: {menu_path}")

    before = await read_layout(menu)
    describe(before, "--- before ---")

    matches = [identifier for identifier, properties in before
               if "label" in properties and properties["label"].value == label]

    if not matches:
        print(f"no entry labelled {label!r}", file=sys.stderr)
        return 1

    print(f"clicking id={matches[0]} ({label!r})")
    await menu.call_event(matches[0], "clicked", Variant("i", 0), int(time.time()))

    # The click is one-way: dbusmenu's Event returns as soon as it is delivered, and the menu is
    # rebuilt on the tool's UI thread afterwards.
    await asyncio.sleep(1.0)

    describe(await read_layout(menu), "--- after ---")
    return 0


def main(argv=None):
    """Parses arguments and clicks."""
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("service", help="The item's bus name, as status-notifier-watcher.py printed it.")
    parser.add_argument("label", help="The menu entry to click, by its exact text.")
    args = parser.parse_args(argv)

    return asyncio.run(run(args.service, args.label))


if __name__ == "__main__":
    sys.exit(main())
