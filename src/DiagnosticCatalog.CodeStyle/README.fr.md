# DiagnosticCatalog.CodeStyle

🌍 **Langues :**  
🇬🇧 [English](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.CodeStyle/README.en.md) | 🇫🇷 Français (ce fichier)

Les **règles de style de code IDE de Roslyn** sous forme de constantes fortement référencées, pour que
`SuppressMessageAttribute` prenne des références vérifiées à la compilation plutôt que des chaînes magiques.

<!-- mirror:begin -->
> ## 🪞 Reflète `Microsoft.CodeAnalysis.CSharp.CodeStyle 5.6.0`
>
> **120 règles, 3 catégories**, chaque identifiant et chaque
> catégorie lus dans les analyseurs de cette version. Régénéré le 2026-08-05.
<!-- mirror:end -->

> Non officiel. Sans affiliation avec Microsoft, ni approbation ni support de sa part.

## Pourquoi

Les règles `IDExxxx` sont celles que la plupart des projets rencontrent en premier, parce qu'elles
arrivent avec le SDK .NET plutôt qu'avec un paquet : activez `EnforceCodeStyleInBuild`, donnez une
sévérité à une règle dans `.editorconfig`, et elle se met à faire échouer les compilations. Ce que
presque personne ne sait, c'est à quelle **catégorie** chacune appartient.

```csharp
[SuppressMessage("Style", "IDE0008:Use explicit type", Justification = "...")]
```

Trois chaînes, et rien n'en vérifie aucune. Trompez-vous d'identifiant et la suppression ne fait
silencieusement rien — l'avertissement reste, tout simplement. Trompez-vous de catégorie et **il ne
se passe rien du tout**, jamais : la plateforme .NET ne lit jamais cet argument, donc aucune erreur,
aucun avertissement et aucun test en échec ne vous le dira. Auriez-vous su qu'`IDE0008` est
`"Style"` mais qu'`IDE0076` est `"CodeQuality"`, et qu'`IDE0043` est `"Compiler"` ?

```csharp
using DiagnosticCatalog.CodeStyle;

[SuppressMessage(
    CodeStyleRule.IDE0008.Category,
    CodeStyleRule.IDE0008.Id,
    Justification = "The generated shape is clearer with var here.")]
```

