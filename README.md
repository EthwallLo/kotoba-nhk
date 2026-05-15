# Kotoba NHK

Petit projet Python pour recuperer les articles de :

- NHK News Web : <https://news.web.nhk/newsweb>
- NHK Easy : <https://news.web.nhk/news/easy/>

Le scraper utilise uniquement la bibliotheque standard de Python. Il lit le HTML
ou les endpoints JSON publics de NHK, dedoublonne les resultats, puis affiche le
titre, l'heure de publication et l'URL.

## Utilisation rapide

```powershell
python main.py
```

Sans option, l'application propose de choisir le site au demarrage.

Choisir le site sans prompt :

```powershell
python main.py --site news
python main.py --site easy
```

Limiter le nombre d'articles :

```powershell
python main.py --site easy --limit 10
```

Recuperer les articles d'une date precise :

```powershell
python main.py --site news --date 2026-05-15
python main.py --site easy --date 2026-05-15
```

Exporter en JSON :

```powershell
python main.py --site easy --format json --output articles.json
```

Exporter en CSV :

```powershell
python main.py --site news --format csv --output articles.csv
```

Recuperer aussi le contenu public expose par NHK pour chaque article :

```powershell
python main.py --site news --limit 5 --with-content
python main.py --site easy --limit 5 --with-content
```

Le script tente d'abord d'obtenir le jeton `accountless` utilise par le site NHK
pour lire `articleBody` sur News Web et les pages detaillees sur NHK Easy. Si ce
jeton n'est pas disponible, il revient aux donnees publiques disponibles. Dans
les sorties JSON et CSV, `content_is_truncated` indique ce fallback tronque.

En JSON avec contenu :

```powershell
python main.py --site easy --limit 5 --with-content --format json --output articles.json
```

## Installation optionnelle

```powershell
python -m pip install -e .
nhk-articles --limit 10
```

## Interface graphique C#

Une petite interface Windows Forms est disponible dans `Kotoba.NhkGui`.
Elle permet de choisir `Classique` ou `Easy`, de choisir une date, puis
d'afficher les articles dans une fenetre.

```powershell
dotnet run --project Kotoba.NhkGui/Kotoba.NhkGui.csproj
```

La fenetre lance le backend Python avec `python3 main.py --date YYYY-MM-DD --format json`.
Avant de lancer l'interface, verifie donc que cette commande fonctionne dans le
meme terminal :

```powershell
python3 --version
```

L'option `Charger contenu` demande aussi le texte des articles. Comme pour le
script Python, NHK peut demander une session localisee au Japon pour certains
contenus.

## Tests

```powershell
python -m unittest discover -s tests
```
