# Running Bill-Book on your own PCs

The whole product on your own Windows 11 PCs, reachable from anywhere on your
own domain name, with HTTPS included and no static public IP. Run it on **one
PC**, or **split it across PCs**, one part each:

```
 browser ─ https://app.yourname.in ─▶ Cloudflare ◀─ outbound tunnel ─ Web PC
                                                                        │ /api
                                                                        ▼
               Worker PC ─────▶ Services PC ◀──────────────────── Gateway PC
                   │              │      │                              │
                   ▼              ▼      ▼ SFTP                         │
               Database PC ◀──────┘   File server PC                    │
                   ▲────────────────────────────────────────────────────┘
```

| PC (example address) | Runs | Connects to |
|---|---|---|
| **Web** (192.168.1.24) | nginx serving the four web apps, and the Cloudflare Tunnel | Gateway |
| **Gateway** (192.168.1.23) | the gateway every API call passes through | Services, Database |
| **Services** (192.168.1.21) | the eight services, and the jobs that set up the database | Database, File server |
| **Worker** (192.168.1.22) | the costing worker, which prices stock movements | Database, Services |
| **Database** (192.168.1.20) | PostgreSQL | — |
| **File server** (192.168.1.25) | Windows' own SFTP server, holding uploads and archived invoices | — |

The examples below use these addresses; use your own.

On one PC, all of these run side by side.

**Why no static IP is needed.** The web PC connects **out** to Cloudflare, and
visitors come in through that connection. Nothing is opened to the internet,
and your home IP address is never published. Most Indian home broadband (Jio in
particular) sits behind carrier-grade NAT, where a static-IP setup cannot work
at all without paying for a business line.

