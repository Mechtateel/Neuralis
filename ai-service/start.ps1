# Start Neuralis ai-service with .env loaded via main.py (load_dotenv).
# Create ai-service/.env from .env.example before first run.

Set-Location $PSScriptRoot

if (-not (Test-Path ".\.venv\Scripts\python.exe")) {
    Write-Error "Missing .venv. Run: python -m venv .venv; .\.venv\Scripts\pip install -r requirements.txt"
    exit 1
}

& .\.venv\Scripts\pip install -q -r requirements.txt
& .\.venv\Scripts\uvicorn main:app --host 0.0.0.0 --port 8000 --reload
