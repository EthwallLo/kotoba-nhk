from __future__ import annotations

import json
import re
from dataclasses import dataclass
from datetime import date
from http.cookiejar import Cookie, CookieJar
from html.parser import HTMLParser
from typing import Any
from urllib.parse import quote, urlencode, urljoin, urlparse, urlunparse
from urllib.request import HTTPCookieProcessor, Request, build_opener, urlopen

DEFAULT_URL = "https://news.web.nhk/newsweb"
EASY_URL = "https://news.web.nhk/news/easy/"
EASY_LIST_URL = "https://news.web.nhk/news/easy/news-list.json"
API_BASE_URL = "https://api.web.nhk/r8/t/newsarticle"
AUTH_BASE_URL = "https://news.web.nhk/tix/build_authorize"

ARTICLE_PATH_RE = re.compile(r"^/newsweb/[a-z]{2}/[a-z]{2}-[a-z0-9]+/?$")
PUBLISHED_RE = re.compile(r"\d{1,2}月\d{1,2}日\s+\d{1,2}:\d{2}")
WHITESPACE_RE = re.compile(r"\s+")
NEWS_TIME_SUFFIX_RE = re.compile(r"\s*\(\d{1,2}:\d{2}\)$")
ISO_DATE_RE = re.compile(r"(?P<year>\d{4})[-/](?P<month>\d{1,2})[-/](?P<day>\d{1,2})")
JAPANESE_FULL_DATE_RE = re.compile(
    r"(?P<year>\d{4})\u5e74(?P<month>\d{1,2})\u6708(?P<day>\d{1,2})\u65e5"
)
JAPANESE_MONTH_DAY_RE = re.compile(
    r"(?P<month>\d{1,2})\u6708(?P<day>\d{1,2})\u65e5"
)

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
    site: str = "news"
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


def clean_news_title(value: str) -> str:
    return NEWS_TIME_SUFFIX_RE.sub("", clean_text(value))


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


