"""Local preview of the real post-payment page, with fixture APIs only.

Run: python tools/preview-paid-report.py
Open: http://127.0.0.1:8783/preview
No packages, database, application secrets or LLM calls are required.
"""

import argparse
import http.server
import json
import mimetypes
import pathlib
import re
import threading
import time
import urllib.parse
import uuid


ROOT = pathlib.Path(__file__).resolve().parent.parent
WEBROOT = ROOT / "wwwroot"
MODES = {"waiting", "auto", "ready", "error"}
RESULT = {
    "overall": 67,
    "levelTitle": "Есть вопросы, требующие внимания",
    "levelText": "Тестовая компания: пример результатов завершённой диагностики.",
    "confidence": 92,
    "criticalCount": 1,
    "highCount": 3,
    "mediumCount": 4,
    "sections": [
        {"title": title, "score": score, "status": "APPLICABLE"}
        for title, score in [
            ("Основатели и партнёрства", 45),
            ("Корпоративная структура", 70),
            ("Интеллектуальная собственность", 55),
            ("Команда и исполнители", 75),
            ("Продукт и пользователи", 80),
            ("Данные и ИИ", 60),
            ("Договоры", 85),
            ("Инвестиционная готовность", 65),
        ]
    ],
    "risks": [],
    "strongAreas": ["Оформлены основные договоры", "Определены условия использования продукта"],
}


class PreviewServer(http.server.ThreadingHTTPServer):
    daemon_threads = True

    def __init__(self, port, pdf):
        super().__init__(("127.0.0.1", port), PreviewHandler)
        self.pdf = pdf
        self.sessions = {}
        self.changed = threading.Condition()


class PreviewHandler(http.server.BaseHTTPRequestHandler):
    def send(self, data, status=200, content_type="application/json; charset=utf-8"):
        if not isinstance(data, bytes):
            data = json.dumps(data, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(data)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        try:
            self.wfile.write(data)
        except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError):
            pass  # Switching the iframe scenario cancels its pending request.

    def do_GET(self):
        url = urllib.parse.urlparse(self.path)
        path = url.path
        if path in ("/", "/preview"):
            return self.send((ROOT / "tools/paid-report-preview.html").read_bytes(),
                             content_type="text/html; charset=utf-8")
        if path == "/app":
            session_id = urllib.parse.parse_qs(url.query).get("session", [""])[0]
            with self.server.changed:
                exists = session_id in self.server.sessions
            if not exists:
                return self.send({"error": "unknown_preview_session"}, 404)
            # This origin belongs exclusively to the preview. Production JS stays unchanged.
            state = {"sessionId": session_id, "answers": {}, "currentQuestionId": None}
            init = "<script>localStorage.setItem('fenix_diagnostic_v1'," + json.dumps(json.dumps(state)) + ");</script>"
            html = (WEBROOT / "index.html").read_text(encoding="utf-8").replace("</head>", init + "</head>")
            return self.send(html.encode("utf-8"), content_type="text/html; charset=utf-8")
        if path == "/api/questionnaire":
            return self.send({"sections": [], "questions": [], "versions": {}})
        control = re.fullmatch(r"/preview/session/(preview-[a-f0-9]+)", path)
        if control:
            with self.server.changed:
                session = self.server.sessions.get(control[1])
                data = {"mode": session["mode"], "requests": session["requests"]} if session else None
            return self.send(data or {"error": "not_found"}, 200 if data else 404)
        match = re.fullmatch(r"/api/sessions/(preview-[a-f0-9]+)/(result|pdf)", path)
        if match:
            with self.server.changed:
                session = self.server.sessions.get(match[1])
            if session is None:
                return self.send({"error": "not_found"}, 404)
            if match[2] == "result":
                return self.send({"paid": True, "unlocked": True, "result": RESULT})
            return self.send_pdf(session)
        # Serve only actual public assets, never repository files or a real API.
        asset = (WEBROOT / urllib.parse.unquote(path).lstrip("/")).resolve()
        if WEBROOT in asset.parents and asset.is_file():
            return self.send(asset.read_bytes(), content_type=mimetypes.guess_type(asset.name)[0] or "application/octet-stream")
        return self.send({"error": "preview_endpoint_not_available"}, 404)

    def send_pdf(self, session):
        with self.server.changed:
            session["requests"] += 1
            while session["mode"] in ("waiting", "auto"):
                if session["mode"] == "auto" and time.monotonic() >= session["deadline"]:
                    session["mode"] = "ready"
                    break
                self.server.changed.wait(timeout=0.25)
            mode = session["mode"]
        if mode == "cancelled":
            return self.send({"error": "preview_cancelled"}, 409)
        if mode == "error":
            return self.send({"error": "preview_pdf_error"}, 500)
        return self.send(self.server.pdf, content_type="application/pdf")

    def do_POST(self):
        # This server is loopback-only; controls accept same-origin JSON requests.
        origin = self.headers.get("Origin")
        if origin and origin != "http://" + self.headers.get("Host", ""):
            return self.send({"error": "origin_not_allowed"}, 403)
        try:
            length = int(self.headers.get("Content-Length", 0))
            if length > 8192 or length < 0:
                return self.send({"error": "invalid_body"}, 400)
            data = json.loads(self.rfile.read(length) or b"{}")
            if not isinstance(data, dict):
                raise ValueError()
        except (ValueError, UnicodeDecodeError):
            return self.send({"error": "invalid_body"}, 400)
        path = urllib.parse.urlparse(self.path).path
        if path == "/api/events":
            return self.send({"ok": True})
        if data.get("mode") not in MODES:
            return self.send({"error": "invalid_mode"}, 400)
        try:
            delay = max(1, min(600, int(data.get("delay", 120))))
        except (ValueError, TypeError):
            return self.send({"error": "invalid_delay"}, 400)
        if path == "/preview/session":
            session_id = "preview-" + uuid.uuid4().hex
            with self.server.changed:
                for old in self.server.sessions.values():
                    old["mode"] = "cancelled"
                self.server.sessions = {session_id: {
                    "mode": data["mode"], "requests": 0, "deadline": time.monotonic() + delay,
                }}
                self.server.changed.notify_all()
            return self.send({"id": session_id})
        match = re.fullmatch(r"/preview/session/(preview-[a-f0-9]+)", path)
        if match:
            with self.server.changed:
                session = self.server.sessions.get(match[1])
                if session is None:
                    return self.send({"error": "not_found"}, 404)
                session["mode"] = data["mode"]
                session["deadline"] = time.monotonic() + delay
                self.server.changed.notify_all()
            return self.send({"ok": True})
        return self.send({"error": "preview_endpoint_not_available"}, 404)

    def log_message(self, format, *args):
        pass


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=8783)
    parser.add_argument("--pdf", type=pathlib.Path, default=ROOT / "scenario_medium.pdf",
                        help="Existing sample PDF used by the ready state (no generation).")
    args = parser.parse_args()
    if not args.pdf.is_file():
        parser.error("Sample PDF missing. Supply --pdf with an existing test PDF.")
    pdf = args.pdf.read_bytes()
    if not pdf.startswith(b"%PDF-"):
        parser.error("The sample file must be a PDF.")
    server = PreviewServer(args.port, pdf)
    print(f"Paid report preview: http://127.0.0.1:{args.port}/preview", flush=True)
    print("Fixture data only. Stop with Ctrl+C.", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
