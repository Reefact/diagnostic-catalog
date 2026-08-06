# Le manifeste de catalogues

🌍 **Langues :**  
🇬🇧 [English](./catalogs-manifest.en.md) | 🇫🇷 Français (ce fichier)

Pour quiconque génère plus d'un catalogue, ou en génère un plus d'une fois. Chaque clé de
`catalogs.json`, et les deux lignes du haut qui valent plus qu'il n'y paraît.

## À quoi il sert

Un manifeste déclare un nombre quelconque de catalogues, de n'importe quel type de source, dans un
seul fichier :

```bash
dcat generate --manifest eng/catalogs.json
dcat validate --manifest eng/catalogs.json
```

L'intérêt n'est pas la brièveté. C'est que la liste devient **une donnée que le dépôt possède**
plutôt que des arguments dupliqués entre un script, un workflow planifié et l'historique shell de
quelqu'un. Dans ce dépôt, `eng/catalogs.json` est lu par `dcat`, par le workflow nocturne, et par
`DocumentedSiblingsTests` — qui y découvre les catalogues, si bien qu'un catalogue déclaré là entre
dans les obligations de documentation avant même d'exister.

## La forme

```json
{
  "$schema": "https://raw.githubusercontent.com/Reefact/diagnostic-catalog/main/eng/catalogs.schema.json",
  "catalogs": [
    {
      "package": "SonarAnalyzer.CSharp",
      "namespace": "MyCompany.Catalog",
      "container": "SonarRule",
      "output": "../src/MyCompany.Catalog/SonarRules.g.cs"
    },
    {
      "projects": ["../src/My.Analyzers/My.Analyzers.csproj"],
      "namespace": "My.Catalog",
      "container": "MyRule",
      "output": "../src/My.Catalog/MyRules.g.cs"
    }
  ]
}
```

**Chaque chemin qu'il contient est relatif au manifeste**, pas à votre répertoire courant. C'est ce
qui fait que `dcat generate --manifest eng/catalogs.json` se comporte pareil depuis la racine du
dépôt, depuis `eng/`, et depuis un job de CI qui démarre ailleurs.

Un tableau `catalogs` vide est refusé. Ne rien générer et sortir en `0` se lirait, pour une tâche
planifiée, exactement comme une exécution réussie.

## La ligne `$schema`

Elle vaut les deux secondes qu'elle coûte. Elle documente chaque clé dans votre éditeur et signale une
faute de frappe **là où vous l'avez tapée** — plutôt qu'après le téléchargement d'un paquet, qui est
l'endroit où `dcat` la signale.

`dcat` nomme le fichier, l'entrée et la clé dans les deux cas :

```text
error: catalogs.json: catalogs[2]: "namespace" is missing.
```

## Toutes les clés

Trois sont obligatoires : `namespace`, `container`, `output`. Les autres nomment une source ou
ajustent le comportement.

### Nommer une source

| Clé | Type | Défaut | Ce qu'elle nomme |
| --- | --- | --- | --- |
| `package` | string | — | Un id de paquet à résoudre depuis les sources NuGet configurées. |
| `version` | string | `latest` | Quelle release de `package` : une version exacte, `latest` (dernière **stable**), ou `latest-any` (préversions comprises). |
| `source` | string | toutes les sources activées | De quel flux configuré résoudre `package`, par nom ou par URL. |
| `nupkg` | string | — | Un `.nupkg` déjà sur disque. Son `.nuspec` nomme la source sauf si `sourceName`/`sourceVersion` disent autrement. |
| `projects` | array of string | — | Des projets qui produisent des analyseurs. **Ils doivent déjà être construits ; rien ici ne les construit.** Plusieurs quand les règles sont réparties entre projets, comme un analyseur et ses correctifs le sont souvent. |
| `solution` | string | — | Une solution. Lit les projets qui déclarent `ProducesDiagnosticRules` ; ils doivent déjà être construits. |
| `assemblies` | array of string | — | Des assemblages d'analyseur déjà sur disque. Plusieurs quand un éditeur répartit ses règles entre eux. |
| `configuration` | string | `Release` | Quelle configuration de `projects` ou `solution` lire. |
| `language` | string | `cs` | Les analyseurs de quel langage lire dans un paquet. |

