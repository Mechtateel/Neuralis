"""Step 2: example expectations for local NLP (stdlib unittest, no pytest required)."""

import unittest

from services.text_analyzer import analyze_text


class TestAnalyzeText(unittest.TestCase):
    """At least two documented input/output scenarios (see docstrings)."""

    def test_example_1_headache_comma_fever(self):
        """
        Input:  "headache, fever"
        Expect: symptoms include headache and fever;
                possible_conditions lists educational possibilities;
                recommendation contains disclaimer + consult a doctor;
                risk_level low or medium (two symptoms, not chest/sob).
        """
        r = analyze_text("headache, fever")
        self.assertIn("headache", r.symptoms)
        self.assertIn("fever", r.symptoms)
        self.assertTrue(len(r.possible_conditions) >= 1)
        self.assertIn("not a medical diagnosis", r.recommendation.lower())
        self.assertIn("consult a doctor", r.recommendation.lower())
        self.assertIn("possible conditions", r.recommendation.lower())
        self.assertIn(r.risk_level, ("low", "medium", "high"))
        self.assertGreaterEqual(r.confidence, 0)
        self.assertLessEqual(r.confidence, 100)

    def test_example_2_chest_pain_shortness_of_breath(self):
        """
        Input:  "I have chest pain and shortness of breath"
        Expect: both symptoms detected; risk_level high; safe wording preserved.
        """
        r = analyze_text("I have chest pain and shortness of breath")
        self.assertIn("chest pain", r.symptoms)
        self.assertIn("shortness of breath", r.symptoms)
        self.assertEqual(r.risk_level, "high")
        self.assertEqual(r.confidence, 85.0)
        self.assertIn("possible conditions", r.recommendation.lower())
        self.assertIn("not a medical diagnosis", r.recommendation.lower())
        self.assertIn("consult a doctor", r.recommendation.lower())


if __name__ == "__main__":
    unittest.main()
