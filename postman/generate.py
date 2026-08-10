#!/usr/bin/env python3
"""
Generates `Bill-Book.postman_collection.json` by reading the controllers.

Run from the repository root:

    python3 postman/generate.py

**Generated, never hand-edited.** A Postman collection maintained by hand drifts
from the API within a fortnight — a route is renamed, a field is added, and the
collection keeps passing a body the server stopped accepting. Regenerating takes
a second and cannot drift, so the fix for a wrong request is to fix the
controller or this script, then re-run.

Three things it works out rather than being told:

* **That the gateway can actually reach every endpoint.** Clients only ever see
  the gateway, so the script refuses to generate anything while an `/api` prefix
  has no route — otherwise the collection would quietly pass against a service
  port while the product was broken through the edge, which is the one failure a
  collection exists to catch.
* **Which guard applies.** Anonymous, bearer token, or the shared internal key —
  read from the attributes rather than assumed, including per-action
  `[AllowAnonymous]`, which is how sign-in sits inside an otherwise closed
  controller.
* **What a body looks like.** Built from the C# request model's properties, so a
  new field appears in the collection the day it appears in the model.
"""

import collections
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parent.parent
BACKEND = ROOT / "backend"
OUT = pathlib.Path(__file__).resolve().parent / "Bill-Book.postman_collection.json"

# Matches the ports in each service's launch profile and in the docs.
SVC_PORT = {
    "Identity": 5001, "Platform": 5002, "Master": 5003, "Accounting": 5004,
    "Banking": 5005, "Contacts": 5006, "Inventory": 5007,
}

# Folder order follows CLAUDE.md's status order: reference data first, then the
# services that depend on it.
ORDER = ["Master", "Platform", "Identity", "Contacts", "Inventory", "Accounting", "Banking"]


# --------------------------------------------------------------- request models

def load_models():
    """Every `public class` with auto-properties, so a body can be built from it."""
    models = {}
    for f in BACKEND.rglob("*.cs"):
        s = str(f)
        if "/obj/" in s or "/bin/" in s or "Migrations" in s:
            continue
        src = f.read_text(errors="ignore")
        for m in re.finditer(
            r"public (?:sealed )?class (\w+)\s*(?::\s*[\w<>, ]+)?\s*\{(.*?)\n\}", src, re.S
        ):
            props = re.findall(r"public ([\w\?<>\[\]]+) (\w+)\s*\{\s*get;\s*set;", m.group(2))
            if props:
                models.setdefault(m.group(1), props)
    return models


MODELS = load_models()


def sample(type_name, prop, depth):
    """A placeholder of the right shape. Named `<Field>` so it is obviously not data."""
    t = type_name.rstrip("?")
    low = prop.lower()

    if t.startswith(("List<", "IReadOnlyList<", "ICollection<")):
        inner = t[t.index("<") + 1:-1]
        return [build(inner, depth + 1)] if inner in MODELS else []
    if t in ("string", "String"):
        if "email" in low:
            return "owner@example.com"
        if "password" in low:
            return "P@ssw0rd!"
        if "currencycode" in low or (low.endswith("code") and "currency" in low):
            return "INR"
        if low.endswith("date"):
            return "2026-08-10"
        return f"<{prop}>"
    if t in ("int", "long", "short"):
        return 1
    if t == "decimal":
        return 0
    if t == "bool":
        return False
    if t == "Guid":
        return "00000000-0000-0000-0000-000000000000"
    if t in ("DateOnly", "DateTime", "DateTimeOffset"):
        return "2026-08-10"
    if t in MODELS:
        return build(t, depth + 1)
    return f"<{t}>"


def build(cls, depth=0):
    # Bounded: a model that references itself would otherwise recurse for ever.
    if depth > 2 or cls not in MODELS:
        return {}
    return {p[1][:1].lower() + p[1][1:]: sample(p[0], p[1], depth) for p in MODELS[cls]}


# ------------------------------------------------------------------- controllers

