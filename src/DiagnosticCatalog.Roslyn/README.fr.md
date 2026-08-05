# DiagnosticCatalog.Roslyn

🌍 **Langues :**  
🇬🇧 [English](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.Roslyn/README.en.md) | 🇫🇷 Français (ce fichier)

Les règles **d'écriture d'analyseurs Roslyn** (`RS1xxx`, `RS2xxx`) sous forme de constantes fortement
référencées, pour que `SuppressMessageAttribute` prenne des références vérifiées à la compilation
plutôt que des chaînes magiques.

<!-- mirror:begin -->
> ## 🪞 Reflète `Microsoft.CodeAnalysis.Analyzers 5.6.0`
>
> **52 règles, 9 catégories**, chaque identifiant et chaque
> catégorie lus dans les analyseurs de cette version. Régénéré le 2026-08-05.
<!-- mirror:end -->

> Non officiel. Sans affiliation avec Microsoft, ni approbation ni support de sa part.

## Pourquoi

Neuf catégories, et **deux d'entre elles rompent le motif que suivent les sept autres**.

Sept se lisent `MicrosoftCodeAnalysis` suivi d'un mot : `MicrosoftCodeAnalysisCorrectness`,
`MicrosoftCodeAnalysisDesign`, `MicrosoftCodeAnalysisReleaseTracking`. Personne ne les tape de
mémoire, mais elles sont au moins devinables une fois qu'on en a vu une.

Et puis il y a ceci :

| Règle | Catégorie |
| --- | --- |
| `RS1001` *Missing diagnostic analyzer attribute* | `MicrosoftCodeAnalysisCorrectness` |
| `RS1010` *Create code actions should have a unique EquivalenceKey* | **`Correctness`** |
| `RS1011` *Use code actions that have a unique EquivalenceKey* | **`Correctness`** |
| `RS1016` *Code fix providers should provide FixAll support* | **`Correctness`** |
| `RS1023` *Upgrade MSBuildWorkspace* | **`Library`** |

Le même concept, la correction, orthographié de deux façons dans **un seul paquet** — vingt règles
sous la forme longue et trois sous la courte, sans rien pour dire laquelle est laquelle. `Library` est
une catégorie contenant exactement une règle.

```csharp
[SuppressMessage("MicrosoftCodeAnalysisCorrectness", "RS1016:...", ...)]   // faux, et silencieux
```

Trompez-vous d'identifiant et la suppression ne fait silencieusement rien — l'avertissement reste,
tout simplement. Trompez-vous de catégorie et **il ne se passe rien du tout**, jamais : la plateforme
.NET ne lit jamais cet argument, donc aucune erreur, aucun avertissement et aucun test en échec ne
vous le dira.

```csharp
using DiagnosticCatalog.Roslyn;

[SuppressMessage(
    RoslynRule.RS1016.Category,
    RoslynRule.RS1016.Id,
    Justification = "The fixer is deliberately single-document; FixAll would be wrong here.")]
```

## Qui les exécute sans l'avoir demandé

`Microsoft.CodeAnalysis.Analyzers` atteint un projet **transitivement**, au travers de
`Microsoft.CodeAnalysis.CSharp`. Référencez les API de Roslyn pour écrire un analyseur, un correctif,
un générateur de source ou un test d'analyseur, et ces cinquante-deux règles viennent avec — la même
forme que les analyseurs de xUnit et de MSTest arrivant avec leurs frameworks de test.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.Roslyn" Version="1.0.0" />
```

Ce paquet ne fournit que les constantes. Les vérifications qui valident les déclarations de règles et
leurs sites d'utilisation sont livrées à part dans `DiagnosticCatalog.Analyzers` — qui, malgré la
similitude du nom, est tout autre chose : il détient les diagnostics `DCAT` de cette bibliothèque,
pas les `RS` de Roslyn.

## Ce que contient le paquet

52 règles réparties sur 9 catégories. Treize portent un lien d'aide ; les autres n'en déclarent aucun.

| Catégorie | Règles | De quoi elles parlent |
| --- | --- | --- |
| `MicrosoftCodeAnalysisCorrectness` | 20 | Enregistrement des actions, attributs d'analyseur, construction des descripteurs |
| `MicrosoftCodeAnalysisDesign` | 10 | La forme attendue d'un analyseur ou d'un correctif |
| `MicrosoftCodeAnalysisReleaseTracking` | 9 | `AnalyzerReleases.Shipped.md` et son jumeau non livré — la plage `RS2xxx` |
| `MicrosoftCodeAnalysisPerformance` | 4 | Le travail qu'un analyseur ne devrait pas faire par compilation |
| `Correctness` | 3 | `EquivalenceKey` et le support de FixAll — l'exception en forme courte |
| `MicrosoftCodeAnalysisCompatibility` | 2 | Des interfaces que seul Roslyn peut implémenter |
| `MicrosoftCodeAnalysisDocumentation` | 2 | La documentation d'un analyseur |
| `MicrosoftCodeAnalysisLocalization` | 1 | Des arguments de descripteur localisables |
| `Library` | 1 | `RS1023`, toute seule |

```csharp
[DiagnosticRule]
public static class RS1016
{
    public const string Id = nameof(RS1016);
    public const string Category = RoslynCategory.Correctness;
}
```

## Pas les règles `RS00xx`

Trois paquets Microsoft émettent des règles `RS`, et ce catalogue en détient un :

| Paquet | Identifiants | Ici ? |
| --- | --- | --- |
| `Microsoft.CodeAnalysis.Analyzers` | `RS1xxx`, `RS2xxx` | **oui**, les 52 |
| `Microsoft.CodeAnalysis.PublicApiAnalyzers` | `RS0016`–`RS002x` | non |
| `Microsoft.CodeAnalysis.BannedApiAnalyzers` | `RS0030`, `RS0031`, `RS0035` | non |

Les identifiants se partitionnent proprement, il n'y a donc aucune ambiguïté sur l'endroit où vit
quelle règle. La raison de l'absence des deux autres est l'icône : le badge d'un catalogue porte son
préfixe de règles
([ADR-0032](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0032-badge-a-catalogues-icon-with-its-rule-prefix.fr.md))
plafonné à trois lettres
([ADR-0033](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0033-cap-the-badge-at-three-letters.fr.md)),
si bien que trois catalogues de règles `RS` voudraient les deux mêmes lettres et qu'aucune icône ne
pourrait les distinguer. Les fusionner n'a en revanche aucune forme dans le manifeste, qui prend un
paquet par catalogue. C'est une décision que quelqu'un doit prendre avant que ces 26 règles puissent
être cataloguées ; ce paquet ne prétend pas l'avoir prise.

## Catégories déclarées une seule fois

`RoslynCategory` détient chaque catégorie une fois, et les règles la référencent — si bien que
l'orthographe d'une catégorie n'existe qu'à un seul endroit, ce qui pour
`MicrosoftCodeAnalysisReleaseTracking` vaut plus que d'ordinaire. Elle est **interne par conception** :
une suppression atteint une catégorie au travers de la règle qui la porte,
`RoslynRule.RS1016.Category`, et jamais au travers de la constante de catégorie seule. Les deux se
replient sur la même chaîne aujourd'hui et cessent de s'accorder le jour où une règle bouge
([ADR-0026](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.fr.md)).

## Limites

`[SuppressMessage]` ne peut pas supprimer les avertissements du **compilateur** — `CS0219` et
consorts demandent `#pragma warning disable`, qui prend des identifiants nus et ne peut donc jamais
référencer une constante.

