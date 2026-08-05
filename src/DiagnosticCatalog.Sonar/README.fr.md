# DiagnosticCatalog.Sonar

🌍 **Langues :**  
🇬🇧 [English](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.Sonar/README.en.md) | 🇫🇷 Français (ce fichier)

Les règles **SonarAnalyzer.CSharp** sous forme de constantes fortement référencées, pour que
`SuppressMessageAttribute` prenne des références vérifiées à la compilation plutôt que des chaînes magiques.

<!-- mirror:begin -->
> ## 🪞 Reflète `SonarAnalyzer.CSharp 10.31.0.145097`
>
> **456 règles, 13 catégories**, chaque identifiant et chaque
> catégorie lus dans les analyseurs de cette version. Régénéré le 2026-07-31.
<!-- mirror:end -->

> Non officiel. Sans affiliation avec SonarSource, ni approbation ni support de sa part. « Sonar » et
> « SonarQube » sont des marques de SonarSource S.A.

## Pourquoi

Les deux arguments d'une suppression Sonar sont des chaînes magiques, et aucun n'est vérifié :

```csharp
[SuppressMessage("Major Code Smell", "S1144", Justification = "...")]
```

Trompez-vous d'identifiant et la suppression ne fait silencieusement rien — l'avertissement
reste, tout simplement. Trompez-vous de catégorie et **il ne se passe rien du tout**, jamais :
la plateforme .NET ne lit jamais cet argument, donc aucune compilation, aucun test et aucun
outil ne vous le dira. Et vous ne le devineriez pas : la catégorie de `S1144` est
`"Major Code Smell"`, ni `"Code Smell"`, ni `"Maintainability"`.

```csharp
using DiagnosticCatalog.Sonar;

[SuppressMessage(
    SonarRule.S1144.Category,
    SonarRule.S1144.Id,
    Justification = "Instantiated through reflection by the DI container.")]
```

Une règle que l'amont renomme ou retire casse désormais la compilation au lieu de laisser une
suppression morte derrière elle.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.Sonar" Version="0.1.0" />
```

Ce paquet ne fournit que les constantes. Les vérifications qui valident les déclarations de règles et
leurs sites d'utilisation sont livrées à part dans `DiagnosticCatalog.Analyzers`.

## Utilisation

```csharp
using System.Diagnostics.CodeAnalysis;
using DiagnosticCatalog.Sonar;

public sealed class Repository
{
    [SuppressMessage(
        SonarRule.S1481.Category,
        SonarRule.S1481.Id,
        Justification = "Placeholder retained for the upcoming migration step.")]
    public int Compute()
    {
        int unused = 42;
        return 1;
    }
}
```

Tapez `SonarRule.` et IntelliSense liste chaque règle ; tapez `S1481` et il filtre jusqu'à elle.

## Ce que contient le paquet

456 règles, couvrant 13 catégories :

| | Blocker | Critical | Major | Minor | Info |
| --- | --- | --- | --- | --- | --- |
| Bug | ✓ | ✓ | ✓ | ✓ | |
| Code Smell | ✓ | ✓ | ✓ | ✓ | ✓ |
| Vulnerability | ✓ | ✓ | ✓ | ✓ | |

Chaque règle expose exactement les deux constantes obligatoires — les descripteurs de Sonar ne portent
aucun lien d'aide (0 sur 465), donc aucun n'est inventé ici :

```csharp
[DiagnosticRule]
public static class S1144
{
    public const string Id = nameof(S1144);
    public const string Category = SonarCategory.MajorCodeSmell;
}
```

**Identifiants, catégories et titres.** Les trois sont lus depuis les analyseurs eux-mêmes. Le
titre est la phrase de SonarSource elle-même, portée comme commentaire de documentation de la règle
pour que survoler une constante dise de quoi la règle parle. Les descriptions de règles sont leur
documentation et ne sont délibérément pas redistribuées ici — suivez l'identifiant de la règle jusqu'à
[rules.sonarsource.com](https://rules.sonarsource.com/csharp/) pour celles-là.

## Catégories déclarées une seule fois

Un catalogue répète très peu de catégories distinctes sur un très grand nombre de règles. Chacune est
déclarée une fois dans `SonarCategory` et les règles s'y réfèrent, donc il y a une source unique par valeur :

```csharp
[DiagnosticCategory]
public static class SonarCategory
{
    public const string MajorCodeSmell = "Major Code Smell";
}

