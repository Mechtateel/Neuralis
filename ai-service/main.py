import logging
import os
from pathlib import Path

from dotenv import load_dotenv
from fastapi import FastAPI

from routes.analyze import router as analyze_router
from routes.interpret import router as interpret_router
from routes.lab_analyze import router as lab_analyze_router

# Load ai-service/.env before any service reads OPENAI_API_KEY
_env_file = Path(__file__).resolve().parent / ".env"
load_dotenv(_env_file)

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s %(name)s %(message)s",
)

logger = logging.getLogger(__name__)

if os.getenv("OPENAI_API_KEY", "").strip():
    logger.info("[OpenAI] API key configured (source=.env or environment)")
else:
    logger.warning("[OpenAI] API key missing — clinical interpretation will use fallback text")

app = FastAPI(
    title="Neuralis AI Service",
    version="0.1.0",
    description="Educational symptom analysis with optional GPT interpretation. Not for diagnosis.",
)

app.include_router(analyze_router)
app.include_router(interpret_router)
app.include_router(lab_analyze_router)


@app.get("/health")
async def health() -> dict:
    return {
        "status": "ok",
        "openai_configured": bool(os.getenv("OPENAI_API_KEY", "").strip()),
    }
