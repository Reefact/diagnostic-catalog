# DiagnosticCatalog

🌍 **Langues :**  
🇬🇧 [English](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog/README.en.md) | 🇫🇷 Français (ce fichier)

Déclarez les règles de diagnostic d'un analyseur sous forme de constantes fortement
référencées, pour que `SuppressMessageAttribute` prenne des références vérifiées à la
compilation plutôt que des chaînes magiques.

## Le problème

Les **deux** arguments de `SuppressMessageAttribute` sont des chaînes magiques, et rien
ne valide ni l'un ni l'autre :

```csharp
[SuppressMessage("Major Code Smell", "S1144", Justification = "...")]
```

Ils ne diffèrent que par leur façon d'échouer. Trompez-vous d'**identifiant** — une faute
de frappe, ou une règle que l'éditeur a renommée depuis — et la suppression ne fait
silencieusement rien : l'avertissement reste, sans que rien n'en désigne la cause.
Trompez-vous de **catégorie** et *il ne se passe rien du tout, jamais* : la plateforme .NET
ne lit jamais cet argument, donc aucun compilateur, analyseur, test ou outil ne peut vous
le dire. Et vous ne le devineriez pas — la catégorie de `S1144` est `"Major Code Smell"`,
ni `"Code Smell"`, ni `"Maintainability"`.

```csharp
// Casse la compilation, à la place, si la règle est un jour renommée ou retirée.
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id, Justification = "...")]
```

Sonar, les règles CA de .NET, StyleCop, les règles IDE de Roslyn et celles de xUnit sont déjà
empaquetées sous `DiagnosticCatalog.Sonar`, `DiagnosticCatalog.NetAnalyzers`, `DiagnosticCatalog.StyleCop`
`DiagnosticCatalog.CodeStyle` et `DiagnosticCatalog.Xunit`. Ce paquet-ci est ce qu'il vous faut pour
déclarer un catalogue à vous.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog" Version="0.1.0" />
```

N'ajoutez **pas** `PrivateAssets="all"` si votre projet publie un catalogue destiné à
d'autres : le paquet doit leur parvenir pour qu'ils puissent déclarer leurs propres règles,
et pour que la réflexion à l'exécution sur votre catalogue continue de fonctionner. Les
vérifications, elles, survivent à un attribut non résolu — les analyseurs reconnaissent le
nom de métadonnées pleinement qualifié `DiagnosticCatalog.DiagnosticRuleAttribute`, ce qui
est exactement le mode de défaillance silencieuse que ce choix visait à supprimer — mais ne
comptez pas dessus.

## Déclarer une règle

Une règle est une classe statique non générique marquée `[DiagnosticRule]`, exposant deux
constantes publiques obligatoires. La catégorie doit aboutir à une constante déclarée dans
une classe marquée `[DiagnosticCategory]` :

```csharp
using DiagnosticCatalog;

namespace JustDummies.Analyzers.Suppressions;

[DiagnosticCategory]
internal static class DummiesCategory
{
    public const string Usage = "Usage";
}

public static class Dummies
{
    [DiagnosticRule]
    public static class JD0007
    {
        public const string Id = nameof(JD0007);
        public const string Category = DummiesCategory.Usage;
    }
}
```

Les deux membres doivent être `const`. Une propriété, un champ `static readonly` ou un
`record` ne peuvent pas servir d'argument d'attribut, ce qui explique aussi que le contrat
soit structurel plutôt qu'une interface ou une classe de base.

La classe de catégories justifie sa place sur un catalogue de n'importe quelle taille : très
peu de catégories distinctes se répartissent sur un très grand nombre de règles, et déclarer
chacune une seule fois est ce qui garantit une orthographe unique par valeur. Le marqueur est
ce qui rend cette classe lisible par l'outillage, pour qu'un correctif puisse proposer la
constante nommée à la place d'un littéral. Une règle qui atteint sa catégorie autrement est
signalée par `DCAT0011`.

Gardez des noms de conteneur courts — chaque site d'utilisation les paie deux fois. Une seule
contrainte borne ce raccourcissement : **ne nommez jamais le conteneur d'après le premier
segment de son propre espace de noms.** Un consommateur qui écrit
`using JustDummies.Analyzers.Suppressions;` résout `JustDummies` vers l'espace de noms, pas
vers le conteneur importé, et chaque référence échoue avec `CS0234`. Le consommateur n'a
aucun moyen de contourner cela.

## Utiliser une règle

```csharp
using System.Diagnostics.CodeAnalysis;
using JustDummies.Analyzers.Suppressions;

[SuppressMessage(
    Dummies.JD0007.Category,
    Dummies.JD0007.Id,
    Justification = "This member is instantiated by the test infrastructure.")]
public sealed class DummyFactory
{
}
```

## Métadonnées facultatives

Une règle peut porter les arguments restants de `DiagnosticDescriptor`. Chacun d'eux est une
simple chaîne, donc cela n'ajoute aucune dépendance au-delà de ce paquet :

```csharp
[DiagnosticRule]
public static class JD0007
{
    public const string Id = nameof(JD0007);
    public const string Category = DummiesCategory.Usage;
    public const string Title = "Dummy factories should follow the expected convention";
    public const string MessageFormat = "Type '{0}' does not follow the convention";
    public const string Description = "Explains the condition detected by the analyzer.";
    public const string HelpLinkUri = "https://justdummies.io/analyzers/JD0007";
}
```

Si l'analyseur est le vôtre, il peut alors construire son descripteur à partir des constantes
mêmes que référencent ses suppressions — une seule source de vérité pour les deux :

```csharp
using Microsoft.CodeAnalysis;

