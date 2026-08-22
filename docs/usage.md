# Using MK20Box

Open the **MK20Box** page in SimHub's sidebar.

The editor is a picture of your device. Click a part of it and the right-hand
panel becomes the editor for that part. Press **Send to device** when done.

## Keys

Click a key.

- **Button text** — label over the icon, with colour, size and position
  (top or bottom only; the device ignores anything else).
- **Right-click** — choose a bundled icon, your own picture or an animated GIF.
  *Show whole picture* pads it; otherwise it is cropped to fill.
- **Action** — what pressing it does:

| Action | Runs on | |
|---|---|---|
| Keystroke | Device | One key plus modifiers, e.g. `Ctrl + Shift + C` |
| Macro | Plugin | Keystrokes, typed text, waits, SimHub actions |
| SimHub action | Plugin | Fires a named SimHub command |
| SimHub input | Plugin | Virtual button you bind in Controls & Events |
| Open folder | Device | Enters a nested page |
| One level up | Device | Leaves a folder |
| Previous / Next page | Device | Steps through top-level pages |

**Device actions keep working with SimHub minimised or closed.** Plugin actions
need MK20Box running.

### Copying and clearing keys

| | |
|---|---|
| **Copy key** — `Ctrl+C` | Remembers the key's picture, text and action |
| **Paste key** — `Ctrl+V` | Gives the selected key that picture, text and action |
| **Reset key** — `Del` | Clears it back to blank and unassigned |

All three are on the right-click menu too. A copied key can be pasted onto any
key, on any page, folder or profile — handy for building a row of similar keys.

Pasting keeps the target key's own cell and gives it a fresh internal id, so the
copy and the original never answer for each other's presses.

Pasting onto a key that opens a folder, or resetting one, asks first when that
folder has anything in it. The folder itself is kept, but nothing opens it any
more — the same as changing the action by hand.

## Pages and folders

Twenty keys fills up fast, so a layout can hold as many screens as you like.

**Pages** are the top-level screens. The bar above the device shows *Page 1 of
3*; `<` and `>` step between them while editing, and **Add page** / **Delete
page** manage them. Deleting a page also deletes any folders that belong to it,
and the last remaining page cannot be deleted.

To move between pages *on the device*, give a key the **Previous page** or
**Next page** action. They wrap around, and the device handles them itself.

**Folders** are nested screens reached from a key. Set a key to **Open folder**
and its folder is created automatically, with the bottom-right key preset to
**One level up** so you can get back — the same convention the vendor's own
themes use. Folders can be nested as deep as you want.

Navigating the editor:

- **Double-click** a navigation key to follow it, exactly as the device would.
- The **breadcrumb** under the page bar shows where you are, naming folders by
  the key that opens them.
- **Back** retraces your steps.

Each page has its own keys and encoders. The secondary screen is also per page
unless you turn on **Use one screen for every page and folder**.

> A key press reports only its row and column, never its page. MK20Box gives
> plugin-routed keys a stable id behind the scenes, so a macro or SimHub action
> keeps working when you move that key to another cell, page or folder.

## Secondary screen

Click the strip. It is 428×142 pixels.

Press **Add**, then **Choose...** to bind a SimHub value — 30 sim-racing
presets are listed and searchable, or type any property name. Selecting a
preset also fills in the label, unit, decimals and, for a bar, its range.

The widget you are editing is outlined on the preview. Drag it there, or type
exact **X** and **Y**.

| Widget | |
|---|---|
| Text | Value with optional unit and decimals |
| Text with outline | The same, plus a stroke — readable over artwork |
| Progress bar | Fills between MinValue and MaxValue |
| Clock | Hours, minutes, optional seconds |

Every widget has a **Label** (names it in the list; not drawn on the device),
a **Colour** and a position.

| Type | Extra settings |
|---|---|
| Text | Decimals, unit, text size. Leave the value empty to show **Fixed text** instead — handy for captions like `FUEL` |
| Text with outline | Outline colour and width. Set the width to 0 for plain text |
| Progress bar | MinValue, MaxValue, width and height. Bars draw no text, so they have no unit or decimals |
| Clock | Show seconds, text size, and **Spacing** between digit pairs |

**Use one screen for every page and folder** copies page 1's strip — picture
and widgets alike — onto every page when the layout is built, so telemetry
stays visible wherever you navigate. Nothing is duplicated in storage.

Right-click the strip for a background picture or GIF. *Show whole picture*
letterboxes it; otherwise it fills and crops, and you can drag to choose which
part shows. Widgets always draw above it.

Notes: widgets are driven by the plugin, so they freeze if it stops — including
the clock, as the device has no real-time clock. The clock draws no colon;
**Spacing** controls the gap between digit pairs, and negative values close it
up.

## Encoders

Click an encoder. They are configured per page.

| Mode | |
|---|---|
| Device function | System volume, device volume, brightness, media |
| Keystrokes | A different keystroke per rotate left / click / rotate right |
| Report to plugin | Reports presses to MK20Box |

Use **Keystrokes** when direction matters — in *Report to plugin* mode the
device sends the same event for both directions and the click.

## Profiles

A profile is a complete named layout. **Use one profile for every game** applies
one everywhere; turn it off to choose which profile each game uses, picked from
SimHub's own game list.

Profiles are not tied to a game — every profile is offered for every game, so
the same layout can be reused wherever it suits.

MK20Box follows the game selected in SimHub and, with **Upload profile
automatically** on, sends the matching profile to the device.

**Rename** changes a profile's name only. It keeps its id, so the global
selection and any per-game bindings follow it.

**Duplicate** copies the selected profile, layout and all, under a new name and
switches to the copy — the quickest way to build a variant of a layout that
already works. The copy is independent: editing it cannot disturb the original.

**Reset all settings** clears everything and asks twice.

### Sharing profiles

**Export** writes the selected profile to a `.mk20profile` file. The file
carries the pictures the profile uses, so it works on someone else's machine —
icons from the bundled library are referenced rather than copied, which keeps a
typical export to a few kilobytes.

**Import** adds a shared profile alongside your own. It never overwrites: the
profile gets a new id, and a clashing name gains a suffix, so importing the same
file twice gives you two copies. Imported pictures are unpacked to
`%LOCALAPPDATA%\Mk20Box\SharedMedia`.

An imported profile behaves like any other: it is available for every game. Bind
it to one above if you want it loaded automatically.

`examples/LMU.mk20profile` in the repository is a ready-made Le Mans Ultimate
layout to import and edit. Its keys carry pictures and labels but no actions, so
the bindings are yours to choose.

## Properties exposed to SimHub

`MK20Box.Status`, `.ActiveGame`, `.ActiveProfile`, `.ActiveProfileIsGlobal`,
`.DeviceConnected`, `.DeviceStatus`, `.DevicePort`.

## If something does not work

| Symptom | |
|---|---|
| Plugin missing | `Mk20Box.dll` must sit in the SimHub root, with the `Mk20Box` folder beside it |
| Device not found | Close other software using the port, then **Refresh** and select it |
| Widget not drawn | Pick the property from the list; check the game is running |
| Clock frozen | It is host-driven — SimHub must be running |
| Encoder ignores direction | Switch it to **Keystrokes** |
| Build locked by SimHub | Close SimHub, or use `.\deploy.ps1 -Force` |
