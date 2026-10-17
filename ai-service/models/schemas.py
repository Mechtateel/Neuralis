from typing import Literal

from pydantic import BaseModel, Field


class AnalyzeTextRequest(BaseModel):
    text: str = Field(..., min_length=1, description="Free-text clinical note or symptom description")


class AnalyzeTextResponse(BaseModel):
    """
    Structured local NLP output. Educational only — not a medical diagnosis.
    """

    symptoms: list[str] = Field(default_factory=list, description="Symptom phrases detected from text")
    possible_conditions: list[str] = Field(
        default_factory=list,
        description="Educational possibilities only; not a diagnosis",
    )
    recommendation: str = Field(
        description="Safe guidance including disclaimer and consult-a-doctor wording",
    )
    risk_level: Literal["low", "medium", "high"] = Field(
        description="Heuristic triage hint only; not clinical risk scoring",
    )
    confidence: float = Field(
        ge=0,
        le=100,
        description="Heuristic model confidence score (0–100), aligned with risk band — not a clinical probability",
    )
    ai_interpretation: str = Field(
        default="AI interpretation unavailable.",
        description="Optional GPT-enhanced educational interpretation; never diagnostic.",
    )


class LabMarkerInput(BaseModel):
    name: str = Field(..., description="Analyte or test name")
    value_display: str = Field(..., description="Value with unit and reference when available")
    status: str = Field(default="NORMAL", description="NORMAL | LOW | HIGH | CRITICAL_LOW")


class AnalyzeLabRequest(BaseModel):
    summary: str = Field(default="", description="Rule-based summary from LabInterpretationService")
    priority: str = Field(default="", description="Review priority HIGH | MEDIUM | LOW")
    findings: list[str] = Field(default_factory=list)
    recommendations: list[str] = Field(default_factory=list)
    laboratory_values: list[LabMarkerInput] = Field(default_factory=list)


class AnalyzeLabResponse(BaseModel):
    ai_laboratory_interpretation: str = Field(
        default="AI laboratory interpretation unavailable.",
        description="GPT-enhanced educational lab interpretation; never diagnostic.",
    )
