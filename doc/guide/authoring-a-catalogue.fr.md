# Publier un catalogue

🌍 **Langues :**  
🇬🇧 [English](./authoring-a-catalogue.en.md) | 🇫🇷 Français (ce fichier)

Pour quiconque livre un analyseur, ou souhaite que les suppressions de son équipe soient vérifiées
contre des règles que personne d'autre ne publie. Tout ce qui suit est délibérément lisible sans
ouvrir le code de la fondation.

Une version qui tourne de tout ce qui suit vit dans
[`src/DiagnosticCatalog.Self`](../../src/DiagnosticCatalog.Self) — les règles de cette bibliothèque,
cataloguées par son propre générateur. C'est le produit appliqué à lui-même plutôt qu'une maquette,
et la CI échoue s'il cesse un jour de correspondre aux analyseurs qu'il reflète.

## Le contrat en entier

Une règle est une **classe statique**, marquée, exposant **deux constantes `string` publiques**, dont
l'une atteint une **catégorie déclarée** :

```csharp
using DiagnosticCatalog;

[DiagnosticCategory]
internal static class ContosoCategory
{
    public const string Usage = "Usage";
}

[DiagnosticRule]
public static class CTS0001
{
    public const string Id = nameof(CTS0001);
    public const string Category = ContosoCategory.Usage;
}
```

C'est tout. Pas de classe de base, pas d'interface, rien à enregistrer, aucun générateur à lancer.
Si vous cherchez la partie que vous auriez manquée, il n'y en a pas.

Quatre détails de cet extrait méritent leur place :

* **`static`** — rien n'instancie jamais une règle, et l'analyseur rejette une règle non statique.
* **`const`, pas `static readonly`** — un champ `static readonly` a une valeur à l'exécution mais ne
  peut pas être un argument d'attribut, ce qui est tout l'enjeu. C'est l'erreur que l'on commet en
  premier.
* **`nameof(CTS0001)`** plutôt que `"CTS0001"` — cela résout vers le nom du type conteneur, si bien
  que l'identifiant et la classe ne peuvent pas diverger. Renommez l'un dans l'IDE et l'autre suit.
* **`ContosoCategory.Usage`** plutôt que `"Usage"` — une seule classe porte chaque catégorie une fois,
  et `[DiagnosticCategory]` est ce qui rend cette classe visible à l'outillage. Exigé, pas conseillé :
  `DCAT0011` signale le littéral. La section suivante porte sur cette classe.

## Quand vous vous trompez, l'analyseur propose de corriger

`DCAT0002`, `DCAT0003`, `DCAT0004` et `DCAT0011` signalent une déclaration qui rate le contrat. Les
trois premiers portent un correctif — **proposé uniquement là où la réparation est déjà écrite dans le
code**, et muet sinon :

| Ce que vous avez écrit | Ce qui est proposé |
| --- | --- |
| `public sealed class CTS0001` | *Make 'CTS0001' static* — pour une classe ordinaire qui pourrait porter le mot-clé : pas de paramètres de type, pas de type de base, aucun membre d'instance, pas `partial` |
| `private static readonly string Id = ...` | *Make 'Id' a public constant* — les modificateurs seulement ; la valeur est laissée telle quelle |
| aucun membre `Id` | *Declare 'public const string Id'*, écrit `nameof(CTS0001)` — lu sur votre déclaration plutôt qu'inventé |

`DCAT0011` n'en porte aucun : la réparation est une classe qui n'existe peut-être pas encore, portant
une constante que personne n'a nommée. Rien n'est proposé non plus quand c'est la **valeur** qui est
fausse — un `const int`, une chaîne vide, un initialiseur non constant. Le code ne dit rien de ce que vous vouliez, et un correctif qui devinerait
produirait une règle que le compilateur accepte et que personne ne vérifie.