def load_routes():
    routes = []
    for f in sorted(BACKEND.glob("Api/*/*.Api/Controllers/*.cs")):
        # backend/Api/{Service}/{Service}.Api/Controllers/X.cs
        service = f.parts[len(BACKEND.parts) + 1]
        src = f.read_text()
        for m in re.finditer(
            r"((?:\[[^\]]*\]\s*)+)public sealed class (\w+)\s*:\s*ControllerBase\s*\{(.*?)\n\}",
            src, re.S,
        ):
            class_attrs, controller, body = m.group(1), m.group(2), m.group(3)
            route = re.search(r'\[Route\("([^"]+)"\)\]', class_attrs)
            route = route.group(1) if route else ""
            class_anon = "AllowAnonymous" in class_attrs
            internal = "InternalOnly" in class_attrs

            for a in re.finditer(
                r"((?:^[ \t]*\[[^\]]*\]\s*\n)*)[ \t]*\[Http(Get|Post|Put|Patch|Delete)"
                r'(?:\("([^"]*)"\))?\]\s*\n((?:^[ \t]*\[[^\]]*\]\s*\n)*)'
                r"[ \t]*public [^\n{]*?(\w+)\s*\(([^)]*)\)",
                body, re.S | re.M,
            ):
                before, verb, sub, after, action, params_ = a.groups()
                attrs = before + after

                path = "/" + (route + ("/" + sub if sub else "")).strip("/")
                # {orgId:guid} and {id} both become Postman's :name form.
                path = re.sub(r"\{(\w+):[^}]+\}", r":\1", path)
                path = re.sub(r"\{(\w+)\}", r":\1", path)

                body_model = re.search(r"\[FromBody\]\s*([\w<>]+)", params_)
                example = None
                if body_model:
                    t = body_model.group(1)
                    example = ([build(t[t.index("<") + 1:-1])] if t.startswith("List<")
                               else build(t))

                routes.append({
                    "service": service, "controller": controller, "action": action,
                    "verb": verb.upper(), "path": path, "body": example,
                    # Per-action [AllowAnonymous] matters: sign-in lives inside an
                    # otherwise authenticated controller.
                    "anon": class_anon or "AllowAnonymous" in attrs,
                    "internal": internal,
                })
    return routes


def gateway_prefixes():
    """The `/api/{prefix}` values YARP actually proxies."""
    config = json.loads((BACKEND / "Gateway" / "appsettings.json").read_text())
    found = set()
    for r in config["ReverseProxy"]["Routes"].values():
        m = re.match(r"/api/([^/]+)/", r["Match"]["Path"])
        if m:
            found.add(m.group(1))
    return found


# ------------------------------------------------------------------- collection

