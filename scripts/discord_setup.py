"""Set up the Gaffer's Notebook Discord server from scripts/discord_server.json: roles, categories, channels,
permission overwrites, welcome/FAQ posts and a permanent invite. Idempotent: existing roles/channels (by name) are kept.

Needs a bot that is a member of the server with Administrator permission. The token is read from
%USERPROFILE%\\.gaffers_discord_token (one line) or the DISCORD_BOT_TOKEN environment variable; it is never printed.

Usage: python scripts/discord_setup.py --guild <server id> [--spec scripts/discord_server.json] [--dry-run]
Standard library only.
"""
import argparse, base64, json, os, sys, time, urllib.request, urllib.error

API = "https://discord.com/api/v10"
PERM = {  # Discord permission bits
    "CREATE_INSTANT_INVITE": 1 << 0, "KICK_MEMBERS": 1 << 1, "ADMINISTRATOR": 1 << 3, "ADD_REACTIONS": 1 << 6,
    "VIEW_AUDIT_LOG": 1 << 7, "VIEW_CHANNEL": 1 << 10, "SEND_MESSAGES": 1 << 11, "MANAGE_MESSAGES": 1 << 13,
    "EMBED_LINKS": 1 << 14, "ATTACH_FILES": 1 << 15, "READ_MESSAGE_HISTORY": 1 << 16, "MENTION_EVERYONE": 1 << 17,
    "USE_EXTERNAL_EMOJIS": 1 << 18, "CONNECT": 1 << 20, "SPEAK": 1 << 21, "MUTE_MEMBERS": 1 << 22,
    "CHANGE_NICKNAME": 1 << 26, "MANAGE_NICKNAMES": 1 << 27, "MANAGE_THREADS": 1 << 34, "CREATE_PUBLIC_THREADS": 1 << 35,
    "SEND_MESSAGES_IN_THREADS": 1 << 38, "MODERATE_MEMBERS": 1 << 40,
}
TEXT, VOICE, CATEGORY = 0, 2, 4


def bits(names):
    return sum(PERM[n] for n in names)


def read_token():
    path = os.path.join(os.path.expanduser("~"), ".gaffers_discord_token")
    token = os.environ.get("DISCORD_BOT_TOKEN") or (open(path, encoding="utf-8").read().strip() if os.path.exists(path) else "")
    if not token:
        sys.exit(f"No token: put it in {path} (one line) or in DISCORD_BOT_TOKEN.")
    return token


class Discord:
    def __init__(self, token, dry_run):
        self.token, self.dry_run = token, dry_run

    def call(self, method, path, body=None, tolerant=False):
        """tolerant: a failure prints a warning and returns None instead of stopping (optional features like Community)."""
        if self.dry_run and method != "GET":
            print(f"  [dry-run] {method} {path} {json.dumps(body)[:120] if body else ''}")
            return {"id": "dry"}
        data = json.dumps(body).encode() if body is not None else None
        for attempt in range(6):
            req = urllib.request.Request(API + path, data=data, method=method, headers={
                "Authorization": f"Bot {self.token}", "Content-Type": "application/json", "User-Agent": "GaffersNotebookSetup/1.0"})
            try:
                with urllib.request.urlopen(req) as r:
                    text = r.read().decode()
                    return json.loads(text) if text else {}
            except urllib.error.HTTPError as e:
                detail = e.read().decode(errors="replace")
                if e.code == 429:
                    wait = float(json.loads(detail).get("retry_after", 1)) + 0.2
                    time.sleep(wait)
                    continue
                if tolerant:
                    print(f"  warning: {method} {path} failed: {e.code} {detail[:200]}")
                    return None
                sys.exit(f"{method} {path} failed: {e.code} {detail[:300]}")
        sys.exit(f"{method} {path}: rate limited too often")


