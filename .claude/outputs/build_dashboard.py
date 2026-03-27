import json
import os

DS = {"uid": "grafanacloud-prom", "type": "prometheus"}


def stat(pid, title, expr, unit="short", thresholds=None, x=0, y=0, w=4, h=3):
    return {
        "id": pid,
        "type": "stat",
        "title": title,
        "gridPos": {"x": x, "y": y, "w": w, "h": h},
        "datasource": DS,
        "targets": [
            {
                "datasource": DS,
                "expr": expr,
                "instant": True,
                "refId": "A",
            }
        ],
        "options": {
            "reduceOptions": {
                "calcs": ["lastNotNull"],
                "fields": "",
                "values": False,
            },
            "orientation": "auto",
            "textMode": "auto",
            "colorMode": "background",
            "graphMode": "none",
        },
        "fieldConfig": {
            "defaults": {
                "unit": unit,
                "thresholds": thresholds
                or {
                    "mode": "absolute",
                    "steps": [{"color": "green", "value": None}],
                },
            },
            "overrides": [],
        },
    }


def ts(pid, title, targets, unit="short", x=0, y=0, w=12, h=6):
    return {
        "id": pid,
        "type": "timeseries",
        "title": title,
        "gridPos": {"x": x, "y": y, "w": w, "h": h},
        "datasource": DS,
        "targets": targets,
        "options": {
            "tooltip": {"mode": "multi", "sort": "desc"},
            "legend": {"displayMode": "list", "placement": "bottom"},
        },
        "fieldConfig": {
            "defaults": {"unit": unit},
            "overrides": [],
        },
    }


def row_panel(pid, title, y):
    return {
        "id": pid,
        "type": "row",
        "title": title,
        "gridPos": {"x": 0, "y": y, "w": 24, "h": 1},
        "collapsed": False,
    }


panels = []

# ── Row 1: Overview ───────────────────────────────────────────────────────────
panels.append(row_panel(1, "Overview", 0))

red_at_1 = {
    "mode": "absolute",
    "steps": [{"color": "green", "value": None}, {"color": "red", "value": 1}],
}

overview_stats = [
    (
        2,
        "Requests / min",
        "sum(rate(http_server_request_duration_seconds_count[5m])) * 60",
        "reqpm",
        None,
    ),
    (
        3,
        "Avg Latency (ms)",
        "sum(rate(http_server_request_duration_seconds_sum[5m])) / sum(rate(http_server_request_duration_seconds_count[5m])) * 1000",
        "ms",
        None,
    ),
    (
        4,
        "Active Requests",
        "sum(http_server_active_requests)",
        "short",
        None,
    ),
    (
        5,
        "Active Connections",
        "sum(kestrel_active_connections)",
        "short",
        None,
    ),
    (
        6,
        "Memory Pooled (MB)",
        "sum(aspnetcore_memory_pool_pooled_bytes) / 1048576",
        "decmbytes",
        None,
    ),
    (
        7,
        "Rate Limit Rejects",
        'sum(aspnetcore_rate_limiting_requests_total{result="rejected"}) or vector(0)',
        "short",
        red_at_1,
    ),
]

for i, (pid, title, expr, unit, thr) in enumerate(overview_stats):
    panels.append(stat(pid, title, expr, unit=unit, thresholds=thr, x=i * 4, y=1, w=4, h=3))

# Request Rate timeseries (y=4)
panels.append(
    ts(
        8,
        "Request Rate (req/min)",
        [
            {
                "datasource": DS,
                "expr": "sum(rate(http_server_request_duration_seconds_count[1m])) * 60",
                "legendFormat": "req/min",
                "refId": "A",
            }
        ],
        unit="reqpm",
        x=0,
        y=4,
        w=12,
        h=6,
    )
)

# Latency p50/p95/p99 timeseries (y=4)
panels.append(
    ts(
        9,
        "Latency p50 / p95 / p99",
        [
            {
                "datasource": DS,
                "expr": "histogram_quantile(0.50, sum by (le) (rate(http_server_request_duration_seconds_bucket[5m]))) * 1000",
                "legendFormat": "p50",
                "refId": "A",
            },
            {
                "datasource": DS,
                "expr": "histogram_quantile(0.95, sum by (le) (rate(http_server_request_duration_seconds_bucket[5m]))) * 1000",
                "legendFormat": "p95",
                "refId": "B",
            },
            {
                "datasource": DS,
                "expr": "histogram_quantile(0.99, sum by (le) (rate(http_server_request_duration_seconds_bucket[5m]))) * 1000",
                "legendFormat": "p99",
                "refId": "C",
            },
        ],
        unit="ms",
        x=12,
        y=4,
        w=12,
        h=6,
    )
)

# Memory timeseries (y=10)
panels.append(
    ts(
        10,
        "Memory",
        [
            {
                "datasource": DS,
                "expr": "sum(aspnetcore_memory_pool_pooled_bytes) / 1048576",
                "legendFormat": "Pooled (MB)",
                "refId": "A",
            },
            {
                "datasource": DS,
                "expr": "sum(rate(aspnetcore_memory_pool_allocated_bytes_total[5m])) / 1048576",
                "legendFormat": "Alloc rate (MB/s)",
                "refId": "B",
            },
        ],
        unit="decmbytes",
        x=0,
        y=10,
        w=12,
        h=6,
    )
)

