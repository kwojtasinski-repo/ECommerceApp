"""Shared helpers for the RAG MVP: config loading, path → metadata extraction, weight resolution."""
from __future__ import annotations

import fnmatch
import os
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import yaml

REPO_ROOT = Path(__file__).resolve().parents[2]
CONFIG_PATH = Path(__file__).resolve().parent / "config.yaml"


@dataclass(frozen=True)
class Config:
    raw: dict[str, Any]

    @property
    def source_roots(self) -> list[Path]:
        return [REPO_ROOT / r for r in self.raw["source"]["roots"]]

    @property
    def exclude_globs(self) -> list[str]:
        return list(self.raw["source"].get("exclude_globs", []))

    @property
    def embedder_model(self) -> str:
        return self.raw["embedder"]["model"]

    @property
    def embedder_device(self) -> str:
        return self.raw["embedder"].get("device", "cpu")

    @property
    def collection(self) -> str:
        return self.raw["vector_store"]["collection"]

    @property
    def vector_mode(self) -> str:
        return self.raw["vector_store"].get("mode", "memory")

    @property
    def vector_url(self) -> str:
        return self.raw["vector_store"].get("url", "http://localhost:6333")

    @property
    def chunker(self) -> dict[str, Any]:
        return self.raw["chunker"]

    @property
    def ranking(self) -> dict[str, Any]:
        return self.raw["ranking"]

    @property
    def query_defaults(self) -> dict[str, Any]:
        return self.raw["query"]

    @property
    def snapshot_path(self) -> Path:
        return REPO_ROOT / self.raw["storage"]["snapshot_path"]

    @property
    def manifest_path(self) -> Path:
        return REPO_ROOT / self.raw["storage"]["manifest_path"]


def load_config(path: Path = CONFIG_PATH) -> Config:
    with path.open("r", encoding="utf-8") as fh:
        return Config(raw=yaml.safe_load(fh))


def is_excluded(rel_path: str, exclude_globs: list[str]) -> bool:
    return any(fnmatch.fnmatch(rel_path, g) for g in exclude_globs)


def iter_markdown_files(cfg: Config) -> list[Path]:
    files: list[Path] = []
    for root in cfg.source_roots:
        if not root.exists():
            continue
        for path in root.rglob("*.md"):
            rel = path.relative_to(REPO_ROOT).as_posix()
            if is_excluded(rel, cfg.exclude_globs):
                continue
            files.append(path)
    return sorted(files)


_ADR_FOLDER_RE = re.compile(r"docs/adr/(\d{4})/")
_BC_FROM_TITLE_RE = re.compile(r"^#\s+ADR-\d+\s*[—:-]\s*(.+?)\s*$", re.MULTILINE)


def detect_adr_id(rel_path: str) -> str | None:
    m = _ADR_FOLDER_RE.search(rel_path)
    return m.group(1) if m else None


def detect_doc_kind(rel_path: str) -> str:
    """Coarse classification used in metadata for filtering and debugging."""
    p = rel_path.replace("\\", "/")
    if "/amendments/" in p:
        return "adr_amendment"
    if "/example-implementation/" in p:
        return "adr_example"
    if p.endswith("/checklist.md"):
        return "adr_checklist"
    if p.endswith("/migration-plan.md"):
        return "adr_migration_plan"
    if p.endswith("/README.md") and "/adr/" in p:
        return "adr_router"
    if "/adr/" in p:
        return "adr_main"
    if p.startswith(".github/context/"):
        return "context"
    if p.startswith("docs/architecture/"):
        return "architecture"
    if p.startswith("docs/patterns/"):
        return "pattern"
    if p.startswith("docs/reference/"):
        return "reference"
    if p.startswith("docs/roadmap/"):
        return "roadmap"
    return "other"


def resolve_weight(rel_path: str, file_size_bytes: int, ranking_cfg: dict[str, Any]) -> float:
    """First matching pattern in ranking.weights wins. Stubs (tiny files) are buried."""
    if file_size_bytes < ranking_cfg.get("stub_byte_threshold", 400):
        # An ADR main file is never a stub by definition; only example-implementation files
        # are commonly empty during the stubs phase.
        if "/example-implementation/" in rel_path.replace("\\", "/"):
            return 0.05
    rel_posix = rel_path.replace("\\", "/")
    for entry in ranking_cfg.get("weights", []):
        if fnmatch.fnmatch(rel_posix, entry["pattern"]):
            return float(entry["weight"])
    return 1.0
