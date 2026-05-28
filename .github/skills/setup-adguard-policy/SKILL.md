---
name: setup-adguard-policy
description: >
  Stand up AdGuard Home as the DNS-level egress firewall for a NEW project. Covers
  Docker compose stanza, initial filter file, upstream resolver, reload procedure,
  NXDOMAIN vs 0.0.0.0 block choice, and the bind-mount path trap. Run BEFORE
  context-mode is brought up — the sandbox depends on AdGuard's resolver.
argument-hint: "<project-name> [--strict|--permissive]"
---

# setup-adguard-policy — DNS-level egress firewall

AdGuard Home runs as a Docker container providing a DNS resolver that NXDOMAINs every
domain not on an explicit allowlist (D1 builds the allowlist; this skill stands up the
resolver itself). Required by ADR-0029 for the context-mode sandbox.

---

## When to use

- New project bootstrap, before `docker compose up context-mode`.
- Replacing a no-firewall setup with the canonical ADR-0029 pattern.
- Recovering after a `docker volume prune` that wiped AdGuard's config.

## When NOT to use

- Adding a single domain to an EXISTING allowlist — use D1
  (`.github/skills/ctx-bootstrap-network/SKILL.md`).
- The project does not use context-mode and does not need egress policy.
- Running on a host that already has a system-wide DNS firewall (Pi-hole, NextDNS) —
  conflict; pick one.

---

## Steps

### 1. Pick `--strict` vs `--permissive`

| Mode | Default-allow domains | When to pick |
|---|---|---|
| `--strict` (recommended) | None — every domain blocked unless allowlisted | Production-leaning sandboxes, multi-developer teams |
| `--permissive` | Common dev/CI domains (`github.com`, `npmjs.org`, `huggingface.co`) pre-allowed | Solo dev, rapid prototyping |

`--strict` is the ADR-0029 default. Use `--permissive` only when slower iteration is
unacceptable — log every allowlist exception added and revisit at the end of the
prototype.

### 2. Add the compose service

```yaml
services:
  adguard:
    image: adguard/adguardhome:v0.107.50
    container_name: <project>-adguard
    restart: unless-stopped
    ports:
      - "53:53/udp"   # DNS (LAN-side; sandbox uses container DNS internally)
      - "53:53/tcp"
      - "3000:3000"   # Admin UI (LAN-bind in prod; expose 127.0.0.1:3000 only)
    volumes:
      - ./docker/adguard/conf:/opt/adguardhome/conf
      - ./docker/adguard/work:/opt/adguardhome/work
      - ./docker/adguard/filters:/opt/adguardhome/filters:ro
    cap_add:
      - NET_ADMIN
```

For production-style deployments, change the admin port binding:

```yaml
    ports:
      - "127.0.0.1:3000:3000"
```

so the UI is reachable only from the host.

### 3. Seed the configuration

```sh
mkdir -p docker/adguard/{conf,work,filters}
```

Drop the minimal `AdGuardHome.yaml` into `docker/adguard/conf/`:

```yaml
schema_version: 28
users:
  - name: admin
    # bcrypt of "change-me-on-first-login"; rotate immediately
    password: "$2a$10$replace.with.your.own.bcrypt.hash.here.now"
dns:
  bind_hosts: [0.0.0.0]
  port: 53
  upstream_dns:
    - tls://1.1.1.1
    - tls://8.8.8.8
  protection_enabled: true
  filtering_enabled: true
  blocking_mode: nxdomain          # IMPORTANT — see "Common mistakes" below
filters:
  - enabled: true
    url: file:///opt/adguardhome/filters/<project>-allow.txt
    name: <project>-allow
    id: 1
```

### 4. Drop the initial filter file

```sh
cat > docker/adguard/filters/<project>-allow.txt <<'EOF'
! AdGuard filter — <project>
! Allow-then-deny. Anything not allowed is NXDOMAIN'd.

@@||qdrant^
@@||huggingface.co^
@@||cdn-lfs.huggingface.co^
@@||objects.githubusercontent.com^
@@||github.com^
@@||raw.githubusercontent.com^
@@||api.github.com^

! Default deny (must be LAST):
||*^$important
EOF
```

### 5. Start AdGuard

```sh
docker compose up -d adguard
docker logs <project>-adguard | head -30
```

Look for `[info] dns: starting dns server` — that's the all-clear.

### 6. Rotate the admin password

```sh
docker exec -it <project>-adguard sh -c \
  '/opt/adguardhome/AdGuardHome --check-password "<new-password>"'
```

(Or open `http://localhost:3000`, log in with the seeded credentials, and rotate via UI.)

### 7. Verify NXDOMAIN behaviour

From the host (cross-platform):

