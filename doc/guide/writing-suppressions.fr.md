# Écrire des suppressions que le compilateur vérifie

🌍 **Langues :**  
🇬🇧 [English](./writing-suppressions.en.md) | 🇫🇷 Français (ce fichier)

Pour quiconque écrit `[SuppressMessage(...)]`. Aucune connaissance du fonctionnement interne de
DiagnosticCatalog n'est nécessaire pour lire cette page.

Un mot sur le vocabulaire : une **suppression** désigne ici le fait de faire taire un avertissement,
jamais le fait d'effacer du code. C'est le sens que `SuppressMessageAttribute` porte, et il est
conservé tel quel dans toute la documentation.

## Le problème, en un exemple

Vous avez un avertissement que vous avez décidé d'accepter, alors vous le faites taire :

```csharp
[SuppressMessage("Major Code Smell", "S1144", Justification = "Called by the serializer.")]
private static Order Rebuild(byte[] payload) { /* ... */ }
```

Deux chaînes. Le compilateur n'en vérifie aucune, parce que de son point de vue ce n'est que du
texte. Si bien que tout ce qui suit compile, part en production, et ne fait strictement rien :

```csharp
[SuppressMessage("Major Code Smell", "S1145")]   // faute de frappe — un chiffre
[SuppressMessage("Major Code Smell", "S 1144")]  // espace parasite
[SuppressMessage("Major Code Smell", "S1144")]   // correct aujourd'hui ; la règle est retirée l'an prochain
```

Rien ne vous avertit. La suppression cesse simplement de correspondre, et l'avertissement qu'elle
masquait revient — ou, pire, ne revient jamais parce que le code a été effacé et que la suppression
morte reste là pour toujours. Ce n'est pas une erreur rare. C'est le résultat normal d'un
identifiant écrit à la main sans aucun retour.

## Ce que cette bibliothèque en fait

Elle remplace les deux chaînes par deux **constantes**, que le compilateur vérifie :

```csharp
[SuppressMessage(
    SonarRule.S1144.Category,
    SonarRule.S1144.Id,
    Justification = "Called by the serializer.")]
private static Order Rebuild(byte[] payload) { /* ... */ }
```

Désormais :

* une faute d'orthographe donne une **erreur de compilation**, pas un no-op silencieux ;
* renommer dans l'IDE entraîne tous les sites d'utilisation ;
* la question « où cette règle est-elle supprimée ? » trouve sa réponse dans *Rechercher toutes les
  références*, puisqu'il s'agit bel et bien d'une référence ;
* quand l'éditeur retire la règle, la constante est marquée obsolète et vous êtes prévenu à la
  compilation.