[DiagnosticRule]
public static class S1144
{
    public const string Id = nameof(S1144);
    public const string Category = SonarCategory.MajorCodeSmell;
}
```

Une `const` initialisée depuis une autre `const` reste une constante de compilation, donc
`SonarRule.S1144.Category` demeure valide comme argument d'attribut et se replie toujours en
`"Major Code Smell"` dans les métadonnées. L'indirection ne coûte rien.

`SonarCategory` est aussi utilisable seule — IntelliSense dessus liste exactement les 13 catégories
que cet analyseur utilise réellement.

## Comment il est produit

Ni transcrit depuis la documentation, ni depuis un JSON de métadonnées de règles. Le générateur charge
`SonarAnalyzer.CSharp`, construit les analyseurs qu'il marque de `[DiagnosticAnalyzer]`, et lit les
instances de `DiagnosticDescriptor` qu'ils déclarent réellement. C'est la seule source qui ne puisse
pas se tromper — et comme la plateforme ne valide jamais une catégorie, une valeur copiée depuis une
documentation qui aurait dérivé ne produirait de symptôme nulle part.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package SonarAnalyzer.CSharp --package-version latest \
    --namespace DiagnosticCatalog.Sonar --container SonarRule \
    --output src/DiagnosticCatalog.Sonar/SonarRules.g.cs
```

Neuf entrées sont délibérément exclues : `S9999-cpd`, `S9999-log`, `S9999-metadata`,
`S9999-metrics`, `S9999-symbolRef`, `S9999-telemetry`, `S9999-testMethodDeclaration`,
`S9999-token-type` et `S9999-warning`. Elles portent une catégorie vide parce que ce sont des canaux
internes de métriques et de télémétrie plutôt que des diagnostics supprimables.

## Quelle version amont il reflète

L'assembly consigne sa propre provenance, lisible depuis les métadonnées sans la source :

```csharp
[assembly: CatalogSource(
    source:        "SonarAnalyzer.CSharp",
    sourceVersion: "10.31.0.145097",
    generatedOn:   "2026-07-30")]
```

```csharp
var source = typeof(SonarRule.S1144).Assembly
    .GetCustomAttributes<CatalogSourceAttribute>()
    .Single();
// source.SourceVersion => "10.31.0.145097"
// source.GeneratedOn   => "2026-07-30"
```

Un catalogue est un instantané : l'amont ajoute, retire et recatégorise des règles à chaque
version. Si votre projet référence un `SonarAnalyzer.CSharp` bien plus récent que ce que dit
`SourceVersion`, il se peut que des règles manquent à ce catalogue.

## Comment il reste à jour

Un workflow nocturne régénère chaque catalogue depuis son paquet amont et ouvre une pull request
quand quelque chose a réellement bougé — règles ajoutées, recatégorisées, retirées en amont. Il ne
publie jamais : une catégorie ou un identifiant qui a changé en amont change un contrat publié, et
comme la plateforme ne lit jamais la catégorie d'une suppression, une valeur fausse fusionnée sans
relecture ne produirait de symptôme nulle part. Un humain lit le diff.

Les nuits où l'amont n'a pas bougé ne produisent rien du tout : le générateur compare sa propre
sortie précédente et laisse le fichier intact, `generatedOn` compris.

**Une règle retirée en amont n'est jamais supprimée.** Elle est conservée et marquée `[Obsolete]` en
nommant la version qui l'a abandonnée, pour qu'un projet qui la référence encore obtienne un
avertissement `CS0618` lui disant de retirer la suppression — plutôt qu'une erreur dure sur un membre
qui a disparu. Les consommateurs incorporent les valeurs de constantes à leur propre compilation, donc
en supprimer une casse leur recompilation.

Pour régénérer tous les catalogues d'un coup :

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate --manifest eng/catalogs.json
```

## Comment il arrive sur nuget.org

Ce catalogue roule sur le [train de release](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md) `sonar` et se versionne indépendamment de
la fondation, pour pouvoir suivre la cadence de SonarSource sans entraîner quoi que ce soit d'autre.

La publication ne fait pas partie du nocturne. Un mainteneur pousse une étiquette `sonar-vX.Y.Z`, et le
workflow de release empaquette le paquet, y intègre un SBOM SPDX, et publie via le
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) de NuGet avec une provenance de build signée — aucune clé d'API à longue
durée de vie n'existe nulle part pour fuiter. La moitié empaquetage de ce pipeline est répétée à chaque
pull request, si bien qu'une release ne l'exerce jamais pour la première fois sur une étiquette.

## Limites

`[SuppressMessage]` ne peut pas supprimer les avertissements du **compilateur** — `CS0219` et
consorts demandent `#pragma warning disable`, qui prend des identifiants nus et ne peut donc jamais
référencer une constante. Ce paquet ne couvre que les règles d'analyse `Sxxxx` de Sonar.

