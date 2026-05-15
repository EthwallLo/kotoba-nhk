from __future__ import annotations

import json
import re
from dataclasses import dataclass
from http.cookiejar import Cookie, CookieJar
from html.parser import HTMLParser
from urllib.parse import quote, urlencode, urljoin, urlparse, urlunparse
from urllib.request import HTTPCookieProcessor, Request, build_opener, urlopen

DEFAULT_URL = "https://news.web.nhk/newsweb"
API_BASE_URL = "https://api.web.nhk/r8/t/newsarticle"
AUTH_BASE_URL = "https://news.web.nhk/tix/build_authorize"

ARTICLE_PATH_RE = re.compile(r"^/newsweb/[a-z]{2}/[a-z]{2}-[a-z0-9]+/?$")
PUBLISHED_RE = re.compile(r"\d{1,2}月\d{1,2}日\s+\d{1,2}:\d{2}")
WHITESPACE_RE = re.compile(r"\s+")

DEFAULT_CONSENT_AREA = {
    "areaId": "270",
    "jisx0402": "27128",
    "postal": "5408501",
    "pref": "27",
}


@dataclass(slots=True)
class Article:
    title: str
    url: str
    published_at: str | None = None
    image_url: str | None = None
    content: str | None = None
    content_is_truncated: bool = False
    api_url: str | None = None
    date_published: str | None = None
    date_modified: str | None = None


def clean_text(value: str) -> str:
    return WHITESPACE_RE.sub(" ", value).strip()


def clean_content_text(value: str) -> str:
    normalized = value.replace("\r\n", "\n").replace("\r", "\n")
    paragraphs = []

    for block in re.split(r"\n\s*\n", normalized):
        lines = [clean_text(line) for line in block.splitlines()]
        paragraph = clean_text(" ".join(line for line in lines if line))
        if paragraph:
            paragraphs.append(paragraph)

    return "\n\n".join(paragraphs)


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


def build_article_api_url(article_url: str) -> str | None:
    parsed = urlparse(article_url)
    parts = [part for part in parsed.path.split("/") if part]

    if len(parts) != 3 or parts[0] != "newsweb":
        return None

    _, data_source_id, article_id = parts
    return f"{API_BASE_URL}/{data_source_id}/{article_id}.json"


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
        if not existing.content and article.content:
            existing.content = article.content
        if not existing.content_is_truncated and article.content_is_truncated:
            existing.content_is_truncated = article.content_is_truncated
        if not existing.api_url and article.api_url:
            existing.api_url = article.api_url
        if not existing.date_published and article.date_published:
            existing.date_published = article.date_published
        if not existing.date_modified and article.date_modified:
            existing.date_modified = article.date_modified
        if (not existing.title or existing.title == "(sans titre)") and article.title:
            existing.title = article.title

    return list(deduplicated.values())


def make_request(url: str, headers: dict[str, str] | None = None) -> Request:
    default_headers = {
        "Accept-Language": "ja,en;q=0.8",
        "User-Agent": "kotoba-nhk-news/0.1 (+https://news.web.nhk/newsweb)",
    }
    if headers:
        default_headers.update(headers)

    return Request(
        url,
        headers=default_headers,
    )


def fetch_text(
    url: str,
    timeout: float = 20.0,
    headers: dict[str, str] | None = None,
) -> str:
    with urlopen(make_request(url, headers=headers), timeout=timeout) as response:
        charset = response.headers.get_content_charset() or "utf-8"
        return response.read().decode(charset, errors="replace")


def fetch_json(
    url: str,
    timeout: float = 20.0,
    headers: dict[str, str] | None = None,
) -> object:
    return json.loads(fetch_text(url, timeout=timeout, headers=headers))


def extract_text_value(value: object) -> str | None:
    if isinstance(value, str):
        return clean_text(value)
    if isinstance(value, dict):
        return extract_text_value(value.get("@value"))
    return None


def extract_raw_text_value(value: object) -> str | None:
    if isinstance(value, str):
        return value
    if isinstance(value, dict):
        return extract_raw_text_value(value.get("@value"))
    return None


def extract_content_value(value: object) -> str | None:
    if isinstance(value, str):
        return clean_content_text(value)
    if isinstance(value, dict):
        for key in ("articleBody", "body", "text", "content", "@value"):
            content = extract_content_value(value.get(key))
            if content:
                return content
    if isinstance(value, list):
        parts = [content for item in value if (content := extract_content_value(item))]
        if parts:
            return "\n\n".join(parts)
    return None


