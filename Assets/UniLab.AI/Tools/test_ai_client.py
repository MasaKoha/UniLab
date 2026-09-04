"""Unity を起動せずメールボックスクライアントの入出力を検証する。"""

import contextlib
import importlib.util
import io
import json
import os
from pathlib import Path
import tempfile
import threading
import time
import unittest
from unittest.mock import patch

MODULE_PATH = Path(__file__).with_name("ai_client.py")
SPECIFICATION = importlib.util.spec_from_file_location("ai_client", MODULE_PATH)
CLIENT = importlib.util.module_from_spec(SPECIFICATION)
SPECIFICATION.loader.exec_module(CLIENT)
TEST_TIMEOUT_SECONDS = 2.0


class AiClientTest(unittest.TestCase):
    def test_atomic_request_and_formatted_response(self):
        with tempfile.TemporaryDirectory() as directory:
            mailbox = Path(directory)
            received = []

            def respond():
                deadline = time.monotonic() + TEST_TIMEOUT_SECONDS
                while time.monotonic() < deadline:
                    requests = sorted(mailbox.glob("req-*.json"))
                    if requests:
                        request_path = requests[0]
                        received.append(json.loads(request_path.read_text(encoding="utf-8")))
                        response_path = mailbox / request_path.name.replace("req-", "res-", 1)
                        CLIENT.write_atomic(response_path, {"ok": True, "op": "ping", "text": "観測\n本文", "settled": False})
                        request_path.unlink()
                        return
                    time.sleep(CLIENT.POLL_INTERVAL_SECONDS)

            server = threading.Thread(target=respond)
            server.start()
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                result = CLIENT.main(["--mailbox", directory, "--timeout", str(TEST_TIMEOUT_SECONDS), "ping", '{"value":"日本語"}'])
            server.join(TEST_TIMEOUT_SECONDS)
            self.assertFalse(server.is_alive())
            self.assertEqual(result, 0)
            self.assertEqual(json.loads(received[0]["args"]), {"value": "日本語"})
            self.assertTrue((mailbox / ".enabled").exists())
            lines = output.getvalue().splitlines()
            self.assertEqual(json.loads(lines[0]), {"ok": True, "op": "ping", "settled": False})
            self.assertEqual(lines[1:], ["観測", "本文"])
            self.assertFalse(list(mailbox.glob("*.tmp")))
            self.assertEqual(len(list(mailbox.glob("res-*.json"))), 1)

    def test_timeout_keeps_request_and_reports_failure(self):
        with tempfile.TemporaryDirectory() as directory:
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                result = CLIENT.main(["--mailbox", directory, "--timeout", "0.01", "ping"])
            self.assertEqual(result, 1)
            self.assertIn("タイムアウト", json.loads(output.getvalue())["error"])
            self.assertEqual(len(list(Path(directory).glob("req-*.json"))), 1)

    def test_invalid_arguments_are_rejected_before_writing(self):
        for arguments in ("[]", "{", '{"value":NaN}'):
            with self.subTest(arguments=arguments), tempfile.TemporaryDirectory() as directory:
                with contextlib.redirect_stdout(io.StringIO()):
                    result = CLIENT.main(["--mailbox", directory, "ping", arguments])
                self.assertEqual(result, 1)
                self.assertFalse(list(Path(directory).iterdir()))

    def test_explicit_mailbox_precedes_environment(self):
        with tempfile.TemporaryDirectory() as directory:
            with patch.dict(os.environ, {"UNILAB_AI_MAILBOX": "/unused"}):
                self.assertEqual(CLIENT.resolve_mailbox(directory), Path(directory).resolve())

    def test_parent_search_and_project_bootstrap(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "Assets").mkdir()
            (root / "ProjectSettings").mkdir()
            child = root / "Assets" / "Child"
            child.mkdir()
            with patch.dict(os.environ, {}, clear=True), patch.object(Path, "cwd", return_value=child):
                self.assertEqual(CLIENT.resolve_mailbox(None), root.resolve() / CLIENT.MAILBOX_RELATIVE_PATH)
                (root / CLIENT.MAILBOX_RELATIVE_PATH).mkdir(parents=True)
                self.assertEqual(CLIENT.resolve_mailbox(None), root.resolve() / CLIENT.MAILBOX_RELATIVE_PATH)


if __name__ == "__main__":
    unittest.main()
