# DiagnosticCatalog.Trimming

🌍 **Langues :**  
🇬🇧 [English](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.Trimming/README.en.md) | 🇫🇷 Français (ce fichier)

Les **avertissements de trimming, Native AOT et fichier unique** (`ILxxxx`) sous forme de constantes
fortement référencées, pour que `UnconditionalSuppressMessageAttribute` prenne des références vérifiées
à la compilation plutôt que des chaînes magiques.

<!-- mirror:begin -->
> ## 🪞 Reflète `Microsoft.NET.ILLink.Tasks 10.0.10`
>
> **77 règles, 3 catégories**, chaque identifiant et chaque
> catégorie lus dans les analyseurs de cette version. Régénéré le 2026-08-05.
<!-- mirror:end -->

> Non officiel. Sans affiliation avec Microsoft, ni approbation ni support de sa part.

## Pourquoi ce catalogue n'est pas comme les autres

Chaque autre catalogue de cette famille existe parce que **rien ne lit la catégorie d'une
suppression**. Trompez-vous, et aucune erreur, aucun avertissement et aucun test en échec ne vous le
dira jamais.

Celui-ci est le cas inverse, et il est pire. `UnconditionalSuppressMessageAttribute` **est** analysé
— par deux décodeurs différents, avec deux règles différentes — et un identifiant qu'aucun des deux
n'accepte est jeté en silence. Le décodeur du linker est exact là-dessus :

```csharp
if (!(attribute.ConstructorArguments[1].Value is string warningId)
    || warningId.Length < 6
    || !warningId.StartsWith("IL")
    || !int.TryParse(warningId.AsSpan(2, 4), out info.Id))
```

Tout ce qui n'est pas `IL####` est ignoré d'emblée. L'analyseur de trimming à la compilation
implémente sa *propre* règle — tronquer au premier deux-points, puis correspondre exactement — si bien
que les deux ne s'accordent même pas sur ce qu'ils acceptent.

Et contrairement à un `[SuppressMessage]` mal catégorisé, la conséquence ici n'est pas un
avertissement qui reste discrètement. Une suppression que le linker a jetée signifie que
l'avertissement qu'elle devait faire taire ne l'a jamais été, donc le motif qu'elle couvrait est
supprimé au trimming — et vous l'apprenez sous forme de `TypeLoadException` en production, sur un
chemin de code que personne n'avait exercé avant la publication.

## Pourquoi vous avez ces avertissements, et ne les avez probablement pas demandés

`PublishTrimmed` et `PublishAot` sont optionnels — sauf que plusieurs SDK les activent pour vous :

| Ce que vous construisez | Analyseur de trimming |
| --- | --- |
| **Blazor WebAssembly** | **Activé, à chaque compilation.** `Microsoft.NET.Sdk.BlazorWebAssembly` met `PublishTrimmed` dans ses propres props |
| **MAUI** sur iOS/Android | Activé, en Release |
| Tout ce qui a `PublishAot` | Activé — l'AOT implique le trimming |
| Une bibliothèque déclarant `IsTrimmable` | Activé, même si vous ne publiez jamais vous-même en trimmé |
| Une console, un service ou une appli web ordinaire | Désactivé, sauf demande |

Le commutateur n'est pas la commande de publication ; c'est une **propriété de projet**, donc
`Microsoft.NET.Sdk.Analyzers.targets` active `EnableTrimAnalyzer` à la compilation. Un développeur
Blazor WebAssembly voit `IL2026` à chaque `dotnet build`, sans avoir rien choisi — ce qui a la même
forme que les règles IDE de Roslyn sous `EnforceCodeStyleInBuild`.

## Les deux attributs, et celui qu'il vous faut

C'est la partie qui vaut d'être bien comprise, parce que le catalogue sert les deux et qu'ils ne sont
pas interchangeables.

