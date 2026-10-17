"""Lab analyze endpoint — rule context in, OpenAI optional."""

import os
import unittest

from fastapi.testclient import TestClient

from main import app
from services.openai_interpreter import _LAB_FALLBACK


class TestAnalyzeLab(unittest.TestCase):
    def test_analyze_lab_returns_fallback_without_api_key(self):
        prev = os.environ.pop("OPENAI_API_KEY", None)
        try:
            with TestClient(app) as client:
                resp = client.post(
                    "/analyze-lab",
                    json={
                        "summary": "Abnormal findings detected: Hemoglobin LOW",
                        "priority": "HIGH",
                        "findings": ["Hemoglobin — 9.2 g/dL — LOW"],
                        "recommendations": ["Correlate with clinical context."],
                        "laboratory_values": [
                            {
                                "name": "Hemoglobin",
                                "value_display": "9.2 g/dL (ref 12–16)",
                                "status": "LOW",
                            },
                            {
                                "name": "WBC",
                                "value_display": "12.5 10^9/L (ref 4–11)",
                                "status": "HIGH",
                            },
                        ],
                    },
                )
            self.assertEqual(resp.status_code, 200)
            body = resp.json()
            self.assertEqual(body.get("ai_laboratory_interpretation"), _LAB_FALLBACK)
        finally:
            if prev is not None:
                os.environ["OPENAI_API_KEY"] = prev


if __name__ == "__main__":
    unittest.main()