La clé du manifeste est **`version`**, là où la ligne de commande dit `--package-version`. Sur une
ligne de commande, `--version` signifie déjà « quelle version de l'outil » ; à l'intérieur d'une
entrée, il n'y a pas de collision.

### Nommer une destination

| Clé | Type | Ce qu'elle règle |
| --- | --- | --- |
| `namespace` | string | L'espace de noms que le catalogue généré déclare. |
| `container` | string | La classe statique qui porte les règles. |
| `output` | string | Où la source C# générée est écrite. |

**`container` nomme deux types, pas un.** Un nom finissant par `Rule` nomme aussi la classe de
catégories : `SonarRule` donne `SonarCategory`. C'est pourquoi le singulier compte au-delà du style —
le pluriel produirait `SonarRulesCategory`.

### Enregistrer la provenance

| Clé | Type | Ce qu'elle enregistre |
| --- | --- | --- |
| `sourceName` | string | Ce qu'il faut enregistrer comme source. Par défaut l'id du paquet, le nom d'assemblage du projet, ou le nom du premier assemblage. |
| `sourceVersion` | string | Ce qu'il faut enregistrer comme release de la source. Par défaut la version du paquet, la `Version` déclarée du projet, ou celle du premier assemblage. |

**`sourceVersion` mérite d'être renseignée quand une source sur disque garde une version immobile
pendant que ses règles bougent.** Un assemblage construit depuis une copie de travail porte ce que son
projet a réglé en dernier, souvent inchangé d'une reconstruction à l'autre : un catalogue qui en
dérive seul peut donc prétendre à une source immobile pendant que son contenu change dessous — et
l'enregistrement censé distinguer un instantané du suivant cesse de rien vous dire.

### `$comment`, et pourquoi il est dans le schéma

Le manifeste comme chaque entrée acceptent `$comment`, en chaîne ou en tableau de lignes. JSON n'a pas
de syntaxe de commentaire, et un manifeste incapable de s'expliquer accumule des entrées que personne
n'ose changer :

```json
{
  "$comment": [
    "StyleCop's stable release is years behind what teams actually run,",
    "so this mirrors the prerelease line (ADR-0016)."
  ],
  "package": "StyleCop.Analyzers.Unstable",
  "version": "latest-any"
}
```

## À propos de `language`

Seul `cs` est lisible aujourd'hui. Construire un analyseur Visual Basic demande un Roslyn que le
worker de descripteurs ne transporte pas : une exécution refuserait donc **après avoir téléchargé le
paquet** — d'où l'existence de la clé plutôt qu'une devinette de l'outil.

Un comportement à connaître : sélectionner un langage **exclut les autres** au lieu de ne garder que
son dossier. La plupart des règles siègent souvent dans un assemblage neutre du point de vue du
langage, et une sélection qui n'aurait gardé que `cs/` les aurait silencieusement perdues.

## Ce qu'ajoute `--summary`

```bash
dcat generate --manifest eng/catalogs.json --summary "$RUNNER_TEMP/summary.md"
```

Un rapport Markdown de chaque changement de ce qu'un catalogue publie, celles qui portent sur les règles nommées une par une, sur
toutes les entrées. C'est ce qui transforme une régénération planifiée en une pull request qu'un
humain peut relire plutôt que fusionner à l'aveugle.
[Tenir un catalogue à jour](ci-integration.fr.md) est le motif.

## Où aller ensuite

* [**Tenir un catalogue à jour**](ci-integration.fr.md) — le manifeste dans une tâche planifiée.
* [**La référence `dcat`**](dcat-reference.fr.md) — les mêmes options en ligne de commande.
* [**CONTRIBUTING.md**](../../CONTRIBUTING.md) — comment un catalogue est ajouté à ce dépôt, en
  commençant par son entrée de manifeste.

---

<div align="center">
<a href="./dcat-reference.fr.md">← La référence `dcat`</a> · <a href="./README.fr.md">↑ Table des matières</a> · <a href="./ci-integration.fr.md">Tenir un catalogue à jour →</a>
</div>
