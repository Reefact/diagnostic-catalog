# DiagnosticCatalog.Cli — `dcat`

🌍 **Langues :**  
🇬🇧 [English](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.Cli/README.en.md) | 🇫🇷 Français (ce fichier)

Génère un catalogue de règles [DiagnosticCatalog](https://github.com/Reefact/diagnostic-catalog)
depuis les analyseurs que vous lui désignez, pour que `SuppressMessageAttribute`
prenne des références vérifiées à la compilation plutôt que des chaînes magiques.

```bash
dotnet tool install --global DiagnosticCatalog.Cli
```

## Pourquoi il lit des assemblys

Le contenu d'un catalogue est dérivé des instances de `DiagnosticDescriptor` que les
assemblys d'analyseurs déclarent réellement — jamais de la documentation publiée par
l'éditeur, et jamais de fichiers de métadonnées de règles livrés à côté.

Ce n'est pas une préférence. Roslyn ne lit jamais la *catégorie* d'une suppression : il
apparie sur l'identifiant seul. Une catégorie fausse ne produit donc aucune erreur, aucun
avertissement, aucune suppression manquée et aucun test en échec — ni à la compilation, ni
à l'exécution, jamais. Quand une erreur n'a pas de symptôme, la seule source défendable est
celle qui ne peut pas se tromper, et les descripteurs sont cette source parce qu'ils *sont*
ce avec quoi l'analyseur signale. (`ADR-0009`.)

Le même raisonnement explique que l'outil refuse plutôt que de deviner : s'il ne peut pas
construire un analyseur ou charger un assembly qu'on lui a donné, il n'émet rien et sort
avec un code non nul. Un catalogue auquel une règle manque est indiscernable d'un catalogue
dont l'éditeur l'a retirée, et il publierait cette règle en `[Obsolete]` — en racontant à
vos utilisateurs quelque chose de faux sur le produit de quelqu'un d'autre.

## Générer depuis un paquet

```bash
dcat generate \
  --package SonarAnalyzer.CSharp --package-version latest \
  --namespace MyCompany.Catalog --container SonarRule \
  --output src/MyCompany.Catalog/SonarRules.g.cs
```

`--package-version` accepte une version exacte, `latest` (la dernière version **stable**)
ou `latest-any` (préversions comprises). `--version` garde le sens qu'il a partout
ailleurs : quelle version de `dcat` vous exécutez.

**Vos sources, pas les nôtres.** `dcat` résout via le client NuGet lui-même, donc il lit la
hiérarchie `NuGet.config` exactement comme le fait `dotnet restore` — machine, utilisateur,
et chaque dossier en remontant depuis l'endroit où vous le lancez — et honore les
identifiants qui y sont configurés, y compris ceux chiffrés et fournis par un provider. Un
paquet sur un flux privé fonctionne sans aucun drapeau supplémentaire. Ajoutez
`--source <nom-ou-url>` pour épingler un flux quand plusieurs sont configurés :

```bash
dcat generate --package Vendor.Analyzers --source maison \
  --namespace My.Catalog --container VendorRule \
  --output src/My.Catalog/VendorRules.g.cs
```

## Générer depuis un paquet sur disque

Un `.nupkg` que vous avez construit, récupéré à la main, ou que vous gardez sur un partage —
tout ce qui n'est jamais passé par un flux que cet outil peut atteindre :

```bash
dcat generate \
  --nupkg packages/Vendor.Analyzers.3.1.4.nupkg \
  --namespace My.Catalog --container VendorRule \
  --output src/My.Catalog/VendorRules.g.cs
```

Le paquet se nomme lui-même : `dcat` lit l'identifiant et la version dans son `.nuspec`, pas
dans le nom de fichier — un fichier renommé ne doit pas réécrire en silence ce qu'un
catalogue consigne comme la version dont il a été généré. Passez `--source-name` ou
`--source-version` quand vous en savez plus, ce qui arrive lorsqu'un paquet est reconstruit
sans que sa version bouge.

## Générer depuis votre propre projet

Désignez-lui le projet plutôt que sa sortie, et MSBuild trouve où se trouve
l'assembly :

```bash
dcat generate --project src/My.Analyzers/My.Analyzers.csproj \
  --namespace My.Catalog --container MyRule \
  --output src/My.Catalog/MyRules.g.cs
```

Ce que cela retire d'un manifeste, c'est le chemin `bin/Release/net8.0/` — la seule partie de
la déclaration d'un catalogue qui ne dit rien du catalogue et qui casse quand le projet change
de cible, est renommé, ou est construit ailleurs. La source est consignée depuis ce que le
projet déclare : son `AssemblyName` et sa `Version`, pas les numéros gravés dans l'assembly,
parce qu'`AssemblyVersion` est couramment épinglée à une majeure pendant que la version bouge.

**Il lit ; il ne construit pas.** Le projet doit être déjà construit, et `dcat` le dit — en
nommant le chemin qu'il a regardé et le `dotnet build` qui le produirait — plutôt que de
construire à votre place. C'est ce qui rend `dcat validate --project` sûr à lancer contre une
copie de travail : il ne restaure rien, n'écrit aucun `obj/`, et ne touche aucune sortie.
`--configuration` choisit quelle compilation lire et vaut `Release` par défaut. Un projet
multicible est lu au travers de `netstandard2.0` quand il en construit un, parce que c'est la
compilation que le compilateur d'un consommateur charge réellement.

Répétez `--project` quand les règles sont réparties sur plusieurs projets, comme le sont
souvent un analyseur et ses correctifs.

## Générer depuis une solution

Désignez-lui la solution, et laissez chaque projet dire si ses règles ont leur place dans un
catalogue :

```xml
<PropertyGroup>
  <ProducesDiagnosticRules>true</ProducesDiagnosticRules>
</PropertyGroup>
```

```bash
dcat generate --solution MySolution.slnx \
  --namespace My.Catalog --container MyRule \
  --output src/My.Catalog/MyRules.g.cs
```

**Rien n'est déduit, et c'est tout l'intérêt.** Lesquels des projets d'une solution
produisent des analyseurs ne se devine pas de l'extérieur. Mesuré sur le dépôt de cet outil
lui-même, *référence `Microsoft.CodeAnalysis`* correspond à neuf projets dont un est un
analyseur ; *déclare un `DiagnosticAnalyzer`* correspond à trois, et deux de ceux-là sont des
fixtures — l'une écrite pour échouer à la construction, l'autre dans un assembly écrit pour
ne pas se charger entièrement. Lire le mauvais ensemble n'est pas un désagrément ici :
un projet manqué signifie que ses règles sont absentes, une règle absente est indiscernable
d'une règle retirée, et elles seraient publiées en `[Obsolete]` — en racontant aux
utilisateurs de cet éditeur quelque chose de faux, sans que rien nulle part ne le signale.

Un projet adhère donc en le disant, dans son propre fichier. La propriété est lue par
l'*évaluation* MSBuild, donc rien n'est restauré, rien n'est construit et aucun `obj/` n'est
écrit — c'est ce qui rend `dcat validate --solution` sûr contre une copie de travail. Comme
pour `--project`, les projets doivent être déjà construits, et `--configuration` choisit
quelle compilation est lue.

Une solution où **personne** ne le déclare est refusée plutôt que lue comme vide :

```
no project in MySolution.slnx declares <ProducesDiagnosticRules>true</ProducesDiagnosticRules>.
Add it to the projects whose analyzers should be catalogued, or name them with --project.
Reading none of them and emitting nothing would report success for a catalogue that was
never generated.
```

Le raisonnement complet — y compris les six alternatives rejetées, et pourquoi l'exactitude
d'une heuristique ne peut pas être évaluée sur votre solution — est consigné dans
[l'ADR-0023](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0023-acquire-a-solutions-analyzers-by-declaration.fr.md).

## Générer depuis vos propres analyseurs

Désignez-lui des assemblys que vous avez déjà construits. Répétez `--assembly` quand un
éditeur — ou vous — répartit les règles sur plusieurs, comme StyleCop répartit les siennes
entre l'assembly d'analyse et celui des correctifs :

```bash
dcat generate \
  --assembly src/My.Analyzers/bin/Release/net8.0/My.Analyzers.dll \
  --source-name My.Analyzers --source-version 1.4.0 \
  --namespace My.Catalog --container MyRule \
  --output src/My.Catalog/MyRules.g.cs
```

Si votre analyseur a été construit avec le SDK, il aura un `.deps.json` à côté de lui.
`dcat` le lit et exécute le worker de descripteurs contre **votre** graphe de dépendances,
si bien qu'un analyseur compilé contre un Roslyn différent de celui que l'outil transporte
est lu au travers du sien plutôt que du nôtre.

Il retombe sur le Roslyn de l'outil quand il n'y a pas de graphe — ce qui arrive pour les
analyseurs dépaquetés d'un paquet NuGet, puisque ceux-là voyagent sans — quand votre cache de
paquets ne contient pas ce que le graphe demande, et **quand le graphe ne nomme aucun Roslyn
du tout**. Ce dernier cas compte parce que fournir un graphe *remplace* celui du worker
plutôt que de l'étendre : un graphe sans Roslyn ne laisse pas le worker avec le sien, il le
laisse sans aucun. Le `.deps.json` d'une bibliothèque `netstandard2.0` est exactement cela,
listant l'assembly et rien d'autre, et `dcat` le dit plutôt que de le lire :

```
resolved MyLib => 1.0.0 (from 1 assembly/assemblies on disk)
  MyLib.deps.json names no Roslyn — reading through this tool's
```

`--source-name` et `--source-version` valent d'être passés. Un catalogue consigne de quelle
version il a été généré, et c'est cette consignation qui distingue un instantané du suivant :
le fichier est laissé intact quand ni elle ni aucune règle n'a bougé. Un assembly construit
depuis une copie de travail porte ce que son projet a fixé en dernier — souvent inchangé d'une
reconstruction à l'autre — si bien qu'un catalogue dérivé de lui seul peut prétendre à une
source immobile pendant que ses règles bougent en dessous.

## En générer plusieurs d'un coup

Un manifeste déclare autant de catalogues qu'on veut, depuis n'importe quel genre de source —
`package`, `nupkg`, `projects`, `solution` ou `assemblies`, un par entrée. Les chemins qu'il
contient sont relatifs au manifeste, donc il fonctionne depuis n'importe quel répertoire :

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
    },
    {
      "solution": "../MySolution.slnx",
      "configuration": "Release",
      "namespace": "House.Catalog",
      "container": "HouseRule",
      "output": "../src/House.Catalog/HouseRules.g.cs"
    }
  ]
}
```

```bash
dcat generate --manifest eng/catalogs.json --summary "$RUNNER_TEMP/summary.md"
```

La ligne `$schema` vaut les deux secondes qu'elle coûte. Elle documente chaque clé dans votre
éditeur et signale une clé mal orthographiée là où vous l'avez tapée — plutôt qu'après le
téléchargement d'un paquet, qui est l'endroit où `dcat` la signale. `dcat` nomme le fichier,
l'entrée et la clé dans les deux cas :

```
error: catalogs.json: catalogs[2]: "namespace" is missing.
```

`--summary` écrit un rapport Markdown de chaque changement de ce que le catalogue publie, en
nommant une par une celles qui portent sur les règles — ajoutées, recatégorisées, retitrées,
relinkées, retirées, redéclarées. C'est ce qui fait qu'une régénération planifiée ouvre une pull
request qu'un humain peut relire plutôt que fusionner à l'aveugle.

## Vérifier qu'un catalogue est toujours vrai

`validate` fait tout ce que fait `generate` et s'arrête une étape avant : il acquiert la
source, lit ses descripteurs, calcule le catalogue qui serait écrit — et n'écrit rien. Il
répond à la question de savoir si ce que vous avez sur disque correspond encore à ce que votre
source déclare.

```bash
dcat validate --manifest eng/catalogs.json
```

| Sortie | Signification |
|---|---|
| `0` | À jour. |
| `2` | Périmé — régénérez. |
| `1` | N'a pas pu être vérifié : la source n'a pas voulu se résoudre. Distinct exprès, pour qu'une panne de flux ne soit jamais signalée comme un contrat qui a dérivé. |

C'est la question à laquelle aucun analyseur ne peut répondre à votre place. Les diagnostics
`DCAT` vérifient qu'un catalogue est bien formé et correctement utilisé, à la compilation, ce
qui est le meilleur endroit pour cela — mais aucun d'eux ne peut vérifier qu'il est encore
*à jour*, parce qu'il y faut le paquet de l'éditeur et qu'un compilateur n'a pas à en récupérer
un. Et la péremption est la défaillance sans symptôme : une catégorie qui a bougé en amont
compile toujours, ne supprime rien, et ne dit rien.

## Lire un catalogue

`list` et `explain` lisent un catalogue **compilé** — l'assembly d'un paquet que vous
référencez, pas un fichier source qu'il vous aurait fallu générer vous-même. Rien n'y est
exécuté : un catalogue déclare tout ce qu'il publie comme constantes de métadonnées, il est
donc lu en réflexion seule.

```bash
dcat list  ~/.nuget/packages/diagnosticcatalog.stylecop/0.2.1/lib/netstandard2.0/DiagnosticCatalog.StyleCop.dll
dcat explain <that same path> SA1000
```

```
StyleCop.Analyzers.Unstable 1.2.0.556, generated 2026-07-31

