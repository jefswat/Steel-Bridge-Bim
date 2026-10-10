"""Drive a running Steel Bridge Modeler from Python (standard library only).

    python sbm_app_client.py status
    python sbm_app_client.py set "Bridges[0].Girders.Count" 6
    python sbm_app_client.py import-pgsuper "C:\\models\\Option1.pgs" "CL Route 1" 30+50
"""
import json
import os
import sys

PIPE_DIR = "\\\\.\\pipe\\"


def instances():
    """Process ids of running copies that accept commands."""
    return [int(n.split(".", 1)[1]) for n in os.listdir(PIPE_DIR)
            if n.startswith("SteelBridgeModeler.") and n.split(".", 1)[1].isdigit()]


def send(cmd, *args, pid=None):
    """Send one command; returns the result text or raises RuntimeError with the app's message."""
    ids = instances()
    if pid is None:
        if len(ids) != 1:
            raise RuntimeError("No running Steel Bridge Modeler." if not ids else f"Several copies running: {ids}; pass pid=")
        pid = ids[0]
    with open(f"{PIPE_DIR}SteelBridgeModeler.{pid}", "r+b", buffering=0) as f:
        f.write((json.dumps({"cmd": cmd, "args": [str(a) for a in args]}) + "\n").encode("utf-8"))
        resp = json.loads(f.readline().decode("utf-8"))
    if not resp["ok"]:
        raise RuntimeError(resp["result"])
    return resp["result"]


if __name__ == "__main__":
    print(send(*(sys.argv[1:] or ["status"])))
