"""
Local rule-based text analysis. No external AI.
Not for diagnosis — possible conditions are educational hints only.

Production path: NeuralisAPI TextAnalysisService reads Symptoms/Recommendations from PostgreSQL.
This module remains for standalone ai-service /analyze-text calls and unit tests.
"""

import re
from collections import OrderedDict

from models.schemas import AnalyzeTextResponse

# Longer phrases first so "shortness of breath" wins over "breath"
_PHRASE_TO_SYMPTOM: list[tuple[str, str]] = [
    ("shortness of breath", "shortness of breath"),
    ("chest pain", "chest pain"),
    ("sore throat", "sore throat"),
    ("runny nose", "runny nose"),
    ("muscle ache", "muscle aches"),
    ("muscle aches", "muscle aches"),
    ("stomach ache", "stomach pain"),
    ("abdominal pain", "stomach pain"),
    ("loss of appetite", "loss of appetite"),
    ("night sweats", "night sweats"),
    ("high fever", "fever"),
    ("low grade fever", "fever"),
    ("feeling tired", "fatigue"),
    ("body aches", "muscle aches"),
    ("headache", "headache"),
    ("migraine", "headache"),
    ("fever", "fever"),
    ("temperature", "fever"),
    ("chills", "chills"),
    ("cough", "cough"),
    ("nausea", "nausea"),
    ("vomiting", "vomiting"),
    ("diarrhea", "diarrhea"),
    ("fatigue", "fatigue"),
    ("tired", "fatigue"),
    ("dizziness", "dizziness"),
    ("rash", "rash"),
    ("wheeze", "wheezing"),
    ("wheezing", "wheezing"),
    ("bleeding", "bleeding"),
    ("severe pain", "severe pain"),
    ("throat pain", "sore throat"),
]

# Symptom -> educational "possible conditions" (never framed as diagnosis)
_SYMPTOM_TO_CONDITIONS: dict[str, list[str]] = {
    "headache": ["Tension-type headache (possible)", "Migraine (possible)"],
    "fever": ["Viral illness (possible)", "Other causes of fever (possible)"],
    "chills": ["Febrile illness (possible)", "Viral or bacterial process (possible)"],
    "cough": ["Upper airway irritation (possible)", "Common cold / viral URI (possible)"],
    "sore throat": ["Viral pharyngitis (possible)", "Other throat irritation (possible)"],
    "runny nose": ["Allergic rhinitis (possible)", "Viral upper respiratory infection (possible)"],
    "shortness of breath": ["Asthma exacerbation (possible)", "Other cardiopulmonary causes (possible)"],
    "chest pain": ["Musculoskeletal chest wall pain (possible)", "Other causes needing evaluation (possible)"],
    "nausea": ["Gastroenteritis (possible)", "Other gastrointestinal upset (possible)"],
    "vomiting": ["Gastroenteritis (possible)", "Dehydration risk — seek care if persistent (possible)"],
    "diarrhea": ["Gastroenteritis (possible)", "Dietary intolerance (possible)"],
    "fatigue": ["Viral illness recovery (possible)", "Sleep deprivation or stress (possible)"],
    "muscle aches": ["Viral syndrome (possible)", "Overexertion (possible)"],
    "dizziness": ["Dehydration (possible)", "Inner ear issues (possible)"],
    "rash": ["Dermatitis (possible)", "Viral exanthem (possible)"],
    "wheezing": ["Reactive airways (possible)", "Asthma-like symptoms (possible)"],
    "stomach pain": ["Gastritis / indigestion (possible)", "Other abdominal causes (possible)"],
    "loss of appetite": ["Mild viral illness (possible)", "Many non-specific causes (possible)"],
    "night sweats": ["Infection or other causes (possible)", "Needs clinician review if ongoing (possible)"],
    "bleeding": ["Local injury (possible)", "Requires prompt medical attention if significant"],
    "severe pain": ["Many causes (possible)", "Seek urgent medical advice if severe or worsening"],
}


def _normalize_text(raw: str) -> str:
    return re.sub(r"\s+", " ", raw.strip().lower())