def main():
    routes = load_routes()
    routed = gateway_prefixes()
    svc_var = {s: f"{{{{{s.lower()}Url}}}}" for s in SVC_PORT}

    # Everything a client calls goes through the gateway. It is the only address
    # a deployment exposes, and a collection that reaches past it tests a path
    # no user can take — green here and 404 in production.
    unrouted = sorted({
        r["path"].split("/")[2] for r in routes
        if not r["internal"] and r["path"].startswith("/api/")
        and r["path"].split("/")[2] not in routed
    })
    if unrouted:
        # Loud, because the alternative is a collection that quietly works
        # against a service port while the product is broken through the edge.
        raise SystemExit(
            "These /api prefixes have no gateway route, so no client can reach "
            "them:\n  " + "\n  ".join("/api/" + p for p in unrouted) +
            "\n\nAdd them to backend/Gateway/appsettings.json before regenerating.")

    def host(r):
        # internal/* is the exception, and deliberately so: it is
        # service-to-service, guarded by a shared key rather than a token, and
        # routing it through the public edge would put a key-only door on the
        # internet.
        return svc_var[r["service"]] if r["internal"] else "{{gatewayUrl}}"

    def make(r):
        url = host(r) + r["path"]
        request = {"method": r["verb"], "url": {"raw": url}}
        headers = []
        notes = []

        if r["internal"]:
            headers.append({"key": "X-Internal-Key", "value": "{{internalKey}}",
                            "description": "Shared service-to-service key. Not a user token."})
            request["auth"] = {"type": "noauth"}
            notes.append("shared key, no user token")
        elif r["anon"]:
            request["auth"] = {"type": "noauth"}
            notes.append("anonymous")

        if r["body"] is not None:
            headers.append({"key": "Content-Type", "value": "application/json"})
            request["body"] = {"mode": "raw", "raw": json.dumps(r["body"], indent=2),
                               "options": {"raw": {"language": "json"}}}
        if headers:
            request["header"] = headers

        words = re.sub(r"(?<!^)(?=[A-Z])", " ", r["action"]).capitalize()
        symbol = f"`{r['service']}.{r['controller']}.{r['action']}`"
        return {
            "name": f"{words} — {r['verb']} {r['path']}",
            "description": symbol + (" — " + "; ".join(notes) if notes else ""),
            "request": request,
        }

    login = next(r for r in routes if r["path"] == "/api/auth/login")
    select = next(r for r in routes if r["path"] == "/api/auth/select-organization")

    def auth_step(route, name, description, body, script):
        item = make(route)
        item["name"] = name
        item["description"] = description
        item["request"]["body"]["raw"] = json.dumps(body, indent=2)
        # Only a top-level item may carry an event, which is why these two sit
        # outside the folders rather than in an "Auth" one.
        item["event"] = [{"listen": "test",
                          "script": {"type": "text/javascript", "exec": script}}]
        return item

    items = [
        auth_step(
            login, "① Login  (run me first)",
            "Two-step by design: one account can hold roles in several branches, so "
            "credentials alone cannot say which set of books you are in.\n\n"
            "Captures `preAuthToken` and the first `orgId`.",
            {"email": "{{email}}", "password": "{{password}}"},
            [
                "// Credentials buy a 5-minute pre-auth token carrying no organization.",
                "const b = pm.response.json();",
                "if (b.preAuthToken) { pm.collectionVariables.set('preAuthToken', b.preAuthToken); }",
                "if (b.organizations && b.organizations.length) {",
                "  pm.collectionVariables.set('orgId', b.organizations[0].orgId);",
                "  console.log('Branches:', b.organizations.map(o => o.orgName + ' = ' + o.orgId));",
                "}",
                "pm.test('pre-auth token received', () => pm.expect(b.preAuthToken).to.be.a('string'));",
            ]),
        auth_step(
            select, "② Select organization  (run me second)",
            "Exchanges the pre-auth token for a 15-minute access token scoped to one "
            "branch. Captures `accessToken` and `refreshToken`; everything else in "
            "this collection inherits the bearer token.",
            {"preAuthToken": "{{preAuthToken}}", "orgId": "{{orgId}}"},
            [
                "// The access token carries org_id, permissions and licence status.",
                "const b = pm.response.json();",
                "if (b.accessToken) { pm.collectionVariables.set('accessToken', b.accessToken); }",
                "if (b.refreshToken) { pm.collectionVariables.set('refreshToken', b.refreshToken); }",
                "pm.test('access token received', () => pm.expect(b.accessToken).to.be.a('string'));",
            ]),
    ]

    by_service = collections.defaultdict(list)
    internal = []
    for r in routes:
        if r["path"] in ("/api/auth/login", "/api/auth/select-organization"):
            continue
        (internal if r["internal"] else by_service[r["service"]]).append(r)

    for i, service in enumerate(ORDER, 1):
        rs = sorted(by_service[service], key=lambda r: (r["controller"], r["path"], r["verb"]))
        items.append({
            "name": f"{i} · {service}",
            "description": f"{len(rs)} endpoints across "
                           f"{len({r['controller'] for r in rs})} controllers. "
                           "Bearer token via the gateway unless a request says otherwise.",
            "item": [make(r) for r in rs],
        })

    items.append({
        "name": "8 · Internal (service-to-service)",
        "description": "Never routed through the gateway, and guarded by a shared key "
                       "rather than a token. Several take the tenant in the body because "
                       "the caller is a background worker holding no user token.",
        "item": [make(r) for r in sorted(internal, key=lambda r: (r["service"], r["path"]))],
    })

    collection = {
        "info": {
            "name": "Bill-Book API",
            "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
            "description": (
                "RetailErp / Bill-Book — generated from the controllers by "
                "`postman/generate.py`. Do not hand-edit; re-run it.\n\n"
                "**Run ① Login then ② Select organization first.** Sign-in is two-step "
                "because one account can hold roles in several branches, and the access "
                "token has to name one.\n\n"
                "### Hosts\n"
                "Everything goes through `{{gatewayUrl}}`. It is the only address a "
                "deployment exposes, so a request that reached past it would test a "
                "path no user can take. The one exception is the internal folder: "
                "service-to-service endpoints are key-guarded and deliberately not "
                "published at the edge.\n\n"
                "### Bodies\n"
                "Generated from the C# request models. Placeholders read `<FieldName>` — "
                "they are shapes, not fixtures."),
        },
        "auth": {"type": "bearer",
                 "bearer": [{"key": "token", "value": "{{accessToken}}", "type": "string"}]},
        "variable": [
            {"key": "gatewayUrl", "value": "http://localhost:5000",
             "description": "YARP gateway — what the browser talks to."},
            *[{"key": f"{s.lower()}Url", "value": f"http://localhost:{p}"}
              for s, p in SVC_PORT.items()],
            {"key": "email", "value": "", "description": "Owner email for ① Login."},
            {"key": "password", "value": "",
             "description": "Set this yourself. Never commit a real one."},
            {"key": "preAuthToken", "value": "", "description": "Captured by ① Login."},
            {"key": "accessToken", "value": "",
             "description": "Captured by ② Select organization."},
            {"key": "refreshToken", "value": "",
             "description": "Captured by ② Select organization."},
            {"key": "orgId", "value": "", "description": "Captured by ① Login."},
            {"key": "customerId", "value": "",
             "description": "Needed by the internal endpoints."},
            {"key": "internalKey", "value": "",
             "description": "Matches Internal:Key in the services' configuration."},
        ],
        "item": items,
    }

    OUT.write_text(json.dumps(collection, indent=1) + "\n")
    total = sum(len(i["item"]) for i in items if "item" in i) + 2
    print(f"{OUT.relative_to(ROOT)}: {total} requests, "
          f"{sum(1 for i in items if 'item' in i)} folders")


if __name__ == "__main__":
    main()
