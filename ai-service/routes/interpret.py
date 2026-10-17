import logging

from fastapi import APIRouter
from pydantic import BaseModel, Field

from services.openai_interpreter import generate_interpretation

logger = logging.getLogger(__name__)

router = APIRouter()


class InterpretTextRequest(BaseModel):
    raw_text: str = Field(..., min_length=1)
    symptoms: list[str] = Field(default_factory=list)
    possible_conditions: list[str] = Field(default_factory=list)
    risk_level: str = Field(default="low")
    confidence: float = Field(default=50.0, ge=0, le=100)


class InterpretTextResponse(BaseModel):
    ai_interpretation: str = Field(default="AI interpretation unavailable.")


@router.post("/interpret-text", response_model=InterpretTextResponse)
async def interpret_text_endpoint(body: InterpretTextRequest) -> InterpretTextResponse:
    """GPT overlay only — rule-based NLP runs in NeuralisAPI against PostgreSQL."""
    logger.info(
        "interpret-text: GPT overlay (symptoms=%d, risk=%s)",
        len(body.symptoms),
        body.risk_level,
    )
    text = await generate_interpretation(
        raw_text=body.raw_text,
        symptoms=body.symptoms,
        possible_conditions=body.possible_conditions,
        risk_level=body.risk_level,
        confidence=body.confidence,
    )
    return InterpretTextResponse(ai_interpretation=text)
