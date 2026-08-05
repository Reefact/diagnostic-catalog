# DiagnosticCatalog.NUnit

🌍 **Langues :**  
🇬🇧 [English](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.NUnit/README.en.md) | 🇫🇷 Français (ce fichier)

Les règles **NUnit.Analyzers** sous forme de constantes fortement référencées, pour que
`SuppressMessageAttribute` prenne des références vérifiées à la compilation plutôt que des chaînes magiques.

<!-- mirror:begin -->
> ## 🪞 Reflète `NUnit.Analyzers 4.14.0`
>
> **99 règles, 3 catégories**, chaque identifiant et chaque
> catégorie lus dans les analyseurs de cette version. Régénéré le 2026-08-05.
<!-- mirror:end -->

> Non officiel. Sans affiliation avec le projet NUnit, ni approbation ni support de sa part.

## Pourquoi

Presque chaque projet de test NUnit exécute ces analyseurs sans que personne ne l'ait décidé :
`dotnet new nunit` écrit `NUnit.Analyzers` dans le fichier projet qu'il génère, à côté de `NUnit`
lui-même. Ce ne sont pas une dépendance transitive — `NUnit` n'en déclare aucune — ils arrivent
simplement avec le modèle et restent.

C'est ce qui fait de leurs règles celles que les gens suppriment **dans le code source**. Une règle
que vous avez activée se règle dans `.editorconfig` ; une règle venue avec le modèle reçoit une
exception à l'unique endroit où elle a tort, avec une `Justification` à côté du test qui la mérite.

```csharp
[SuppressMessage("Assertion", "NUnit2007:The actual value should not be a constant", ...)]
```

Trois chaînes, et rien n'en vérifie aucune. Trompez-vous d'identifiant et la suppression ne fait
silencieusement rien — l'avertissement reste, tout simplement. Trompez-vous de catégorie et **il ne se
passe rien du tout**, jamais : la plateforme .NET ne lit jamais cet argument, donc aucune erreur,
aucun avertissement et aucun test en échec ne vous le dira. Auriez-vous su que la catégorie de NUnit
est `"Assertion"` au singulier, là où celle de xUnit est `"Assertions"` au pluriel ?

```csharp
using DiagnosticCatalog.NUnit;

[SuppressMessage(
    NUnitRule.NUnit2007.Category,
    NUnitRule.NUnit2007.Id,
    Justification = "The constant is the subject of this test.")]
```

