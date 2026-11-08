"""Isolated HTTPS -> real service -> journal and two-channel audio integration test.

Run from repository root after cargo build. Needs Output/LoLIntegration/tls/{cert,key}.pem.
Port 2999 must be free; if LOL is running, exit it before running this fixture.
--service PATH selects an already-built debug/release binary; this script never builds.
Tiny valid WAV packs and developer traces prove routing/queueing at volume zero.
"""
import argparse
import http.server
import json
import os
from pathlib import Path
import re
import socket
import ssl
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.request
import wave

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "Output/LoLIntegration"
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--ui", action="store_true")
parser.add_argument("--service", type=Path, default=ROOT / "KillConfirmService/target/debug/cskillconfirm.exe")
args = parser.parse_args()
PROFILE = OUT / ("smoke-profile-" + time.strftime("%Y%m%d-%H%M%S") + "-" + str(os.getpid()))
PROFILE.mkdir(parents=True, exist_ok=True)
OPENER = urllib.request.build_opener(urllib.request.ProxyHandler({}))
data_lock = threading.Lock()
sample = {
    "activePlayer": {"riotId": "国服测试#一区", "summonerName": "中文旧名"},
    "allPlayers": [{"riotId": "国服测试#一区", "summonerName": "中文旧名"},
                   {"riotId": "队友#二区", "summonerName": "队友"}],
    "events": {"Events": []}, "gameData": {"gameTime": 100.0},
}
available = True
revision = 0
served_revision = -1


class RiotFixture(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        global served_revision
        with data_lock:
            payload = json.dumps(sample, ensure_ascii=False).encode()
            ready = available
            response_revision = revision
        self.send_response(200 if ready else 503)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)
        with data_lock:
            served_revision = max(served_revision, response_revision)

    def log_message(self, *_):
        pass


def kill(at, killer="国服测试#一区", target="敌人"):
    return {"EventName": "ChampionKill", "EventTime": at, "KillerName": killer, "VictimName": target}


def multikill(at, count, killer="国服测试#一区"):
    return {"EventName": "Multikill", "EventTime": at, "KillerName": killer, "KillStreak": count}


def add_events(*new_events):
    global revision
    with data_lock:
        events = sample["events"]["Events"]
        for event in new_events:
            events.append(dict(event, EventID=len(events)))
            sample["gameData"]["gameTime"] = max(sample["gameData"]["gameTime"], event["EventTime"])
        revision += 1
        return revision


def wait_sample(target_revision):
    def seen():
        with data_lock:
            return served_revision >= target_revision
    wait_until(seen)


def wait_until(predicate, timeout=12, description="condition"):
    until = time.monotonic() + timeout
    while time.monotonic() < until:
        if predicate():
            return
        time.sleep(.025)
    raise AssertionError(description + " timed out; evidence: " + str(PROFILE))


with socket.socket() as reservation:
    reservation.bind(("127.0.0.1", 0))
    port = reservation.getsockname()[1]

server = http.server.ThreadingHTTPServer(("127.0.0.1", 2999), RiotFixture)
context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
context.load_cert_chain(OUT / "tls/cert.pem", OUT / "tls/key.pem")
server.socket = context.wrap_socket(server.socket, server_side=True)
threading.Thread(target=server.serve_forever, daemon=True).start()
env = dict(os.environ, KILLCONFIRM_DATA_ROOT=str(PROFILE))
service = None
token = ""


def start():
    global service, token
    service = subprocess.Popen([str(args.service.resolve()),
                                "--port", str(port), "--volume", "0", "--developer-mode"],
                               cwd=ROOT / "Widget/KillConfirmService", env=env,
                               stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                               creationflags=subprocess.CREATE_NO_WINDOW)
    wait_until(lambda: (PROFILE / "service-auth-token.txt").exists())
    token = (PROFILE / "service-auth-token.txt").read_text().strip()
    def ready():
        if service.poll() is not None:
            raise RuntimeError("service exited; inspect " + str(PROFILE / "service.log"))
        try:
            return api("/health")[0] == 200
        except OSError:
            return False
    wait_until(ready)


def api(path, body=None):
    raw = None if body is None else json.dumps(body).encode()
    request = urllib.request.Request(f"http://127.0.0.1:{port}{path}", data=raw,
                                     headers={"x-killconfirm-token": token, "Content-Type": "application/json"})
    try:
        with OPENER.open(request, timeout=4) as response:
            content = response.read()
            return response.status, json.loads(content) if content else None
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode()


def select(source, initialize=False):
    status, result = api("/game-source/settings", {"source": source, "initialize_only": initialize})
    assert status == 200, result
    return result