```csharp
// Faire taire l'analyseur À LA COMPILATION — l'IL2026 de votre sortie de build.
[SuppressMessage(TrimRule.IL2026.Category, TrimRule.IL2026.Id, Justification = "…")]

// Faire taire le LINKER, qui lit l'assembly compilé bien après le départ du compilateur.
[UnconditionalSuppressMessage(TrimRule.IL2026.Category, TrimRule.IL2026.Id, Justification = "…")]
```

`SuppressMessageAttribute` porte `[Conditional("CODE_ANALYSIS")]`, il n'est donc **pas conservé dans
l'assembly compilé**. ILLink et ILCompiler s'exécutent après la compilation et lisent les suppressions
dans l'IL — ils ne peuvent pas le voir. C'est toute la raison d'être
d'`UnconditionalSuppressMessageAttribute` : même forme, sans `[Conditional]`, donc il survit.

Utilisez l'inconditionnel quand l'avertissement doit rester tu jusqu'à la publication. Atteignez les
deux au travers de ce catalogue et l'identifiant est vérifié dans les deux cas.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.Trimming" Version="1.0.0" />
```

Ce paquet ne fournit que les constantes. Les vérifications qui valident les déclarations de règles et
leurs sites d'utilisation sont livrées à part dans `DiagnosticCatalog.Analyzers` — y compris
**`DCAT0009`**, qui signale un `UnconditionalSuppressMessage` dont l'identifiant n'est pas `IL####`.
Ce diagnostic est arrivé avant ce catalogue : la vérification existait, et il n'y avait aucune
constante à lui donner.

## Ce que contient le paquet

77 règles réparties sur 3 catégories, et **aucune d'elles ne porte de lien d'aide** — l'analyseur n'en
déclare aucun. Il n'y a rien vers quoi cliquer, ce qui est exactement là où un catalogue gagne sa
place : le commentaire de documentation sur chaque constante est le seul endroit où la formulation
propre à la règle est disponible au point d'utilisation.

| Catégorie | Règles | De quoi elles parlent |
| --- | --- | --- |
| `Trimming` | 64 | De la réflexion que le trimmer ne peut pas suivre — la plage `IL2xxx` |
| `AOT` | 7 | Du code qui a besoin de génération de code à l'exécution, plus les règles `FeatureGuard` (`IL3050`, `IL4000`) |
| `SingleFile` | 6 | Des chemins de fichiers d'assembly qui n'existent pas dans un bundle fichier unique (`IL300x`) |

```csharp
[DiagnosticRule]
public static class IL2026
{
    public const string Id = nameof(IL2026);
    public const string Category = TrimCategory.Trimming;
}
```

## Catégories déclarées une seule fois

`TrimCategory` détient chaque catégorie une fois, et les règles la référencent — si bien que
l'orthographe d'une catégorie n'existe qu'à un seul endroit. Elle est **interne par conception** : une
suppression atteint une catégorie au travers de la règle qui la porte, `TrimRule.IL2026.Category`, et
jamais au travers de la constante de catégorie seule. Les deux se replient sur la même chaîne
aujourd'hui et cessent de s'accorder le jour où une règle bouge
([ADR-0026](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.fr.md)).

## Comment il est produit

Pas transcrit depuis la documentation. Le générateur lit dans les métadonnées des assemblys d'analyse
les types qu'ils marquent de `[DiagnosticAnalyzer]`, construit ceux-là, et lit les instances de
`DiagnosticDescriptor` qu'ils déclarent réellement — la seule source qui ne puisse pas avoir dérivé.
L'analyseur est livré dans `Microsoft.NET.ILLink.Tasks`, le paquet même que le SDK restaure quand vous
publiez en trimmé.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package Microsoft.NET.ILLink.Tasks --package-version latest \
    --namespace DiagnosticCatalog.Trimming --container TrimRule \
    --output src/DiagnosticCatalog.Trimming/TrimRules.g.cs