La sortie compilée est **octet pour octet identique** à la version avec des littéraux. Les
constantes sont repliées à la compilation : cela ne coûte rien à votre application — ni dépendance,
ni vérification au démarrage, ni un seul octet. Voir
[l'empreinte nulle](#ce-que-ça-laisse-dans-mon-application) plus bas.

## Démarrer

### 1. Référencer un catalogue

Un catalogue est un paquet de constantes pour les règles d'un analyseur. Prenez celui qui correspond
à l'analyseur dont vous faites taire les avertissements :

```xml
<PackageReference Include="DiagnosticCatalog.Sonar" Version="0.1.0" />
```

Il y en a un pour [SonarAnalyzer](https://www.nuget.org/packages/DiagnosticCatalog.Sonar), un pour
[les analyseurs .NET](https://www.nuget.org/packages/DiagnosticCatalog.NetAnalyzers) (`CAxxxx`) et
un pour [StyleCop](https://www.nuget.org/packages/DiagnosticCatalog.StyleCop) (`SAxxxx`) et un
pour [les règles IDE de Roslyn](https://www.nuget.org/packages/DiagnosticCatalog.CodeStyle) (`IDExxxx`)
et un pour [celles de xUnit](https://www.nuget.org/packages/DiagnosticCatalog.Xunit) (`xUnitxxxx`)
et un pour [celles de NUnit](https://www.nuget.org/packages/DiagnosticCatalog.NUnit) (`NUnitxxxx`)
et un pour [celles de MSTest](https://www.nuget.org/packages/DiagnosticCatalog.MSTest) (`MSTESTxxxx`)
et un pour [les avertissements de trimming et AOT](https://www.nuget.org/packages/DiagnosticCatalog.Trimming) (`ILxxxx`).

C'est la seule ligne dont vous avez besoin pour la garantie elle-même. Une règle mal orthographiée
est désormais une erreur de compilation, parce que `SonarRule.S1144.Id` est un membre que le
compilateur résout — aucun analyseur n'intervient là-dedans.

Les diagnostics `DCAT` ci-dessous sont un paquet séparé, `DiagnosticCatalog.Analyzers`, et ce sont
eux qui trouvent les suppressions que vous n'avez *pas* encore converties. **Il n'a aujourd'hui
aucune version sur nuget.org**, si bien qu'un catalogue ne l'amène pas avec lui — rien ne peut
référencer un paquet qui n'a jamais été publié
([ADR-0007](../adr/0007-depend-across-trains-through-published-packages.fr.md)). Il roule sur le train
`lib` : le prochain tag l'expédiera ;
[l'état du projet](https://github.com/Reefact/diagnostic-catalog#-project-status) est la réponse à
jour.

### 2. Écrire la suppression

```csharp
using System.Diagnostics.CodeAnalysis;
using DiagnosticCatalog.Sonar;

[SuppressMessage(
    SonarRule.S1144.Category,
    SonarRule.S1144.Id,
    Justification = "Called by the serializer through reflection.")]
private static Order Rebuild(byte[] payload) { /* ... */ }
```

Chaque catalogue nomme son conteneur d'après son éditeur : `SonarRule`, `NetAnalyzersRule`,
`StyleCopRule`, `DcatRule`. Au singulier, parce que le site d'utilisation se lit `SonarRule.S1144` —
une règle, nommée.

### 3. Migrer l'existant

Vous n'avez pas à le faire à la main. Compilez une fois, et chaque suppression littérale qui
correspond à une règle de votre catalogue est signalée par `DCAT0006`, avec un correctif attaché.
Acceptez-le une fois, ou utilisez **Corriger toutes les occurrences** pour convertir un document, un
projet ou la solution entière en une étape.

Il gère la forme que Visual Studio génère, suffixe compris :

```csharp
[SuppressMessage("Major Code Smell", "S1144:Unused private members should be removed")]
// devient
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S1144.Id)]
```

Le suffixe est abandonné. C'était de la prose reprenant le titre de la règle, que le catalogue porte
dans sa documentation XML — survolez la constante et vous le retrouvez.

## Les raccourcis, et celui qu'il faut éviter

Les noms de conteneur longs deviennent répétitifs. Un **alias** est la sortie recommandée :

```csharp
using Unused = DiagnosticCatalog.Sonar.SonarRule.S1144;

[SuppressMessage(Unused.Category, Unused.Id, Justification = "...")]
```

Vérifié exactement comme la forme longue : l'analyse travaille sur les symboles, jamais sur le texte
que vous avez tapé.

`using static` fonctionne aussi mais n'est **pas** recommandé :

```csharp
using static DiagnosticCatalog.Sonar.SonarRule.S1144;

[SuppressMessage(Category, Id, Justification = "...")]   // correct — mais une seule règle par fichier
```

Un second `using static` dans le même fichier rend `Category` et `Id` ambigus, et la seule
correction est de revenir en arrière. L'alias passe à l'échelle ; celui-ci non.

## Ce dont on va vous parler, et pourquoi

Quatre diagnostics peuvent apparaître sur une suppression. Référence complète dans
[le guide des diagnostics](diagnostics.fr.md) ; voici ce que chacun signifie en pratique.

**`DCAT0001` — les deux arguments viennent de règles différentes.**

```csharp
[SuppressMessage(SonarRule.S1144.Category, SonarRule.S2094.Id)]
```

Copier-coller, presque toujours. C'est signalé *même quand les deux règles partagent une catégorie*,
parce qu'alors la ligne fonctionne aujourd'hui et casse le jour où l'éditeur recatégorise l'une des
deux — le genre de défaut qui remonte des années plus tard sans le moindre indice attaché.

Deux correctifs sont proposés et aucun n'est marqué par défaut, parce que vous seul savez quelle
moitié était la faute de frappe. Bon à savoir pendant que vous choisissez : Roslyn apparie une
suppression sur **l'identifiant seul** et ne regarde jamais la catégorie. Corriger la catégorie ne
change donc rien à ce qui est supprimé, là où corriger l'identifiant le change.

**`DCAT0006` — ces littéraux correspondent à une règle que vous avez.** La migration ci-dessus.

**`DCAT0007` — vous n'en avez migré que la moitié.**

```csharp
[SuppressMessage(SonarRule.S1144.Category, "S1144")]
```

Complété depuis la règle que l'autre argument nomme déjà. Si le littéral nomme *autre chose* —
`"S9999"` à côté de `SonarRule.S1144.Category` — vous obtenez le diagnostic et **aucun** correctif,
parce que le compléter ferait taire une règle différente de celle qui est tue aujourd'hui. C'est
votre décision, pas celle d'une ampoule.

**`DCAT0009` — `UnconditionalSuppressMessage` avec une règle non `IL`.** Cet attribut est lu par le
*trimmer*, qui n'accepte que les identifiants `IL####` et jette tout le reste. La suppression que
vous avez écrite ne fait donc rien, et rien d'autre dans la chaîne d'outils ne vous l'aurait jamais
dit.

## Les transformer en erreurs de build

Les trois qui regardent un site d'usage sont des erreurs par défaut ; les autres des avertissements.
Tous se configurent comme n'importe quel diagnostic Roslyn :

```ini
# .editorconfig
[*.cs]
dotnet_diagnostic.DCAT0009.severity = error        # relever un livré en avertissement
dotnet_diagnostic.DCAT0006.severity = suggestion   # migration progressive
```

`DCAT0001` et `DCAT0007` sont déjà des erreurs, `DCAT0006` aussi : les trois signifient qu'une
suppression ne fait pas ce qu'elle a l'air de faire, et une garantie tenue seulement là où quelqu'un
y a pensé n'en est pas une
([ADR-0027](../adr/0027-ship-the-use-site-diagnostics-as-errors.fr.md)).

Cela a un coût qu'il vaut mieux connaître avant de référencer le paquet. Sur une base de code
existante, `DCAT0006` se déclenche sur **toutes** les suppressions littérales d'un coup, et étant une
erreur il casse le build ce jour-là — `TreatWarningsAsErrors` n'y est plus pour rien. Descendez-le à
`suggestion`, migrez à votre rythme, puis supprimez la ligne.

## Ce que ça laisse dans mon application

Rien. `SuppressMessageAttribute` est `[Conditional("CODE_ANALYSIS")]`, ce qui veut dire que le
compilateur ne l'écrit pas du tout dans votre assemblage à moins que vous ne le demandiez. Les
constantes sont repliées avant cela, si bien qu'il ne reste rien de toute la suppression : pas
d'attribut, pas de chaînes, pas de référence au catalogue.

Le paquet catalogue est une commodité de compilation. Ce n'est pas une dépendance d'exécution, et
c'est asserté par un test plutôt que promis — voir `tests/DiagnosticCatalog.ZeroFootprint.UnitTests`,
et [la garantie d'empreinte nulle](zero-footprint.fr.md) pour ce que ce test établit exactement, et
ce qu'il n'établit pas.

L'unique exception est délibérée : `UnconditionalSuppressMessage` ne porte pas de `[Conditional]`,
précisément pour que le *trimmer* puisse le lire dans l'assemblage compilé bien après que le
compilateur a fini. Il est préservé, avec les valeurs du catalogue repliées dedans comme de simples
chaînes.

## Deux choses que ceci ne peut pas aider

Dit franchement plutôt que laissé à votre découverte :

| Ce que vous écrivez | Pourquoi c'est hors de portée |
| --- | --- |
| `#pragma warning disable S1144` | Prend un identifiant nu, pas une expression. Aucune constante ne peut y être substituée, jamais. |
| `dotnet_diagnostic.S1144.severity` dans `.editorconfig` | Les clés de configuration sont du texte brut, entièrement hors du modèle de compilation C#. |

Et une frontière qui mérite d'être claire : ceci vérifie qu'une suppression est **structurellement
cohérente** — qu'elle nomme une vraie règle, de façon cohérente. Elle n'a aucune opinion sur le fait
que supprimer cette règle *à cet endroit* était une bonne idée. Ce jugement reste le vôtre, et c'est
à cela que sert `Justification`.

## Où regarder ensuite

* [`DiagnosticCatalog.Self`](../../src/DiagnosticCatalog.Self/README.md) — les règles `DCAT`
  elles-mêmes en tant que catalogue, pour quand vous supprimez l'un de *ces* diagnostics.
* [La référence des diagnostics](diagnostics.fr.md) — chaque identifiant `DCAT`, ce qui le
  déclenche, comment le configurer.
* [Publier un catalogue](authoring-a-catalogue.fr.md) — si votre équipe possède un analyseur.

---

<div align="center">
<a href="./alternatives.fr.md">← Les alternatives</a> · <a href="./README.fr.md">↑ Table des matières</a> · <a href="./adopting-a-catalogue.fr.md">Adopter un catalogue sur une base de code existante →</a>
</div>