def all_events():
    status, result = api("/events?after=0&wait_ms=250")
    assert status in (200, 204), result
    return result["events"] if status == 200 else []


def events():
    """Combat feedback only; LOL observation records are asserted separately."""
    return [event for event in all_events() if event["event_channel"] == "combat"]


def observations():
    return [event for event in all_events() if event["event_channel"] == "observation"]


def assert_observation(event, generation):
    lol = event["detail"]["lol"]
    assert event["schema"] == 2 and event["source"] == "lol", event
    assert event["source_generation"] == generation, event
    assert event["intent"]["kind"] == "observation" and not event["intent"]["play_main"], event
    assert event["kill_count"] == 0 and not event["play_main_animation"], event
    assert event["event_kind"] == "lol_" + lol["event"], event
    return lol


def configure_audio():
    channels = {}
    for short, kind in (("sfx", "sound_effect"), ("voice", "character_voice")):
        folder = PROFILE / ("fixture-" + short)
        folder.mkdir(exist_ok=True)
        slots = {}
        for count in range(1, 6):
            name = f"lol-fixture-{short}-kill{count}.wav"
            with wave.open(str(folder / name), "wb") as wav:
                wav.setnchannels(1)
                wav.setsampwidth(2)
                wav.setframerate(8000)
                wav.writeframes(b"\0\0" * 400)
            slots[f"kill_{count}"] = name
        (folder / "manifest.json").write_text(json.dumps({
            "id": "lol-integration-" + short, "name": "LOL integration " + short,
            "audio_kind": kind, "audio_routing": "generic_event",
            "audio": {"slots": slots, "overlay_slots": []},
        }), encoding="utf-8")
        channels[kind] = {"preset": "lol-integration-" + short, "custom_path": str(folder),
                          "enabled": True, "volume_percent": 100, "interrupt_previous": True}
    status, result = api("/audio/channels", channels)
    assert status == 200, result


epoch_counts = []
audio_counts = []
active_generation = 0


def audio_evidence():
    path = PROFILE / "service-trace.log"
    trace = path.read_text(encoding="utf-8") if path.exists() else ""
    routing = "\n".join(line for line in trace.splitlines() if "Lua returned " in line)
    channels = {kind: [int(value) for value in re.findall(r"lol-fixture-" + kind + r"-kill([1-5])\.wav", routing)]
                for kind in ("sfx", "voice")}
    queued = [int(value) for value in re.findall(r"LOL combat audio queued: kill_count=(\d+)", trace)]
    return channels, queued


def assert_audio():
    wait_until(lambda: len(audio_evidence()[1]) >= len(audio_counts), description="LOL audio queue")
    channels, queued = audio_evidence()
    assert queued == audio_counts, ("audio queue", queued, audio_counts)
    for kind, actual in channels.items():
        assert actual == audio_counts, (kind, actual, audio_counts)
    log = (PROFILE / "service.log").read_text(encoding="utf-8")
    assert "LOL combat audio failed:" not in log, log
    assert "failed to add file to mixer:" not in log, log
    assert "audio channel playback failed:" not in log, log


def assert_journal():
    actual = events()
    assert [event["kill_count"] for event in actual] == epoch_counts, actual
    for event in actual:
        assert event["source_generation"] == active_generation, event
        assert event["player_name"] == "国服测试#一区", event
        assert event["play_main_animation"] and event["event_channel"] == "combat", event
        assert not any(event[key] for key in ("is_headshot", "is_knife_kill", "is_grenade_kill", "is_assist")), event
        # Event contract v2 fields are appended to the same v1 event.
        assert event["schema"] == 2 and event["source"] == "lol", event
        assert event["intent"]["kind"] == "kill" and event["intent"]["tier"] == event["kill_count"], event
        lol = event["detail"]["lol"]
        assert lol["event"] == "champion_kill" and lol["local_role"] == "killer" and lol["relation"] == "self", event
        assert lol.get("multikill", 1) == event["kill_count"], event


def expect_more(*counts):
    epoch_counts.extend(counts)
    audio_counts.extend(counts)
    wait_until(lambda: len(events()) >= len(epoch_counts), description="LOL event publication")
    assert_journal()
    assert_audio()


def no_extra(delay=.85):
    time.sleep(delay)
    assert_journal()
    assert_audio()


