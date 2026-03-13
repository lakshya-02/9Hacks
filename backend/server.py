"""
SF3D FastAPI Backend  —  Two-Port Architecture
===============================================
Port 8080  (Upload)   Unity  →  POST /generate     →  sends image, gets job_id back
Port 8081  (Download) Unity  →  GET  /download/{id} →  receives .glb binary

Usage:
    cd d:/stable-fast-3d
    python backend/server.py
"""

import argparse
import asyncio
import io
import logging
import sys
import uuid
from contextlib import asynccontextmanager, nullcontext
from pathlib import Path

# ── Path fix: add stable-fast-3d root so sf3d module is importable ────────────
ROOT_DIR    = Path(__file__).resolve().parent.parent   # d:/stable-fast-3d
OUTPUT_DIR  = ROOT_DIR / "output" / "jobs"             # where GLB files are saved
sys.path.insert(0, str(ROOT_DIR))
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
# ─────────────────────────────────────────────────────────────────────────────

import rembg
import torch
import uvicorn
from fastapi import FastAPI, File, HTTPException, Query, Request, UploadFile
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse, JSONResponse, Response
from PIL import Image

from sf3d.system import SF3D
from sf3d.utils import get_device, remove_background, resize_foreground

logging.basicConfig(level=logging.INFO, format="%(asctime)s  %(levelname)s  %(message)s")
logger = logging.getLogger(__name__)


def parse_args():
    p = argparse.ArgumentParser(description="SF3D Backend")
    p.add_argument("--host",               default="0.0.0.0",                    type=str)
    p.add_argument("--upload-port",        default=8080,                          type=int)
    p.add_argument("--download-port",      default=8081,                          type=int)
    p.add_argument("--device",             default=get_device(),                  type=str)
    p.add_argument("--pretrained-model",   default="stabilityai/stable-fast-3d", type=str)
    p.add_argument("--texture-resolution", default=2048,                          type=int)
    p.add_argument("--foreground-ratio",   default=0.85,                          type=float)
    p.add_argument("--remesh",             default="quad",
                   choices=["none", "triangle", "quad"])
    return p.parse_args()


args = parse_args()

# ── Shared model state (both apps share the same loaded model) ────────────────
_model         = None
_rembg_session = None
_device        = None
# ─────────────────────────────────────────────────────────────────────────────


def load_model():
    """Load SF3D model into GPU. Called once at startup."""
    global _model, _rembg_session, _device

    _device = args.device
    if not (torch.cuda.is_available() or torch.backends.mps.is_available()):
        _device = "cpu"

    logger.info(f"Loading SF3D model on device: {_device}")
    _model = SF3D.from_pretrained(
        args.pretrained_model,
        config_name="config.yaml",
        weight_name="model.safetensors",
    )
    _model.to(_device)
    _model.eval()

    _rembg_session = rembg.new_session()
    logger.info("Model loaded. Both servers are ready.")


def unload_model():
    global _model
    logger.info("Releasing GPU memory...")
    del _model
    if torch.cuda.is_available():
        torch.cuda.empty_cache()


# ── Lifespan for upload app (loads model, shared with download app) ───────────
@asynccontextmanager
async def upload_lifespan(app: FastAPI):
    load_model()
    yield
    unload_model()


# ═════════════════════════════════════════════════════════════════════════════
#  PORT 8080  —  UPLOAD APP  (Unity sends image here)
# ═════════════════════════════════════════════════════════════════════════════
upload_app = FastAPI(title="SF3D Upload Server", version="1.0.0", lifespan=upload_lifespan)

upload_app.add_middleware(CORSMiddleware, allow_origins=["*"], allow_methods=["*"], allow_headers=["*"])


@upload_app.get("/")
async def upload_root():
    return {
        "server":  "SF3D Upload Server",
        "port":    args.upload_port,
        "route":   "POST /generate — send image, get job_id",
        "device":  _device,
    }


@upload_app.get("/health")
async def upload_health():
    return {
        "status":       "ok",
        "device":       _device,
        "model_loaded": _model is not None,
        "texture_resolution": args.texture_resolution,
        "remesh":       args.remesh,
    }