Cette limite mord plus fort ici qu'ailleurs, et cela vaut d'être dit franchement. Plusieurs règles
`RS` sont signalées contre un assembly entier ou un fichier projet plutôt que contre un nœud de
syntaxe — `RS1036` *Specify EnforceExtendedAnalyzerRules*, `RS1038` *Compiler extensions should
target netstandard2.0*, `RS2008` *Enable analyzer release tracking* — et la réponse habituelle à
celles-là est `#pragma` ou une entrée `.editorconfig`, dont aucune ne peut prendre de constante. Ce
dépôt en fait taire trois de cette façon dans ses propres tests. Là où `[SuppressMessage]`
s'applique, les constantes d'ici fonctionnent ; là où il ne s'applique pas, aucun catalogue ne peut
aider.

## Comment il est produit

Pas transcrit depuis la documentation. Le générateur lit dans les métadonnées des assemblys d'analyse
les types qu'ils marquent de `[DiagnosticAnalyzer]`, construit ceux-là, et lit les instances de
`DiagnosticDescriptor` qu'ils déclarent réellement — la seule source qui ne puisse pas avoir dérivé,
et c'est elle qui a fait apparaître les deux catégories hors motif ci-dessus.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package Microsoft.CodeAnalysis.Analyzers --package-version latest \
    --namespace DiagnosticCatalog.Roslyn --container RoslynRule \
    --output src/DiagnosticCatalog.Roslyn/RoslynRules.g.cs
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
qui a disparu. Les consommateurs incorporent les valeurs de constantes à leur propre compilation,
donc en supprimer une casse leur recompilation.

## Comment il arrive sur nuget.org

Ce catalogue roule sur le [train de release](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md) `roslyn`
et se versionne indépendamment de la fondation, pour pouvoir suivre les versions de
Microsoft.CodeAnalysis.Analyzers sans entraîner quoi que ce soit d'autre.

La publication ne fait pas partie du nocturne. Un mainteneur pousse une étiquette `roslyn-vX.Y.Z`, et
le workflow de release empaquette le paquet, y intègre un SBOM SPDX, et publie via le
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) de NuGet
avec une provenance de build signée — aucune clé d'API à longue durée de vie n'existe nulle part pour
fuiter.

## Voir aussi

Treize catalogues frères sont générés depuis ce dépôt de la même façon, chacun lu depuis les
descripteurs d'un seul analyseur :

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
catalogue est bâti, et son README en est le guide. Si vous êtes ici parce que vous écrivez des
analyseurs, ce guide vous vise.

## Documentation

Pour utiliser un catalogue, dans l'ordre où le travail se fait :

- [**Démarrage**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/getting-started.fr.md)
  — dix minutes : référencer ce paquet, réécrire une suppression, la casser exprès et regarder le
  compilateur l'attraper.
- [**Écrire des suppressions que le compilateur vérifie**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/writing-suppressions.fr.md)
  — la version complète, migration des littéraux que vous avez déjà comprise.
- [**Publier un catalogue**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/authoring-a-catalogue.fr.md)
  — le contrat structurel, et comment en livrer un pour les règles de votre propre analyseur.
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
analyseur Microsoft, lui-même sous licence MIT.
