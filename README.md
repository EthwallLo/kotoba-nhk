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

Recuperer aussi le contenu public expose par NHK pour chaque article :

```powershell
python main.py --limit 5 --with-content
```

Le script tente d'abord d'obtenir le jeton `accountless` utilise par le site NHK
pour lire `articleBody`. Si ce jeton n'est pas disponible, il revient au resume
public. Dans les sorties JSON et CSV, `content_is_truncated` indique ce fallback
tronque.

En JSON avec contenu :

```powershell
python main.py --limit 5 --with-content --format json --output articles.json
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
