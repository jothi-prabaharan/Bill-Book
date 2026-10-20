# Desktop and mobile apps

Bill Book also comes as an app you install: on Windows, Ubuntu and macOS computers,
and on Android and iOS phones and tablets. Every screen is the same as in the
browser; the apps add what only an installed app can do, and they keep themselves
up to date.

## Progressive Web App (PWA)

Bill-Book can be installed directly from the browser as a standalone desktop or mobile application on `localhost` as well as staging and production environments:

- **Browser omnibox install**: Chrome and Edge display an "Install Bill-Book" button in the address bar.
- **In-app install button**: An **Install App** button appears in the top bar and user account menu when the application is eligible for installation.
- **Standalone desktop experience**: Runs in an independent application window with its own taskbar icon and launch shortcuts.
- **Real-time ledger safety**: API calls (`/api/*`) strictly bypass caching to guarantee double-entry accounting integrity. A branded offline fallback page (`/offline.html`) handles network dropouts with automatic retry.

## On a computer

**Receipts and labels print straight away.** A till receipt, a kitchen ticket, a
restaurant bill and a sheet of labels go to their printer without the print
dialog. Choose the printers once per computer under **Printing** in the menu bar:

| Menu item | What it does |
|---|---|
| **Print receipts and labels without asking** | On by default. Off, every print opens the dialog first |
| **Receipt printer** | Where receipts, kitchen tickets and bills go. *System default* uses the computer's default printer |
| **Label printer** | Where price tags, shelf labels and batch labels go |
| **Refresh printer list** | Picks up a printer plugged in after the app started |

Each page prints at its own paper size: a receipt at the roll's width and as long
as it needs to be, a label at exactly its size. An **A4 sticker sheet** always
opens the dialog, since it goes to an office printer. If the chosen printer
refuses the page (it was unplugged, or renamed), the ordinary print dialog opens
so the page is not lost.

**The window remembers where you left it**: the same monitor, the same size,
maximised if it was. If that monitor has been unplugged it opens on the main
screen instead, and a window last used on a wide screen is shrunk to fit a
laptop's. Unplugging a monitor while the app is open brings the window back onto
one that is still there.

**One copy at a time.** Opening the app again brings the open window to the front,
so a till never prints the same receipt twice.

Scanning with a camera works as in the browser. On a Mac, the first scan asks for
permission to use the camera.

## On a phone or tablet

The app turns with the device, in all four directions on a tablet, and works in
split screen on Android and in Split View and Slide Over on an iPad. Every page
already works at a phone's width; a dialog opens as a sheet from the bottom.

On Android, the **back button** goes back a page, and from the first page it
leaves the app.

The first scan asks for permission to use the camera.

## Updates

The app checks for a new version when it starts and whenever you come back to it:
switching back to it, waking the computer, or **Help › Check for updates** on a
computer.

**An optional update** shows **Update available** with the new version's notes.
**Later** puts it away until there is a newer version still, or until you choose
**Help › Check for updates**. You can also close it with **Esc**, by clicking
outside it, or with Android's back button.

**A required update** shows **Update required**, and nothing closes it: not
**Esc**, not a click outside it, not the back button. The app behind it can't be
used until it is updated. It appears when your version is older than the oldest
one still supported, or when the new version is marked as required.

**Update now** depends on the device:

- **On a computer** the new version downloads with a progress bar, then **Restart
  and install** closes the app, installs it and opens it again. If the download
  fails, **Try again** appears.
- **On a phone or tablet** it opens the app's page in Google Play or the App
  Store. Come back to Bill Book once it has updated.

The browser version needs none of this: it is always the latest.

## Client Configuration & Endpoints

### Pointing Installed Apps at Your Enterprise Server

A single desktop installation can connect to your enterprise cloud or local server. By placing a `desktop-config.json` configuration file in the application directory, the server connection URL can be customized:

| Operating System | Configuration Directory |
|---|---|
| Windows | `%APPDATA%\Bill Book` |
| macOS | `~/Library/Application Support/Bill Book` |
| Linux | `~/.config/Bill Book` |

```json
{
  "serverUrl": "https://books.example.com",
  "updateUrl": "https://updates.example.com"
}
```

Printer selections and hardware profiles are preserved locally in `desktop-settings.json`.

### Version Management

Administrators can control version rollout across Windows, macOS, Android, and iOS clients using a centralized version manifest:

- **Optional Updates**: Informs users that a new release is available while allowing them to continue working.
- **Mandatory Updates**: Automatically prompts users to update before accessing financial transactions when a minimum required version is set.
- **Release Notes**: Displays key feature highlights directly in the in-app update notification modal.