**Inside the office, each PC does need a fixed address**, so the others can
find it. Reserve one for each PC on your router ("DHCP reservation" or "address
reservation" in its settings). Use addresses rather than PC names: the
containers cannot resolve Windows PC names.

It uses the same images as the Azure deployment (`deploy/azure`); only the
surroundings differ:

| | Azure | Your PCs |
|---|---|---|
| Database | PostgreSQL Flexible Server | PostgreSQL 16 in a container |
| Uploaded files | Blob Storage | an SFTP server on its own PC, or a folder on the services PC |
| Secrets | Key Vault | `deploy/local/.env` on each PC |
| Events | Service Bus | written to the log (nothing consumes one yet) |
| Web apps | Static Web Apps | nginx in a container |
| Public address | Azure's | your domain, through Cloudflare Tunnel |

---

## What you need

| PC | Needs | RAM |
|---|---|---|
| Web, Gateway, Services, Worker, Database | Windows 11 Pro, **Docker Desktop** (<https://www.docker.com/products/docker-desktop/>; accept the WSL 2 option and restart when asked), **Git for Windows** (<https://git-scm.com/download/win>) | Services 8 GB; the others 4 GB |
| File server | Windows 11. Nothing to install: its SFTP server is built into Windows | any |
| All on one PC | Docker Desktop and Git | 16 GB |

Plus **a domain name** for step 4: around ₹800 a year for `.in` or `.com`, from
Cloudflare itself or any registrar.

---

## 1. Get the code

On every PC that runs Docker, open **PowerShell** and run:

```powershell
cd $HOME
git clone https://github.com/jothi-prabaharan/Bill-Book.git
cd Bill-Book\deploy\local
```

If you are using a file server, set it up first ([The file server
PC](#the-file-server-pc) below), because step 2 asks for its details.

## 2. Create the settings

On **one** PC only:

```powershell
powershell -ExecutionPolicy Bypass -File setup.ps1
```

It asks:

- **one PC or split**
- your **domain name**: enter it as `yourname.in`, without `app.`, or leave it empty for now
- your **business name**, **your name** and **your email**
- if split, the fixed addresses of the **Database**, **Services** and **Gateway**
  PCs (the Web and Worker PCs only connect out, so nothing needs theirs)
- whether to use an **SFTP file server**, and if so its address, the account
  you created on it, and its key fingerprint

It generates every password and key, and **prints the password for your owner
account**.

- **One PC:** it writes `.env`.
- **Split:** it writes a `pcs` folder with one file per PC. Copy each file to
  its PC's `Bill-Book\deploy\local` folder and **rename it to `.env`**. A USB
  drive is the safest way to carry them. Each file holds only what its PC needs:
  the web PC, the one facing the internet, never gets the database password or
  the signing keys.

  | File | Goes to |
  |---|---|
  | `database.env` | Database PC |
  | `services.env` | Services PC |
  | `worker.env` | Worker PC |
  | `gateway.env` | Gateway PC |
  | `web.env` | Web PC |

> **Keep a copy of `.env`, or of the `pcs` folder, somewhere safe** (a password
> manager or a USB drive in a drawer), then delete the `pcs` folder from the PC
> you ran setup on. Without it a backup cannot be restored; with it, someone can
> sign in as any user.

## 3. Start it

On each PC, in `Bill-Book\deploy\local`:

```powershell
docker compose up -d --build
```

The first run builds the images and takes 15–30 minutes. Later starts take
seconds.

**When split, start the PCs in this order: Database, Services, Worker, Gateway,
Web.** Each waits for, or retries, the one before it, but that order gets
everything up first time.

Check with `docker compose ps`. Every service should say `running`. On the
services PC (or the one PC), three jobs run once and then stop; they should say
`exited (0)`:

- `files-init` prepares the upload folder
- `migrate` sets up the database
- `first-branch` loads your first branch's chart of accounts, GST rates, units,
  numbering series, reports and print templates

**Open <http://localhost:8081> on the web PC (or the one PC) and sign in** with
the email and password `setup.ps1` printed. You are the owner of your business,
with a licence that runs for ten years. You can add more branches and users
from **Settings**.

> The **Start a free trial** page on the sign-in screen does not work on a new
> installation yet. It answers *Signups are temporarily unavailable while
> capacity is added*, because nothing yet makes room for trial businesses in the
> database (stage H0.5 in `docs/Modules.md`). Your own business does not need
> it: `setup.ps1` creates it.

| On the web PC | |
|---|---|
| <http://localhost:8081> | web app |
| <http://localhost:8082> | customer portal |
| <http://localhost:8083> | platform admin |
| <http://localhost:8084> | docs |

### Firewall, when split

Each PC must accept connections from the PCs that use it, and only from those.
On each PC, open **PowerShell as Administrator** and run its line, using your
own addresses:

```powershell
# Database PC: PostgreSQL, from the Services, Worker and Gateway PCs
New-NetFirewallRule -DisplayName "Bill-Book database" -Direction Inbound -Protocol TCP -LocalPort 5433 -RemoteAddress 192.168.1.21,192.168.1.22,192.168.1.23 -Action Allow

# Services PC: the eight services, from the Gateway and Worker PCs
New-NetFirewallRule -DisplayName "Bill-Book services" -Direction Inbound -Protocol TCP -LocalPort 7501-7508 -RemoteAddress 192.168.1.23,192.168.1.22 -Action Allow

# Gateway PC: the gateway, from the Web PC
New-NetFirewallRule -DisplayName "Bill-Book gateway" -Direction Inbound -Protocol TCP -LocalPort 8080 -RemoteAddress 192.168.1.24 -Action Allow
```

The Web and Worker PCs need no rule: they only connect out. If Windows also
pops up asking whether to allow *Docker Desktop Backend*, allow it on
**Private** networks only.

**Traffic between the PCs is not encrypted.** Sign-in tokens, the key the
services use between themselves, and the database password travel as plain
text on the office network. Keep these PCs on a wired network that only your
own staff use, and never on guest Wi-Fi. The public side is always HTTPS,
through Cloudflare.

---

## The file server PC

Uploaded attachments and archived invoices can live on a PC of their own,
reached over **SFTP**, the encrypted form of FTP, which Windows includes.
Plain FTP is not supported: it sends the password and every file readable to
anyone on the network, and it cannot guarantee that saving a file never
overwrites another.

On the file server PC, open **PowerShell as Administrator**:

```powershell
# 1. Turn on Windows' own SFTP server, and start it with Windows.
Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0
Set-Service sshd -StartupType Automatic
Start-Service sshd

# 2. An account used only by Bill-Book, and the folder it may write to.
#    Pick a long password; setup.ps1 asks for it.
$password = Read-Host "Password for the billbook account" -AsSecureString
New-LocalUser -Name billbook -Password $password -PasswordNeverExpires -UserMayNotChangePassword
New-Item -ItemType Directory -Path D:\BillBookFiles
icacls D:\BillBookFiles /grant "billbook:(OI)(CI)M"

# 3. Lock that account into the folder, file transfer only.
Add-Content C:\ProgramData\ssh\sshd_config @"

Match User billbook
    ChrootDirectory D:\BillBookFiles
    ForceCommand internal-sftp
    AllowTcpForwarding no
    PermitTTY no
"@
Restart-Service sshd

# 4. Accept SFTP from the Services PC only (use its address).
Set-NetFirewallRule -Name OpenSSH-Server-In-TCP -RemoteAddress 192.168.1.21

# 5. The key fingerprint setup.ps1 asks for.
ssh-keygen -lf C:\ProgramData\ssh\ssh_host_ed25519_key.pub
```

Use any drive and folder in place of `D:\BillBookFiles`. `setup.ps1` wants this
PC's address, the user name `billbook`, that password, and the `SHA256:…`
fingerprint from step 5. **Give it the fingerprint**: without one, another
machine on the network could pose as your file server and receive every upload.

Files land in folders a person can find:

```
D:\BillBookFiles\
└── 0000000001\                   your customer code
    └── 00000000-…-0001\          the branch
        └── retail-erp\
            ├── contacts\attachments\…
            └── sales\invoices\…
```

**While the file server is off, nothing can be uploaded, and posting an invoice
fails**, because posting archives its PDF. Keep it on during business hours,
like the others.

To move files onto a file server later, fill in `SFTP_HOST`, `SFTP_USERNAME`,
`SFTP_PASSWORD`, `SFTP_ROOT=/` and `SFTP_HOST_KEY` in the services PC's `.env`,
then run `docker compose up -d`. Files uploaded before the switch stay in the
services PC's own folder and are not moved automatically.

---

## 4. Put it on your domain

**a. Move the domain's DNS to Cloudflare.** Create a free account at
<https://dash.cloudflare.com>, then choose *Add a domain* and pick the Free plan.
Cloudflare shows two nameservers; set those at the registrar you bought the
domain from. It takes from a few minutes to a few hours; Cloudflare emails you
when the domain is active. (If you buy the domain from Cloudflare, this is
already done.)

**b. Create a tunnel.** In the Cloudflare dashboard open **Zero Trust →
Networks → Tunnels → Create a tunnel**. Choose **Cloudflared**, name it
`billbook`, and on the install screen pick **Docker**. The command shown ends
in `--token eyJh…`. Copy **only the token** (the long `eyJ…` text). Do not run
the command itself; the compose file runs the tunnel for you.

**c. Map your site names.** On the tunnel's **Public hostnames** tab (also
called *Published application routes*), add:

| Subdomain | Domain | Type | URL |
|---|---|---|---|
| `app` | yourname.in | HTTP | `web:8081` |
| `portal` | yourname.in | HTTP | `web:8082` |
| `docs` | yourname.in | HTTP | `web:8084` |

Leave out `admin` unless you need it away from the office (see *Security* below).

**d. Give the token to the web PC** (or the one PC). Open its `.env` in Notepad
(`notepad .env`), paste the token after `CLOUDFLARE_TUNNEL_TOKEN=`, and add
`,tunnel` to the end of the `COMPOSE_PROFILES=` line. If you skipped the domain
in step 2, also set `APP_URL=https://app.yourname.in` and
`PORTAL_URL=https://portal.yourname.in` in the **services** PC's `.env` (or the
one PC's). Then, on each PC you changed:

```powershell
docker compose up -d
```

Open **https://app.yourname.in**. The tunnel shows as *Healthy* in the dashboard
within a few seconds of starting.

## 5. Keep it running

The site is up only while its PCs are on, awake and online. On every PC:

- **Docker Desktop → Settings → General →** tick *Start Docker Desktop when you
  sign in to your computer*. The containers are set to restart by themselves
  whenever Docker is running.
- **Windows Settings → System → Power →** set sleep to **Never** while plugged in.
- **Windows Update → Advanced options → Active hours**: set them to your business
  hours so a restart for updates lands outside them.
- Docker Desktop starts when you **sign in**, not at power-on. After a power cut
  or an update restart, someone has to sign in to Windows before that PC's part
  returns. The file server's SFTP needs no sign-in: it is a Windows service.

## 6. Back up every night

On the **database PC** (or the one PC):

```powershell
powershell -ExecutionPolicy Bypass -File backup.ps1 -Install
```

This dumps every database into `deploy\local\backups` each night at 2 AM,
keeping 14 days, together with the uploaded files **if they are kept on that
same PC**. Run `backup.ps1` without `-Install` to back up right now.

**Files on a file server are backed up there**, not by this script. On the file
server PC, turn on **File History** (Settings → System → Storage → Advanced
storage settings → Backup options) for `D:\BillBookFiles`, or copy the folder
to a USB drive weekly.

**A backup on the same disk is not a backup.** Point OneDrive or Google Drive at
the `backups` folder, or copy it to a USB drive weekly. If a disk fails, that
copy and your `.env` files are all that is left of every customer's books.

To restore, stop the services, then put a database back from a backup folder.
On the database PC (or the one PC):

```powershell
docker compose exec db pg_restore -U postgres --clean --if-exists -d EP_Admin /backups/2026-09-24_0200/EP_Admin.dump
docker compose exec db pg_restore -U postgres --clean --if-exists -d IN000001 /backups/2026-09-24_0200/IN000001.dump
```

Stop the services first (`docker compose stop` on the services PC and the
worker PC, or on the one PC, then `docker compose start db` there), and run
`docker compose up -d` everywhere afterwards. When the files are kept on the one
PC, put them back as well:

```powershell
docker compose run --rm --entrypoint sh files-init -c "tar -xzf /backups/2026-09-24_0200/files.tar.gz -C /data && chown -R 1654:1654 /data/files"
```

Replace `2026-09-24_0200` with the folder you are restoring from. To restore on
a new PC, do steps 1 and 3 with the **old** `.env` rather than a new one from
`setup.ps1`.

---

## Updating to a new version

On every PC, **Services PC first**:

```powershell
cd $HOME\Bill-Book
git pull
cd deploy\local
docker compose up -d --build
```

`migrate` runs again on every `up` on the services PC, applies whatever database
changes the new version brings, and the services start only once it has
succeeded. If it fails, the old containers keep running;
`docker compose logs migrate` says why.

## When something is wrong

```powershell
docker compose ps                  # what is running on this PC
docker compose logs -f master      # follow one service's log
docker compose logs --tail 100     # the last lines from everything
docker compose restart gateway     # restart one service
docker compose down                # stop everything on this PC (data is kept)
```

`docker compose down -v` **deletes the database and every uploaded file on that
PC.** Never run it on a PC holding real data.

To inspect the database with pgAdmin or DBeaver, connect to port `5433` on the
database PC (or `localhost:5433` on the one PC), user `postgres`, with the
`POSTGRES_PASSWORD` from `.env`.

When split and something cannot connect, check from the PC that is failing
whether the other one answers, for example from the gateway PC:

```powershell
Test-NetConnection 192.168.1.21 -Port 7501
```

`TcpTestSucceeded : False` means an address in `.env` is wrong, that PC's part is
not running, or its firewall rule is missing.

**`migrate` exits with "The database "EP_Admin" does not exist".** The database
container creates `EP_Admin` and `IN000001` from `db/init` only on its first start
with an empty data volume. The app does not create them, because they belong to
the installation and not to the app. If the volume was created before the init
script existed, or a database was dropped by hand, create the missing one on the
database PC and run `migrate` again:

```powershell
docker compose exec db psql -U postgres -c "CREATE DATABASE \"EP_Admin\" ENCODING 'UTF8' TEMPLATE template0"
docker compose up migrate
```

---

## Security

- **Strangers cannot create businesses on your PCs today**, because the free
  trial sign-up is refused on a new installation (see step 3). Once stage H0.5
  makes it work, anyone who finds `app.yourname.in` could start a trial there.
  Their data would be isolated from yours but on your disks. Putting Cloudflare
  Access in front of the site (next point) closes that off.
- **Keep `admin` off the internet**, or put Cloudflare Access in front of it (Zero
  Trust → Access → Applications, free for up to 50 users), which asks for an
  email code before the page even loads. The same works for the whole site if
  only your own staff should reach it.
- **The web apps listen only on the web PC itself** (and the tunnel). Set
  `BIND_ADDRESS=0.0.0.0` in the web PC's `.env` to let other PCs in the shop open
  `http://<web-pc-address>:8081` directly. Windows asks once whether to allow it
  through the firewall.
- **Turn on BitLocker** on every PC (Settings → Privacy & security → Device
  encryption). Without it, anyone who takes a PC or its disk can read its `.env`,
  the database or the uploaded files.
- **Emails** (invitations, password resets) need a mailbox. Set one in the app
  under **Settings → Email and SMTP** after signing in. A Gmail account works with an
  [app password](https://myaccount.google.com/apppasswords).
