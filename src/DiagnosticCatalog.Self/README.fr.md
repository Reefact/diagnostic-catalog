# DiagnosticCatalog.Self

🌍 **Langues :**  
🇬🇧 [English](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.Self/README.en.md) | 🇫🇷 Français (ce fichier)

Les règles `DCAT` — celles que signale [`DiagnosticCatalog.Analyzers`](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.Analyzers/README.fr.md)
— sous forme de constantes que vous pouvez référencer.

C'est la bibliothèque appliquée à elle-même. Les analyseurs qui vérifient *vos* suppressions publient leurs
propres règles de la manière exacte qu'ils demandent à tout le monde, et ils le font au travers du même
générateur qui produit les catalogues Sonar, analyseurs .NET et StyleCop.

## Quand vous en voulez

Quand vous supprimez un diagnostic `DCAT` et préféreriez que la suppression soit vérifiée :

```csharp
using System.Diagnostics.CodeAnalysis;
using DiagnosticCatalog.Self;

// Migration d'une grande base de code : ce fichier passe en dernier, et les littéraux ici sont voulus.
[SuppressMessage(
    DcatRule.DCAT0006.Category,
    DcatRule.DCAT0006.Id,
    Justification = "Legacy suppressions, migrated in the next pass.")]
public static class LegacyInterop
{
}
```

Sans le catalogue vous écririez `[SuppressMessage("DiagnosticCatalog", "DCAT0006")]` — deux chaînes que rien
ne vérifie, ce qui est exactement le problème que ce dépôt existe pour supprimer. Il aurait été curieux de
laisser nos propres règles comme le seul endroit où il fallait encore les écrire à la main.

La plupart des projets n'en auront pas besoin : `.editorconfig` est la façon habituelle d'abaisser un
diagnostic `DCAT`, et il prend du texte brut dans lequel aucune constante ne pourra jamais être substituée.
Tournez-vous vers le catalogue quand vous supprimez à un *endroit précis*, pour une raison qui vaut d'être
écrite.

## D'où il vient

Généré depuis les instances de `DiagnosticDescriptor` des analyseurs eux-mêmes, jamais depuis la
documentation, si bien que l'identifiant et la catégorie sont les valeurs que l'analyseur signale
réellement. Le régénérer tient en une commande :

```sh
dotnet run --project src/DiagnosticCatalog.Cli -- generate --manifest eng/catalogs.json
```

La CI le régénère à chaque pull request et échoue si le résultat diffère de ce qui est commité — ainsi un
nouvel identifiant `DCAT` ne peut pas être livré sans le catalogue qui le publie.

## Versionnage

Ce catalogue roule sur le train `lib`, avec les analyseurs qu'il reflète, et c'est délibéré : les deux sont
générés depuis une seule source dans un seul dépôt et ne doivent jamais décrire des jeux de règles
différents. Les dix autres catalogues se versionnent indépendamment parce qu'un éditeur extérieur donne leur
cadence ([ADR-0015](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0015-a-catalogues-version-runs-on-its-own-line.fr.md)) ; personne à
l'extérieur ne donne celle-ci.

Une règle retirée est reportée en `[Obsolete]` plutôt que supprimée, comme partout ailleurs ici : les
constantes sont incorporées dans votre assembly à *votre* compilation, si bien qu'en retirer une casse votre
build avec un message qui ne nomme rien d'utile.

## Voir aussi

Treize catalogues frères sont générés depuis ce dépôt de la même façon, chacun lu depuis les descripteurs d'un
seul analyseur — à ceci près que les leurs appartiennent à quelqu'un d'autre :

- [`DiagnosticCatalog.Sonar`](https://www.nuget.org/packages/DiagnosticCatalog.Sonar)
  — les règles SonarAnalyzer.CSharp (`Sxxxx`).
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
- [`DiagnosticCatalog.BannedApi`](https://www.nuget.org/packages/DiagnosticCatalog.BannedApi)
  — les règles d'API bannies (`RS0030`, `RS0031`, `RS0035`).

**Vous voulez un catalogue à vous ?** C'est à cela que sert
[le guide de l'auteur de catalogue](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/authoring-a-catalogue.fr.md), et ce paquet en est
l'exemple travaillé : une classe statique de constantes marquée `[DiagnosticRule]`, générée depuis
l'analyseur qui les signale. Le marqueur est livré dans [`DiagnosticCatalog`](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog/README.fr.md).

## Documentation

- [**Les diagnostics `DCAT`**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/diagnostics.fr.md)
  — chaque règle cataloguée ici, vue du côté qui la signale.
- [**Écrire des suppressions que le compilateur vérifie**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/writing-suppressions.fr.md)
  — comment utiliser ces constantes, ce qui est identique pour n'importe quel autre catalogue.
- [**Architecture du dépôt**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/architecture.fr.md)
  — la boucle d'auto-application dont ce paquet est une moitié, et pourquoi elle tourne dans un seul sens.

La [**carte de la documentation**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/README.fr.md)
choisit une page selon ce que vous cherchez à faire ; chaque guide existe en anglais et en français.

## Licence

Apache-2.0.