def extract_symptoms(text: str) -> list[str]:
    """Keyword / phrase matching only."""
    normalized = _normalize_text(text)
    if not normalized:
        return []

    found: OrderedDict[str, None] = OrderedDict()
    for phrase, symptom in _PHRASE_TO_SYMPTOM:
        if phrase in normalized:
            found.setdefault(symptom, None)

    # Also catch comma-separated tokens like "headache, fever"
    for token in re.split(r"[,;]+", normalized):
        t = token.strip()
        if not t:
            continue
        for phrase, symptom in _PHRASE_TO_SYMPTOM:
            if phrase == t or (len(t) > 2 and t == phrase):
                found.setdefault(symptom, None)
                break
        else:
            if t in ("headache", "fever", "cough", "nausea", "fatigue", "chills", "rash"):
                mapping = {
                    "headache": "headache",
                    "fever": "fever",
                    "cough": "cough",
                    "nausea": "nausea",
                    "fatigue": "fatigue",
                    "chills": "chills",
                    "rash": "rash",
                }
                found.setdefault(mapping[t], None)

    return list(found.keys())


def map_symptoms_to_conditions(symptoms: list[str]) -> list[str]:
    out: OrderedDict[str, None] = OrderedDict()
    for s in symptoms:
        for cond in _SYMPTOM_TO_CONDITIONS.get(s, []):
            out.setdefault(cond, None)
    if not out and symptoms:
        out.setdefault("Non-specific symptoms (possible — discuss with a clinician)", None)
    return list(out.keys())


def _confidence_from_risk(risk_level: str) -> float:
    """Display score aligned with risk band (educational, not calibrated probability)."""
    return {"low": 30.0, "medium": 60.0, "high": 85.0}.get(risk_level, 50.0)


def _compute_risk_level(symptoms: list[str], text_lower: str) -> str:
    high_triggers = {"chest pain", "shortness of breath", "severe pain", "bleeding"}
    if symptoms and high_triggers.intersection(set(symptoms)):
        return "high"
    if any(w in text_lower for w in ("can't breathe", "cannot breathe", "crushing chest", "coughing blood")):
        return "high"
    if "fever" in symptoms and ("cough" in symptoms or "sore throat" in symptoms):
        return "medium"
    if len(symptoms) >= 3:
        return "medium"
    if symptoms:
        return "low"
    return "low"


def _build_recommendation(symptoms: list[str], possible_conditions: list[str], risk_level: str) -> str:
    parts = [
        "This output lists **possible conditions** only for educational purposes.",
        "It is **not a medical diagnosis** and cannot replace professional assessment.",
        "Please **consult a doctor** or appropriate clinician if you are unwell, if symptoms are new or worsening, "
        "or if you are unsure.",
    ]
    if risk_level == "high":
        parts.append(
            "Some wording you used can match symptoms that may need **prompt in-person or emergency care** — "
            "when in doubt, seek medical attention."
        )
    elif risk_level == "medium":
        parts.append(
            "Consider scheduling a routine medical visit if symptoms persist beyond a few days or interfere with daily activities."
        )
    if not symptoms:
        parts.append(
            "No specific symptom keywords were detected; sharing more detail with a clinician will help them advise you."
        )
    if possible_conditions:
        parts.append(
            "Discuss any items that concern you with a qualified professional — they can interpret them in your full context."
        )
    return " ".join(parts)


def analyze_text(text: str) -> AnalyzeTextResponse:
    """Main entry: structured local analysis."""
    text_lower = _normalize_text(text)
    symptoms = extract_symptoms(text)
    possible_conditions = map_symptoms_to_conditions(symptoms)
    risk_level = _compute_risk_level(symptoms, text_lower)
    recommendation = _build_recommendation(symptoms, possible_conditions, risk_level)

    return AnalyzeTextResponse(
        symptoms=symptoms,
        possible_conditions=possible_conditions,
        recommendation=recommendation,
        risk_level=risk_level,
        confidence=_confidence_from_risk(risk_level),
    )
