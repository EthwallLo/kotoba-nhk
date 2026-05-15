from __future__ import annotations

import argparse
import csv
import io
import json
import sys
from datetime import date
from pathlib import Path
from typing import Iterable

from .scraper import (
    Article,
    DEFAULT_URL,
    EASY_URL,
    enrich_articles_with_content,
    fetch_articles_for_site,
    filter_articles_by_date,
)

SITE_LABELS = {
    "news": "NHK News Web",
    "easy": "NHK Easy",
}


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Recupere la liste des articles NHK News Web ou NHK Easy."
    )
    parser.add_argument(
        "--site",
        choices=tuple(SITE_LABELS),
        help="Site a utiliser. Sans cette option, un choix est propose au demarrage.",
    )
    parser.add_argument(
        "--url",
        default=None,
        help="Page NHK News Web a scraper. Reserve au site 'news'.",
    )
    parser.add_argument(
        "--limit",
        "-n",
        type=int,
        default=None,
        help="Nombre maximum d'articles a afficher.",
    )
    parser.add_argument(
        "--date",
        type=parse_date_argument,
        default=None,
        help="Date des articles a recuperer, au format YYYY-MM-DD.",
    )
    parser.add_argument(
        "--format",
        "-f",
        choices=("table", "json", "csv"),
        default="table",
        help="Format de sortie.",
    )
    parser.add_argument(
        "--with-content",
        action="store_true",
        help="Recupere aussi le contenu public de chaque article via l'API NHK.",
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


def parse_date_argument(value: str) -> date:
    try:
        return date.fromisoformat(value)
    except ValueError as exc:
        raise argparse.ArgumentTypeError(
            "--date doit utiliser le format YYYY-MM-DD."
        ) from exc


def infer_site_from_url(url: str | None) -> str | None:
    if not url:
        return None
    if "/news/easy" in url:
        return "easy"
    if "/newsweb" in url:
        return "news"
    return None


def choose_site_interactively() -> str:
    print("Quel site veux-tu utiliser ?")
    print("  1. NHK News Web")
    print("  2. NHK Easy")

    while True:
        try:
            choice = input("Choix [1/2] : ").strip().lower()
        except EOFError:
            return "news"
        if choice in {"1", "news", "n", ""}:
            return "news"
        if choice in {"2", "easy", "e"}:
            return "easy"
        print("Choix invalide. Tape 1 pour News Web ou 2 pour Easy.")


def resolve_site(args: argparse.Namespace) -> str:
    if args.site:
        return args.site

    inferred_site = infer_site_from_url(args.url)
    if inferred_site:
        return inferred_site

    return choose_site_interactively()


def resolve_url(args: argparse.Namespace, site: str) -> str | None:
    if args.url:
        return args.url
    if site == "news":
        return DEFAULT_URL
    if site == "easy":
        return EASY_URL
    return None


def article_to_dict(article: Article) -> dict[str, str | bool | None]:
    return {
        "site": article.site,
        "title": article.title,
        "published_at": article.published_at,
        "date_published": article.date_published,
        "date_modified": article.date_modified,
        "url": article.url,
        "api_url": article.api_url,
        "image_url": article.image_url,
        "content": article.content,
        "content_is_truncated": article.content_is_truncated,
    }


def format_json(articles: Iterable[Article]) -> str:
    payload = [article_to_dict(article) for article in articles]
    return json.dumps(payload, ensure_ascii=False, indent=2)


def format_csv(articles: Iterable[Article]) -> str:
    buffer = io.StringIO()
    writer = csv.DictWriter(
        buffer,
        fieldnames=(
            "site",
            "title",
            "published_at",
            "date_published",
            "date_modified",
            "url",
            "api_url",
            "image_url",
            "content",
            "content_is_truncated",
        ),
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
        lines.append(f"{index:02d}. {article.site} | {published} | {title}")
        lines.append(f"    {article.url}")
        if article.content:
            suffix = "..." if article.content_is_truncated else ""
            lines.append(f"    {article.content}{suffix}")
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
    site = resolve_site(args)
    url = resolve_url(args, site)

    try:
        articles = fetch_articles_for_site(site, url=url, timeout=args.timeout)
    except Exception as exc:  # pragma: no cover - exact urllib errors vary.
        print(f"Erreur pendant la recuperation NHK: {exc}", file=sys.stderr)
        return 1

    if args.limit is not None:
        if args.limit < 0:
            parser.error("--limit doit etre positif.")

    if args.date is not None:
        articles = filter_articles_by_date(articles, args.date)

    if args.limit is not None:
        articles = articles[: args.limit]

    if args.with_content:
        try:
            articles = enrich_articles_with_content(articles, timeout=args.timeout)
        except Exception as exc:  # pragma: no cover - exact urllib errors vary.
            print(f"Erreur pendant la recuperation du contenu NHK: {exc}", file=sys.stderr)
            return 1

    rendered = render_articles(articles, args.format)
    if args.output:
        args.output.write_text(rendered + "\n", encoding="utf-8")
    else:
        print(rendered)
    return 0
