# DiagnosticCatalog

🌍 **Langues :**  
🇬🇧 [English](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog/README.en.md) | 🇫🇷 Français (ce fichier)

Déclarez les règles de diagnostic d'un analyseur sous forme de constantes fortement
référencées, pour que `SuppressMessageAttribute` prenne des références vérifiées à la
compilation plutôt que des chaînes magiques.

Un seul paquet, les deux moitiés : les attributs avec lesquels un catalogue se déclare, et
les analyseurs `DCAT` et leurs correctifs qui vérifient ce que vous écrivez avec.

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
déclarer un catalogue à vous — et référencer l'un de ceux-là vous l'apporte déjà, avec les
vérifications qu'il porte.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog" Version="0.1.0" />
```

N'ajoutez **pas** `PrivateAssets="all"` si votre projet publie un catalogue destiné à
d'autres. Un seul paquet porte les deux moitiés, donc le cacher les cache toutes les deux :
vos consommateurs perdent `[DiagnosticRule]`, dont ils ont besoin pour déclarer leurs propres
règles et que la réflexion à l'exécution sur votre catalogue résout, et ils perdent les
vérifications avec — un consommateur écrit de la façon ordinaire cesse de compiler au lieu de
simplement passer inaperçu. Les deux moitiés de cela sont mesurées contre une restauration
réelle par `tools/packaging/verify-consumption.sh`, dans les vérifications
`PrivateAssets="all" is what stops a catalogue propagating it` et
`hiding the foundation also withholds the attribute assembly`.

Ne prenez aucune position et les analyseurs parviennent d'eux-mêmes à vos consommateurs — la
vérification `a catalogue that takes no position propagates the analyzer` — ce qui est la
façon dont chaque catalogue de ce dépôt est écrit. Le projet qui devrait décliner est une
**bibliothèque** qui a pris un catalogue pour ses propres suppressions : elle ne doit
l'attribut à personne, et `PrivateAssets="all"` sur sa référence est ce qui l'empêche de
livrer les diagnostics à des applications qui n'ont jamais choisi le catalogue.

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

## Les vérifications qui viennent avec

Les analyseurs `DCAT` et leurs correctifs sont livrés **à l'intérieur de ce paquet**, sous
`analyzers/dotnet/cs/`, à côté de `lib/`. Il n'y a rien d'autre à référencer : ils arrivent avec
la fondation, et la fondation arrive avec chaque catalogue bâti dessus.

Ils vérifient deux choses : qu'une **déclaration** de règle satisfait le contrat structurel — sa
forme, son `Id`, sa `Category`, la façon dont cette catégorie est atteinte et ce que dit le nom
de son type — et qu'une **suppression** qui en référence une est cohérente : deux arguments qui
ne nomment pas la `Category` d'une règle et l'`Id` de cette même règle, une suppression à moitié
migrée mêlant une référence et un littéral, un littéral qu'une référence de catalogue
remplacerait, et un `UnconditionalSuppressMessage` que le trimmer jette.

Un projet qui consomme un catalogue et ne déclare aucune règle à lui ne voit que le second
ensemble. Les diagnostics de déclaration ne signalent que les types marqués `[DiagnosticRule]` et
rendent la main immédiatement sur tout le reste.

Un assembly d'analyse ne devient jamais une dépendance d'exécution de l'application
consommatrice : `tools/packaging/verify-consumption.sh` restaure ce paquet comme le fait un
consommateur et vérifie que `DiagnosticCatalog.Analyzers.dll` et `DiagnosticCatalog.CodeFixes.dll`
restent hors du dossier de sortie tandis que `DiagnosticCatalog.dll` y parvient. Appliquer
`[DiagnosticRule]` n'ajoute pas non plus de comportement à l'exécution — le runtime résout les
types d'attributs paresseusement, donc `DiagnosticCatalog.dll` n'est jamais chargée à moins que
quelque chose ne fasse de la réflexion sur les types de règles.

Les analyseurs n'ont jamais besoin du *type* de l'attribut, seulement de son nom : ils
reconnaissent `DiagnosticCatalog.DiagnosticRuleAttribute` par son nom de métadonnées pleinement
qualifié. Un projet qui déclare son propre `internal sealed class DiagnosticRuleAttribute` dans
l'espace de noms `DiagnosticCatalog` est donc vérifié exactement comme celui qui a pris le paquet.
Ce que cela ne fait pas, c'est livrer les analyseurs — ceux-là arrivent avec ce paquet, et un
projet qui l'a caché n'a ni l'un ni l'autre.

## Migrer une base de code existante

Adopter un catalogue n'est pas un changement discret : les diagnostics de site d'utilisation sont
des erreurs par défaut (`DCAT0001`, `DCAT0006` et `DCAT0007`), donc une suppression littérale
qu'une référence de catalogue remplacerait casse la compilation au lieu d'avertir. Le correctif
qui la réécrit est la façon dont une base de code adopte un catalogue en pratique :

```csharp
[SuppressMessage("Major Code Smell", "S1144", Justification = "kept for reflection")]
// devient
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id, Justification = "kept for reflection")]
```

*Corriger toutes les occurrences* l'applique à un document, un projet ou une solution en une
étape, et le `using` dont la référence a besoin est ajouté pour vous. Tout le reste de l'attribut
est laissé exactement tel qu'écrit — `Justification`, `Scope`, `Target` et `MessageId` sont à vous.

Deux comportements à connaître avant de le lancer :

* **Le suffixe de nom convivial est retiré.** Visual Studio écrit
  `"S1144:Unused private members should be removed"` ; le correctif reconnaît cette forme et
  remplace le tout par la référence. La prose ne vivait dans la suppression que parce que rien
  d'autre ne la portait — la documentation propre à la règle s'en charge désormais.
* **Quand deux catalogues décrivent la même règle, aucun correctif n'est proposé.** Le diagnostic
  apparaît quand même, donc rien n'est caché, mais choisir entre les deux vous revient.

Une suppression laissée à moitié migrée — une référence, un littéral — est signalée elle aussi, et
complétée depuis la règle que l'argument déjà migré nomme :

```csharp
[SuppressMessage(SonarRule.S1144.Category, "S1144", Justification = "kept for reflection")]
// devient
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id, Justification = "kept for reflection")]
```

Seul le littéral est réécrit ; quelle que soit l'orthographe que vous avez choisie de l'autre
côté, un alias compris, elle est laissée intacte. Et si le littéral nomme quelque chose que la
règle référencée ne nomme pas — `"S9999"` à côté de `SonarRule.S1144.Category` — vous obtenez le
diagnostic et aucun correctif. Compléter celui-là ferait taire une autre règle que celle qui est
tue aujourd'hui, et c'est une décision qui vous revient, pas à une ampoule.

## Quand les deux arguments nomment des règles différentes

Ce cas-là reçoit **deux** correctifs et aucune recommandation :

```text
Use SonarRule.S1144.Id        — keep the category, correct the identifier
Use SonarRule.S2094.Category  — keep the identifier, correct the category
```

Vous seul savez laquelle des deux moitiés était la faute de frappe, donc aucune n'est proposée par
défaut. Bon à savoir pendant que vous choisissez : Roslyn apparie une suppression sur
l'**identifiant seul** et ne regarde jamais la catégorie, si bien que corriger la catégorie laisse
exactement en l'état ce qui est supprimé, tandis que corriger l'identifiant le change.

## Correctifs pour une règle écrite à la main

Un catalogue est normalement généré, et du code généré satisfait le contrat par construction. Quand
vous en écrivez un vous-même, des correctifs sont là pour la partie mécanique :

```csharp
[DiagnosticRule]
public sealed class JD0007                      // → Rendre 'JD0007' static
{
    private static readonly string Id = "JD0007";   // → Faire de 'Id' une constante publique
                                                    // → Déclarer 'public const string Category'
}
```

Chacun n'est proposé **que là où la réparation est déjà écrite dans le code**. `static` n'est pas
proposé pour un type générique, pour une `struct`, ni pour une classe portant un membre d'instance
— le mot-clé n'y compilerait pas, et retirer ce qui l'en empêche est un changement de votre
conception plutôt qu'une réparation de celle-ci. Une classe `partial` est refusée elle aussi : les
parties que le correctif ne voit pas sont celles qui décident.

Les réparations de membres corrigent des modificateurs et jamais la valeur. Un `const int Id`, une
chaîne vide, un initialiseur qui n'est pas constant — ceux-là sont signalés sans correctif, parce
que le code ne dit rien de ce que vous vouliez.

> **Celui auquel réfléchir avant d'appuyer.** *Déclarer 'public const string Category'* écrit
> `"TODO"`. C'est une vraie chaîne, donc `DCAT0004` cesse d'être signalé — vous avez échangé un
> avertissement contre un marqueur. Une catégorie que personne ne remplit est fausse pour toujours
> et invisible dans toutes les compilations, parce que Roslyn apparie une suppression sur son
> identifiant seul. `Id` est différent : il est écrit `nameof(JD0007)`, lu sur la déclaration
> plutôt qu'inventé.

## Ce que les analyseurs ne font pas

Ils ne valident pas une chaîne arbitraire. `[SuppressMessage("Usage", "S1144")]` avec la mauvaise
catégorie ne correspond à aucune règle connue, et rien n'est signalé — le mécanisme qui rend une
catégorie fausse impossible est la constante elle-même, que le compilateur vérifie. Ces analyseurs
vous amènent aux constantes et vous y maintiennent.

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

Chaque catalogue de règles bâti sur ce paquet est listé au même endroit, généré depuis les
descripteurs mêmes des analyseurs plutôt qu'écrit à la main. Si vous exécutez l'un de ces
analyseurs, ses règles n'ont pas besoin d'être déclarées :

**[Les catalogues disponibles](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/README.fr.md#-les-catalogues-disponibles)**

Ils valent aussi comme exemples travaillés du contrat ci-dessus : un conteneur de règles, les
catégories déclarées une seule fois, et la version amont que l'ensemble reflète, enregistrée dans
`[assembly: CatalogSource]`.

Pour le contrat expliqué depuis zéro plutôt que par l'exemple, voir
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

Pour les vérifications que ce paquet apporte avec lui :

- [**Les diagnostics `DCAT`**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/diagnostics.fr.md)
  — chaque identifiant que ces analyseurs signalent, ce qui le déclenche, pourquoi il existe, si un
  correctif est proposé, et la clé `.editorconfig` qui le configure.
- [**Configuration**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/configuration.fr.md)
  — les sévérités, le commutateur par catégorie, le code généré, et l'erreur de `PrivateAssets`
  qui fait tout taire.
- [**Adopter un catalogue sur une base de code existante**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/adopting-a-catalogue.fr.md)
  — la montée en sévérité et dans quel ordre convertir, quand la migration ci-dessus est vaste.
- [**Le contrat de règle**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/rule-contract.fr.md)
  — les cinq exigences contre lesquelles une déclaration est vérifiée, et chaque forme syntaxique
  qu'un site d'utilisation peut prendre.
- [**Dépannage**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/troubleshooting.fr.md)
  — par symptôme, à commencer par « rien n'est signalé du tout ».

La [**carte de la documentation**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/README.fr.md)
choisit une page selon ce que vous cherchez à faire ; chaque guide existe en anglais et en français.
La [**spécification**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/specification.fr.md)
en est la version normative, y compris le comportement de plateforme vérifié sur lequel la conception
s'appuie.

## Licence

Apache-2.0
