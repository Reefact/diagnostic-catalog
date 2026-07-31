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

Une règle est une **classe statique**, marquée, exposant **deux constantes `string` publiques** :

```csharp
using DiagnosticCatalog;

[DiagnosticRule]
public static class CTS0001
{
    public const string Id = nameof(CTS0001);
    public const string Category = "Usage";
}
```

C'est tout. Pas de classe de base, pas d'interface, rien à enregistrer, aucun générateur à lancer.
Si vous cherchez la partie que vous auriez manquée, il n'y en a pas.

Trois détails de cet extrait méritent leur place :

* **`static`** — rien n'instancie jamais une règle, et l'analyseur rejette une règle non statique.
* **`const`, pas `static readonly`** — un champ `static readonly` a une valeur à l'exécution mais ne
  peut pas être un argument d'attribut, ce qui est tout l'enjeu. C'est l'erreur que l'on commet en
  premier.
* **`nameof(CTS0001)`** plutôt que `"CTS0001"` — cela résout vers le nom du type conteneur, si bien
  que l'identifiant et la classe ne peuvent pas diverger. Renommez l'un dans l'IDE et l'autre suit.

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
        public const string Category = "Usage";
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

`[DiagnosticCategory]` est optionnel — les constantes fonctionnent sans. Ce qu'il apporte, c'est que
l'outillage peut distinguer une constante de catégorie de n'importe quelle autre constante `string`
de votre assemblage, si bien que le correctif de `DCAT0006` peut proposer `ContosoCategory.Usage`
plutôt qu'un littéral nu.

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

## Boucler la boucle : une seule source de vérité

Si vous possédez l'analyseur en plus du catalogue, alimentez le descripteur **depuis le catalogue** :

```csharp
private static readonly DiagnosticDescriptor Rule = new(
    id:                 ContosoRule.CTS0001.Id,
    title:              ContosoRule.CTS0001.Title,
    messageFormat:      ContosoRule.CTS0001.MessageFormat,
    category:           ContosoRule.CTS0001.Category,
    defaultSeverity:    DiagnosticSeverity.Warning,
    isEnabledByDefault: true,
    description:        ContosoRule.CTS0001.Description,
    helpLinkUri:        ContosoRule.CTS0001.HelpLinkUri);
```

Désormais l'analyseur qui *signale* la règle et chaque suppression qui la *fait taire* lisent les
mêmes constantes. La catégorie que vos utilisateurs écrivent est exacte par construction plutôt que
par diligence — et « par diligence » est précisément ce qui échoue, parce qu'une catégorie est une
chaîne que personne d'autre que vous ne publie et que rien ne vérifie.

C'est la raison la plus forte pour un projet de première partie d'adopter la convention, et c'est
quelque chose qu'un catalogue tiers ne pourra jamais offrir : le miroir de l'analyseur de quelqu'un
d'autre ne peut que copier ce que cet analyseur déclare aujourd'hui.

## Empaquetage

Référencez la fondation de la façon ordinaire — **pas** `PrivateAssets="all"` :

```xml
<PackageReference Include="DiagnosticCatalog" Version="0.1.0" />
```

| Qui vous êtes | Ce dont vous avez besoin | Comment référencer |
| --- | --- | --- |
| **Consommateur** — écrit des suppressions | les analyseurs | `DiagnosticCatalog.Analyzers`, `PrivateAssets="all"` |
| **Auteur de catalogue** — déclare des règles | `[DiagnosticRule]` résoluble *par vos propres consommateurs* | référence `DiagnosticCatalog` ordinaire |

Cacher la dépendance avec `PrivateAssets="all"` est l'erreur qui compte ici : vos consommateurs ne
peuvent alors plus résoudre `DiagnosticRuleAttribute`, `[DiagnosticRule]` se dégrade en type
d'erreur, et — c'est la mauvaise partie — les analyseurs ne trouvent **aucune règle** et ne
signalent **rien**. Tout a l'air propre. C'est exactement la défaillance que cette bibliothèque
existe pour éliminer, alors ne la reproduisez pas dans votre propre paquet.

### Ne pas prendre la dépendance du tout

Si vous préférez livrer un catalogue sans la moindre dépendance, déclarez l'attribut vous-même :

```csharp
namespace DiagnosticCatalog
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    internal sealed class DiagnosticRuleAttribute : System.Attribute { }
}
```

C'est supporté et testé, pas une astuce. Les analyseurs apparient le marqueur par son **nom
pleinement qualifié**, jamais par identité de symbole, si bien que votre copie est reconnue
exactement comme la vraie. C'est le même motif que PolySharp emploie pour `IsExternalInit`.

### Si vous référencez aussi les analyseurs

Un catalogue qui référence `DiagnosticCatalog.Analyzers` **les propage à ses propres
consommateurs** : référencer votre catalogue suffit alors à obtenir la vérification. Cela a été
mesuré contre une vraie restauration plutôt que lu dans la documentation de NuGet, qui dit le
contraire :

| Votre référence à `DiagnosticCatalog.Analyzers` | Les analyseurs tournent pour vos consommateurs |
| --- | --- |
| pas de `PrivateAssets` | **oui** |
| `PrivateAssets="none"` | oui |
| `PrivateAssets="all"` | non |

Si vous préférez ne pas imposer l'analyse à tout le monde en aval, dites-le explicitement avec
`PrivateAssets="all"`. **Le silence se propage.**

## Versionnement : la règle qui va vous mordre

Les constantes sont **incorporées dans vos consommateurs à *leur* compilation**. Un consommateur qui
a référencé `ContosoRule.CTS0001.Id` n'a pas enregistré un lien vers votre assemblage — il a copié
la chaîne `"CTS0001"` dans le sien.

La conséquence : **supprimer une `const` casse la recompilation** de tous ceux qui l'utilisaient, et
elle la casse avec un `CS0117` nu qui ne nomme rien d'utile. Alors quand une règle est retirée en
amont, reportez-la :

```csharp
[DiagnosticRule]
[Obsolete("Retired in Contoso.Analyzers 4.0. No replacement.")]
public static class CTS0001
{
    public const string Id = nameof(CTS0001);
    public const string Category = ContosoCategory.Usage;
}
```

Maintenant un consommateur qui la référence encore obtient `CS0618` — qui *nomme la règle et dit ce
qui s'est passé* — au lieu d'une erreur de compilation qui l'envoie chercher un espace de noms
manquant.

Il en va de même pour le renommage : une constante de catégorie dont le nom change casse tous les
consommateurs qui la référençaient. Choisissez des noms avec lesquels vous pouvez vivre, et voyez
[ADR-0012](../adr/0012-a-catalogue-never-renames-a-member-it-published.md) pour la façon dont ce
dépôt s'y tient lui-même.

Au-delà, du SemVer ordinaire : une nouvelle règle est un **mineur**, une règle retirée mais
conservée est un **mineur**, et retirer ou renommer quoi que ce soit de publié est un **majeur**.

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

Si vous reflétez à l'échelle, ce dépôt génère trois catalogues de cette façon et publie le
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
<a href="./writing-suppressions.fr.md">← Écrire des suppressions que le compilateur vérifie</a> · <a href="./README.fr.md">↑ Table des matières</a> · <a href="./diagnostics.fr.md">Les diagnostics DCAT →</a>
</div>