results = []
try:
    start()
    configure_audio()
    select("cs2")
    add_events(kill(100), multikill(100, 5))  # historical kill/marker before attachment
    first = select("lol")
    active_generation = first["generation"]
    wait_until(lambda: api("/game-source/status")[1]["posts"] > 0)
    no_extra()
    results.append("PASS: HTTPS self-signed loopback connection; historical kill ignored")
    add_events(kill(110), kill(110.1, killer="队友"),
               {"EventName": "DragonKill", "EventTime": 110.2, "KillerName": "国服测试#一区"},
               multikill(110.3, 2))  # unmatched marker cannot create a kill
    expect_more(1)
    no_extra()
    recorded = [assert_observation(event, active_generation) for event in observations()]
    assert [(item["event"], item["local_role"]) for item in recorded] == [
        ("champion_kill", "none"), ("dragon_kill", "killer"), ("multikill", "killer")], recorded
    assert recorded[0]["actor"] == "队友" and recorded[0]["victim"] == "敌人", recorded
    assert recorded[1]["relation"] == "self" and recorded[2]["multikill"] == 2, recorded
    results.append("PASS: Chinese identity, self-only champion kill, dedup, no objective/multikill double-trigger")
    results.append("PASS: teammate kill, dragon and unmatched marker are silent observation records with detail.lol")

    observed_before = len(observations())
    add_events({"EventName": "FirstBlood", "EventTime": 120.0, "Recipient": "队友"},
               {"EventName": "Ace", "EventTime": 120.1, "Acer": "国服测试#一区", "AcingTeam": "ORDER"},
               {"EventName": "BaronKill", "EventTime": 120.2, "KillerName": "队友", "Stolen": "True",
                "Assisters": ["国服测试#一区"]},
               {"EventName": "TurretKilled", "EventTime": 120.3, "KillerName": "Minion_T100L1S04N0024",
                "TurretKilled": "Turret_T2_L_03_A", "Assisters": []},
               {"EventName": "FutureEvent", "EventTime": 120.4, "Payload": {"x": 1}})
    wait_until(lambda: len(observations()) >= observed_before + 5, description="LOL observation publication")
    no_extra()
    recorded = [assert_observation(event, active_generation) for event in observations()[observed_before:]]
    assert [item["event"] for item in recorded] == [
        "first_blood", "ace", "baron_kill", "turret_killed", "other"], recorded
    assert recorded[1]["local_role"] == "acer" and recorded[2]["local_role"] == "assister", recorded
    assert recorded[2]["stolen"] is True and recorded[3]["relation"] == "neutral", recorded
    assert recorded[3]["structure"] == "Turret_T2_L_03_A", recorded
    assert recorded[4]["riot_event_name"] == "FutureEvent" and recorded[4]["extra"]["Payload"]["x"] == 1, recorded
    results.append("PASS: FirstBlood/Ace/Baron/Turret/unknown events recorded without animation or audio")

    add_events(kill(111), multikill(111, 2))
    expect_more(2)
    add_events(kill(112), multikill(112, 3))
    expect_more(3)
    add_events(kill(113), multikill(113, 4), kill(114), multikill(114, 5))
    expect_more(4, 5)
    no_extra()
    status, journal = api("/events?after=0&wait_ms=250")
    assert status == 200, journal
    (PROFILE / "journal-all-events.json").write_text(json.dumps(journal, ensure_ascii=False, indent=2), encoding="utf-8")
    # The rendering replay consumes combat feedback only.
    journal["events"] = [event for event in journal["events"] if event["event_channel"] == "combat"]
    assert [event["kill_count"] for event in journal["events"]] == [1, 2, 3, 4, 5], journal
    (PROFILE / "journal-events.json").write_text(json.dumps(journal, ensure_ascii=False, indent=2), encoding="utf-8")
    results.append("PASS: official Multikill 2/3/4/5 in same/across polls; one event and both WAV routes per kill")
    results.append("Rendering replay events: " + str(PROFILE / "journal-events.json"))

    add_events(multikill(111, 2), multikill(112, 3), multikill(114, 5))
    no_extra()
    add_events(kill(300))
    expect_more(1)
    add_events(multikill(300, 2))  # already published single: never replay with a new tier
    no_extra()
    add_events(kill(500, target="敌人甲"), multikill(500, 2),
               kill(500, target="敌人乙"), multikill(500, 3))
    expect_more(2, 3)
    assert [event["target_name"] for event in events()[-2:]] == ["敌人甲", "敌人乙"]
    results.append("PASS: repeated/late markers do not replay; separated ordinary kill resets to 1; same-time kills keep order")

    assert api("/streak/settings", {"active": True, "streak_mode": "none"})[0] == 200
    add_events(kill(600), multikill(600, 5))
    expect_more(5)
    results.append("PASS: official LOL tiers remain independent of the CS/shared streak setting")

    assert select("csgo_legacy", initialize=True)["source"] == "lol"
    assert api("/gsi-game/settings", {"version": "cs2"})[0] == 409
    assert api("/counter-strike/cfg?version=cs2", {})[0] == 409
    results.append("PASS: stale UI initialization cannot override LOL; CS-only config is blocked")

    # Observed kill is emitted immediately; events accumulated during an outage
    # become a new baseline upon reconnect.
    add_events(kill(900))
    expect_more(1)
    with data_lock:
        available = False
    wait_until(lambda: api("/game-source/status")[1]["last_post_age_ms"] is None)
    add_events(kill(901), multikill(901, 4))
    with data_lock:
        available = True
    wait_until(lambda: api("/game-source/status")[1]["last_post_age_ms"] is not None)
    no_extra()
    add_events(kill(902, killer="中文旧名"), multikill(902, 2, killer="中文旧名"))
    expect_more(2)
    results.append("PASS: failure flushes only an observed pending kill; reconnect skips backlog; legacy alias still routes tier 2")

    select("csgo_legacy")
    epoch_counts.clear()
    add_events(multikill(1000, 3), kill(1001))
    no_extra()
    assert api("/gsi-game/settings")[1]["version"] == "csgo_legacy"
    second = select("lol")
    active_generation = second["generation"]
    wait_until(lambda: api("/game-source/status")[1]["posts"] > 0)
    assert second["generation"] != first["generation"]
    no_extra()
    add_events(kill(1010))
    expect_more(1)
    results.append("PASS: source switching clears queue and re-baselines without replay")
    service.terminate(); service.wait(timeout=5)
    start()
    configure_audio()
    epoch_counts.clear()
    persisted = api("/game-source/settings")[1]
    assert persisted["source"] == "lol"
    active_generation = persisted["generation"]
    assert select("cs2", initialize=True)["source"] == "lol"
    wait_until(lambda: api("/game-source/status")[1]["posts"] > 0)
    no_extra()
    add_events(kill(1100), multikill(1100, 5))
    expect_more(5)
    results.append("PASS: source persists across service restart; historical events skipped and new tier 5 routes both channels")
    evidence = {"service": str(args.service.resolve()), "profile": str(PROFILE),
                "expected_kill_counts": audio_counts, "resolved_wav_routes": audio_evidence()[0],
                "completed_audio_queues": audio_evidence()[1]}
    (PROFILE / "audio-evidence.json").write_text(json.dumps(evidence, indent=2), encoding="utf-8")
    results.append("Audio evidence: " + str(PROFILE / "audio-evidence.json"))
    if args.ui:
        for name in ("source-ui-pass.txt", "source-ui-failure.txt"):
            (PROFILE / name).unlink(missing_ok=True)
        settings = {"LocalService.Port": {"Kind": "int", "Text": str(port)},
                    "GameSource": {"Kind": "string", "Text": "lol"}}
        (PROFILE / "settings.json").write_text(json.dumps(settings), encoding="utf-8")
        executable = ROOT / "ControlPanel/bin/x64/Debug/net8.0-windows10.0.19041.0/win-x64/KillConfirmControlPanel.exe"
        ui_env = dict(env, KILLCONFIRM_UI_VALIDATION="1", KILLCONFIRM_GAME_SOURCE_VALIDATION="1",
                      KILLCONFIRM_INSTALL_ROOT=str(executable.parent) + os.sep)
        startup = subprocess.STARTUPINFO()
        startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        startup.wShowWindow = 0
        ui = subprocess.Popen([str(executable)], env=ui_env, startupinfo=startup,
                              stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        try:
            ui.wait(timeout=120)
            failure = PROFILE / "source-ui-failure.txt"
            assert not failure.exists(), failure.read_text() if failure.exists() else ""
            assert (PROFILE / "source-ui-pass.txt").exists(), "UI validation did not finish"
            results.append((PROFILE / "source-ui-pass.txt").read_text())
        finally:
            if ui.poll() is None:
                ui.terminate(); ui.wait(timeout=5)
    (PROFILE / "integration-pass.txt").write_text("\n".join(results), encoding="utf-8")
    (OUT / "integration-pass.txt").write_text("\n".join(results), encoding="utf-8")
    print("\n".join(results))
except Exception as error:
    (PROFILE / "integration-failure.txt").write_text("\n".join(results) + "\n" + repr(error), encoding="utf-8")
    print("Failure evidence: " + str(PROFILE), file=sys.stderr)
    raise
finally:
    if service and service.poll() is None:
        service.terminate(); service.wait(timeout=5)
    server.shutdown(); server.server_close()