# Outbound HTTP by target (y=10)
panels.append(
    ts(
        11,
        "Outbound HTTP by Target (req/min)",
        [
            {
                "datasource": DS,
                "expr": "sum by (server_address) (rate(http_client_request_duration_seconds_count[5m])) * 60",
                "legendFormat": "{{server_address}}",
                "refId": "A",
            }
        ],
        unit="reqpm",
        x=12,
        y=10,
        w=12,
        h=6,
    )
)

# ── Row 2: Lead Pipeline ──────────────────────────────────────────────────────
panels.append(row_panel(12, "Lead Pipeline", 16))

yellow_at_1 = {
    "mode": "absolute",
    "steps": [{"color": "green", "value": None}, {"color": "yellow", "value": 1}],
}

lead_stats = [
    (13, "Leads Received",     "sum(leads_received_total) or vector(0)",           "short", None),
    (14, "Leads Enriched",     "sum(leads_enriched_total) or vector(0)",           "short", None),
    (15, "Notifications Sent", "sum(leads_notification_sent_total) or vector(0)", "short", None),
    (16, "Scraper Failures",   "sum(scraper_calls_failed_total) or vector(0)",    "short", red_at_1),
    (17, "Gmail Skipped",      "sum(gmail_token_missing_total) or vector(0)",     "short", yellow_at_1),
    (18, "Drive Skipped",      "sum(gdrive_token_missing_total) or vector(0)",    "short", yellow_at_1),
]

for i, (pid, title, expr, unit, thr) in enumerate(lead_stats):
    panels.append(stat(pid, title, expr, unit=unit, thresholds=thr, x=i * 4, y=17, w=4, h=3))

# ── Row 3: Claude API ─────────────────────────────────────────────────────────
panels.append(row_panel(19, "Claude API", 20))

claude_stats = [
    (20, "Claude Calls",   "sum(claude_calls_total) or vector(0)",        "short",       None),
    (21, "Input Tokens",   "sum(claude_tokens_input_total) or vector(0)", "short",       None),
    (22, "Output Tokens",  "sum(claude_tokens_output_total) or vector(0)","short",       None),
    (23, "Estimated Cost", "sum(claude_cost_usd_USD_total) or vector(0)", "currencyUSD", None),
]

for i, (pid, title, expr, unit, thr) in enumerate(claude_stats):
    panels.append(stat(pid, title, expr, unit=unit, thresholds=thr, x=i * 6, y=21, w=6, h=3))

# Claude call duration timeseries (y=24)
panels.append(
    ts(
        24,
        "Claude Call Duration",
        [
            {
                "datasource": DS,
                "expr": "histogram_quantile(0.50, sum by (le) (rate(claude_call_duration_ms_bucket[5m])))",
                "legendFormat": "p50",
                "refId": "A",
            },
            {
                "datasource": DS,
                "expr": "histogram_quantile(0.95, sum by (le) (rate(claude_call_duration_ms_bucket[5m])))",
                "legendFormat": "p95",
                "refId": "B",
            },
        ],
        unit="ms",
        x=0,
        y=24,
        w=12,
        h=6,
    )
)

# Fan-out writes timeseries (y=24)
panels.append(
    ts(
        25,
        "Fan-Out Writes",
        [
            {
                "datasource": DS,
                "expr": "sum by (destination) (rate(fanout_writes_total[5m]))",
                "legendFormat": "{{destination}}",
                "refId": "A",
            }
        ],
        unit="wps",
        x=12,
        y=24,
        w=12,
        h=6,
    )
)

# ── Row 4: Form Funnel ────────────────────────────────────────────────────────
panels.append(row_panel(26, "Form Funnel", 30))

funnel_stats = [
    (27, "Viewed",    "sum(form_viewed_total) or vector(0)",    "short"),
    (28, "Started",   "sum(form_started_total) or vector(0)",   "short"),
    (29, "Submitted", "sum(form_submitted_total) or vector(0)", "short"),
    (30, "Succeeded", "sum(form_succeeded_total) or vector(0)", "short"),
]

for i, (pid, title, expr, unit) in enumerate(funnel_stats):
    panels.append(stat(pid, title, expr, unit=unit, x=i * 6, y=31, w=6, h=3))

# ── Assemble dashboard ────────────────────────────────────────────────────────
dashboard = {
    "uid": "real-estate-star-api",
    "title": "Real Estate Star \u2014 API",
    "tags": ["real-estate-star", "api"],
    "timezone": "browser",
    "schemaVersion": 38,
    "version": 1,
    "refresh": "30s",
    "panels": panels,
}

payload = {"dashboard": dashboard, "overwrite": True, "folderId": 0}

out_path = os.path.join(
    os.path.dirname(os.path.abspath(__file__)),
    "grafana_dashboard.json"
)

with open(out_path, "w") as f:
    json.dump(payload, f, indent=2)

print(f"Written: {out_path}")
print(f"Total panels: {len(panels)}")

# Quick sanity check
with open(out_path) as f:
    reloaded = json.load(f)
print(f"JSON is valid. Panel count in file: {len(reloaded['dashboard']['panels'])}")