private static readonly DiagnosticDescriptor Descriptor = new(
    JD0007.Id, JD0007.Title, JD0007.MessageFormat, JD0007.Category,
    DiagnosticSeverity.Warning, isEnabledByDefault: true,
    description: JD0007.Description, helpLinkUri: JD0007.HelpLinkUri);
```

`DiagnosticSeverity` peut être une constante, donc une règle *peut* aussi exposer
`public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;` — mais contrairement
aux constantes de chaîne ci-dessus, ce type vit dans `Microsoft.CodeAnalysis.Common`, si bien
qu'une règle qui le déclare impose une dépendance à Roslyn à tout consommateur du catalogue.
Ne l'ajoutez que dans un projet qui référence déjà Microsoft.CodeAnalysis, comme votre
analyseur lui-même. Un paquet de catalogue autonome devrait s'en tenir aux chaînes.

Le texte localisé (`LocalizableString`, descripteurs adossés à un resx) sort du modèle `const` ;
les fichiers de ressources restent le bon outil pour les chaînes traduites.

## Ce que ce paquet n'est pas

Ce paquet contient **les attributs uniquement** — `[DiagnosticRule]` et
`[assembly: CatalogSource]`. Il ne vérifie rien.

Les analyseurs qui valident les déclarations de règles, vérifient que `Category` et `Id`
proviennent de la même règle, et proposent de remplacer les littéraux de chaîne par des
références de catalogue sont livrés à part :

```xml
<PackageReference Include="DiagnosticCatalog.Analyzers" Version="..." PrivateAssets="all" />
```

Appliquer `[DiagnosticRule]` n'introduit aucun comportement à l'exécution. Le runtime résout
les types d'attributs paresseusement, donc `DiagnosticCatalog.dll` n'est jamais chargée à
moins que quelque chose ne fasse de la réflexion sur les types de règles.

Si vous ne voulez aucune dépendance de paquet du tout, les analyseurs reconnaissent l'attribut
par son nom de métadonnées pleinement qualifié. Déclarer votre propre
`internal sealed class DiagnosticRuleAttribute` dans l'espace de noms `DiagnosticCatalog`
fonctionne tout aussi bien.

## Consigner d'où vient un catalogue

Un catalogue qui reflète l'analyseur de quelqu'un d'autre est un instantané. `CatalogSource`
consigne quelle version amont il reflète et à quelle date, lisible depuis les métadonnées :

```csharp
[assembly: CatalogSource(
    source:        "SonarAnalyzer.CSharp",
    sourceVersion: "10.31.0.145097",
    generatedOn:   "2026-07-30")]
```

La date est une chaîne parce que les arguments d'attribut doivent être des constantes de
compilation et qu'aucun type date ne peut l'être ; le format est ISO 8601, `yyyy-MM-dd`. Un
catalogue de première main maintenu à côté de son propre analyseur n'en a pas besoin — les
deux sont livrés à une seule version.

## Voir aussi

Quatorze catalogues bâtis sur ce paquet sont déjà publiés, générés depuis les descripteurs mêmes des
analyseurs plutôt qu'écrits à la main. Si vous utilisez l'un d'eux, ses règles n'ont pas à être déclarées :

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
- [`DiagnosticCatalog.Self`](https://www.nuget.org/packages/DiagnosticCatalog.Self)
  — les règles `DCATxxxx` que signalent les analyseurs de catalogue, cataloguées de la même façon.

Ils valent aussi d'être lus comme des exemples travaillés du contrat ci-dessus : un conteneur de
règles, les catégories déclarées une seule fois, et la version amont que l'ensemble reflète consignée
dans `[assembly: CatalogSource]`.

Pour le contrat expliqué à partir de zéro plutôt que par l'exemple, voyez
[le guide de l'auteur de catalogue](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/authoring-a-catalogue.fr.md).

## Documentation

Pour déclarer un catalogue, dans l'ordre où le travail se fait :

- [**Publier un catalogue**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/authoring-a-catalogue.fr.md)
  — le contrat structurel, la forme à livrer réellement, la déclaration des catégories une seule
  fois, et la règle de versionnage qui vous mordra si vous la sautez.
- [**Boucler la boucle avec votre propre analyseur**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/first-party-analyzers.fr.md)
  — alimenter votre `DiagnosticDescriptor` depuis votre propre catalogue, et le membre qui
  imposerait Roslyn à tous vos consommateurs.
- [**Versionner un catalogue**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/versioning-a-catalogue.fr.md)
  — ne jamais supprimer une règle, ne jamais renommer un membre, et ce que chaque changement fait
  au numéro.
- [**Empaqueter un catalogue**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/packaging-a-catalogue.fr.md)
  — quoi référencer, ce qui se propage à vos consommateurs, et ce que nuget.org fait de votre
  README.

La [**carte de la documentation**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/README.fr.md)
choisit une page selon ce que vous cherchez à faire ; chaque guide existe en anglais et en français.
La [**spécification**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/specification.fr.md)
en est la version normative, y compris le comportement de plateforme vérifié sur lequel la conception
s'appuie.

## Licence

Apache-2.0
