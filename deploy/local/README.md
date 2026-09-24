# Running Bill Book on your own PC

The whole product on one Windows 11 PC, reachable from anywhere on your own
domain name. No static IP, no router settings, and HTTPS included.

```
 browser ── https://app.yourname.in ──▶ Cloudflare ◀── outbound tunnel ── your PC
                                                                           │
                                   web (nginx, 4 apps) ─ /api ─▶ gateway ─▶ 8 services ─▶ PostgreSQL
```

Your PC connects **out** to Cloudflare and visitors come in through that
connection. Nothing on your PC is opened to the internet, and your home IP
address is never published. That is why no static IP is needed. Most Indian
home broadband (Jio in particular) sits behind carrier-grade NAT, where a
static-IP setup cannot work at all without paying for a business line.

Same images as the Azure deployment (`deploy/azure`); only the surroundings
differ:

| | Azure | This PC |
|---|---|---|
| Database | PostgreSQL Flexible Server | PostgreSQL 16 in a container |
| Uploaded files | Blob Storage | a Docker volume |
| Secrets | Key Vault | `deploy/local/.env` |
| Events | Service Bus | written to the log (nothing consumes one yet) |
| Web apps | Static Web Apps | nginx in a container |
| Public address | Azure's | your domain, through Cloudflare Tunnel |

---

## What you need

- **Windows 11 Pro**, 16 GB RAM recommended (the stack uses about 4 GB), 20 GB free disk
- **Docker Desktop**: <https://www.docker.com/products/docker-desktop/>. Accept the
  WSL 2 option during install and restart when asked.
- **Git for Windows**: <https://git-scm.com/download/win>
- **A domain name**, only for step 4. Around ₹800 a year for `.in` or `.com`, from
  Cloudflare itself or any registrar.

---

## 1. Get the code

Open **PowerShell** and run:

```powershell
cd $HOME
git clone https://github.com/jothi-prabaharan/Bill-Book.git
cd Bill-Book\deploy\local
```

## 2. Create the settings file

```powershell
powershell -ExecutionPolicy Bypass -File setup.ps1
```

It asks four things:

- your **domain name**: enter it as `yourname.in`, without `app.`, or leave it empty for now
- your **business name**
- **your name**
- **your email**

It writes `.env` with freshly generated passwords and keys, and **prints the
password for your owner account**. That password is also in `.env`, as
`BOOTSTRAP_OWNER_PASSWORD`.

> **Copy `.env` somewhere safe** (a password manager or a USB drive). It holds the
> database password and the encryption key. Lose it and a backup cannot be
> restored; leak it and someone can sign tokens as any user.

## 3. Start it

```powershell
docker compose up -d --build
```

The first run builds eleven images and takes 15–30 minutes. Later starts take
seconds. Check progress with:

```powershell
docker compose ps
```

Every service should say `running`. Three jobs run once and stop, and should
say `exited (0)`:

- `files-init` prepares the upload folder
- `migrate` sets up the database
- `first-branch` loads your first branch's chart of accounts, GST rates, units,
  numbering series, reports and print templates

**Open <http://localhost:8081> and sign in** with the email and password
`setup.ps1` printed. You are the owner of your business, with a licence that
runs for ten years, and you can add more branches and users from **Settings**.

> The **Start a free trial** page on the sign-in screen does not work on a new
> installation yet. It answers *Signups are temporarily unavailable while
> capacity is added*, because nothing yet makes room for trial businesses in the
> database (stage H0.5 in
> `docs/Modules.md`). Your own business does not need it: `setup.ps1` creates
> it.

| On this PC | |
|---|---|
| <http://localhost:8081> | web app |
| <http://localhost:8082> | customer portal |
| <http://localhost:8083> | platform admin |
| <http://localhost:8084> | docs |

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

Leave out `admin` unless you need it away from this PC (see *Security* below).

**d. Give the token to your PC.** Open `.env` in Notepad
(`notepad .env`), paste the token after `CLOUDFLARE_TUNNEL_TOKEN=`, and remove
the `#` in front of `COMPOSE_PROFILES=tunnel`. If you skipped the domain in
step 2, also set `APP_URL=https://app.yourname.in` and
`PORTAL_URL=https://portal.yourname.in`. Then run:

