import unittest

from nhk_articles.scraper import normalize_article_url, parse_articles


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

    def test_normalize_article_url_rejects_non_articles(self) -> None:
        self.assertIsNone(normalize_article_url("/newsweb/video", "https://news.web.nhk/newsweb"))
        self.assertIsNone(
            normalize_article_url("https://example.com/newsweb/na/na-k1", "https://news.web.nhk/newsweb")
        )


if __name__ == "__main__":
    unittest.main()