id        SA1000
category  StyleCop.CSharp.SpacingRules
help      https://github.com/DotNetAnalyzers/StyleCopAnalyzers/blob/master/documentation/SA1000.md

[SuppressMessage(
    StyleCopRule.SA1000.Category,
    StyleCopRule.SA1000.Id,
    Justification = "…")]
```

L'extrait est le point important : c'est la ligne à copier, pleinement qualifiée comme vous
l'écririez. Les deux commandes indiquent quelle version amont le catalogue reflète et quand il
a été généré avant de répondre, parce qu'un catalogue est un instantané et que son âge décide
si sa réponse est digne de confiance.

## Reproductibilité

La même version amont produit les mêmes octets : les règles et les catégories sont émises dans
l'ordre ordinal, les titres sont lus en culture invariante, et une règle que l'éditeur a
retirée est reportée en `[Obsolete]` plutôt que supprimée, parce que les consommateurs
incorporent les valeurs `const` et qu'en retirer une casse leur recompilation.

Épinglez `--date yyyy-MM-dd` quand vous avez besoin que deux exécutions sur les mêmes entrées
soient identiques à l'octet près ; laissée vide, elle estampille la date du jour, qui n'atteint
le fichier que si quelque chose d'autre a changé aussi.

## Codes de sortie

| Code | Signification |
|---|---|
| `0` | Les catalogues ont été générés. |
| `1` | L'exécution n'a pas pu aboutir : un paquet amont qui ne s'est pas résolu, un analyseur qui n'a pas pu être construit, une sortie qui n'a pas pu être écrite. |
| `2` | `validate` seulement : le catalogue ne correspond plus à sa source. |
| `64` | La ligne de commande est erronée. Aucune nouvelle tentative n'y changera rien. |

## Exécution

`dcat` cible **.NET 8** et avance en roll-forward d'une majeure à l'autre, si bien qu'une seule
compilation tourne sur .NET 8 et tout ce qui est plus récent.

Les descripteurs sont lus dans un **processus worker séparé**, qui avance en roll-forward vers
la *dernière* majeure installée plutôt que vers la première trouvée. C'est ce qui empêche le
plancher qui rend `dcat` installable de décider de ce qu'il peut lire : un analyseur construit
pour une cible plus récente se charge quand même, pourvu que ce runtime soit présent. Cela
signifie aussi qu'un analyseur dont la construction plante emporte le worker et laisse `dcat`
vous dire lequel — plutôt que de voir l'exécution entière disparaître.

Ce que ce worker transporte décide aussi des langages catalogables, et il ne transporte que
**Roslyn C#**. Lire des descripteurs veut dire *construire* chaque analyseur, et un analyseur
Visual Basic dérive de types de `Microsoft.CodeAnalysis.VisualBasic`, qui n'est pas là — donc
`--language` accepte `cs` et refuse tout le reste sur la ligne de commande, plutôt qu'après le
téléchargement d'un paquet. C'est une position arrêtée plutôt qu'un manque en attente de
travaux, et le raisonnement est consigné dans
[l'ADR-0020](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0020-a-catalogue-is-generated-for-c-sharp-only.fr.md).

Les deux processus que `dcat` engendre — ce worker, et MSBuild pour `--project` et
`--solution` — reçoivent
un budget et sont arrêtés s'ils le dépassent. Construire un analyseur, c'est du code tiers, et
c'est la seule étape ici qui peut *se figer* plutôt qu'échouer ; un enfant coincé emporterait
sinon l'outil avec lui, laissant un pipeline tourner jusqu'à son propre délai d'expiration sans
rien à lire. Les valeurs par défaut sont de 10 minutes pour une lecture de descripteurs et de
2 minutes pour une évaluation de projet, face à des temps mesurés en secondes. Fixez
`DCAT_TIMEOUT_SECONDS` à un nombre entier positif de secondes pour allonger les deux.

## Documentation

- [**L'outil `dcat`**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/dcat.fr.md)
  — les quatre verbes, quelle source lui désigner, et pourquoi il lit des descripteurs plutôt que
  de la documentation.
- [**La référence `dcat`**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/dcat-reference.fr.md)
  — chaque commande, option et code de sortie, vérifiés contre les types de paramètres de l'outil.
- [**Le manifeste de catalogues**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/catalogs-manifest.fr.md)
  — chaque clé de `catalogs.json`.
- [**Garder un catalogue à jour**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/ci-integration.fr.md)
  — `validate` dans un pipeline, la pull request de dérive nocturne, et pourquoi les codes de
  sortie `1` et `2` doivent être traités différemment.
- [**Publier un catalogue**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/authoring-a-catalogue.fr.md)
  — ce que la sortie générée doit satisfaire, si vous êtes sur le point d'en livrer un.

La [**carte de la documentation**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/README.fr.md)
choisit une page selon ce que vous cherchez à faire ; chaque guide existe en anglais et en français.

---

Non officiel au regard de chaque éditeur d'analyseur qu'il lit ; sans affiliation avec aucun
d'eux ni approbation de leur part.
