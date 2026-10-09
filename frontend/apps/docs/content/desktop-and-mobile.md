# Desktop and mobile apps

Bill Book also comes as an app you install: on Windows, Ubuntu and macOS computers,
and on Android and iOS phones and tablets. Every screen is the same as in the
browser; the apps add what only an installed app can do, and they keep themselves
up to date.

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

## For administrators

### The update server

Updates come from any web server that serves a folder of files, for example
`http://YOUR_SERVER_IP:PORT/updates`. The folder holds:

- `version.json`, which every app reads;
- the computer installers, and the `latest.yml`, `latest-linux.yml` and
  `latest-mac.yml` files electron-builder writes beside them, which the desktop
  app downloads from.

The phone and tablet apps download from their stores; they only read
`version.json`.

**On IIS**, copy `frontend/apps/desktop/update-server/web.config` into the
folder too. IIS refuses to serve a file type it has no MIME type for, so without
it every desktop update stops at `latest.yml`. The file also stops
`version.json` and the `latest*.yml` files from being cached. Other web servers
serve these files as they are, and no CORS headers are needed on any of them.

### version.json

A template is in `frontend/apps/desktop/update-server/version.json`:

```json
{
  "electron": {
    "latestVersion": "1.2.0",
    "minSupportedVersion": "1.1.0",
    "forceUpdate": false,
    "releaseNotes": "Critical fixes and stability improvements."
  },
  "android": {
    "latestVersion": "1.2.0",
    "minSupportedVersion": "1.1.0",
    "forceUpdate": false,
    "storeUrl": "market://details?id=com.billbook.app"
  },
  "ios": {
    "latestVersion": "1.2.0",
    "minSupportedVersion": "1.1.0",
    "forceUpdate": false,
    "storeUrl": "itms-apps://itunes.apple.com/app/idYOUR_APP_ID"
  }
}
```

Each app reads its own section, `electron` on every computer. What a copy does is
decided in this order:

| The installed version is… | It shows |
|---|---|
| older than `minSupportedVersion` | **Update required**, whatever `forceUpdate` says |
| older than `latestVersion`, and `forceUpdate` is `true` | **Update required** |
| older than `latestVersion`, and `forceUpdate` is `false` | **Update available** |
| `latestVersion` or newer | nothing, even when `forceUpdate` is `true` |

`releaseNotes` is shown as written, line breaks included. `minSupportedVersion`,
`forceUpdate`, `releaseNotes` and `storeUrl` may be left out. A section the app
cannot read is ignored rather than blocking anyone, and so is a `version.json`
that cannot be reached: an app with no connection carries on as it was.

### Releasing a new version

1. Raise the version in `frontend/apps/desktop/package.json`, and for the phone
   apps in `frontend/apps/mobile/package.json`.
2. Build the installers (below) and upload them, with their `latest*.yml` files,
   to the update folder.
3. Submit the phone and tablet apps to their stores and wait for them to go live.
4. **Edit `version.json` last.** An app that reads the new version before the
   installer is there would offer a download that is not there yet.

To make everyone update, raise `minSupportedVersion` to the new version or set
`forceUpdate` to `true`.

### Building the computer installers

Build each installer on its own operating system: the Windows one on Windows, the
macOS one on a Mac, the Linux ones on Linux.

```bash
cd frontend
export BILLBOOK_SERVER_URL=https://books.example.com      # the gateway
export BILLBOOK_UPDATE_URL=http://YOUR_SERVER_IP:PORT/updates
npx nx run desktop:package:windows   # BillBook-Setup-1.2.0.exe and latest.yml
npx nx run desktop:package:linux     # .deb, .AppImage and latest-linux.yml
npx nx run desktop:package:mac       # .dmg, .zip and latest-mac.yml
```

Everything lands in `frontend/dist/apps/desktop/release`.

- **macOS** updates itself only when the app is signed with an Apple Developer ID
  and notarised. Set electron-builder's `CSC_LINK`, `CSC_KEY_PASSWORD`,
  `APPLE_ID`, `APPLE_APP_SPECIFIC_PASSWORD` and `APPLE_TEAM_ID` before building.
- **Windows** installs and updates unsigned, but Windows warns before the first
  install until the installer is signed with a code-signing certificate
  (`CSC_LINK`, `CSC_KEY_PASSWORD`).
- **Linux**: both the `.AppImage` and the `.deb` update themselves; the `.deb`
  asks for the administrator password to install.

The desktop app sends its `/api` calls to `BILLBOOK_SERVER_URL` itself, so the
gateway needs no extra allowed origin for it.

### Pointing an installed computer at another server

One installer can serve several sites. Put a `desktop-config.json` in the user's
own Bill Book folder and it overrides what the installer was built with:

| System | Folder |
|---|---|
| Windows | `%APPDATA%\Bill Book` |
| macOS | `~/Library/Application Support/Bill Book` |
| Linux | `~/.config/Bill Book` |

```json
{
  "serverUrl": "https://books.example.com",
  "updateUrl": "http://YOUR_SERVER_IP:PORT/updates"
}
```

`updateManifestUrl` can name a `version.json` somewhere else; otherwise it is
`version.json` in `updateUrl`. Restart the app after changing the file. The
printer choices are kept beside it, in `desktop-settings.json`.

### Building the phone and tablet apps

You need Android Studio (JDK 21, Android SDK 36) for Android, and a Mac with Xcode
for iOS.

```bash
cd frontend
export BILLBOOK_API_URL=https://books.example.com          # the gateway
export BILLBOOK_UPDATE_URL=http://YOUR_SERVER_IP:PORT/updates
npx nx run mobile:sync           # web build → apps/mobile/www → Android and iOS projects
npx nx run mobile:open-android   # build, sign and upload from Android Studio
npx nx run mobile:open-ios       # archive and upload from Xcode
```

`npx nx run mobile:build-android` builds the `.apk` and `.aab` from the command
line once a release signing key is set up in `apps/mobile/android`.

Unlike the computer app, a phone calls the gateway directly, so add both of the
apps' addresses to the gateway's `Cors:AllowedOrigins`: `https://localhost`
(Android) and `capacitor://localhost` (iOS).

The app id is `com.billbook.app` on both stores. It can't be changed once the
app is published, so change it in `apps/mobile/capacitor.config.ts` and both
native projects before the first upload if it should be different.