def build_easy_article_url(news_id: str) -> str:
    return f"https://news.web.nhk/news/easy/{news_id}/{news_id}.html"


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
                    site="news",
                    image_url=image_url,
                )
            )

        self._current = None

    def _pick_title(self, texts: list[str]) -> str:
        assert self._current is not None

        excluded = {"動画", "ニュース", "配信中", "一覧へ", "もっと見る", "JUST IN"}
        candidates = [
            clean_news_title(text)
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


def article_matches_date(article: Article, target_date: date) -> bool:
    candidates = (
        article.published_at,
        article.date_published,
        article.date_modified,
        article.url,
    )
    return any(text_matches_date(candidate, target_date) for candidate in candidates if candidate)


def filter_articles_by_date(articles: list[Article], target_date: date) -> list[Article]:
    return [article for article in articles if article_matches_date(article, target_date)]


def text_matches_date(value: str, target_date: date) -> bool:
    full_date = JAPANESE_FULL_DATE_RE.search(value)
    if full_date:
        return date_parts_match(
            target_date,
            year=int(full_date.group("year")),
            month=int(full_date.group("month")),
            day=int(full_date.group("day")),
        )

    iso_date = ISO_DATE_RE.search(value)
    if iso_date:
        return date_parts_match(
            target_date,
            year=int(iso_date.group("year")),
            month=int(iso_date.group("month")),
            day=int(iso_date.group("day")),
        )

    month_day = JAPANESE_MONTH_DAY_RE.search(value)
    if month_day:
        return (
            int(month_day.group("month")) == target_date.month
            and int(month_day.group("day")) == target_date.day
        )

    return False


def date_parts_match(target_date: date, year: int, month: int, day: int) -> bool:
    return target_date.year == year and target_date.month == month and target_date.day == day


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


def fetch_text_with_opener(
    opener: Any,
    url: str,
    timeout: float = 20.0,
    headers: dict[str, str] | None = None,
) -> str:
    with opener.open(make_request(url, headers=headers), timeout=timeout) as response:
        charset = response.headers.get_content_charset() or "utf-8"
        return response.read().decode(charset, errors="replace")


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


class EasyArticleHTMLParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.paragraphs: list[str] = []
        self._current: list[str] | None = None
        self._skip_depth = 0

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        if tag in {"script", "style", "rt", "rp"}:
            self._skip_depth += 1
            return
        if tag == "p":
            self._current = []

    def handle_data(self, data: str) -> None:
        if self._current is not None and self._skip_depth == 0:
            self._current.append(data)

    def handle_endtag(self, tag: str) -> None:
        if tag in {"script", "style", "rt", "rp"} and self._skip_depth:
            self._skip_depth -= 1
            return
        if tag == "p" and self._current is not None:
            text = clean_text("".join(self._current))
            if text:
                self.paragraphs.append(text)
            self._current = None


EASY_PUBLISHED_RE = re.compile(r"^\d{4}年\d{1,2}月\d{1,2}日\s+\d{1,2}時\d{1,2}分$")
EASY_CONTENT_STOP_PREFIXES = (
    "ニュースをさがす",
    "がいこくごのニュース",
    "NEWS WEB EASY publishes",
    "「NHKやさしいことばニュース」は",
    "NHK やさしいことばニュース",
    "NHK AM・FM",
    "Copyright NHK",
)


def parse_easy_article_content(html: str) -> tuple[str | None, str | None]:
    parser = EasyArticleHTMLParser()
    parser.feed(html)

    published_at = None
    content: list[str] = []
    collecting = False

    for paragraph in parser.paragraphs:
        if paragraph in {"読みこみ中...", "読込中...", "読み込み中..."}:
            continue

        if not collecting and EASY_PUBLISHED_RE.fullmatch(paragraph):
            published_at = paragraph
            collecting = True
            continue

        if not collecting:
            continue

        if any(paragraph.startswith(prefix) for prefix in EASY_CONTENT_STOP_PREFIXES):
            break

        content.append(paragraph)

    return published_at, "\n\n".join(content) if content else None


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


@dataclass(slots=True)
class AccountlessSession:
    token: str
    opener: Any


def create_accountless_session(
    redirect_url: str = DEFAULT_URL,
    timeout: float = 20.0,
    area: dict[str, str] | None = None,
) -> AccountlessSession:
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
            return AccountlessSession(token=cookie.value, opener=opener)

    raise RuntimeError("NHK accountless token was not returned.")


def fetch_accountless_token(
    redirect_url: str = DEFAULT_URL,
    timeout: float = 20.0,
    area: dict[str, str] | None = None,
) -> str:
    return create_accountless_session(
        redirect_url=redirect_url,
        timeout=timeout,
        area=area,
    ).token


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


def parse_easy_articles(payload: object) -> list[Article]:
    if not isinstance(payload, list):
        return []

    articles: list[Article] = []
    for date_group in payload:
        if not isinstance(date_group, dict):
            continue

        for _, items in date_group.items():
            if not isinstance(items, list):
                continue

            for item in items:
                if not isinstance(item, dict) or not item.get("news_display_flag", True):
                    continue

                news_id = extract_text_value(item.get("news_id"))
                title = extract_text_value(item.get("title"))
                if not news_id or not title:
                    continue

                image_url = (
                    extract_text_value(item.get("news_easy_image_uri"))
                    or extract_text_value(item.get("news_web_image_uri"))
                )
                article = Article(
                    title=title,
                    url=build_easy_article_url(news_id),
                    site="easy",
                    published_at=extract_text_value(item.get("news_prearranged_time")),
                    image_url=image_url,
                    api_url=EASY_LIST_URL,
                    date_published=extract_text_value(item.get("news_publication_time")),
                    date_modified=extract_text_value(item.get("news_publication_time")),
                )
                articles.append(article)

    return deduplicate_articles(articles)


def fetch_easy_articles(timeout: float = 20.0) -> list[Article]:
    accountless_session = create_accountless_session(
        redirect_url=EASY_URL,
        timeout=timeout,
    )
    payload = fetch_json(
        EASY_LIST_URL,
        timeout=timeout,
        headers={
            "Authorization": f"Bearer {accountless_session.token}",
            "Referer": EASY_URL,
        },
    )
    return parse_easy_articles(payload)


def fetch_easy_article_content(
    article: Article,
    accountless_session: AccountlessSession,
    timeout: float = 20.0,
) -> Article:
    html = fetch_text_with_opener(
        accountless_session.opener,
        article.url,
        timeout=timeout,
        headers={"Referer": EASY_URL},
    )
    published_at, content = parse_easy_article_content(html)
    article.published_at = published_at or article.published_at
    article.content = content or article.content
    article.content_is_truncated = not bool(content)
    return article


def enrich_articles_with_content(
    articles: list[Article], timeout: float = 20.0
) -> list[Article]:
    if articles and articles[0].site == "easy":
        accountless_session = create_accountless_session(
            redirect_url=EASY_URL,
            timeout=timeout,
        )
        return [
            fetch_easy_article_content(
                article,
                accountless_session=accountless_session,
                timeout=timeout,
            )
            for article in articles
        ]

    accountless_token = None
    if articles:
        try:
            accountless_token = create_accountless_session(
                redirect_url=articles[0].url,
                timeout=timeout,
            ).token
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


def fetch_articles_for_site(
    site: str,
    url: str | None = None,
    timeout: float = 20.0,
) -> list[Article]:
    if site == "easy":
        return fetch_easy_articles(timeout=timeout)
    if site == "news":
        return fetch_articles(url or DEFAULT_URL, timeout=timeout)
    raise ValueError(f"Site inconnu: {site}")
