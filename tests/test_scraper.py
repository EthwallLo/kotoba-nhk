import unittest
from datetime import date

from nhk_articles.scraper import (
    Article,
    build_article_api_url,
    build_easy_article_url,
    filter_articles_by_date,
    normalize_article_url,
    parse_articles,
    parse_easy_article_content,
    parse_easy_articles,
    update_article_from_detail,
)


class ScraperTests(unittest.TestCase):
    def test_parse_articles_from_nhk_like_markup(self) -> None:
        html = """
        <a href="/newsweb/na/na-k10015122111000">
          <img src="https://imgu.web.nhk/news/example.jpg" alt="Alt ignore si titre present">
          <span>イギリス 閣僚が辞任 地方選大敗の与党 党首選挙の実施焦点に</span>
          <span>5月15日 5:31</span>
        </a>
        <a href="/newsweb/video">動画</a>
        <a href="/newsweb/na/na-k10015122111000">
          <span>イギリス 閣僚が辞任 地方選大敗の与党 党首選挙の実施焦点に</span>
        </a>
        """

        articles = parse_articles(html)

        self.assertEqual(len(articles), 1)
        self.assertEqual(
            articles[0].title,
            "イギリス 閣僚が辞任 地方選大敗の与党 党首選挙の実施焦点に",
        )
        self.assertEqual(articles[0].published_at, "5月15日 5:31")
        self.assertEqual(
            articles[0].url,
            "https://news.web.nhk/newsweb/na/na-k10015122111000",
        )

    def test_title_can_fall_back_to_image_alt(self) -> None:
        html = """
        <a href="/newsweb/na/na-k10015122091000">
          <img src="/news/image.jpg" alt="大相撲夏場所6日目の見どころ">
        </a>
        """

        articles = parse_articles(html)

        self.assertEqual(len(articles), 1)
        self.assertEqual(articles[0].title, "大相撲夏場所6日目の見どころ")
        self.assertEqual(articles[0].image_url, "https://news.web.nhk/news/image.jpg")

    def test_just_in_label_is_not_used_as_title(self) -> None:
        html = """
        <a href="/newsweb/na/na-k10015122481000">
          <span>JUST IN</span>
          <span>株価 一時1600円以上値下がり 利益確定売りや長期金利上昇で(15:13)</span>
        </a>
        """

        articles = parse_articles(html)

        self.assertEqual(len(articles), 1)
        self.assertEqual(
            articles[0].title,
            "株価 一時1600円以上値下がり 利益確定売りや長期金利上昇で",
        )

    def test_normalize_article_url_rejects_non_articles(self) -> None:
        self.assertIsNone(normalize_article_url("/newsweb/video", "https://news.web.nhk/newsweb"))
        self.assertIsNone(
            normalize_article_url("https://example.com/newsweb/na/na-k1", "https://news.web.nhk/newsweb")
        )

    def test_build_article_api_url(self) -> None:
        self.assertEqual(
            build_article_api_url("https://news.web.nhk/newsweb/na/na-k10015122111000"),
            "https://api.web.nhk/r8/t/newsarticle/na/na-k10015122111000.json",
        )

    def test_build_easy_article_url(self) -> None:
        self.assertEqual(
            build_easy_article_url("ne2026051413177"),
            "https://news.web.nhk/news/easy/ne2026051413177/ne2026051413177.html",
        )

    def test_update_article_from_detail(self) -> None:
        article = Article(
            title="Ancien titre",
            url="https://news.web.nhk/newsweb/na/na-k10015122111000",
        )
        payload = {
            "headline": {"@value": "Titre detaille"},
            "abstract": "Premier paragraphe.\n\nDeuxieme paragraphe.",
            "canonical": "https://news.web.nhk/newsweb/na/na-k10015122111000",
            "datePublished": "2026-05-15T05:31:00+09:00",
            "dateModified": "2026-05-15T05:45:00+09:00",
            "image": [{"url": "https://imgu.web.nhk/news/example.jpg"}],
        }

        updated = update_article_from_detail(
            article,
            payload,
            "https://api.web.nhk/r8/t/newsarticle/na/na-k10015122111000.json",
        )

        self.assertEqual(updated.title, "Titre detaille")
        self.assertEqual(updated.content, "Premier paragraphe.\n\nDeuxieme paragraphe.")
        self.assertFalse(updated.content_is_truncated)
        self.assertEqual(updated.date_published, "2026-05-15T05:31:00+09:00")
        self.assertEqual(updated.date_modified, "2026-05-15T05:45:00+09:00")
        self.assertEqual(updated.image_url, "https://imgu.web.nhk/news/example.jpg")
        self.assertEqual(
            updated.api_url,
            "https://api.web.nhk/r8/t/newsarticle/na/na-k10015122111000.json",
        )

    def test_update_article_flags_truncated_abstracts(self) -> None:
        article = Article(
            title="Titre",
            url="https://news.web.nhk/newsweb/na/na-k10015122111000",
        )
        payload = {"abstract": "x" * 100}

        updated = update_article_from_detail(
            article,
            payload,
            "https://api.web.nhk/r8/t/newsarticle/na/na-k10015122111000.json",
        )

        self.assertTrue(updated.content_is_truncated)

    def test_article_body_is_preferred_over_abstract(self) -> None:
        article = Article(
            title="Titre",
            url="https://news.web.nhk/newsweb/na/na-k10015122111000",
        )
        payload = {
            "abstract": "x" * 100,
            "articleBody": "Premier paragraphe.\n\nDeuxieme paragraphe complet.",
        }

        updated = update_article_from_detail(
            article,
            payload,
            "https://api.web.nhk/r8/t/newsarticle/na/na-k10015122111000.json",
        )

        self.assertEqual(
            updated.content,
            "Premier paragraphe.\n\nDeuxieme paragraphe complet.",
        )
        self.assertFalse(updated.content_is_truncated)

    def test_parse_easy_articles(self) -> None:
        payload = [
            {
                "2026-05-14": [
                    {
                        "news_id": "ne2026051413177",
                        "title": "トランプ大統領と習近平国家主席が会って話をした",
                        "news_prearranged_time": "2026-05-14 20:15:00",
                        "news_publication_time": "2026-05-14 20:32:28",
                        "news_easy_image_uri": "",
                        "news_web_image_uri": "https://news.web.nhk/news/example.jpg",
                        "news_display_flag": True,
                    }
                ]
            }
        ]

        articles = parse_easy_articles(payload)

        self.assertEqual(len(articles), 1)
        self.assertEqual(articles[0].site, "easy")
        self.assertEqual(articles[0].title, "トランプ大統領と習近平国家主席が会って話をした")
        self.assertEqual(articles[0].published_at, "2026-05-14 20:15:00")
        self.assertEqual(
            articles[0].url,
            "https://news.web.nhk/news/easy/ne2026051413177/ne2026051413177.html",
        )

    def test_parse_easy_article_content_removes_ruby_and_footer(self) -> None:
        html = """
        <main>
          <p>読み<ruby>こみ<rt>こ</rt></ruby>中...</p>
          <p>2026年5月14日 20時15分</p>
          <p>14日、<ruby>北京<rt>ぺきん</rt></ruby>で話をしました。</p>
          <p>次の段落です。</p>
          <p>ニュースをさがす</p>
          <p>これはフッターです。</p>
        </main>
        """

        published_at, content = parse_easy_article_content(html)

        self.assertEqual(published_at, "2026年5月14日 20時15分")
        self.assertEqual(content, "14日、北京で話をしました。\n\n次の段落です。")


    def test_filter_articles_by_date_matches_news_month_day(self) -> None:
        articles = [
            Article(
                title="A",
                url="https://news.web.nhk/newsweb/na/na-k10015122111000",
                published_at="5\u670815\u65e5 5:31",
            ),
            Article(
                title="B",
                url="https://news.web.nhk/newsweb/na/na-k10015122121000",
                published_at="5\u670814\u65e5 20:10",
            ),
        ]

        filtered = filter_articles_by_date(articles, date(2026, 5, 15))

        self.assertEqual([article.title for article in filtered], ["A"])

    def test_filter_articles_by_date_matches_easy_iso_date(self) -> None:
        articles = [
            Article(
                title="A",
                url="https://news.web.nhk/news/easy/ne2026051413177/ne2026051413177.html",
                site="easy",
                published_at="2026-05-14 20:15:00",
            ),
            Article(
                title="B",
                url="https://news.web.nhk/news/easy/ne2026051513177/ne2026051513177.html",
                site="easy",
                published_at="2026-05-15 20:15:00",
            ),
        ]

        filtered = filter_articles_by_date(articles, date(2026, 5, 15))

        self.assertEqual([article.title for article in filtered], ["B"])


if __name__ == "__main__":
    unittest.main()