def overwrites_for(spec_overwrites, roles, guild_id):
    out = []
    for role_name, rule in (spec_overwrites or {}).items():
        role_id = guild_id if role_name == "@everyone" else roles[role_name]
        out.append({"id": role_id, "type": 0, "allow": str(bits(rule.get("allow", []))), "deny": str(bits(rule.get("deny", [])))})
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--guild", required=True, help="server id (Discord: Settings > Advanced > Developer Mode, right-click server > Copy ID)")
    ap.add_argument("--spec", default=os.path.join(os.path.dirname(__file__), "discord_server.json"))
    ap.add_argument("--dry-run", action="store_true")
    args = ap.parse_args()
    spec = json.load(open(args.spec, encoding="utf-8"))
    d = Discord(read_token(), args.dry_run)
    g = args.guild

    guild = d.call("GET", f"/guilds/{g}")
    print(f"Server: {guild.get('name')} (owner {guild.get('owner_id')})")

    # Roles: @everyone base permissions, then the named roles (highest first so the order in the list is the hierarchy).
    existing = {r["name"]: r["id"] for r in d.call("GET", f"/guilds/{g}/roles")}
    d.call("PATCH", f"/guilds/{g}/roles/{g}", {"permissions": str(bits(spec["everyone"]["allow"]) & ~bits(spec["everyone"]["deny"]))})
    roles = {}
    for r in spec["roles"]:
        if r["name"] in existing:
            roles[r["name"]] = existing[r["name"]]
            print(f"role exists: {r['name']}")
            continue
        made = d.call("POST", f"/guilds/{g}/roles", {"name": r["name"], "color": int(r["color"].lstrip("#"), 16),
                                                     "hoist": r["hoist"], "mentionable": r["mentionable"], "permissions": str(bits(r["permissions"]))})
        roles[r["name"]] = made["id"]
        print(f"role created: {r['name']}")
    if "Maker" in roles and guild.get("owner_id"):
        d.call("PUT", f"/guilds/{g}/members/{guild['owner_id']}/roles/{roles['Maker']}")

    # Channels.
    channels = d.call("GET", f"/guilds/{g}/channels")
    by_name = {(c["type"], c.get("parent_id"), c["name"]): c["id"] for c in channels}
    cats = {c["name"]: c["id"] for c in channels if c["type"] == CATEGORY}
    posts = {}
    for cat in spec["categories"]:
        ow = overwrites_for(cat.get("overwrites"), roles, g)
        if cat["name"] in cats:
            cat_id = cats[cat["name"]]
            print(f"category exists: {cat['name']}")
        else:
            cat_id = d.call("POST", f"/guilds/{g}/channels", {"name": cat["name"], "type": CATEGORY, "permission_overwrites": ow})["id"]
            print(f"category created: {cat['name']}")
        for ch in cat["channels"]:
            ctype = VOICE if ch.get("type") == "voice" else TEXT
            key = (ctype, cat_id, ch["name"])
            if key in by_name:
                ch_id = by_name[key]
                print(f"  channel exists: #{ch['name']}")
            else:
                body = {"name": ch["name"], "type": ctype, "parent_id": cat_id, "permission_overwrites": ow}
                if ctype == TEXT:
                    body["topic"] = ch.get("topic", "")
                    body["rate_limit_per_user"] = ch.get("slowmode", 0)
                ch_id = d.call("POST", f"/guilds/{g}/channels", body)["id"]
                print(f"  channel created: #{ch['name']}")
            if ch.get("post"):
                posts[ch["post"]] = ch_id

    # Pinned posts: only when the channel is still empty, so re-running never duplicates them.
    for key, ch_id in posts.items():
        if not args.dry_run and d.call("GET", f"/channels/{ch_id}/messages?limit=1"):
            print(f"post exists: {key}")
            continue
        text = "\n".join(spec[key]).replace("{itch_url}", spec["itch_url"])
        msg = d.call("POST", f"/channels/{ch_id}/messages", {"content": text})
        if not args.dry_run:
            d.call("PUT", f"/channels/{ch_id}/pins/{msg['id']}")
        print(f"post created: {key}")

    all_channels = {c["name"]: c["id"] for c in d.call("GET", f"/guilds/{g}/channels") if c["type"] == TEXT}

    # Server settings: icon, safety levels, system channel, Community (rules/updates channels), locale.
    icon = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "local", "discord", "discord-icon.png")
    settings = {"verification_level": 2, "default_message_notifications": 1, "explicit_content_filter": 2, "preferred_locale": "en-US"}
    if os.path.exists(icon):
        settings["icon"] = "data:image/png;base64," + base64.b64encode(open(icon, "rb").read()).decode()
    else:
        print("no icon found: run python scripts/make_discord_art.py first")
    if "general" in all_channels:
        settings["system_channel_id"] = all_channels["general"]
        settings["system_channel_flags"] = 1 | 8   # no join sticker button, no boost messages
    d.call("PATCH", f"/guilds/{g}", settings)
    if "welcome" in all_channels and "mod-chat" in all_channels and "COMMUNITY" not in guild.get("features", []):
        d.call("PATCH", f"/guilds/{g}", {"features": guild.get("features", []) + ["COMMUNITY"],
                                         "rules_channel_id": all_channels["welcome"], "public_updates_channel_id": all_channels["mod-chat"]}, tolerant=True)

    # AutoMod: block spam, mention spam and the Discord keyword presets (profanity, sexual content, slurs); staff is exempt.
    existing_rules = {r["name"] for r in (d.call("GET", f"/guilds/{g}/auto-moderation/rules", tolerant=True) or [])}
    exempt = [roles[n] for n in ("Maker", "Moderator") if n in roles]
    block = [{"type": 1, "metadata": {"custom_message": "Message blocked: keep it friendly, please."}}]
    for name, trig, meta in (("Spam", 3, {}), ("Mention spam", 5, {"mention_total_limit": 8}), ("Bad words", 4, {"presets": [1, 2, 3]})):
        if name not in existing_rules:
            d.call("POST", f"/guilds/{g}/auto-moderation/rules", {"name": name, "event_type": 1, "trigger_type": trig, "trigger_metadata": meta,
                                                                  "actions": block, "enabled": True, "exempt_roles": exempt}, tolerant=True)
            print(f"automod rule: {name}")

    # Webhook for posting build news from outside Discord (announcements and devlog). The URL is a secret: saved to a file, never printed.
    hook_path = os.path.join(os.path.expanduser("~"), ".gaffers_discord_webhook")
    if "announcements" in all_channels and not os.path.exists(hook_path) and not args.dry_run:
        hook = d.call("POST", f"/channels/{all_channels['announcements']}/webhooks", {"name": "Gaffer's Notebook news"}, tolerant=True)
        if hook and hook.get("token"):
            open(hook_path, "w", encoding="utf-8").write(f"https://discord.com/api/webhooks/{hook['id']}/{hook['token']}\n")
            print(f"webhook saved to {hook_path}")

    if "welcome_message" in posts:
        inv = d.call("POST", f"/channels/{posts['welcome_message']}/invites", {"max_age": 0, "max_uses": 0, "unique": False})
        print(f"\nInvite: https://discord.gg/{inv.get('code', 'dry')}")
    print("Done. Banner (needs boost level 2) is set by hand: Server Settings > Overview, file local/discord/discord-banner.png.")


if __name__ == "__main__":
    main()
