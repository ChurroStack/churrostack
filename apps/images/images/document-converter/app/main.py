"""Document Converter API.

Converts uploaded office documents to PDF using a headless LibreOffice
(`soffice`) subprocess per job. Flow is async: POST /convert enqueues the job
and returns 202 with a job id; GET /convert/{id} returns 202 (not ready) while
queued/converting, then 200 with the PDF once done. See README.md for the full
contract.

Jobs are held in an in-memory asyncio.Queue and drained by a single worker
task, so conversions run strictly in FIFO submission order, one at a time.

Cleanup: the original upload is deleted as soon as conversion succeeds, and the
resulting PDF is expired (deleted) PDF_TTL seconds after it becomes ready by an
in-process periodic sweep — no system cron involved.
"""

from __future__ import annotations

import asyncio
import logging
import os
import shutil
import subprocess
import time
import uuid
from contextlib import asynccontextmanager
from dataclasses import dataclass, field
from pathlib import Path
from typing import Optional

from fastapi import APIRouter, FastAPI, File, HTTPException, UploadFile
from fastapi.responses import FileResponse, JSONResponse

logger = logging.getLogger("document-converter")
logging.basicConfig(
    level=os.getenv("LOG_LEVEL", "INFO"),
    format="%(asctime)s %(levelname)s [document-converter] %(message)s",
)

# --- Settings (all overridable via ENV) -----------------------------------

BASE_PATH = os.getenv("BASE_PATH", "").rstrip("/")
WORK_DIR = Path(os.getenv("WORK_DIR", "/app/home/jobs"))
SOFFICE_BIN = os.getenv("SOFFICE_BIN", "soffice")
CONVERT_TIMEOUT = int(os.getenv("CONVERT_TIMEOUT", "120"))
PDF_TTL = int(os.getenv("PDF_TTL", "300"))
SWEEP_INTERVAL = int(os.getenv("SWEEP_INTERVAL", "60"))
MAX_UPLOAD_MB = int(os.getenv("MAX_UPLOAD_MB", "100"))
MAX_UPLOAD_BYTES = MAX_UPLOAD_MB * 1024 * 1024

WORK_DIR.mkdir(parents=True, exist_ok=True)

# --- Job registry (in-memory; see README for the single-replica caveat) ---


@dataclass
class Job:
    id: str
    status: str  # pending | processing | done | failed
    dir: Path
    original_name: str
    input_path: Optional[Path] = None
    output_path: Optional[Path] = None
    error: Optional[str] = None
    created_at: float = field(default_factory=time.time)
    ready_at: Optional[float] = None


JOBS: dict[str, Job] = {}
# Explicit FIFO queue + a single consumer worker below: jobs are converted
# strictly in the order they were submitted, one at a time.
JOB_QUEUE: "asyncio.Queue[str]" = asyncio.Queue()


def _convert_sync(job: Job) -> None:
    """Blocking soffice invocation; must run off the event loop thread."""
    profile_dir = job.dir / "lo"
    cmd = [
        SOFFICE_BIN,
        "--headless",
        "--norestore",
        "--convert-to",
        "pdf",
        "--outdir",
        str(job.dir),
        str(job.input_path),
        # Per-job profile: required for a writable LibreOffice profile when
        # running as a non-root UID and to avoid a shared-profile lock.
        f"-env:UserInstallation=file://{profile_dir}",
    ]
    result = subprocess.run(cmd, capture_output=True, text=True, timeout=CONVERT_TIMEOUT)
    expected_output = job.dir / (job.input_path.stem + ".pdf")
    if result.returncode != 0 or not expected_output.exists():
        raise RuntimeError(
            f"soffice exited {result.returncode}: "
            f"{(result.stderr or result.stdout or '').strip()[:2000]}"
        )
    job.output_path = expected_output


async def _run_conversion(job_id: str) -> None:
    """Converts a single job. Only ever called by the single worker loop below,
    so no extra locking is needed to keep conversions serialized."""
    job = JOBS.get(job_id)
    if job is None:
        return
    job.status = "processing"
    logger.info("job=%s status=processing input=%s", job_id, job.original_name)
    try:
        await asyncio.to_thread(_convert_sync, job)
    except Exception as exc:  # noqa: BLE001 - surface soffice/timeout failures to the client
        job.status = "failed"
        job.error = str(exc)
        logger.warning("job=%s status=failed error=%s", job_id, job.error)
        return

    # Success: drop the original upload immediately, keep only the PDF on disk.
    try:
        if job.input_path and job.input_path.exists():
            job.input_path.unlink()
    except OSError as exc:
        logger.warning("job=%s cleanup_input_failed error=%s", job_id, exc)

    job.status = "done"
    job.ready_at = time.time()
    logger.info("job=%s status=done output=%s", job_id, job.output_path)