> **Celui auquel réfléchir avant d'appuyer.** *Declare 'public const string Category'* écrit `"TODO"`.
> C'est une vraie chaîne : `DCAT0004` cesse donc d'être signalé dès que vous l'appliquez — vous avez
> échangé un avertissement qui nommait le problème contre un marqueur que seul un lecteur remarquera,
> et une mauvaise catégorie est invisible dans tous les builds, pour toujours. Appliquez-le quand vous
> êtes sur le point de le remplir.

Détail complet dans [la référence des diagnostics](diagnostics.fr.md#diagnostics-de-déclaration).

## La forme à livrer réellement

Imbriquez les règles dans un conteneur, pour que le site d'utilisation se lise bien :

```csharp
namespace Contoso.Analyzers.Suppressions;

public static class ContosoRule
{
    [DiagnosticRule]
    public static class CTS0001
    {
        public const string Id = nameof(CTS0001);
        public const string Category = ContosoCategory.Usage;
    }
}
```

```csharp
[SuppressMessage(ContosoRule.CTS0001.Category, ContosoRule.CTS0001.Id, Justification = "...")]
```

**Nommez le conteneur pour le site d'utilisation, pas pour le fichier.** Chaque suppression paie ce
nom deux fois, et vos utilisateurs ne peuvent pas le raccourcir — ils peuvent l'aliaser, mais le nom
que vous choisissez est celui qui apparaît dans chaque revue de code. `ContosoRule` se lit mieux que
`ContosoAnalyzersDiagnosticRuleDefinitions`. Les catalogues de ce dépôt sont nommés de la même
façon, et au singulier : le site d'utilisation se lit `SonarRule.S1144` — une règle, nommée.

## Déclarer vos catégories une seule fois

Un vrai catalogue répète très peu de catégories sur un très grand nombre de règles. Le catalogue
Sonar de ce dépôt dépense 456 déclarations de règles sur **13** valeurs de catégorie distinctes.
Écrire le littéral dans chaque règle, c'est 456 occasions pour l'une d'elles de dériver.

```csharp
[DiagnosticCategory]
public static class ContosoCategory
{
    public const string Usage = "Usage";
    public const string Design = "Design";
}

public static class ContosoRule
{
    [DiagnosticRule]
    public static class CTS0001
    {
        public const string Id = nameof(CTS0001);
        public const string Category = ContosoCategory.Usage;   // ← pas un littéral
    }
}
```

**L'indirection est gratuite.** Une `const` initialisée depuis une autre `const` reste une constante
de compilation : `ContosoRule.CTS0001.Category` reste donc valide comme argument d'attribut et
finit toujours en littéral `"Usage"` dans l'assemblage compilé. Rien ne change en aval.

`[DiagnosticCategory]` est **exigé** — `DCAT0011` signale une règle qui atteint sa catégorie
autrement. Les constantes fonctionneraient sans, et c'est justement le point. Ce qu'il apporte, c'est que
l'outillage peut distinguer une constante de catégorie de n'importe quelle autre constante `string`
de votre assemblage, si bien que le correctif de `DCAT0006` peut proposer `ContosoCategory.Usage`
plutôt qu'un littéral nu. Non marquée, la classe est invisible et l'indirection n'achète rien. La
décision est
[ADR-0028](../adr/0028-require-every-rule-to-reach-its-category-through-a-declared-constant.fr.md).

## Les métadonnées optionnelles, et celle qui coûte cher

Une règle peut porter davantage :

```csharp
[DiagnosticRule]
public static class CTS0001
{
    public const string Id = nameof(CTS0001);
    public const string Category = ContosoCategory.Usage;

    public const string Title = "Factories should be named with the 'Factory' suffix";
    public const string MessageFormat = "Type '{0}' is registered as a factory but is not named '...Factory'";
    public const string Description = "Factories are discovered by name at start-up, so one that ...";
    public const string HelpLinkUri = "https://contoso.example/rules/CTS0001";
}
```

Rien ne les exige et rien ne les valide. Elles existent parce que ce sont exactement les arguments de
`DiagnosticDescriptor` — ce qui est la section suivante, et la meilleure raison de faire tout ceci.

> **Une mise en garde.** Vous serez tenté d'ajouter
> `public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;`. Une énumération *peut*
> être constante, mais `DiagnosticSeverity` vit dans `Microsoft.CodeAnalysis.Common` — la déclarer
> impose donc une dépendance Roslyn à **tous les consommateurs de votre catalogue**, y compris à des
> applications qui n'écrivent que des suppressions. Déclarez `Severity` dans votre projet
> d'analyseur, qui référence déjà Roslyn. Un paquet catalogue autonome reste sur de simples chaînes.

> **Une limite connue.** Le texte localisé — `LocalizableString`, descripteurs adossés à des resx —
> ne peut pas être une `const` et tombe donc hors de ce modèle. Le catalogue couvre l'axe identifiant
> et catégorie ; les fichiers de ressources restent le bon outil pour le texte traduit.

## Trois sujets qui ont leur propre page

Le contrat ci-dessus est tout ce qu'un catalogue doit satisfaire. Ce qui l'entoure — alimenter votre
analyseur depuis lui, le publier, le republier — est là où sont les décisions, et chacun en porte
assez pour se lire seul :

* [**Boucler la boucle avec votre propre analyseur**](first-party-analyzers.fr.md) — si vous possédez
  les deux, le `DiagnosticDescriptor` et la suppression peuvent lire les mêmes constantes, et la
  catégorie que vos utilisateurs écrivent devient exacte par construction. Également le seul membre
  qui imposerait une dépendance Roslyn à tous les consommateurs de votre catalogue.
* [**Versionner un catalogue**](versioning-a-catalogue.fr.md) — les constantes sont incorporées chez
  vos consommateurs à *leur* compilation : en supprimer une casse leur build avec un message qui ne
  nomme rien. Ne jamais supprimer une règle ; ne jamais renommer un membre ; ce que chaque changement
  fait à votre numéro de version.
* [**Empaqueter un catalogue**](packaging-a-catalogue.fr.md) — comment référencer la fondation,
  comment livrer sans aucune dépendance, ce qui se propage à vos consommateurs que vous l'ayez voulu
  ou non, et ce que nuget.org fait de votre README.

## Si vous reflétez l'analyseur de quelqu'un d'autre

Un catalogue qui reflète un tiers est un instantané, et rien dans l'assemblage compilé ne dirait
autrement quelle version il reflète. Enregistrez-la :

```csharp
[assembly: CatalogSource(
    source:        "Contoso.Analyzers",
    sourceVersion: "4.2.1",
    generatedOn:   "2026-07-31")]
```

La date est une **chaîne**, pas un `DateTime`, pour la même raison que tout le reste ici : les
arguments d'attribut doivent être des constantes de compilation et aucun type de date ne peut en
être une. Utilisez `yyyy-MM-dd`.

Un catalogue de première partie maintenu à côté de son propre analyseur n'a besoin de rien de tout
cela — les deux partent d'un dépôt à une version.

Si vous reflétez à l'échelle, ce dépôt génère treize catalogues de cette façon et publie le
générateur comme outil. Les catalogues Sonar, analyseurs .NET et StyleCop sous `src/` sont ce à quoi
la sortie ressemble avec 465, 318 et 193 règles ; la méthode est au §14 de
[la spécification](../specification.fr.md).

## Où regarder ensuite

* [`src/DiagnosticCatalog.Self`](../../src/DiagnosticCatalog.Self) — tout ce qui précède, généré,
  livré, et vérifié à chaque pull request.
* [`eng/catalogs.json`](../../eng/catalogs.json) — comment chaque catalogue de ce dépôt déclare d'où
  viennent ses règles.
* [La référence des diagnostics](diagnostics.fr.md) — ce qu'on dira à vos utilisateurs, et quand.

---

<div align="center">
<a href="./zero-footprint.fr.md">← La garantie d'empreinte nulle</a> · <a href="./README.fr.md">↑ Table des matières</a> · <a href="./first-party-analyzers.fr.md">Boucler la boucle avec votre propre analyseur →</a>
</div>
