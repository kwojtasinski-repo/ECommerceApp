"""Shared helpers for the RAG MVP: config loading, path → metadata extraction, weight resolution."""
from __future__ import annotations

import fnmatch
import os
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import yaml

REPO_ROOT = Path(os.environ.get("RAG_WORKSPACE") or Path(__file__).resolve().parents[2])
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
        """Qdrant collection name. RAG_COLLECTION env var overrides config (useful for multi-repo)."""
        return os.environ.get("RAG_COLLECTION") or self.raw["vector_store"]["collection"]

    @property
    def adr_id_patterns(self) -> list[str]:
        """List of regex patterns (with named group 'id') to extract ADR ID from rel_path."""
        rules = self.raw.get("metadata_rules", {}).get("adr_id_patterns", [])
        return [r["pattern"] for r in rules]

    @property
    def doc_kind_rules(self) -> list[dict[str, str]]:
        """Ordered list of {glob, kind} rules for classifying documents."""
        return self.raw.get("metadata_rules", {}).get("doc_kind_rules", [])

    @property
    def vector_mode(self) -> str:
        return self.raw["vector_store"].get("mode", "memory")

    @property
    def vector_url(self) -> str:
        return self.raw["vector_store"].get("url", "http://localhost:6333")

    @property
    def vector_local_path(self) -> str:
        """Path for embedded Qdrant local storage (used when mode == 'local')."""
        return self.raw["vector_store"].get("local_path", "/data/qdrant")

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

    @property
    def stats_path(self) -> Path | None:
        p = self.raw.get("storage", {}).get("stats_path")
        return (REPO_ROOT / p) if p else None

    @property
    def named_queries(self) -> list[dict[str, Any]]:
        """Named queries loaded from queries.yaml (empty list if file not found)."""
        return self.raw.get("named_queries", [])


def _find_companion_file(config_yaml_path: Path, key: str, fallback_name: str) -> Path | None:
    """
    Resolve a companion config file path declared in config.yaml's config_files section.

    Resolution order (no hardcoded paths):
      1. config.yaml config_files.<key> — relative path resolved from config.yaml's directory
      2. <config_yaml_dir>/<fallback_name> — convention: same folder as config.yaml
    Returns None if the resolved file does not exist.
    """
    config_dir = config_yaml_path.parent
    raw_path: str | None = None

    # Try to read config_files section from the already-parsed raw dict (passed in).
    # This is called after the main config is loaded, so we read the file again minimally.
    try:
        with config_yaml_path.open("r", encoding="utf-8") as fh:
            raw = yaml.safe_load(fh) or {}
        raw_path = raw.get("config_files", {}).get(key)
    except Exception:
        pass

    candidates = []
    if raw_path:
        candidates.append(config_dir / raw_path)
    candidates.append(config_dir / fallback_name)

    return next((p for p in candidates if p.exists()), None)


def load_config(path: Path = CONFIG_PATH) -> Config:
    """Load config.yaml and merge companion metadata-rules.yaml and queries.yaml.

    File resolution:
      - config.yaml location: 'path' argument (defaults to CONFIG_PATH).
      - metadata-rules.yaml:  declared in config.yaml[config_files][metadata_rules],
                               resolved relative to config.yaml's directory.
      - queries.yaml:          declared in config.yaml[config_files][queries],
                               resolved relative to config.yaml's directory.
    No hardcoded paths — all resolution is relative to config.yaml's location.
    """
    with path.open("r", encoding="utf-8") as fh:
        raw: dict[str, Any] = yaml.safe_load(fh) or {}

    # Load metadata-rules.yaml and merge into raw["metadata_rules"].
    rules_file = _find_companion_file(path, "metadata_rules", "metadata-rules.yaml")
    if rules_file:
        with rules_file.open("r", encoding="utf-8") as fh:
            rules_raw = yaml.safe_load(fh) or {}
        raw.setdefault("metadata_rules", {})
        raw["metadata_rules"].update(rules_raw)

    # Load queries.yaml and merge into raw["named_queries"].
    queries_file = _find_companion_file(path, "queries", "queries.yaml")
    if queries_file:
        with queries_file.open("r", encoding="utf-8") as fh:
            queries_raw = yaml.safe_load(fh) or {}
        raw["named_queries"] = queries_raw.get("named_queries", [])

    return Config(raw=raw)


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


_BC_FROM_TITLE_RE = re.compile(r"^#\s+ADR-\d+\s*[—:-]\s*(.+?)\s*$", re.MULTILINE)

# Fallback regexes used when config has no metadata_rules (backwards compatibility).
_ADR_FOLDER_RE_DEFAULT = re.compile(r"adr/(?P<id>\d{4})/")


def detect_adr_id(rel_path: str, cfg: "Config | None" = None) -> str | None:
    """Extract ADR ID from rel_path using config-driven regex patterns (first match wins).
    Falls back to the built-in folder regex if no config patterns are provided.
    """
    p = rel_path.replace("\\", "/")
    patterns = cfg.adr_id_patterns if cfg else []
    if not patterns:
        # Backwards-compatible fallback.
        m = _ADR_FOLDER_RE_DEFAULT.search(p)
        return m.group("id") if m else None
    for pattern in patterns:
        m = re.search(pattern, p)
        if m:
            try:
                return m.group("id")
            except IndexError:
                return None
    return None


def detect_doc_kind(rel_path: str, cfg: "Config | None" = None) -> str:
    """Classify document kind using config-driven glob rules (first match wins).
    Falls back to built-in rules if no config rules are provided.
    """
    p = rel_path.replace("\\", "/")
    rules = cfg.doc_kind_rules if cfg else []
    if rules:
        for rule in rules:
            if fnmatch.fnmatch(p, rule["glob"]):
                return rule["kind"]
        return "other"
    # Backwards-compatible built-in fallback (no config rules).
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
