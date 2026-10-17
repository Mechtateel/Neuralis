import logging
import os
import re
from typing import Final

import httpx

logger = logging.getLogger(__name__)

_OPENAI_URL: Final[str] = "https://api.openai.com/v1/chat/completions"
_MODEL: Final[str] = os.getenv("OPENAI_MODEL", "gpt-4o-mini")
_TIMEOUT_SECONDS: Final[float] = float(os.getenv("OPENAI_TIMEOUT_SECONDS", "12"))
_FALLBACK: Final[str] = "AI interpretation unavailable."
_LAB_FALLBACK: Final[str] = "AI laboratory interpretation unavailable."

_SYSTEM_PROMPT: Final[str] = (
    "You are a medical education assistant embedded in a clinical demo system. "
    "Generate educational, non-diagnostic interpretation only. "
    "Never provide definitive diagnoses, treatment plans, emergency orders, or fear-inducing wording. "
    "Use cautious language like 'may', 'can be associated with', 'possible'. "
    "Always include an explicit disclaimer that this is informational only and users should consult a licensed clinician."
)

_LAB_SYSTEM_PROMPT: Final[str] = (
    "You are a medical education assistant helping clinicians review laboratory results in a demo system. "
    "Provide educational laboratory interpretation only — never a diagnosis, prescription, or definitive conclusion. "
    "Never state certainty. Do not say the patient has a specific disease. "
    "Use cautious wording such as 'may indicate', 'could be associated with', 'may warrant further evaluation'. "
    "Explain which values appear abnormal, what patterns may be present, and whether medical follow-up may be appropriate. "
    "Correlate markers together when relevant. "
    "Always include that results must be interpreted with clinical history and professional assessment."
)


def _sanitize(text: str, *, fallback: str = _FALLBACK) -> str:
    cleaned = re.sub(r"\s+", " ", text).strip()
    if not cleaned:
        return fallback
    banned = [
        "you have",
        "confirmed",
        "definitely",
        "will die",
        "certain diagnosis",
        "this confirms",
        "you definitely have",
    ]
    low = cleaned.lower()
    if any(p in low for p in banned):
        if fallback == _LAB_FALLBACK:
            return (
                "The reported laboratory pattern may warrant further clinical evaluation. "
                "Such findings can be associated with several underlying conditions and should be "
                "interpreted together with medical history and professional assessment. "
                "This interpretation is educational only and not a medical diagnosis."
            )
        return (
            "Based on the entered text, the detected symptom pattern may be associated with multiple possible causes. "
            "This interpretation is informational only and not a medical diagnosis. "
            "Please consult a licensed healthcare professional for clinical evaluation."
        )
    if "not a medical diagnosis" not in low and "informational only" not in low and "educational only" not in low:
        cleaned += " This interpretation is educational only and not a medical diagnosis. Consult a licensed clinician."
    return cleaned


async def _complete_chat(*, system_prompt: str, user_prompt: str, max_tokens: int, fallback: str) -> str:
    api_key = (os.getenv("OPENAI_API_KEY") or "").strip()
    if not api_key:
        logger.warning("[OpenAI] API key missing")
        logger.info("[OpenAI] Fallback triggered (reason=no_api_key)")
        return fallback

    logger.info("[OpenAI] Request started")
    logger.info("[OpenAI] Model used: %s", _MODEL)

    payload = {
        "model": _MODEL,
        "temperature": 0.2,
        "max_tokens": max_tokens,
        "messages": [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": user_prompt},
        ],
    }
    headers = {"Authorization": f"Bearer {api_key}", "Content-Type": "application/json"}

    try:
        async with httpx.AsyncClient(timeout=_TIMEOUT_SECONDS) as client:
            response = await client.post(_OPENAI_URL, headers=headers, json=payload)
            response.raise_for_status()
            data = response.json()
        text = (
            data.get("choices", [{}])[0]
            .get("message", {})
            .get("content", "")
        )
        if not (text or "").strip():
            logger.warning("[OpenAI] Fallback triggered (reason=empty_content)")
            return fallback
        result = _sanitize(text, fallback=fallback)
        logger.info("[OpenAI] Response received (chars=%d)", len(result))
        logger.info("[OpenAI] Success")
        return result
    except httpx.TimeoutException:
        logger.warning("[OpenAI] Timeout")
        logger.info("[OpenAI] Fallback triggered (reason=timeout)")
        return fallback
    except httpx.HTTPStatusError as exc:
        logger.warning(
            "[OpenAI] HTTP error status=%s",
            exc.response.status_code if exc.response is not None else "unknown",
        )
        logger.info("[OpenAI] Fallback triggered (reason=http_error)")
        return fallback
    except Exception:
        logger.exception("[OpenAI] Request failed")
        logger.info("[OpenAI] Fallback triggered (reason=exception)")
        return fallback


async def generate_interpretation(
    *,
    raw_text: str,
    symptoms: list[str],
    possible_conditions: list[str],
    risk_level: str,
    confidence: float,
) -> str:
    user_prompt = (
        "Create one concise educational clinical interpretation paragraph.\n"
        f"Input text: {raw_text}\n"
        f"Detected symptoms: {symptoms}\n"
        f"Possible conditions (heuristic): {possible_conditions}\n"
        f"Risk level: {risk_level}\n"
        f"Confidence score: {confidence}\n"
        "Constraints: no diagnosis certainty, no treatment instructions, no emergency command language."
    )
    return await _complete_chat(
        system_prompt=_SYSTEM_PROMPT,
        user_prompt=user_prompt,
        max_tokens=220,
        fallback=_FALLBACK,
    )


async def generate_lab_interpretation(
    *,
    summary: str,
    priority: str,
    findings: list[str],
    recommendations: list[str],
    laboratory_values: list[dict[str, str]],
) -> str:
    markers_lines = [
        f"- {m.get('name', '')}: {m.get('value_display', '')} ({m.get('status', 'NORMAL')})"
        for m in laboratory_values
    ]
    user_prompt = (
        "Create one concise educational AI laboratory interpretation paragraph for a physician reviewer.\n"
        f"Summary (rule-based): {summary}\n"
        f"Priority: {priority}\n"
        f"Findings:\n" + "\n".join(f"  • {f}" for f in findings[:20]) + "\n"
        f"Recommendations:\n" + "\n".join(f"  • {r}" for r in recommendations[:10]) + "\n"
        "Laboratory values:\n" + ("\n".join(markers_lines) if markers_lines else "  (none extracted)") + "\n"
        "Explain abnormal values, possible patterns, educational significance, and follow-up considerations. "
        "Constraints: no diagnosis certainty, no prescriptions, no 'you have' disease statements."
    )
    return await _complete_chat(
        system_prompt=_LAB_SYSTEM_PROMPT,
        user_prompt=user_prompt,
        max_tokens=280,
        fallback=_LAB_FALLBACK,
    )