@upload_app.post("/generate")
async def upload_generate(
    file:                 UploadFile = File(...),
    texture_resolution:  int        = Query(default=None),
    foreground_ratio:    float      = Query(default=None),
    remesh:              str        = Query(default=None),
    target_vertex_count: int        = Query(default=-1),
):
    """
    Unity sends:  POST http://{ip}:8080/generate  (multipart, field='file')
    Returns:      {"job_id": "uuid"}
    """
    if _model is None:
        raise HTTPException(status_code=503, detail="Model not ready yet.")

    tex_res    = texture_resolution or args.texture_resolution
    fg_ratio   = foreground_ratio   or args.foreground_ratio
    remesh_opt = remesh             or args.remesh
    job_id     = str(uuid.uuid4())

    # 1 — read and preprocess
    try:
        image_bytes = await file.read()
        image = Image.open(io.BytesIO(image_bytes)).convert("RGBA")
        image = remove_background(image, _rembg_session)
        image = resize_foreground(image, fg_ratio)
    except Exception as exc:
        raise HTTPException(status_code=400, detail=f"Image read failed: {exc}")

    # 2 — run SF3D
    try:
        with torch.no_grad():
            ctx = (
                torch.autocast(device_type=_device, dtype=torch.bfloat16)
                if "cuda" in _device else nullcontext()
            )
            with ctx:
                mesh, _ = _model.run_image(
                    image,
                    bake_resolution=tex_res,
                    remesh=remesh_opt,
                    vertex_count=target_vertex_count,
                )
    except Exception as exc:
        raise HTTPException(status_code=500, detail=f"Inference failed: {exc}")

    # 3 — save GLB to disk so download server can serve it
    glb_path = OUTPUT_DIR / f"{job_id}.glb"
    glb_buffer = io.BytesIO()
    mesh.export(glb_buffer, file_type="glb", include_normals=True)
    glb_bytes = glb_buffer.getvalue()
    glb_path.write_bytes(glb_bytes)

    logger.info(f"Job {job_id} | {len(glb_bytes):,} bytes | tex={tex_res}px | remesh={remesh_opt}")

    # 4 — return job_id to Unity
    return {"job_id": job_id}


@upload_app.api_route("/{full_path:path}", methods=["GET", "POST", "PUT", "DELETE", "OPTIONS"])
async def upload_catch_all(request: Request, full_path: str):
    return JSONResponse(status_code=200, content={
        "info": f"/{full_path} not found on upload server (port {args.upload_port})",
        "valid_routes": ["GET /", "GET /health", "POST /generate"],
    })


# ═════════════════════════════════════════════════════════════════════════════
#  PORT 8081  —  DOWNLOAD APP  (Unity fetches .glb from here)
# ═════════════════════════════════════════════════════════════════════════════
download_app = FastAPI(title="SF3D Download Server", version="1.0.0")

download_app.add_middleware(CORSMiddleware, allow_origins=["*"], allow_methods=["*"], allow_headers=["*"])


@download_app.get("/")
async def download_root():
    return {
        "server": "SF3D Download Server",
        "port":   args.download_port,
        "route":  "GET /download/{job_id} — fetch generated .glb",
    }


@download_app.get("/download/{job_id}")
async def download_model(job_id: str):
    """
    Unity sends:  GET http://{ip}:8081/download/{job_id}
    Returns:      raw .glb binary
    """
    glb_path = OUTPUT_DIR / f"{job_id}.glb"

    if not glb_path.exists():
        raise HTTPException(status_code=404, detail=f"Job '{job_id}' not found or not ready.")

    return FileResponse(
        path=str(glb_path),
        media_type="model/gltf-binary",
        filename="mesh.glb",
    )


@download_app.api_route("/{full_path:path}", methods=["GET", "POST", "PUT", "DELETE", "OPTIONS"])
async def download_catch_all(request: Request, full_path: str):
    return JSONResponse(status_code=200, content={
        "info": f"/{full_path} not found on download server (port {args.download_port})",
        "valid_routes": ["GET /", "GET /download/{job_id}"],
    })


# ═════════════════════════════════════════════════════════════════════════════
#  MAIN — run both servers simultaneously
# ═════════════════════════════════════════════════════════════════════════════
async def main():
    cfg_upload   = uvicorn.Config(upload_app,   host=args.host, port=args.upload_port,   log_level="warning")
    cfg_download = uvicorn.Config(download_app, host=args.host, port=args.download_port, log_level="warning")

    srv_upload   = uvicorn.Server(cfg_upload)
    srv_download = uvicorn.Server(cfg_download)

    logger.info(f"Upload   server → http://localhost:{args.upload_port}   (POST /generate)")
    logger.info(f"Download server → http://localhost:{args.download_port}  (GET  /download/{{job_id}})")

    await asyncio.gather(srv_upload.serve(), srv_download.serve())


if __name__ == "__main__":
    asyncio.run(main())
