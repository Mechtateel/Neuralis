import logging

from fastapi import APIRouter

from models.schemas import AnalyzeTextRequest, AnalyzeTextResponse
from services.openai_interpreter import generate_interpretation
from services.text_analyzer import analyze_text

logger = logging.getLogger(__name__)

router = APIRouter()


@router.post("/analyze-text", response_model=AnalyzeTextResponse)
async def analyze_text_endpoint(body: AnalyzeTextRequest) -> AnalyzeTextResponse:
    logger.info(
        "analyze-text: NLP starting (input_chars=%d)",
        len(body.text or ""),
    )
    base = analyze_text(body.text)
    logger.info(
        "analyze-text: NLP complete (symptoms=%d, risk=%s, confidence=%s)",
        len(base.symptoms),
        base.risk_level,
        base.confidence,
    )

    base.ai_interpretation = await generate_interpretation(
        raw_text=body.text,
        symptoms=base.symptoms,
        possible_conditions=base.possible_conditions,
        risk_level=base.risk_level,
        confidence=base.confidence,
    )

    interpretation_ok = (
        base.ai_interpretation
        and base.ai_interpretation.strip().lower() != "ai interpretation unavailable."
    )
    logger.info(
        "analyze-text: response ready (openai_interpretation_active=%s)",
        interpretation_ok,
    )
    return base