async def _worker_loop() -> None:
    """The single consumer of JOB_QUEUE: pulls job ids strictly in the order
    they were queued and converts them one at a time. Running exactly one of
    these tasks is what gives the FIFO, one-at-a-time processing guarantee."""
    while True:
        job_id = await JOB_QUEUE.get()
        try:
            await _run_conversion(job_id)
        except Exception:  # noqa: BLE001 - never let one bad job kill the worker
            logger.exception("job=%s worker_loop_unhandled_error", job_id)
        finally:
            JOB_QUEUE.task_done()


async def _sweep_loop() -> None:
    """In-process 'cron': expires ready PDFs PDF_TTL seconds after ready_at and
    clears out stale failed job dirs on the same TTL. No system cron used."""
    while True:
        await asyncio.sleep(SWEEP_INTERVAL)
        now = time.time()
        expired_ids = [
            jid
            for jid, job in JOBS.items()
            if (job.status == "done" and job.ready_at is not None and now - job.ready_at > PDF_TTL)
            or (job.status == "failed" and now - job.created_at > PDF_TTL)
        ]
        for jid in expired_ids:
            job = JOBS.pop(jid, None)
            if job is None:
                continue
            shutil.rmtree(job.dir, ignore_errors=True)
            logger.info("job=%s status=expired reason=ttl_sweep", jid)


@asynccontextmanager
async def lifespan(app: FastAPI):
    logger.info(
        "startup base_path=%r work_dir=%s pdf_ttl=%ss sweep_interval=%ss",
        BASE_PATH,
        WORK_DIR,
        PDF_TTL,
        SWEEP_INTERVAL,
    )
    worker_task = asyncio.create_task(_worker_loop())
    sweep_task = asyncio.create_task(_sweep_loop())
    try:
        yield
    finally:
        worker_task.cancel()
        sweep_task.cancel()
        for task in (worker_task, sweep_task):
            try:
                await task
            except asyncio.CancelledError:
                pass


# --- App -------------------------------------------------------------------

app = FastAPI(
    title="Document Converter",
    description="Converts uploaded documents to PDF via headless LibreOffice.",
    docs_url=f"{BASE_PATH}/docs",
    openapi_url=f"{BASE_PATH}/openapi.json",
    redoc_url=f"{BASE_PATH}/redoc",
    lifespan=lifespan,
)

router = APIRouter()


@router.post("/convert", status_code=202)
async def create_conversion(file: UploadFile = File(...)):
    if not file.filename:
        raise HTTPException(status_code=400, detail="Uploaded file must have a filename")

    job_id = uuid.uuid4().hex
    job_dir = WORK_DIR / job_id
    job_dir.mkdir(parents=True, exist_ok=True)

    suffix = Path(file.filename).suffix
    input_path = job_dir / f"input{suffix}"

    size = 0
    try:
        with input_path.open("wb") as out:
            while chunk := await file.read(1024 * 1024):
                size += len(chunk)
                if size > MAX_UPLOAD_BYTES:
                    raise HTTPException(
                        status_code=413,
                        detail=f"File exceeds MAX_UPLOAD_MB={MAX_UPLOAD_MB}",
                    )
                out.write(chunk)
    except HTTPException:
        shutil.rmtree(job_dir, ignore_errors=True)
        raise

    job = Job(
        id=job_id,
        status="pending",
        dir=job_dir,
        original_name=file.filename,
        input_path=input_path,
    )
    JOBS[job_id] = job
    queue_position = JOB_QUEUE.qsize() + 1
    logger.info(
        "job=%s status=pending input=%s size_bytes=%s queue_position=%s",
        job_id, file.filename, size, queue_position,
    )

    await JOB_QUEUE.put(job_id)

    return JSONResponse(
        status_code=202,
        content={
            "id": job_id,
            "status": "pending",
            "queue_position": queue_position,
            "status_url": f"{BASE_PATH}/convert/{job_id}",
        },
    )


@router.get("/convert/{job_id}")
async def get_conversion(job_id: str, meta: bool = False):
    job = JOBS.get(job_id)
    if job is None:
        # Covers both "never existed" and "PDF already expired and swept".
        raise HTTPException(status_code=404, detail="Unknown or expired job id; please re-submit")

    if job.status in ("pending", "processing"):
        return JSONResponse(
            status_code=202,
            content={"id": job.id, "status": job.status},
            headers={"Retry-After": "2"},
        )

    if job.status == "failed":
        return JSONResponse(
            status_code=500,
            content={"id": job.id, "status": "failed", "error": job.error},
        )

    # status == "done"
    if meta:
        return {"id": job.id, "status": "done", "ready_at": job.ready_at}

    if not job.output_path or not job.output_path.exists():
        raise HTTPException(status_code=404, detail="PDF has expired; please re-submit")

    pdf_name = Path(job.original_name).stem + ".pdf"
    return FileResponse(job.output_path, media_type="application/pdf", filename=pdf_name)


app.include_router(router, prefix=BASE_PATH)


@app.get("/health")
async def health():
    """Unprefixed liveness probe (kept outside BASE_PATH for simple k8s wiring)."""
    return {"status": "ok"}
