<p align="center">
  <img src="../icon.png" width="128" alt="">
</p>

# DiagnosticCatalog

🌍 **Langues :**  
🇬🇧 [English](../README.md) | 🇫🇷 Français (ce fichier)

<!-- dcat-doc:missing SonarRule.S1145 la référence que le lecteur est invité à casser exprès ; l'erreur de compilation est le propos -->

|  |  |
| :-- | :-- |
| **Build** | [![ci](https://github.com/Reefact/diagnostic-catalog/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Reefact/diagnostic-catalog/actions/workflows/ci.yml) |
| **Qualité** | [![Quality Gate](https://sonarcloud.io/api/project_badges/measure?project=reefact_diagnostic-catalog&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=reefact_diagnostic-catalog) [![Coverage](https://sonarcloud.io/api/project_badges/measure?project=reefact_diagnostic-catalog&metric=coverage)](https://sonarcloud.io/summary/new_code?id=reefact_diagnostic-catalog) |
| **Sécurité** | [![codeql](https://github.com/Reefact/diagnostic-catalog/actions/workflows/codeql.yml/badge.svg?branch=main)](https://github.com/Reefact/diagnostic-catalog/actions/workflows/codeql.yml) [![OpenSSF Scorecard](https://api.securityscorecards.dev/projects/github.com/Reefact/diagnostic-catalog/badge)](https://securityscorecards.dev/viewer/?uri=github.com/Reefact/diagnostic-catalog) |
| **Paquet** | [![NuGet](https://img.shields.io/nuget/vpre/DiagnosticCatalog?logo=nuget)](https://www.nuget.org/packages/DiagnosticCatalog) ![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-512BD4) |
| **Projet** | [![License](https://img.shields.io/github/license/Reefact/diagnostic-catalog)](../LICENSE) [![Conventional Commits](https://img.shields.io/badge/Conventional%20Commits-1.0.0-fe5196?logo=conventionalcommits&logoColor=white)](https://www.conventionalcommits.org) |

---

**Des suppressions d'analyseur écrites en constantes que le compilateur vérifie, avec une raison qui n'est pas facultative.**

## 🚨 Le problème

`[SuppressMessage("Major Code Smell", "S1144")]`, ce sont deux chaînes magiques et une raison
facultative, et la plateforme .NET n'en valide aucune des trois.

Trompez-vous d'**identifiant** et la suppression ne correspond à rien : l'avertissement revient, ou
non, selon que le code qui le levait existe encore. Trompez-vous de **catégorie** et il ne se passe
strictement rien, jamais — aucun compilateur, analyseur, test ni outil ne lit cet argument, aucun
n'est donc en mesure de vous le dire. Et vous ne la devineriez pas : `S1144` est un
`Major Code Smell`, pas un `Code Smell` ; `SA1000` vit dans `StyleCop.CSharp.SpacingRules`.

Omettez la **justification** et la décision est perdue pour de bon. L'avertissement est tu, il ne
reste donc rien à réexaminer, et la raison pour laquelle il était acceptable n'a vécu que dans la
tête de qui a écrit la ligne.

## 💡 L'approche

Déclarez chaque règle une fois, comme une classe statique de constantes de compilation, et
référencez ces constantes partout ailleurs. Une référence mal tapée arrête le build, là où une
chaîne mal tapée compile sans broncher en une suppression qui ne fait rien. Une règle que l'éditeur
retire est conservée et marquée `[Obsolete]` : une montée de version vous avertit au lieu de casser
la recompilation. Et la catégorie a exactement une source de vérité publiée, lue depuis le
`DiagnosticDescriptor` de l'analyseur lui-même plutôt que retapée de mémoire.

La raison cesse d'être facultative en même temps : `DCAT0014` exige qu'une `Justification` soit
**présente**. Ce qu'elle dit n'est jamais jugé — c'est une question humaine, et un outil qui noterait
de la prose se tromperait dans les deux sens.

## 🔁 Avant et après

<!-- dcat-doc:missing-justification la moitié « avant » est la forme incorrecte que cette page oppose ; la moitié « après » porte la raison -->

```csharp
// Avant — deux chaînes que rien ne valide, et une raison que rien ne demande.
[SuppressMessage("Major Code Smell", "S1144")]
private ReportSerializer() { }

// Après — deux constantes que le compilateur résout, et une raison que le build exige.
[SuppressMessage(
    SonarRule.S1144.Category,
    SonarRule.S1144.Id,
    Justification = "Invoked by the serializer through reflection.")]
private ReportSerializer() { }
```

Cassez la référence exprès — écrivez `SonarRule.S1145` — et le build s'arrête sur `CS0117`, là où la
chaîne qu'elle remplace aurait compilé en une suppression qui ne faisait silencieusement rien.

## 🏁 L'installer

Une référence, vers le catalogue correspondant à un analyseur que vous exécutez déjà :

```xml
<PackageReference Include="DiagnosticCatalog.Sonar" Version="1.0.0" />
```

[Démarrer](guide/getting-started.fr.md) en est la version en dix minutes, avec la référence cassée
exprès pour que vous voyiez la différence en deux compilations.

## ✅ Ce que cette unique référence vous donne

Référencer un catalogue active automatiquement les vérifications et les correctifs dans ce projet.

* **Des constantes** pour chaque règle que l'analyseur publie — identifiants, catégories, liens
  d'aide, et le titre de la règle au survol : la prose que vous colliez dans une suppression a
  désormais un domicile.
* **Des analyseurs** qui signalent les suppressions que vous n'avez pas converties, une paire qui
  nomme deux règles différentes, et une suppression que le *trimmer* jetterait.
* **Des correctifs** qui réécrivent une paire littérale en référence et ajoutent le `using`, une
  occurrence à la fois ou sur une solution entière avec *Corriger toutes les occurrences*.
* **Une justification sur chaque suppression**, exigée et non suggérée.
* **Aucun assemblage d'analyse à l'exécution.** Les analyseurs tournent dans le compilateur et nulle
  part ailleurs, et les constantes sont réduites à leurs valeurs avant l'écriture de votre
  assemblage.

Où cette vérification s'arrête, et comment un projet la demande ou la décline, c'est
[Configuration](guide/configuration.fr.md). Ce qu'un catalogue doit à ses propres consommateurs,
c'est [Empaqueter un catalogue](guide/packaging-a-catalogue.fr.md).

## 📦 Les catalogues disponibles

Vous n'avez presque certainement pas besoin d'en écrire un. Référencez le catalogue qui correspond à
un analyseur que vous exécutez déjà :

<!-- catalogue-index:begin -->

| Paquet | Catalogue les règles de | Identifiants |
| --- | --- | --- |
| **`DiagnosticCatalog.Sonar`** | [SonarAnalyzer.CSharp](https://github.com/SonarSource/sonar-dotnet) | `Sxxxx` |
| **`DiagnosticCatalog.NetAnalyzers`** | L'analyse de code .NET, les règles que le SDK livre | `CAxxxx` |
| **`DiagnosticCatalog.StyleCop`** | [StyleCop.Analyzers](https://github.com/DotNetAnalyzers/StyleCopAnalyzers) | `SAxxxx` |
| **`DiagnosticCatalog.CodeStyle`** | Le style IDE de Roslyn — ce que `.editorconfig` configure et qu'`EnforceCodeStyleInBuild` active | `IDExxxx` |
| **`DiagnosticCatalog.Xunit`** | [xunit.analyzers](https://github.com/xunit/xunit.analyzers), que tout projet de test xUnit exécute déjà puisque `xunit` en dépend | `xUnitxxxx` |
| **`DiagnosticCatalog.NUnit`** | [NUnit.Analyzers](https://github.com/nunit/nunit.analyzers), que `dotnet new nunit` inscrit dans le fichier projet qu'il génère | `NUnitxxxx` |
| **`DiagnosticCatalog.MSTest`** | [MSTest.Analyzers](https://github.com/microsoft/testfx), que tout projet MSTest exécute déjà puisque `MSTest.TestFramework` en dépend | `MSTESTxxxx` |
| **`DiagnosticCatalog.Trimming`** | Les avertissements de trimming, Native AOT et fichier unique, que Blazor WebAssembly, MAUI et `PublishAot` activent à chaque build | `ILxxxx` |
| **`DiagnosticCatalog.AspNetCore`** | ASP.NET Core et Blazor, que tout projet web exécute et qu'aucun ne peut désinstaller puisqu'elles vivent dans le framework partagé | `ASPxxxx`, `BLxxxx` |
| **`DiagnosticCatalog.Syslib`** | Les générateurs de source du runtime .NET — `LibraryImport`, les générateurs COM et regex, la sérialisation JSON | `SYSLIB1xxx` |
| **`DiagnosticCatalog.Roslyn`** | L'écriture d'analyseurs, qui arrive avec `Microsoft.CodeAnalysis.CSharp` pour quiconque écrit un analyseur ou un correctif | `RS1xxx`, `RS2xxx` |
| **`DiagnosticCatalog.PublicApi`** | [PublicApiAnalyzers](https://github.com/dotnet/roslyn-analyzers), pour une bibliothèque qui suit sa surface dans `PublicAPI.Shipped.txt` | `RS00xx` |
| **`DiagnosticCatalog.BannedApi`** | [BannedApiAnalyzers](https://github.com/dotnet/roslyn-analyzers), pour une base de code qui bannit une API dans `BannedSymbols.txt` | `RS0030`, `RS0031`, `RS0035` |

<!-- catalogue-index:end -->

Chacun d'eux est **généré** depuis les descripteurs de l'analyseur lui-même, jamais écrit à la main,
et la page de chaque paquet indique quelle version amont il reflète actuellement. Les règles `DCAT`
que cette bibliothèque signale sont cataloguées de la même façon, sous `DiagnosticCatalog.Self` :
faire taire l'une d'elles est donc aussi une référence vérifiée. Les règles que personne n'a
cataloguées ne sont pas hors d'atteinte pour autant : `DiagnosticCatalog` seul est ce que vous
référencez pour déclarer un catalogue pour vos propres analyseurs ou un référentiel interne —
[Publier un catalogue](guide/authoring-a-catalogue.fr.md) parcourt ce chemin de bout en bout.

Ces catalogues sont non officiels. Ils ne sont ni affiliés à, ni approuvés par, ni supportés par
SonarSource, Microsoft, le projet StyleCop.Analyzers, xUnit.net ou le projet NUnit. « Sonar » et
« SonarQube » sont des marques de SonarSource S.A.

## 🧭 Quand l'utiliser, et quand non

Cela en vaut la peine quand :

* vous avez des suppressions aujourd'hui, et comptez en avoir davantage ;
* plusieurs nomment la même règle, si bien qu'un renommage par l'éditeur touche plusieurs fichiers ;
* vous montez les versions de vos analyseurs et voulez qu'une montée vous dise ce qui a bougé ;
* vous voulez répondre à « où cette règle est-elle supprimée, et pourquoi ? » avec *Rechercher
  toutes les références* plutôt qu'avec une recherche textuelle.

Cela n'en vaut pas la peine quand :

* une poignée de suppressions tient dans un projet et personne n'en ajoute ;
* vous faites taire des règles uniquement par `#pragma warning disable` ou `.editorconfig` — ni l'un
  ni l'autre ne peut prendre une constante, et aucune version de ceci n'y changera rien ;
* vous voulez qu'un outil juge si une suppression était *raisonnable*. Cela reste une question
  humaine.

[Quand ne pas l'utiliser](guide/when-not-to-use.fr.md) est écrit pour vous en dissuader là où il le
faut, et [les alternatives](guide/alternatives.fr.md) couvrent ce qui existe par ailleurs.

## 📖 Documentation

* [**Démarrer**](guide/getting-started.fr.md) — dix minutes, une référence, une erreur volontaire.
* [**Adopter un catalogue**](guide/adopting-a-catalogue.fr.md) — migrer une base de code qui
  supprime déjà, et dans quel ordre convertir.
* [**Les diagnostics `DCAT`**](guide/diagnostics.fr.md) — chaque identifiant, ce qui le déclenche, et
  ce que sa sévérité signifie.
* [**Configuration**](guide/configuration.fr.md) — les clés de sévérité, et les trois leviers qui
  décident de ce qui tourne où.
* [**Publier un catalogue**](guide/authoring-a-catalogue.fr.md) et
  [**l'empaqueter**](guide/packaging-a-catalogue.fr.md) — pour vos propres analyseurs ou un
  référentiel interne.
* [**L'outil `dcat`**](guide/dcat.fr.md) — le générateur qui écrit tous les catalogues ci-dessus.
* [**Dépannage**](guide/troubleshooting.fr.md) — par symptôme : rien n'est signalé, `CS0117`,
  `CS0618`, `DCAT0006` sur tous les fichiers d'un coup.

La [carte de la documentation](guide/README.fr.md) choisit une page selon ce que vous cherchez à
faire, et chaque page y existe en anglais et en français. La
[spécification](specification.fr.md) en est la version normative, et les
[enregistrements de décision](adr/) portent le raisonnement derrière la conception.

## 🤝 Contribuer et sécurité

Vous avez trouvé un bug, ou vous voulez un catalogue qui n'est pas encore là ? Ouvrez une issue sur
le [gestionnaire d'issues](https://github.com/Reefact/diagnostic-catalog/issues) — il y a un
formulaire pour chaque cas. Les contributions sont bienvenues : commencez par
[CONTRIBUTING.md](../CONTRIBUTING.md) et par le
[Code de conduite](../CODE_OF_CONDUCT.md) que chacun accepte en prenant part ici.

Les releases sont publiées avec une provenance de build signée et un SBOM SPDX embarqué. Pour les
vulnérabilités de sécurité, suivez le processus privé décrit dans [SECURITY.md](../SECURITY.md), qui
porte aussi les détails de vérification.

## 📄 Licence

[Apache-2.0](../LICENSE)