Le jour où une règle passe dans une autre catégorie, la seconde version la suit et la première
reste à nommer une catégorie que la règle ne porte plus — en silence, et aussi longtemps que la
ligne survit.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.NUnit" Version="1.0.0" />
```

Ce paquet ne fournit que les constantes. Les vérifications qui valident les déclarations de règles et
leurs sites d'utilisation sont livrées à part dans `DiagnosticCatalog.Analyzers`.

## Ce que contient le paquet

99 règles réparties sur 3 catégories, et **les 99 portent toutes à la fois le titre que déclare leur
descripteur et un lien d'aide** vers les pages de règles de NUnit — une complétude que seul le
catalogue xUnit égale ici.

| Catégorie | Règles | De quoi elles parlent |
| --- | --- | --- |
| `Assertion` | 59 | Les assertions, et les contraintes du modèle classique qui les remplacent — la plage `NUnit2xxx` |
| `Structure` | 38 | Comment tests, fixtures et leurs paramètres sont déclarés — `NUnit1xxx` |
| `Style` | 2 | Comment une assertion est écrite plutôt que si elle est juste — `NUnit3xxx` |

```csharp
[DiagnosticRule]
public static class NUnit2007
{
    public const string Id = nameof(NUnit2007);
    public const string Category = NUnitCategory.Assertion;
    public const string HelpLinkUri = "https://github.com/nunit/nunit.analyzers/tree/master/documentation/NUnit2007.md";
}
```

## La catégorie est `Assertion`, pas `Assertions`

Si vous exécutez aussi xUnit quelque part, voici le piège qui vaut d'être nommé. xUnit classe ses
règles d'assertion sous `"Assertions"` et NUnit classe les siennes sous `"Assertion"` — une lettre
d'écart, sur deux analyseurs qu'une solution peut fort bien exécuter côte à côte. Rien dans la
plateforme ne lit cet argument, donc se tromper ne coûte aucune erreur ni aucun avertissement, jamais.
L'atteindre au travers de `NUnitRule.NUnit2007.Category` supprime la question.

## Catégories déclarées une seule fois

`NUnitCategory` détient chaque catégorie une fois, et les règles la référencent — si bien que
l'orthographe d'une catégorie n'existe qu'à un seul endroit. Elle est **interne par conception** : une
suppression atteint une catégorie au travers de la règle qui la porte, `NUnitRule.NUnit2007.Category`,
et jamais au travers de la constante de catégorie seule. Les deux se replient sur la même chaîne
aujourd'hui et cessent de s'accorder le jour où NUnit déplace la règle
([ADR-0026](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.fr.md)).

## Comment il est produit

Pas transcrit depuis la documentation. Le générateur lit dans les métadonnées des assemblys d'analyse
les types qu'ils marquent de `[DiagnosticAnalyzer]`, construit ceux-là, et lit les instances de
`DiagnosticDescriptor` qu'ils déclarent réellement — la seule source qui ne puisse pas avoir dérivé.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package NUnit.Analyzers --package-version latest \
    --namespace DiagnosticCatalog.NUnit --container NUnitRule \
    --output src/DiagnosticCatalog.NUnit/NUnitRules.g.cs
```

## Comment il reste à jour

Un workflow nocturne régénère chaque catalogue depuis son paquet amont et ouvre une pull request
quand quelque chose a réellement bougé — règles ajoutées, recatégorisées, retirées en amont. Il ne
publie jamais : une catégorie ou un identifiant qui a changé en amont change un contrat publié, et
comme la plateforme ne lit jamais la catégorie d'une suppression, une valeur fausse fusionnée sans
relecture ne produirait de symptôme nulle part. Un humain lit le diff.

**Une règle retirée en amont n'est jamais supprimée.** Elle est conservée et marquée `[Obsolete]` en
nommant la version qui l'a abandonnée, pour qu'un projet qui la référence encore obtienne un
avertissement `CS0618` lui disant de retirer la suppression — plutôt qu'une erreur dure sur un membre
qui a disparu. Les consommateurs incorporent les valeurs de constantes à leur propre compilation, donc
en supprimer une casse leur recompilation.

## Comment il arrive sur nuget.org

Ce catalogue roule sur le [train de release](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md) `nunit` et se versionne
indépendamment de la fondation, pour pouvoir suivre les versions de NUnit.Analyzers sans entraîner quoi que ce soit d'autre.

La publication ne fait pas partie du nocturne. Un mainteneur pousse une étiquette `nunit-vX.Y.Z`, et
le workflow de release empaquette le paquet, y intègre un SBOM SPDX, et publie via le
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) de NuGet
avec une provenance de build signée — aucune clé d'API à longue durée de vie n'existe nulle part pour
fuiter. La moitié empaquetage de ce pipeline est répétée à chaque pull request, si bien qu'une release
ne l'exerce jamais pour la première fois sur une étiquette.

## Limites

`[SuppressMessage]` ne peut pas supprimer les avertissements du **compilateur** — `CS0219` et
consorts demandent `#pragma warning disable`, qui prend des identifiants nus et ne peut donc jamais
référencer une constante. Ce paquet ne couvre que les règles d'analyse `NUnitxxxx`.

## Voir aussi

Treize catalogues frères sont générés depuis ce dépôt de la même façon, chacun lu depuis les descripteurs
d'un seul analyseur :

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
en est la version normative.

## Licence

Apache-2.0. Les identifiants, catégories, titres et liens d'aide des règles sont lus depuis un
analyseur tiers, lui-même sous licence MIT.