```

## Comment il reste à jour

Un workflow nocturne régénère chaque catalogue depuis son paquet amont et ouvre une pull request
quand quoi que ce soit que le catalogue publie a bougé. Il ne
publie jamais : une catégorie ou un identifiant qui a changé en amont change un contrat publié, et
comme la plateforme ne lit jamais la catégorie d'une suppression, une valeur fausse fusionnée sans
relecture ne produirait de symptôme nulle part. Un humain lit le diff.

**Une règle retirée en amont n'est jamais supprimée.** Elle est conservée et marquée `[Obsolete]` en
nommant la version qui l'a abandonnée, pour qu'un projet qui la référence encore obtienne un
avertissement `CS0618` lui disant de retirer la suppression — plutôt qu'une erreur dure sur un membre
qui a disparu. Les consommateurs incorporent les valeurs de constantes à leur propre compilation, donc
en supprimer une casse leur recompilation.

## Comment il arrive sur nuget.org

Ce catalogue roule sur le [train de release](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md) `trimming` et se versionne
indépendamment de la fondation, pour pouvoir suivre les versions d'ILLink du SDK sans entraîner quoi que ce soit d'autre.

La publication ne fait pas partie du nocturne. Un mainteneur pousse une étiquette `trimming-vX.Y.Z`, et
le workflow de release empaquette le paquet, y intègre un SBOM SPDX, et publie via le
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) de NuGet
avec une provenance de build signée — aucune clé d'API à longue durée de vie n'existe nulle part pour
fuiter. La moitié empaquetage de ce pipeline est répétée à chaque pull request, si bien qu'une release
ne l'exerce jamais pour la première fois sur une étiquette.

## Limites

`[SuppressMessage]` ne peut pas supprimer les avertissements du **compilateur** — `CS0219` et
consorts demandent `#pragma warning disable`, qui prend des identifiants nus et ne peut donc jamais
référencer une constante. Ce paquet ne couvre que les règles d'analyse `ILxxxx`.

## Voir aussi

Chaque catalogue publié par ce dépôt est listé au même endroit — choisissez celui qui correspond à
un analyseur que vous exécutez :

**[Les catalogues disponibles](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/README.fr.md#-les-catalogues-disponibles)**

**Vous voulez un catalogue à vous ?** Les règles de votre analyseur, ou un jeu de règles interne, se
déclarent exactement comme celles-ci : une classe statique de constantes marquée `[DiagnosticRule]`,
référencée par les consommateurs au lieu d'être retapée. Ce marqueur est livré par
[`DiagnosticCatalog`](https://www.nuget.org/packages/DiagnosticCatalog), la fondation sur laquelle ce catalogue est
bâti, et son README est le guide.

## Documentation

Pour utiliser un catalogue, dans l'ordre où le travail se fait :

- [**Démarrage**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/getting-started.fr.md)
  — dix minutes : référencer ce paquet, réécrire une suppression, la casser exprès et regarder le
  compilateur l'attraper.
- [**Écrire des suppressions que le compilateur vérifie**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/writing-suppressions.fr.md)
  — la version complète, migration des littéraux que vous avez déjà comprise.
- [**Adopter un catalogue sur une base de code existante**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/adopting-a-catalogue.fr.md)
  — la montée en sévérité, *Corriger toutes les occurrences*, le cadrage par dossier, et dans quel
  ordre convertir.
- [**Configuration**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/configuration.fr.md)
  — chaque clé de sévérité, le commutateur par catégorie, et l'erreur de `PrivateAssets` qui fait
  tout taire.
- [**Dépannage**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/troubleshooting.fr.md)
  — par symptôme : rien n'est signalé, `CS0117`, `CS0618` après une montée de version.

La [**carte de la documentation**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/README.fr.md)
choisit une page selon ce que vous cherchez à faire ; chaque guide existe en anglais et en français. La
[**spécification**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/specification.fr.md)
en est la version normative — le §9.1 est celui qui parle de cet attribut.

## Licence

Apache-2.0. Les identifiants, catégories et titres des règles sont lus depuis un analyseur Microsoft,
lui-même sous licence MIT.
