from __future__ import annotations

import re
from dataclasses import dataclass
from html.parser import HTMLParser
from urllib.parse import urljoin, urlparse, urlunparse
from urllib.request import Request, urlopen

DEFAULT_URL = "https://news.web.nhk/newsweb"

ARTICLE_PATH_RE = re.compile(r"^/newsweb/[a-z]{2}/[a-z]{2}-[a-z0-9]+/?$")
PUBLISHED_RE = re.compile(r"\d{1,2}月\d{1,2}日\s+\d{1,2}:\d{2}")
WHITESPACE_RE = re.compile(r"\s+")


@dataclass(slots=True)
class Article:
    title: str
    url: str
    published_at: str | None = None
    image_url: str | None = None


def clean_text(value: str) -> str:
    return WHITESPACE_RE.sub(" ", value).strip()


def normalize_article_url(href: str | None, base_url: str) -> str | None:
    if not href:
        return None

    absolute = urljoin(base_url, href)
    parsed = urlparse(absolute)
    base = urlparse(base_url)

    if parsed.netloc != base.netloc:
        return None
    if not ARTICLE_PATH_RE.match(parsed.path):
        return None

    return urlunparse((parsed.scheme, parsed.netloc, parsed.path.rstrip("/"), "", "", ""))


class ArticleHTMLParser(HTMLParser):
    def __init__(self, base_url: str) -> None:
        super().__init__(convert_charrefs=True)
        self.base_url = base_url
        self.articles: list[Article] = []
        self._current: dict[str, object] | None = None

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        attributes = dict(attrs)

        if tag == "a":
            article_url = normalize_article_url(attributes.get("href"), self.base_url)
            if article_url:
                self._current = {
                    "url": article_url,
                    "texts": [],
                    "image_url": None,
                    "image_alt": None,
                }
            return

        if self._current is not None and tag == "img":
            self._capture_image(attributes)

    def handle_startendtag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        if self._current is not None and tag == "img":
            self._capture_image(dict(attrs))

    def handle_data(self, data: str) -> None:
        if self._current is None:
            return

        text = clean_text(data)
        if text:
            texts = self._current["texts"]
            assert isinstance(texts, list)
            texts.append(text)

    def handle_endtag(self, tag: str) -> None:
        if tag == "a" and self._current is not None:
            self._finish_current_article()

    def _capture_image(self, attributes: dict[str, str | None]) -> None:
        assert self._current is not None

        image_url = attributes.get("src")
        image_alt = clean_text(attributes.get("alt") or "")

        if image_url and self._current.get("image_url") is None:
            self._current["image_url"] = urljoin(self.base_url, image_url)
        if image_alt and self._current.get("image_alt") is None:
            self._current["image_alt"] = image_alt

    def _finish_current_article(self) -> None:
        assert self._current is not None

        texts = self._current["texts"]
        assert isinstance(texts, list)

        published_at = next((text for text in texts if PUBLISHED_RE.fullmatch(text)), None)
        title = self._pick_title(texts)

        url = self._current["url"]
        image_url = self._current["image_url"]
        assert isinstance(url, str)
        assert image_url is None or isinstance(image_url, str)

        if title:
            self.articles.append(
                Article(
                    title=title,
                    published_at=published_at,
                    url=url,
                    image_url=image_url,
                )
            )

        self._current = None

    def _pick_title(self, texts: list[str]) -> str:
        assert self._current is not None

        excluded = {"動画", "ニュース", "配信中", "一覧へ", "もっと見る"}
        candidates = [
            text
            for text in texts
            if text not in excluded and not PUBLISHED_RE.fullmatch(text)
        ]

        if candidates:
            return candidates[0]

        image_alt = self._current.get("image_alt")
        if isinstance(image_alt, str):
            return image_alt
        return ""


def parse_articles(html: str, base_url: str = DEFAULT_URL) -> list[Article]:
    parser = ArticleHTMLParser(base_url)
    parser.feed(html)
    return deduplicate_articles(parser.articles)


def deduplicate_articles(articles: list[Article]) -> list[Article]:
    deduplicated: dict[str, Article] = {}

    for article in articles:
        existing = deduplicated.get(article.url)
        if existing is None:
            deduplicated[article.url] = article
            continue

        if not existing.published_at and article.published_at:
            existing.published_at = article.published_at
        if not existing.image_url and article.image_url:
            existing.image_url = article.image_url
        if (not existing.title or existing.title == "(sans titre)") and article.title:
            existing.title = article.title

    return list(deduplicated.values())


def fetch_html(url: str = DEFAULT_URL, timeout: float = 20.0) -> str:
    request = Request(
        url,
        headers={
            "Accept-Language": "ja,en;q=0.8",
            "User-Agent": "kotoba-nhk-news/0.1 (+https://news.web.nhk/newsweb)",
        },
    )

    with urlopen(request, timeout=timeout) as response:
        charset = response.headers.get_content_charset() or "utf-8"
        return response.read().decode(charset, errors="replace")


def fetch_articles(url: str = DEFAULT_URL, timeout: float = 20.0) -> list[Article]:
    html = fetch_html(url, timeout=timeout)
    return parse_articles(html, base_url=url)
