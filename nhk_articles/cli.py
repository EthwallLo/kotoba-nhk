from __future__ import annotations

import argparse
import csv
import io
import json
import sys
from pathlib import Path
from typing import Iterable

from .scraper import Article, DEFAULT_URL, fetch_articles


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Recupere la liste des articles de NHK News Web."
    )
    parser.add_argument(
        "--url",
        default=DEFAULT_URL,
        help=f"Page NHK a scraper. Valeur par defaut: {DEFAULT_URL}",
    )
    parser.add_argument(
        "--limit",
        "-n",
        type=int,
        default=None,
        help="Nombre maximum d'articles a afficher.",
    )
    parser.add_argument(
        "--format",
        "-f",
        choices=("table", "json", "csv"),
        default="table",
        help="Format de sortie.",
    )
    parser.add_argument(
        "--output",
        "-o",
        type=Path,
        help="Fichier de sortie. Par defaut, affiche dans le terminal.",
    )
    parser.add_argument(
        "--timeout",
        type=float,
        default=20.0,
        help="Timeout HTTP en secondes.",
    )
    return parser


def article_to_dict(article: Article) -> dict[str, str | None]:
    return {
        "title": article.title,
        "published_at": article.published_at,
        "url": article.url,
        "image_url": article.image_url,
    }


def format_json(articles: Iterable[Article]) -> str:
    payload = [article_to_dict(article) for article in articles]
    return json.dumps(payload, ensure_ascii=False, indent=2)


def format_csv(articles: Iterable[Article]) -> str:
    buffer = io.StringIO()
    writer = csv.DictWriter(
        buffer,
        fieldnames=("title", "published_at", "url", "image_url"),
        lineterminator="\n",
    )
    writer.writeheader()
    for article in articles:
        writer.writerow(article_to_dict(article))
    return buffer.getvalue()


def format_table(articles: list[Article]) -> str:
    if not articles:
        return "Aucun article trouve."

    lines = []
    for index, article in enumerate(articles, start=1):
        published = article.published_at or "date inconnue"
        title = article.title or "(sans titre)"
        lines.append(f"{index:02d}. {published} | {title}")
        lines.append(f"    {article.url}")
    return "\n".join(lines)


def render_articles(articles: list[Article], output_format: str) -> str:
    if output_format == "json":
        return format_json(articles)
    if output_format == "csv":
        return format_csv(articles)
    return format_table(articles)


def configure_stdio() -> None:
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8", errors="replace")


def main(argv: list[str] | None = None) -> int:
    configure_stdio()
    parser = build_parser()
    args = parser.parse_args(argv)

    try:
        articles = fetch_articles(args.url, timeout=args.timeout)
    except Exception as exc:  # pragma: no cover - exact urllib errors vary.
        print(f"Erreur pendant la recuperation NHK: {exc}", file=sys.stderr)
        return 1

    if args.limit is not None:
        if args.limit < 0:
            parser.error("--limit doit etre positif.")
        articles = articles[: args.limit]

    rendered = render_articles(articles, args.format)
    if args.output:
        args.output.write_text(rendered + "\n", encoding="utf-8")
    else:
        print(rendered)
    return 0