## Voir aussi

Douze catalogues frères sont générés depuis ce dépôt de la même façon, chacun lu depuis les descripteurs
d'un seul analyseur :

- [`DiagnosticCatalog.NetAnalyzers`](https://www.nuget.org/packages/DiagnosticCatalog.NetAnalyzers)
  — les règles d'analyse de code .NET (`CAxxxx`).
- [`DiagnosticCatalog.StyleCop`](https://www.nuget.org/packages/DiagnosticCatalog.StyleCop)
  — les règles StyleCop.Analyzers (`SAxxxx`).
- [`DiagnosticCatalog.CodeStyle`](https://www.nuget.org/packages/DiagnosticCatalog.CodeStyle)
  — les règles de style de code IDE de Roslyn (`IDExxxx`).
- [`DiagnosticCatalog.Xunit`](https://www.nuget.org/packages/DiagnosticCatalog.Xunit)
  — les règles xunit.analyzers (`xUnitxxxx`).
- [`DiagnosticCatalog.NUnit`](https://www.nuget.org/packages/DiagnosticCatalog.NUnit)
  — les règles NUnit.Analyzers (`NUnitxxxx`).
- [`DiagnosticCatalog.MSTest`](https://www.nuget.org/packages/DiagnosticCatalog.MSTest)
  — les règles MSTest.Analyzers (`MSTESTxxxx`).
- [`DiagnosticCatalog.Trimming`](https://www.nuget.org/packages/DiagnosticCatalog.Trimming)
  — les avertissements de trimming, Native AOT et fichier unique (`ILxxxx`).
- [`DiagnosticCatalog.AspNetCore`](https://www.nuget.org/packages/DiagnosticCatalog.AspNetCore)
  — les règles ASP.NET Core et Blazor (`ASPxxxx`, `BLxxxx`).
- [`DiagnosticCatalog.Syslib`](https://www.nuget.org/packages/DiagnosticCatalog.Syslib)
  — les diagnostics des générateurs de source du runtime .NET (`SYSLIB1xxx`).
- [`DiagnosticCatalog.Roslyn`](https://www.nuget.org/packages/DiagnosticCatalog.Roslyn)
  — les règles d'écriture d'analyseurs Roslyn (`RS1xxx`, `RS2xxx`).
- [`DiagnosticCatalog.PublicApi`](https://www.nuget.org/packages/DiagnosticCatalog.PublicApi)
  — les règles de suivi d'API publique (`RS00xx`).
- [`DiagnosticCatalog.Self`](https://www.nuget.org/packages/DiagnosticCatalog.Self)
  — les règles `DCATxxxx` de cette bibliothèque, pour supprimer un diagnostic que les analyseurs de
  catalogue signalent eux-mêmes.

**Vous voulez un catalogue à vous ?** Les règles de votre analyseur, ou un jeu de règles interne, se
déclarent exactement comme celles-ci : une classe statique de constantes marquée `[DiagnosticRule]`,
référencée par les consommateurs plutôt que retapée. Ce marqueur est livré dans
[`DiagnosticCatalog`](https://www.nuget.org/packages/DiagnosticCatalog), la fondation sur laquelle ce
catalogue est bâti, et son README en est le guide.

## Documentation

Pour utiliser un catalogue, dans l'ordre où le travail se fait :

- [**Démarrage**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/getting-started.fr.md)
  — dix minutes : référencer ce paquet, réécrire une suppression, la casser exprès et regarder le
  compilateur l'attraper.
- [**Écrire des suppressions que le compilateur vérifie**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/writing-suppressions.fr.md)
  — la version complète, migration des littéraux que vous avez déjà comprise.
- [**Adopter un catalogue sur une base de code existante**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/adopting-a-catalogue.fr.md)
  — la montée en sévérité, *Corriger toutes les occurrences*, le cadrage par dossier, et dans quel ordre convertir.
- [**Configuration**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/configuration.fr.md)
  — chaque clé de sévérité, le commutateur par catégorie, et l'erreur de `PrivateAssets` qui
  fait tout taire.
- [**Dépannage**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/troubleshooting.fr.md)
  — par symptôme : rien n'est signalé, `CS0117`, `CS0618` après une montée de version.

La [**carte de la documentation**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/README.fr.md)
choisit une page selon ce que vous cherchez à faire ; chaque guide existe en anglais et en français. La
[**spécification**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/specification.fr.md)
en est la version normative.

## Licence

Apache-2.0. Les identifiants et catégories de règles sont des faits à propos d'un analyseur tiers ;
ce paquet n'est pas une œuvre dérivée des descriptions de règles de SonarSource.
