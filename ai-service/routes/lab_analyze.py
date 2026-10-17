import logging

from fastapi import APIRouter

from models.schemas import AnalyzeLabRequest, AnalyzeLabResponse
from services.openai_interpreter import generate_lab_interpretation

logger = logging.getLogger(__name__)

router = APIRouter()


@router.post("/analyze-lab", response_model=AnalyzeLabResponse)
async def analyze_lab_endpoint(body: AnalyzeLabRequest) -> AnalyzeLabResponse:
    logger.info(
        "[LAB AI] OpenAI request sent (markers=%d, priority=%s)",
        len(body.laboratory_values),
        body.priority,
    )
    interpretation = await generate_lab_interpretation(
        summary=body.summary,
        priority=body.priority,
        findings=body.findings,
        recommendations=body.recommendations,
        laboratory_values=[
            {
                "name": m.name,
                "value_display": m.value_display,
                "status": m.status,
            }
            for m in body.laboratory_values
        ],
    )
    active = (
        interpretation.strip().lower() != "ai laboratory interpretation unavailable."
    )
    logger.info("[LAB AI] OpenAI response received (active=%s, chars=%d)", active, len(interpretation))
    payload = AnalyzeLabResponse(ai_laboratory_interpretation=interpretation)
    logger.info(
        "[LAB AI] FastAPI response payload: ai_laboratory_interpretation_len=%d",
        len(payload.ai_laboratory_interpretation),
    )
    return payload