```powershell
docker compose up -d
```

Open **https://app.yourname.in**. The tunnel shows as *Healthy* in the dashboard
within a few seconds of starting.

## 5. Keep it running

The site is up only while this PC is on, awake and online.

- **Docker Desktop → Settings → General →** tick *Start Docker Desktop when you
  sign in to your computer*. The containers are set to restart by themselves
  whenever Docker is running.
- **Windows Settings → System → Power →** set sleep to **Never** while plugged in.
- **Windows Update → Advanced options → Active hours**: set them to your business
  hours so a restart for updates lands outside them.
- Docker Desktop starts when you **sign in**, not at power-on. After a power cut
  or an update restart, someone has to sign in to Windows before the site returns.

## 6. Back up every night

```powershell
powershell -ExecutionPolicy Bypass -File backup.ps1 -Install
```

This dumps every database and all uploaded files into `deploy\local\backups`
each night at 2 AM, keeping 14 days. Run `backup.ps1` without `-Install` to back
up right now.

**A backup on the same disk is not a backup.** Point OneDrive or Google Drive at
the `backups` folder, or copy it to a USB drive weekly. If the PC's disk fails,
that copy and `.env` are all that is left of every customer's books.

To restore, stop the services, then put a database back from a backup folder:

```powershell
docker compose stop
docker compose start db
docker compose exec db pg_restore -U postgres --clean --if-exists -d EP_Admin /backups/2026-09-24_0200/EP_Admin.dump
docker compose exec db pg_restore -U postgres --clean --if-exists -d IN000001 /backups/2026-09-24_0200/IN000001.dump
docker compose run --rm --entrypoint sh files-init -c "tar -xzf /backups/2026-09-24_0200/files.tar.gz -C /data && chown -R 1654:1654 /data/files"
docker compose up -d
```

Replace `2026-09-24_0200` with the folder you are restoring from. Restore on a
new PC the same way, after steps 1 and 3, using the **old** `.env` rather than
a new one from `setup.ps1`.

---

## Updating to a new version

```powershell
cd $HOME\Bill-Book
git pull
cd deploy\local
docker compose up -d --build
```

`migrate` runs again on every `up`, applies whatever database changes the new
version brings, and the services start only once it has succeeded. If it fails,
the old containers keep running; `docker compose logs migrate` says why.

## When something is wrong

```powershell
docker compose ps                  # what is running
docker compose logs -f master      # follow one service's log
docker compose logs --tail 100     # the last lines from everything
docker compose restart gateway     # restart one service
docker compose down                # stop everything (data is kept)
```

`docker compose down -v` **deletes the database and every uploaded file.** Never
run it on a PC holding real data.

To inspect the database with pgAdmin or DBeaver on this PC, connect to
`localhost:5433`, user `postgres`, with the `POSTGRES_PASSWORD` from `.env`.

---

## Security

- **Strangers cannot create businesses on your PC today**, because the free
  trial sign-up is refused on a new installation (see step 3). Once stage H0.5
  makes it work, anyone who finds `app.yourname.in` could start a trial there.
  Their data would be isolated from yours but on your disk. Putting Cloudflare
  Access in front of the site (next point) closes that off.
- **Keep `admin` off the internet**, or put Cloudflare Access in front of it (Zero
  Trust → Access → Applications, free for up to 50 users), which asks for an
  email code before the page even loads. The same works for the whole site if
  only your own staff should reach it.
- **Nothing listens on your network by default.** The four apps are bound to
  `127.0.0.1`, reachable only from this PC and through the tunnel. Set
  `BIND_ADDRESS=0.0.0.0` in `.env` to let other PCs in the shop open
  `http://<this-pc's-name>:8081` directly. Windows asks once whether to allow it
  through the firewall.
- **Emails** (invitations, password resets) need a mailbox. Set one in the app
  under **Settings → Email and SMTP** after signing in. A Gmail account works with an
  [app password](https://myaccount.google.com/apppasswords).