```sh
nslookup qdrant 127.0.0.1
# expected: resolves to the qdrant container IP

nslookup api.openai.com 127.0.0.1
# expected: NXDOMAIN  (NOT "0.0.0.0", NOT "127.0.0.1")
```

If `api.openai.com` resolves to `0.0.0.0`, the `blocking_mode` is wrong — see Common
mistakes.

### 8. Verify from inside the sandbox

After context-mode is up and routed through AdGuard (see E2):

```sh
docker exec -i <project>-context-mode sh -c \
  'nslookup api.openai.com 2>&1 | grep -E "NXDOMAIN|can.t find"'
```

### 9. Wire the project's reload helper (optional)

ECommerceApp ships [`scripts/adguard/domain-policy.ps1`](../../../scripts/adguard/domain-policy.ps1)
with subcommands `status`, `reload`, `add`, `remove`. For a NEW project, either copy
that script (PowerShell required) or use the bare commands:

```sh
# Reload after filter file edit
docker compose restart adguard

# Show active filters
docker exec -i <project>-adguard sh -c 'cat /opt/adguardhome/conf/AdGuardHome.yaml | grep -A5 filters:'
```

Bash equivalent of the `domain-policy.ps1` reload (for portable hosts):

```sh
#!/usr/bin/env sh
# scripts/adguard/reload.sh
docker compose restart adguard \
  && echo "AdGuard restarted; filters reloaded." \
  || { echo "Restart failed; check 'docker logs <project>-adguard'." >&2; exit 1; }
```

---

## Common mistakes

- **`blocking_mode: 0.0.0.0` instead of `nxdomain`.** Some clients retry forever on
  `0.0.0.0` (interpreted as "this host"); others connect successfully to `0.0.0.0:443`
  and then hang. `nxdomain` is the unambiguous "this domain doesn't exist" signal that
  every client handles correctly. ADR-0029 mandates `nxdomain`.
- **Bind-mount path mismatch between container and host.** AdGuard reads
  `/opt/adguardhome/conf/AdGuardHome.yaml` — the bind-mount source MUST be
  `./docker/adguard/conf` (not `./adguard/conf` or `./conf`). Path mismatch → AdGuard
  starts with an empty config and effectively no filtering.
- **Forgetting `upstream_dns`.** Without an upstream resolver, even allowlisted
  domains return NXDOMAIN — AdGuard has no way to look them up. Always set
  `upstream_dns` (Cloudflare DoT + Google DoT are sensible defaults).
- **Editing the filter file but not restarting AdGuard.** Filter changes are loaded
  at startup. Symptom: filter edit looks correct on disk but `nslookup` shows the old
  behaviour. Always `docker compose restart adguard` after edits.
- **Default-allowing wildcards (`@@||*.cloudfront.net^`).** Defeats the allowlist —
  one allowed parent gives blanket access to thousands of subdomains. List specific
  subdomains, not parents.
- **Missing default-deny line at the end of the filter file.** Without `||*^$important`
  as the final rule, AdGuard's behaviour for unmatched domains is "fall through to
  upstream resolver, then allow". The filter then permits everything that isn't
  explicitly blocked. ADR-0029 requires allowlist semantics — default deny is
  mandatory.

---

## Worked example: AdGuard for "AcmeApp" in strict mode

1. `docker/adguard/{conf,work,filters}` created.
2. `AdGuardHome.yaml` with `blocking_mode: nxdomain`, `upstream_dns: [tls://1.1.1.1, tls://8.8.8.8]`.
3. `acmeapp-allow.txt` with 7 allow rules (qdrant, huggingface CDN, github) +
   final `||*^$important`.
4. `docker compose up -d adguard` → starts in 4 s.
5. `nslookup api.openai.com 127.0.0.1` → NXDOMAIN ✅.
6. `nslookup qdrant 127.0.0.1` → 172.20.0.4 ✅.
7. AdGuard ready; context-mode bootstrap (E2) can proceed.

---

## Related skills / docs

- [.github/skills/ctx-bootstrap-network/SKILL.md](../ctx-bootstrap-network/SKILL.md) (D1 — allowlist contents)
- [.github/skills/setup-context-mode-new-project/SKILL.md](../setup-context-mode-new-project/SKILL.md) (E2)
- [docs/playbooks/context-mode-bootstrap.md](../../../docs/playbooks/context-mode-bootstrap.md) (P1)
- [docs/adr/0029/0029-context-mode-mcp-sandbox.md](../../../docs/adr/0029/0029-context-mode-mcp-sandbox.md) (§egress-policy)
- [scripts/adguard/domain-policy.ps1](../../../scripts/adguard/domain-policy.ps1) — PowerShell wrapper this repo uses
- [docker/adguard/](../../../docker/adguard/) — reference configuration
