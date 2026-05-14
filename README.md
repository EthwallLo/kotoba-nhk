# Kotoba NHK News

Petit projet Python pour recuperer la liste des articles affiches sur
<https://news.web.nhk/newsweb>.

Le scraper utilise uniquement la bibliotheque standard de Python. Il lit le HTML
de la page NHK, extrait les liens d'articles `/newsweb/...`, dedoublonne les
resultats, puis affiche le titre, l'heure de publication et l'URL.

## Utilisation rapide

```powershell
python main.py
```

Limiter le nombre d'articles :

```powershell
python main.py --limit 10
```

Exporter en JSON :

```powershell
python main.py --format json --output articles.json
```

Exporter en CSV :

```powershell
python main.py --format csv --output articles.csv
```

## Installation optionnelle

```powershell
python -m pip install -e .
nhk-articles --limit 10
```

## Tests

```powershell
python -m unittest discover -s tests
```
# kotoba-nhk
# kotoba-nhk
