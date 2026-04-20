#!/usr/bin/env python3
"""
LeanFlow Edge Agent — Raspberry Pi / Edge Device
Circle 4: Shopfloor Bridge

Connects to HiveMQ Cloud and publishes shopfloor data to LeanFlow.
Runs on any device with Python 3 and paho-mqtt installed.

Install: pip install paho-mqtt
Run: python3 leanflow_edge_agent.py
"""

import paho.mqtt.client as mqtt
import json
import time
import ssl
import os
import random
from datetime import datetime

# ── Configuration ──────────────────────────────────────────────
BROKER_HOST = os.getenv("MQTT_BROKER_HOST", "f3af16b6da07485e8956e1ef4e3d8e37.s1.eu.hivemq.cloud")
BROKER_PORT = int(os.getenv("MQTT_BROKER_PORT", "8883"))
USERNAME = os.getenv("MQTT_USERNAME", "leanflow")
PASSWORD = os.getenv("MQTT_PASSWORD", "LeanFlow2026!")
DEVICE_ID = os.getenv("DEVICE_ID", "PI-EDGE-001")

# ── MQTT Topics ─────────────────────────────────────────────────
TOPIC_PRODUCTION = "leanflow/production/{item_code}/completed"
TOPIC_INVENTORY  = "leanflow/inventory/{item_code}/count"
TOPIC_MACHINE    = "leanflow/machine/{machine_id}/status"
TOPIC_HEARTBEAT  = "leanflow/edge/{device_id}/heartbeat"

# ── Connection ──────────────────────────────────────────────────
client = mqtt.Client(client_id=f"leanflow-edge-{DEVICE_ID}")
client.username_pw_set(USERNAME, PASSWORD)
client.tls_set(cert_reqs=ssl.CERT_REQUIRED, tls_version=ssl.PROTOCOL_TLS)

def on_connect(client, userdata, flags, rc):
    if rc == 0:
        print(f"[{datetime.now()}] Connected to HiveMQ Cloud")
        # Send heartbeat on connect
        send_heartbeat()
    else:
        print(f"[{datetime.now()}] Connection failed: {rc}")

def on_disconnect(client, userdata, rc):
    print(f"[{datetime.now()}] Disconnected: {rc}")

client.on_connect = on_connect
client.on_disconnect = on_disconnect

# ── Publisher Functions ─────────────────────────────────────────
def publish(topic, payload):
    result = client.publish(topic, json.dumps(payload), qos=1)
    print(f"[{datetime.now()}] Published to {topic}: {payload}")
    return result

def send_heartbeat():
    publish(TOPIC_HEARTBEAT.format(device_id=DEVICE_ID), {
        "deviceId": DEVICE_ID,
        "status": "ONLINE",
        "timestamp": datetime.utcnow().isoformat()
    })

def report_production_completed(item_code, quantity, operator_id="OPERATOR"):
    """Call this when a work order is completed"""
    publish(TOPIC_PRODUCTION.format(item_code=item_code), {
        "quantity": quantity,
        "operatorId": operator_id,
        "deviceId": DEVICE_ID,
        "timestamp": datetime.utcnow().isoformat()
    })

def report_inventory_count(item_code, count):
    """Call this when a barcode scan updates inventory"""
    publish(TOPIC_INVENTORY.format(item_code=item_code), {
        "count": count,
        "deviceId": DEVICE_ID,
        "timestamp": datetime.utcnow().isoformat()
    })

def report_machine_status(machine_id, status, downtime_hours=0):
    """Call this when machine status changes"""
    publish(TOPIC_MACHINE.format(machine_id=machine_id), {
        "status": status,
        "downtimeHours": downtime_hours,
        "deviceId": DEVICE_ID,
        "timestamp": datetime.utcnow().isoformat()
    })

# ── Demo Mode ───────────────────────────────────────────────────
def run_demo_mode():
    """
    Demo mode: simulates a factory shift
    Replace this with real sensor/barcode scanner code
    """
    print(f"\n[{datetime.now()}] Starting LeanFlow Edge Agent — Demo Mode")
    print(f"Device: {DEVICE_ID}")
    print(f"Broker: {BROKER_HOST}:{BROKER_PORT}")
    print("=" * 60)

    client.connect(BROKER_HOST, BROKER_PORT, keepalive=60)
    client.loop_start()
    time.sleep(2)

    # Simulate a factory shift
    events = [
        ("production", "ITEM-001", 50, None),
        ("inventory",  "ITEM-003", 280, None),
        ("machine",    None, None, ("SMT-01", "RUNNING", 0)),
        ("production", "ITEM-002", 25, None),
        ("machine",    None, None, ("PRESS-01", "DOWNTIME", 2)),
        ("inventory",  "ITEM-005", 8, None),
        ("production", "ITEM-004", 75, None),
        ("machine",    None, None, ("PRESS-01", "RUNNING", 0)),
    ]

    for event in events:
        event_type = event[0]
        if event_type == "production":
            report_production_completed(event[1], event[2], f"OP-{DEVICE_ID}")
        elif event_type == "inventory":
            report_inventory_count(event[1], event[2])
        elif event_type == "machine":
            machine_data = event[3]
            report_machine_status(machine_data[0], machine_data[1], machine_data[2])
        time.sleep(1)

    # Send final heartbeat
    send_heartbeat()
    print(f"\n[{datetime.now()}] Demo shift completed — {len(events)} events published")
    print("LeanFlow has received and processed all events automatically.")

    time.sleep(2)
    client.loop_stop()
    client.disconnect()

# ── Real Mode (for actual Raspberry Pi deployment) ──────────────
def run_real_mode():
    """
    Real mode: connects and waits for real events
    Integrate your barcode scanner / sensor here
    """
    print(f"\n[{datetime.now()}] Starting LeanFlow Edge Agent — Real Mode")
    client.connect(BROKER_HOST, BROKER_PORT, keepalive=60)
    client.loop_start()

    print("Edge agent running. Press Ctrl+C to stop.")
    print("Sending heartbeat every 60 seconds...")

    try:
        while True:
            send_heartbeat()
            time.sleep(60)
    except KeyboardInterrupt:
        print("\nStopping edge agent...")
        client.loop_stop()
        client.disconnect()

# ── Main ────────────────────────────────────────────────────────
if __name__ == "__main__":
    import sys
    mode = sys.argv[1] if len(sys.argv) > 1 else "demo"
    if mode == "real":
        run_real_mode()
    else:
        run_demo_mode()