Le jour où une règle passe dans une autre catégorie, la seconde version la suit et la première
continue de compiler pendant qu'elle cesse discrètement de correspondre.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.CodeStyle" Version="1.0.0" />
```

Ce paquet ne fournit que les constantes. Les vérifications qui valident les déclarations de règles et
leurs sites d'utilisation sont livrées à part dans `DiagnosticCatalog.Analyzers`.

## Ce que contient le paquet

120 règles réparties sur 3 catégories :

| Catégorie | Règles |
| --- | --- |
| `Style` | 116 |
| `CodeQuality` | `IDE0064`, `IDE0076`, `IDE0077` |
| `Compiler` | `IDE0043` |

119 des 120 portent le titre que déclare leur descripteur ; `RemoveUnnecessaryImportsFixable`
n'en déclare aucun, elle se documente donc plutôt par son identifiant et sa catégorie. 117 portent un
lien d'aide, 116 vers la référence des règles de style de Microsoft et un — `EnableGenerateDocumentationFile`
— vers le ticket Roslyn qui le suit :

```csharp
[DiagnosticRule]
public static class IDE0008
{
    public const string Id = nameof(IDE0008);
    public const string Category = CodeStyleCategory.Style;
    public const string HelpLinkUri =
        "https://learn.microsoft.com/dotnet/fundamentals/code-analysis/style-rules/ide0008";
}
```

**Trois identifiants ne sont pas de la forme `IDExxxx`**, et ils sont ici parce que les analyseurs
les déclarent : `IDE0005_gen` (la moitié « code généré » de *la directive using est inutile*),
`EnableGenerateDocumentationFile` (*mettre la propriété MSBuild `GenerateDocumentationFile` à
`true`*, ce qui est ce qui fait fonctionner `IDE0005` dans une compilation) et
`RemoveUnnecessaryImportsFixable`. Un catalogue rapporte ce que son amont déclare plutôt que ce qui
aurait l'air propre.

**`IDE0079` n'est pas ici, et son absence est délibérée.** *Retirer la suppression inutile*
est déclarée par un analyseur qui ne porte aucun attribut `[DiagnosticAnalyzer]` : l'IDE la pilote
au travers d'une interface distincte, et aucun compilateur ne la charge jamais — avec la règle réglée
sur `warning` et l'application du style de code activée, une compilation ne la signale pas du tout sur
une suppression inutile. Un catalogue existe pour rendre vérifiables les arguments d'une suppression,
et une règle qu'aucune compilation ne peut lever est une référence à laquelle ce paquet ne peut donner
aucun sens
([ADR-0031](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0031-find-analyzers-the-way-the-compiler-finds-them.fr.md)).

## Une note sur les versions

`Microsoft.CodeAnalysis.CSharp.CodeStyle` est versionné avec le **compilateur**, pas avec le
SDK. Une version qui déclare un compilateur plus récent que celui qui s'exécute est refusée d'emblée :

```
warning CS9057: Analyzer assembly '...' cannot be used because it references version
'5.6.0.0' of the compiler, which is newer than the currently running version '5.0.0.0'.
```

Cela contraint la version que *vous* pouvez installer, et c'est pourquoi ce catalogue roule sur un
train à lui plutôt que de partager la cadence de quelqu'un d'autre. Cela ne contraint pas ce qui est
lu ici : le générateur lit des descripteurs et n'exécute aucun analyseur, donc un catalogue peut
refléter une version que votre compilateur refuserait de charger.

**Vous n'avez très probablement pas besoin de ce paquet du tout.** Les mêmes analyseurs atteignent
presque chaque projet au travers du SDK .NET, où `EnforceCodeStyleInBuild` les active et
`.editorconfig` règle leur sévérité. Ce catalogue nomme les règles ; d'où elles viennent regarde
votre compilation.

## Catégories déclarées une seule fois

`CodeStyleCategory` détient chaque catégorie une fois, et les règles la référencent — si bien que
l'orthographe d'une catégorie n'existe qu'à un seul endroit. Elle est **interne par conception** : une
suppression atteint une catégorie au travers de la règle qui la porte,
`CodeStyleRule.IDE0008.Category`, et jamais au travers de la constante de catégorie seule. Les deux se
replient sur la même chaîne aujourd'hui et cessent de s'accorder le jour où Roslyn déplace la règle
([ADR-0026](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.fr.md)).

## Comment il est produit

Pas transcrit depuis la documentation. Le générateur lit dans les métadonnées des assemblys d'analyse
les types qu'ils marquent de `[DiagnosticAnalyzer]`, construit ceux-là, et lit les instances de
`DiagnosticDescriptor` qu'ils déclarent réellement — la seule source qui ne puisse pas avoir dérivé.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package Microsoft.CodeAnalysis.CSharp.CodeStyle --package-version latest \
    --namespace DiagnosticCatalog.CodeStyle --container CodeStyleRule \
    --output src/DiagnosticCatalog.CodeStyle/CodeStyleRules.g.cs
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

Ce catalogue roule sur le [train de release](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md) `codestyle` et se versionne
indépendamment de la fondation, pour pouvoir suivre les versions de Roslyn sans entraîner quoi que ce soit d'autre.

La publication ne fait pas partie du nocturne. Un mainteneur pousse une étiquette `codestyle-vX.Y.Z`, et
le workflow de release empaquette le paquet, y intègre un SBOM SPDX, et publie via le
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) de NuGet
avec une provenance de build signée — aucune clé d'API à longue durée de vie n'existe nulle part pour
fuiter. La moitié empaquetage de ce pipeline est répétée à chaque pull request, si bien qu'une release
ne l'exerce jamais pour la première fois sur une étiquette.

## Limites

`[SuppressMessage]` ne peut pas supprimer les avertissements du **compilateur** — `CS0219` et
consorts demandent `#pragma warning disable`, qui prend des identifiants nus et ne peut donc jamais
référencer une constante. La catégorie `Compiler` ci-dessus n'y fait pas exception : `IDE0043` est une
règle d'analyse que Roslyn classe sous cette catégorie, pas un diagnostic `CSxxxx`.

Beaucoup de règles `IDExxxx` se configurent dans `.editorconfig` plutôt que de se supprimer dans le
code source, et c'est en général le meilleur outil : une sévérité s'applique à un projet entier, là où
une suppression s'applique à un membre. Ce paquet est pour les cas où l'exception est locale et mérite
une `Justification` à côté du code.

## Voir aussi

Onze catalogues frères sont générés depuis ce dépôt de la même façon, chacun lu depuis les descripteurs
d'un seul analyseur :

- [`DiagnosticCatalog.Sonar`](https://www.nuget.org/packages/DiagnosticCatalog.Sonar)
  — les règles SonarAnalyzer.CSharp (`Sxxxx`).
- [`DiagnosticCatalog.NetAnalyzers`](https://www.nuget.org/packages/DiagnosticCatalog.NetAnalyzers)
  — les règles d'analyse de code .NET (`CAxxxx`).
- [`DiagnosticCatalog.StyleCop`](https://www.nuget.org/packages/DiagnosticCatalog.StyleCop)
  — les règles StyleCop.Analyzers (`SAxxxx`).
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
