"""Integration checks for /analyze-text (NLP always; OpenAI optional)."""

import os
import unittest

from fastapi.testclient import TestClient

from main import app
from services.openai_interpreter import _FALLBACK
from services.text_analyzer import analyze_text

TEST_INPUT = "Chest pain, dizziness, fatigue"


class TestAnalyzePipeline(unittest.TestCase):
    def test_nlp_chest_pain_dizziness_fatigue(self):
        """Step 7 test input: NLP must run without OpenAI."""
        r = analyze_text(TEST_INPUT)
        self.assertIn("chest pain", r.symptoms)
        self.assertIn("dizziness", r.symptoms)
        self.assertIn("fatigue", r.symptoms)
        self.assertEqual(r.risk_level, "high")
        self.assertEqual(r.confidence, 85.0)
        self.assertIn("not a medical diagnosis", r.recommendation.lower())

    def test_analyze_text_endpoint_returns_nlp_when_openai_missing(self):
        """Full HTTP path: NLP fields present even if OPENAI_API_KEY unset."""
        prev = os.environ.pop("OPENAI_API_KEY", None)
        try:
            with TestClient(app) as client:
                resp = client.post("/analyze-text", json={"text": TEST_INPUT})
            self.assertEqual(resp.status_code, 200)
            body = resp.json()
            self.assertIn("chest pain", body["symptoms"])
            self.assertIn(body["risk_level"], ("low", "medium", "high"))
            self.assertTrue(body.get("recommendation"))
            self.assertEqual(body.get("ai_interpretation"), _FALLBACK)
        finally:
            if prev is not None:
                os.environ["OPENAI_API_KEY"] = prev

    def test_health_reports_openai_configured_flag(self):
        with TestClient(app) as client:
            resp = client.get("/health")
        self.assertEqual(resp.status_code, 200)
        self.assertIn("openai_configured", resp.json())


if __name__ == "__main__":
    unittest.main()