def extract_image_url(value: object) -> str | None:
    if isinstance(value, dict):
        return extract_text_value(value.get("url"))
    if isinstance(value, list):
        for item in value:
            image_url = extract_image_url(item)
            if image_url:
                return image_url
    return None


def update_article_from_detail(article: Article, payload: object, api_url: str) -> Article:
    if not isinstance(payload, dict):
        return article

    article.api_url = api_url
    article.title = (
        extract_text_value(payload.get("headline"))
        or extract_text_value(payload.get("name"))
        or article.title
    )
    article_body = (
        extract_content_value(payload.get("articleBody"))
        or extract_content_value(payload.get("detailedArticleBody"))
    )
    raw_abstract = extract_raw_text_value(payload.get("abstract"))
    abstract = clean_content_text(raw_abstract) if raw_abstract else None
    description = extract_content_value(payload.get("description"))
    article.content = article_body or abstract or description or article.content
    article.content_is_truncated = not bool(article_body) and bool(
        raw_abstract and len(raw_abstract) >= 100
    )
    article.date_published = extract_text_value(payload.get("datePublished"))
    article.date_modified = extract_text_value(payload.get("dateModified"))
    article.url = extract_text_value(payload.get("canonical")) or article.url
    article.image_url = extract_image_url(payload.get("image")) or article.image_url
    return article


def make_cookie(name: str, value: str, domain: str = ".web.nhk") -> Cookie:
    return Cookie(
        version=0,
        name=name,
        value=value,
        port=None,
        port_specified=False,
        domain=domain,
        domain_specified=True,
        domain_initial_dot=domain.startswith("."),
        path="/",
        path_specified=True,
        secure=False,
        expires=None,
        discard=True,
        comment=None,
        comment_url=None,
        rest={},
        rfc2109=False,
    )


def build_consent_cookie_value(area: dict[str, str] | None = None) -> str:
    consent = {
        "status": "optedin",
        "entity": "household",
        "area": area or DEFAULT_CONSENT_AREA,
    }
    return quote(json.dumps(consent, ensure_ascii=False, separators=(",", ":")), safe="")


def fetch_accountless_token(
    redirect_url: str = DEFAULT_URL,
    timeout: float = 20.0,
    area: dict[str, str] | None = None,
) -> str:
    cookie_jar = CookieJar()
    cookie_jar.set_cookie(make_cookie("consentToUse", build_consent_cookie_value(area)))
    opener = build_opener(HTTPCookieProcessor(cookie_jar))

    params = urlencode(
        {
            "idp": "r-alaz",
            "profileType": "anonymous",
            "redirect_uri": f"{redirect_url}?ctu=in",
        }
    )

    with opener.open(make_request(f"{AUTH_BASE_URL}?{params}"), timeout=timeout):
        pass

    for cookie in cookie_jar:
        if cookie.name == "z_at":
            return cookie.value

    raise RuntimeError("NHK accountless token was not returned.")


def fetch_article_content(
    article: Article,
    timeout: float = 20.0,
    accountless_token: str | None = None,
) -> Article:
    api_url = build_article_api_url(article.url)
    if api_url is None:
        return article

    headers = {"Authorization": f"Bearer {accountless_token}"} if accountless_token else None
    payload = fetch_json(api_url, timeout=timeout, headers=headers)
    return update_article_from_detail(article, payload, api_url)


def enrich_articles_with_content(
    articles: list[Article], timeout: float = 20.0
) -> list[Article]:
    accountless_token = None
    if articles:
        try:
            accountless_token = fetch_accountless_token(
                redirect_url=articles[0].url,
                timeout=timeout,
            )
        except Exception:
            accountless_token = None

    return [
        fetch_article_content(
            article,
            timeout=timeout,
            accountless_token=accountless_token,
        )
        for article in articles
    ]


def fetch_html(url: str = DEFAULT_URL, timeout: float = 20.0) -> str:
    return fetch_text(url, timeout=timeout)


def fetch_articles(url: str = DEFAULT_URL, timeout: float = 20.0) -> list[Article]:
    html = fetch_html(url, timeout=timeout)
    return parse_articles(html, base_url=url)
