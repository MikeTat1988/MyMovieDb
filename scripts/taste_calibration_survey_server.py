import argparse
import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import threading


def build_handler(repo_root: Path, html_path: Path, result_path: Path):
    text_result_path = result_path.with_suffix(".txt")

    class SurveyHandler(BaseHTTPRequestHandler):
        def log_message(self, format, *args):
            return

        def do_GET(self):
            if self.path not in ("/", "/survey"):
                self.send_response(404)
                self.end_headers()
                self.wfile.write(b"Not found")
                return

            body = html_path.read_bytes()
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def do_POST(self):
            if self.path != "/save":
                self.send_response(404)
                self.end_headers()
                self.wfile.write(b"Not found")
                return

            length = int(self.headers.get("Content-Length", "0"))
            raw = self.rfile.read(length)
            try:
                payload = json.loads(raw.decode("utf-8"))
                result_path.parent.mkdir(parents=True, exist_ok=True)
                result_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
                text_report = payload.get("textReport")
                if isinstance(text_report, str) and text_report.strip():
                    text_result_path.write_text(text_report, encoding="utf-8")
                response = json.dumps({"ok": True, "path": str(text_result_path)})
                body = response.encode("utf-8")
                self.send_response(200)
                self.send_header("Content-Type", "application/json; charset=utf-8")
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)
            except Exception as exc:
                body = str(exc).encode("utf-8")
                self.send_response(500)
                self.send_header("Content-Type", "text/plain; charset=utf-8")
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)

    return SurveyHandler


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=5127)
    parser.add_argument("--minutes", type=int, default=120)
    args = parser.parse_args()

    script_dir = Path(__file__).resolve().parent
    repo_root = script_dir.parent
    html_path = script_dir / "taste-calibration-survey.html"
    result_path = repo_root / "logs" / "taste-calibration-survey-results.json"

    if not html_path.exists():
        raise FileNotFoundError(f"Survey HTML not found: {html_path}")

    handler = build_handler(repo_root, html_path, result_path)
    server = ThreadingHTTPServer(("127.0.0.1", args.port), handler)
    timer = threading.Timer(args.minutes * 60, server.shutdown)
    timer.daemon = True
    timer.start()

    try:
        server.serve_forever()
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
