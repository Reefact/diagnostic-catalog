# DiagnosticCatalog — Spécification de la bibliothèque fondation

> **Versions linguistiques** — Ce document est une traduction. La version
> canonique est [`specification.en.md`](specification.en.md). En cas de
> divergence entre les deux, la version anglaise prévaut.

---

## 1. Statut du document

Cette spécification définit la première version de la bibliothèque fondation
provisoirement nommée `DiagnosticCatalog`. Le nom définitif du produit pourra
être modifié sans remettre en cause l'architecture décrite ici.

**Révision 4** ajoute la provenance des catalogues (§7.6) et fait passer les
catalogues générés d'évolution prévue à réalité spécifiée et implémentée (§14.1,
§14.2), avec `DiagnosticCatalog.Sonar` comme implémentation de référence. Elle
corrige aussi le §2.1, qui ne présentait que `checkId` comme chaîne magique : les
deux arguments le sont, et ils diffèrent par le mode de défaillance, pas par
nature.

**Révision 3** recentrait la proposition de valeur sur `checkId`
(§2), consigne le comportement vérifié de la chaîne de suppression .NET (§3) —
y compris le fait que `SuppressMessageAttribute` n'est jamais émis en
métadonnées par défaut (§3.4), ce qui invalidait l'un des tests de la révision 2
elle-même —
corrige le statut d'`UnconditionalSuppressMessageAttribute` (§9), fixe le chemin
d'implémentation de l'analyzer (§10), ajoute quatre diagnostics (§11) et scinde
le packaging NuGet en deux (§16). Toute affirmation portant sur le comportement
de la plateforme .NET est sourcée en
[annexe A](#annexe-a--comportements-vérifiés-de-la-plateforme) ; les questions de
conception non tranchées sont listées en
[annexe B](#annexe-b--décisions-ouvertes).

---

## 2. Vision

`DiagnosticCatalog` fournit une convention et un outillage .NET permettant de
représenter les diagnostics d'analyzers sous forme de références fortement
structurées et découvrables.

### 2.1 Le problème

Les **deux** arguments de `SuppressMessageAttribute` sont des chaînes magiques :

```csharp
[SuppressMessage("Major Code Smell", "S1144", Justification = "...")]
```

Rien ne valide ni l'un ni l'autre. Ils ne diffèrent que par *la manière* dont ils
échouent, et chaque mode de défaillance est mauvais à sa façon :

| Argument | Quand il est faux | Ce qui vous le signale |
| --- | --- | --- |
| `checkId` | La suppression ne fait rien. Le diagnostic continue d'être signalé. | Le symptôme seulement — un avertissement que vous croyiez traité, sans rien qui pointe la cause. Dans une base de code portant un arriéré d'avertissements, personne ne le remarque. |
| `category` | Rien ne se produit. La suppression fonctionne exactement comme prévu. | **Rien, jamais.** La plateforme ne lit jamais cette valeur (§3.2) : aucun compilateur, aucun analyzer, aucun test, aucun outil ne peut signaler l'erreur. |

Un `checkId` faux laisse une **suppression morte** : elle a l'apparence d'une
décision d'ingénierie délibérée et justifiée alors qu'elle n'offre aucune
protection. Cela se produit dès que l'identifiant comporte une faute de frappe
(`S1441` pour `S1144`), qu'un analyzer amont renomme ou retire une règle, qu'une
suppression est copiée-collée d'une règle vers une autre, ou qu'une suppression
survit au code pour lequel elle avait été écrite.

Une `category` fausse est le défaut le plus discret : elle ne casse jamais rien,
elle n'est donc jamais découverte. Et elle n'est pas devinable — la catégorie de
`S1144` est `"Major Code Smell"`, ni `"Code Smell"` ni `"Maintainability"`
(§14). Rien dans la chaîne d'outils ne vous corrigera jamais.

### 2.2 La correction

Remplacer les chaînes littérales :

```csharp
[SuppressMessage(
    "Major Code Smell",
    "S1144",
    Justification = "Instantiated through reflection by the DI container.")]
```

par des références que le compilateur résout et qu'IntelliSense découvre :

```csharp
[SuppressMessage(
    SonarRule.S1144.Category,
    SonarRule.S1144.Id,
    Justification = "Instantiated through reflection by the DI container.")]
```

Une règle renommée ou supprimée casse désormais la compilation au lieu de
désactiver silencieusement une suppression.

### 2.3 Ce que chaque argument gagne

Les deux arguments gagnent au catalogue, mais pas la même chose, et cette
différence commande les priorités de la spécification :

| Argument | Ce que le catalogue apporte | Pourquoi |
| --- | --- | --- |
| `checkId` | **La correction.** Un identifiant périmé ou mal orthographié devient une erreur de compilation au lieu d'un no-op silencieux. | C'est la proposition de valeur de la bibliothèque. |
| `category` | **Une source de vérité.** La valeur autoritative est publiée une fois : personne ne devine, personne ne dérive. | Rien d'autre ne pourra jamais vous dire que la valeur est fausse (§2.1, §3.2). |

### 2.4 Cohérence structurelle

La fondation vérifie en outre que la catégorie et l'identifiant utilisés dans un
attribut de suppression appartiennent bien à la *même* règle. Comme la
plateforme ignore totalement la catégorie (§3.2), une paire incohérente est
**indétectable par tout autre moyen** — ce qui est précisément la raison pour
laquelle un analyzer est le seul endroit où ce contrôle peut vivre. C'est une
garantie d'hygiène, pas une correction fonctionnelle.

### 2.5 Périmètre

`DiagnosticCatalog` ne contient aucun catalogue spécifique à Sonar, Microsoft,
StyleCop, JustDummies ou FirstClassErrors. Il définit uniquement le modèle
commun, les conventions et les contrôles.

---

## 3. Fondements techniques

### 3.1 Les arguments d'attribut doivent être des constantes

Les arguments d'un attribut C# doivent être déterminables à la compilation. Les
identifiants et catégories exposés par les catalogues doivent donc être des
`const string`. Une propriété, un `record`, une instance statique ou un champ
`static readonly` ne peut pas remplacer ces constantes dans un attribut.

Le modèle public ne peut donc **pas** être fondé sur une classe abstraite ou une
interface imposant des propriétés `Id` et `Category`. Le contrat doit être :

* structurel ;
* matérialisé par des constantes ;
* identifié par un attribut marqueur ;
* validé par un analyzer Roslyn.

### 3.2 La plateforme ignore la catégorie

`SuppressMessageAttribute` n'expose qu'**un seul** constructeur,
`(string category, string checkId)`. Les deux paramètres sont requis,
positionnels et non-nullables — la catégorie ne peut pas être omise.

Elle n'est cependant **jamais utilisée pour le matching**. Le
`SuppressMessageAttributeState` de Roslyn le dit explicitement :

> *Ignore the category parameter because it does not identify the diagnostic and
> category information can be obtained from diagnostics themselves.*

Trois conséquences structurent cette spécification :

1. Une catégorie erronée est fonctionnellement inoffensive — et n'est donc
   jamais détectée par quoi que ce soit. C'est la cible idéale d'un analyzer
   (§2.4).
2. Le rôle du catalogue sur l'axe catégorie est de **publier la valeur
   autoritative**, pas de faire fonctionner la suppression.
3. Tout catalogue généré doit dériver ses catégories du `DiagnosticDescriptor`
   réel de l'analyzer ciblé, jamais d'une supposition : une valeur inexacte ne
   sera jamais signalée par rien (§25.4).

### 3.3 Le `checkId` porte un nom lisible facultatif

Roslyn tronque `checkId` au premier deux-points :

```csharp
var separatorIndex = info.Id.IndexOf(':');
if (separatorIndex != -1)
{
    info.Id = info.Id.Remove(separatorIndex);
}
```

Ainsi `"S1144:Unused private members should be removed"` cible bien le
diagnostic `S1144`. **C'est la forme que Visual Studio génère** via son code
fix intégré *Supprimer → dans la source* ; elle domine donc les bases de code
existantes — exactement le code que cette bibliothèque doit migrer. La détection
des littéraux doit la normaliser (§11.6).

### 3.4 L'attribut est absent des métadonnées compilées

`SuppressMessageAttribute` est déclaré `[Conditional("CODE_ANALYSIS")]` dans la
BCL. À moins que le symbole de préprocesseur `CODE_ANALYSIS` ne soit défini, le
compilateur **ne l'émet pas du tout dans l'assembly** — la réflexion sur un
membre supprimé ne retourne rien :

```csharp
[AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
[Conditional("CODE_ANALYSIS")]
public sealed class SuppressMessageAttribute : Attribute
```

Trois conséquences :

1. Roslyn lit les suppressions dans le **modèle sémantique de la compilation en
   cours**, jamais dans les métadonnées. Rien au §10 ni au §13 ne dépend de
   l'émission, le chemin d'analyse est donc inchangé.
2. Vérifier le repliement des constantes par réflexion (§21.5) exige que le
   projet de test définisse `CODE_ANALYSIS`. Sans lui, l'assertion lit
   silencieusement `null`.
3. C'est la raison d'être d'`UnconditionalSuppressMessageAttribute` (§9.1), et
   cela scinde la question de l'empreinte **par attribut**, et non par
   bibliothèque :

   * `SuppressMessageAttribute` — rien ne survit. L'attribut est omis et les
     valeurs référencées sont repliées en constantes ; l'assembly livrée ne
     porte donc aucune trace de la suppression.
   * `UnconditionalSuppressMessageAttribute` — l'inverse, par conception. Il ne
     porte aucun `[Conditional]` précisément pour *être* préservé, et il est
     émis avec la catégorie et l'identifiant du catalogue repliés en chaînes
     littérales.

   Vérifié sur un membre portant les deux attributs, compilé sans
   `CODE_ANALYSIS` : la réflexion retourne `null` pour le premier et un attribut
   renseigné indiquant `CheckId='IL2026'`, `Category='Trimming'` pour le second.

`DiagnosticRuleAttribute` ne doit donc **jamais** être rendu `[Conditional]`. La
découverte des règles au franchissement des frontières d'assembly (§13) lit ce
marqueur dans les métadonnées référencées ; un marqueur conditionnel rendrait
tout catalogue distribué sous forme de package invisible à l'analyzer.

### 3.5 Le développeur ne saisit jamais ces valeurs

Le parcours réel n'est pas « écrire l'attribut à la main » :

1. Roslyn signale `JD0007`. L'IDE propose *Supprimer `JD0007` → dans la source*.
   Le fixer intégré insère les littéraux, **avec la catégorie exacte issue du
   `DiagnosticDescriptor` de la règle** et le suffixe `:Title`.
2. `DCAT0006` propose alors *Utiliser une référence de catalogue*, ce qui réécrit
   ces littéraux en références de catalogue.

Aucune des deux valeurs n'est jamais saisie à la main, et l'étape 1 fournit des
valeurs de départ autoritatives. C'est la raison pour laquelle **`DCAT0006` et
son code fix constituent la porte d'entrée principale du produit**, et non un
supplément facultatif (§24).

Cela cadre aussi honnêtement l'argument de découvrabilité de la catégorie : dans
Visual Studio, le fixer intégré insère déjà la bonne valeur. L'apport du
catalogue sur cet axe concerne les autres éditeurs, les workflows
`dotnet build`, et les fichiers `GlobalSuppressions.cs` écrits à la main.

### 3.6 Compatibilité future

[dotnet/runtime#68153](https://github.com/dotnet/runtime/issues/68153) propose un
constructeur sans catégorie pour les deux attributs de suppression, avec le même
raisonnement qu'au §3.2. La demande est **encore ouverte, sans décision**. Rien
dans cette conception n'a besoin de l'anticiper : si elle aboutit, le modèle de
catalogue survit sans changement et seul `DCAT0001` devient sans objet.

---

## 4. Objectifs

La bibliothèque doit permettre :

1. de définir une règle de diagnostic selon une convention commune ;
2. d'utiliser cette règle dans `SuppressMessageAttribute` ;
3. d'utiliser les règles trim/AOT dans `UnconditionalSuppressMessageAttribute`
   (§9) ;
4. de garantir que `Category` et `Id` proviennent de la même règle ;
5. de détecter les définitions de règles invalides ;
6. de remplacer les chaînes littérales par des références de catalogue ;
7. de détecter les suppressions partiellement migrées (une référence, un
   littéral) ;
8. de fournir des code fixers lorsque la correction est déterminable ;
9. d'être utilisée par des catalogues publics ou internes ;
10. de fonctionner avec des règles écrites manuellement ou générées ;
11. d'alimenter un `DiagnosticDescriptor` depuis les mêmes constantes (§15.2) ;
12. de ne produire aucun comportement runtime dans l'application consommatrice.

---

## 5. Non-objectifs

La première version ne doit pas :

* remplacer `SuppressMessageAttribute` ;
* créer un nouvel attribut propriétaire de suppression ;
* déterminer si une suppression est fonctionnellement légitime ;
* vérifier la qualité sémantique d'une justification ;
* télécharger automatiquement les catalogues de fournisseurs tiers ;
* contenir directement les règles Sonar, Microsoft ou StyleCop ;
* imposer une classe de base aux règles ;
* fournir un moteur de règles runtime ;
* modifier la sévérité des analyzers ciblés ;
* désactiver automatiquement un diagnostic ;
* générer automatiquement une justification.

### 5.1 Hors d'atteinte par construction

Deux mécanismes de suppression ne peuvent pas bénéficier de cette bibliothèque,
et la documentation doit le dire clairement plutôt que de laisser le lecteur le
découvrir :

| Mécanisme | Pourquoi c'est hors d'atteinte |
| --- | --- |
| `#pragma warning disable JD0007` | Attend des identifiants nus, pas des expressions. Aucune constante ne peut y être substituée. |
| `dotnet_diagnostic.JD0007.severity` dans `.editorconfig` | Les clés de configuration sont du texte brut, hors du modèle de compilation C#. |

La fondation vérifie la cohérence structurelle d'une suppression, jamais sa
pertinence métier ou technique.

---

## 6. Organisation de la solution

```text
DiagnosticCatalog/
├── src/
│   ├── DiagnosticCatalog/                 → lib, expose les attributs
│   ├── DiagnosticCatalog.Analyzers/        → assemblies d'analyse
│   ├── DiagnosticCatalog.CodeFixes/        → assemblies de code fixers
│   ├── DiagnosticCatalog.Sonar/            → catalogue généré (§14)
│   ├── DiagnosticCatalog.NetAnalyzers/     → catalogue généré (§14)
│   └── DiagnosticCatalog.StyleCop/         → catalogue généré (§14)
├── eng/
│   └── CatalogGen/                         → générateur, jamais livré (§14.1)
├── tests/
│   ├── DiagnosticCatalog.Analyzers.Tests/
│   ├── DiagnosticCatalog.CodeFixes.Tests/
│   ├── DiagnosticCatalog.CompilationTests/ → compilation réelle + réflexion
│   └── DiagnosticCatalog.Packaging.Tests/  → restore réel des packages produits
├── samples/
│   ├── ManualCatalog/
│   └── CatalogConsumer/
└── doc/
```

Deux packages NuGet sont produits, et non un seul — voir §16 pour la
justification. Il n'y a pas de projet `.Package` distinct : chaque package est
produit par le projet qui possède son contenu.

### 6.1 Suivi des versions d'analyzers

Les deux projets d'analyzers doivent livrer `AnalyzerReleases.Shipped.md` et
`AnalyzerReleases.Unshipped.md`. Sans ces fichiers, le SDK d'analyzers Roslyn
signale `RS2008` pour chaque diagnostic déclaré.

---

## 7. Modèle public d'une règle

### 7.1 Attribut marqueur

```csharp
namespace DiagnosticCatalog;

/// <summary>
/// Identifies a static type that represents a diagnostic rule.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class,
    AllowMultiple = false,
    Inherited = false)]
public sealed class DiagnosticRuleAttribute : Attribute
{
}
```

Cet attribut indique qu'une classe représente une règle de diagnostic. Il ne
supprime aucun diagnostic et ne modifie aucun comportement du compilateur.

L'attribut ne porte volontairement **aucun argument**. Placer l'identifiant et
la catégorie sur l'attribut dupliquerait les constantes sans supprimer le besoin
de les avoir, puisqu'on ne peut pas référencer les arguments d'un attribut
depuis un autre attribut.

Il ne doit pas non plus être rendu `[Conditional]`. La découverte des règles lit
le marqueur dans les métadonnées des assemblies référencées (§13) ; un marqueur
conditionnel rendrait tout catalogue distribué sous forme de package invisible à
l'analyzer (§3.4).

### 7.2 Reconnaissance par nom de métadonnée

L'analyzer reconnaît l'attribut par son **nom de métadonnée pleinement
qualifié**, `DiagnosticCatalog.DiagnosticRuleAttribute`, indépendamment de
l'assembly qui le déclare.

C'est une décision porteuse, pas un détail d'implémentation. Elle signifie qu'un
catalogue peut soit référencer `DiagnosticCatalog`, soit déclarer
son propre `internal sealed class DiagnosticRuleAttribute` dans ce namespace — le
pattern `IsExternalInit` / PolySharp — et rester totalement sans dépendance.

Elle élimine aussi un mode de défaillance silencieux. Si la reconnaissance
reposait sur l'identité de symbole, un catalogue dont les consommateurs ne
peuvent pas résoudre `DiagnosticCatalog.dll` verrait
`[DiagnosticRule]` dégradé en type d'erreur ; l'analyzer ne trouverait plus
aucune règle et **tous les contrôles se tairaient sans rien signaler**.

En complément, l'analyzer peut accepter la forme purement structurelle — une
classe statique imbriquée exposant `const string Id` et `const string Category` —
l'attribut restant le signal d'adhésion explicite et recommandé.

### 7.3 Définition minimale

Une règle valide est une classe statique marquée `[DiagnosticRule]` exposant deux
constantes publiques :

```csharp
[DiagnosticRule]
public static class JD0007
{
    public const string Id = nameof(JD0007);
    public const string Category = "Usage";
}
```

La forme canonique complète imbrique les règles dans une classe conteneur :

```csharp
namespace JustDummies.Analyzers.Suppressions;

public static class JustDummiesRules
{
    [DiagnosticRule]
    public static class JD0007
    {
        public const string Id = nameof(JD0007);
        public const string Category = "Usage";
    }
}
```

`nameof(JD0007)` à l'intérieur de `JD0007` résout vers le nom du type conteneur
et constitue une expression constante valide. L'utiliser rend `DCAT0005`
structurellement inviolable.

### 7.4 Nommage du conteneur

Chaque site d'utilisation paie deux fois le nom du conteneur :

```csharp
[SuppressMessage(JustDummiesRules.JD0007.Category, JustDummiesRules.JD0007.Id)]
[SuppressMessage(Dummies.JD0007.Category, Dummies.JD0007.Id)]
```

La forme catalogue est intrinsèquement plus verbeuse que le littéral qu'elle
remplace. Garder les noms de conteneurs **courts** — `Dummies.JD0007.Id` plutôt
que `JustDummiesRules.JD0007.Id`. Le point est d'autant plus important pour les
gros catalogues générés.

Nommer le conteneur d'après ce qu'il contient : **`{Éditeur}Rule`** pour les
règles et **`{Éditeur}Category`** pour les catégories (§7.7). `SonarRule.S1144.Id`
se lit alors « règle Sonar S1144, son id », et le singulier porte mieux au site
d'utilisation qu'un pluriel.

**Une contrainte borne ce raccourcissement : ne jamais nommer le conteneur
d'après le premier segment de son propre namespace.** Un conteneur
`JustDummies` déclaré dans `namespace JustDummies.Analyzers.Suppressions` est
inutilisable. Un consommateur écrivant `using JustDummies.Analyzers.Suppressions;`
résout le nom simple `JustDummies` vers le namespace — membre du namespace global,
trouvé avant tout type importé par une directive `using` — et chaque référence
échoue avec `CS0234`. Seul l'auteur du catalogue peut corriger, en renommant ; le
consommateur n'a aucun contournement.

### 7.5 Métadonnées facultatives

Une règle peut exposer des métadonnées supplémentaires :

```csharp
[DiagnosticRule]
public static class JD0007
{
    public const string Id = nameof(JD0007);
    public const string Category = "Usage";

    public const string Title =
        "Dummy factories should follow the expected convention";

    public const string MessageFormat =
        "Type '{0}' does not follow the expected dummy factory convention";

    public const string Description =
        "Explains the condition detected by the analyzer.";

    public const string HelpLinkUri =
        "https://justdummies.io/analyzers/JD0007";

    public const DiagnosticSeverity Severity = DiagnosticSeverity.Warning;
}
```

Ces membres ne sont pas nécessaires à l'utilisation dans un attribut de
suppression, et la première version ne les valide pas. Ils ne sont pour autant
pas décoratifs : ce sont les arguments de `DiagnosticDescriptor` (§15.2).

**Réserve sur les dépendances.** Tous les membres ci-dessus sont de simples
chaînes, sauf `Severity`. `DiagnosticSeverity` est une énumération et peut donc
être constante, mais elle vit dans `Microsoft.CodeAnalysis.Common` — une règle
qui l'expose impose une dépendance Roslyn à tous les consommateurs du catalogue.
Ne déclarer `Severity` que dans un projet référençant déjà
Microsoft.CodeAnalysis, typiquement l'analyzer lui-même (§15.2). Un package de
catalogue autonome doit s'en tenir aux chaînes.

**Limite connue.** Le texte localisé (`LocalizableString`, descriptors adossés à
des resx) sort du modèle `const`. Le catalogue couvre l'axe identifiant/catégorie ;
les fichiers de ressources restent le bon outil pour le texte traduit.

Pour les catalogues tiers, le titre amont est porté par un commentaire de
documentation XML plutôt que par une constante `Title`, et aucune constante
`Description` n'est émise — voir §14.1 et l'ADR-0014.

### 7.6 Provenance d'un catalogue

Un catalogue qui reflète l'analyzer de quelqu'un d'autre est un instantané, et
rien dans l'assembly compilée ne dirait autrement quelle version il reflète ni à
quel point il est périmé. La fondation expose donc un second attribut, au niveau
assembly :

```csharp
namespace DiagnosticCatalog;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class CatalogSourceAttribute : Attribute
{
    public CatalogSourceAttribute(string source, string sourceVersion, string generatedOn);

    public string Source { get; }
    public string SourceVersion { get; }
    public string GeneratedOn { get; }
}
```

Appliqué par le catalogue généré :

```csharp
[assembly: CatalogSource(
    source:        "SonarAnalyzer.CSharp",
    sourceVersion: "10.31.0.145097",
    generatedOn:   "2026-07-30")]
```

La date est une **chaîne**, pas un `DateTime` : les arguments d'attribut doivent
être des constantes de compilation et aucun type de date ne peut l'être (§3.1).
La valeur est une date calendaire ISO 8601, `yyyy-MM-dd`, la convention même pour
laquelle `AssemblyMetadataAttribute` est employé.

Comme `DiagnosticRuleAttribute`, cet attribut ne doit jamais être rendu
`[Conditional]` (§3.4) : il est lu depuis les métadonnées, ce qui est tout son
objet. Un analyzer ultérieur pourra s'en servir pour signaler un catalogue dont
l'instantané a dépassé un seuil d'ancienneté configuré, ou dont le
`SourceVersion` ne correspond plus au package d'analyzer réellement référencé par
le projet. Aucun de ces contrôles n'est dans le MVP.

L'attribut vise les catalogues *générés*. Un catalogue propriétaire maintenu à la
main à côté de son propre analyzer n'a besoin d'aucune trace de provenance : les
deux sont livrés depuis un même dépôt à une même version (§15).

### 7.7 Catégories déclarées une seule fois

Un catalogue répète très peu de catégories distinctes sur un très grand nombre de
règles : le catalogue Sonar dépense 456 déclarations de règles pour 13 valeurs
distinctes, StyleCop 193 pour 8. Répéter le littéral dans chaque règle, c'est 456
endroits où une valeur peut dériver. La fondation expose donc un troisième
attribut, qui marque la classe déclarant chaque catégorie une fois :

```csharp
namespace DiagnosticCatalog;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DiagnosticCategoryAttribute : Attribute
{
}
```

```csharp
[DiagnosticCategory]
public static class SonarCategory
{
    public const string MajorCodeSmell = "Major Code Smell";
    public const string MinorCodeSmell = "Minor Code Smell";
}

public static class SonarRule
{
    [DiagnosticRule]
    public static class S1144
    {
        public const string Id = nameof(S1144);
        public const string Category = SonarCategory.MajorCodeSmell;
    }
}
```

**L'indirection est gratuite.** Une `const` initialisée depuis une autre `const`
reste une constante de compilation : `SonarRule.S1144.Category` demeure valide
comme argument d'attribut et se replie toujours vers le littéral
`"Major Code Smell"` dans les métadonnées (annexe A10). Rien ne change au §10 non
plus : l'argument résout toujours vers le champ `Category` déclaré sur le type de
règle, donc `DCAT0001` compare les deux mêmes symboles qu'avant. L'initialiseur ne
participe pas à la résolution.

**Ce que le marqueur apporte.** Rien au §8 n'exige que `Category` soit initialisée
depuis une constante nommée plutôt que depuis un littéral : les catégories
fonctionneraient sans lui. Le marqueur existe parce que, sans lui, un analyzer ne
peut pas distinguer une constante de catégorie de n'importe quelle autre constante
chaîne de l'assembly. Avec lui, le code fixer de `DCAT0006` peut proposer
`SonarCategory.MajorCodeSmell` au lieu d'un littéral nu, et un contrôle ultérieur
peut valider que la classe ne contient que des membres `public const string` non
vides. L'appliquer est facultatif ; un catalogue qui répète ses littéraux reste
valide.

Comme les deux autres attributs, il ne doit jamais être rendu `[Conditional]`
(§3.4).

**Nommage des constantes.** Un catalogue généré dérive chaque nom de constante
mécaniquement de la valeur de catégorie : retirer les caractères non identifiants,
capitaliser chaque mot. `"Major Code Smell"` devient `MajorCodeSmell` ;
`"StyleCop.CSharp.SpacingRules"` devient `StyleCopCSharpSpacingRules`. Le résultat
bégaie parfois, et retirer un préfixe commun se lirait mieux. C'est délibérément
écarté : le préfixe commun change dès que l'amont ajoute une catégorie hors de ce
préfixe, ce qui renommerait toutes les constantes existantes et casserait tous les
consommateurs qui en référençaient une (§23.1). Ici la stabilité passe avant
l'élégance.

---

## 8. Contrat structurel d'une règle

### 8.1 Le type représentant la règle

Le type doit être :

* une classe ;
* statique ;
* non générique ;
* accessible depuis le code consommateur lorsque le catalogue est public.

Invalide — non statique :

```csharp
[DiagnosticRule]
public sealed class JD0007
{
}
```

Invalide — générique :

```csharp
[DiagnosticRule]
public static class JD0007<T>
{
}
```

### 8.2 Le membre `Id`

La règle doit posséder exactement un membre public nommé `Id` :

```csharp
public const string Id = "...";
```

La valeur doit être non nulle, non vide, pas composée uniquement d'espaces, et
correspondre à l'identifiant canonique du diagnostic. La forme recommandée est
`nameof(JD0007)`.

Lorsque l'identifiant du diagnostic n'est pas un identifiant C# valide, le nom
du type et l'identifiant diffèrent nécessairement :

```csharp
[DiagnosticRule]
public static class RULE_001
{
    public const string Id = "RULE-001";
    public const string Category = "Usage";
}
```

### 8.3 Le membre `Category`

La règle doit posséder exactement un membre public nommé `Category` :

```csharp
public const string Category = "...";
```

La valeur doit être non vide et correspondre à la catégorie déclarée par le
`DiagnosticDescriptor` de l'analyzer d'origine. Comme rien ne le vérifie à
l'exécution (§3.2), l'exactitude relève ici de la crédibilité du catalogue.

### 8.4 Absence d'héritage

Une règle ne doit pas hériter d'une classe de base représentant un diagnostic.
Une classe statique ne peut pas utiliser un modèle d'héritage classique, et des
propriétés abstraites ne pourraient jamais servir d'arguments constants
d'attribut. `DiagnosticCatalog` définit donc un contrat structurel vérifié par
analyzer, et non un contrat orienté objet imposé par héritage.

---

## 9. Attributs de suppression pris en charge

Les deux attributs de suppression **ne sont pas interchangeables**. Ils sont
décodés par des composants différents, avec des identifiants acceptés
différents :

| Attribut | Décodé par | Identifiants acceptés | Catégorie utilisée ? |
| --- | --- | --- | --- |
| `SuppressMessageAttribute` | Roslyn (`SuppressMessageAttributeState`) | tous | non |
| `UnconditionalSuppressMessageAttribute` | ILLink / ILCompiler | **`IL####` uniquement** | non |

### 9.1 `UnconditionalSuppressMessageAttribute` ne concerne que trim/AOT

Le nom signifie « pas `[Conditional]` ». La BCL documente la distinction sur le
type lui-même :

> *UnconditionalSuppressMessageAttribute is different than
> SuppressMessageAttribute in that it doesn't have a ConditionalAttribute. So it
> is always preserved in the compiled assembly.*

Cette préservation est une exigence et non un détail : ILLink et ILCompiler
lisent les suppressions dans l'**assembly compilée**, bien après le passage du
compilateur ; un attribut `[Conditional]` leur serait donc invisible (§3.4).

Son décodeur rejette tout ce qui n'est pas un identifiant d'avertissement IL :

```csharp
if (!(attribute.ConstructorArguments[1].Value is string warningId)
    || warningId.Length < 6
    || !warningId.StartsWith("IL")
    || !int.TryParse(warningId.AsSpan(2, 4), out info.Id))
```

`[UnconditionalSuppressMessage(Rules.JD0007.Category, Rules.JD0007.Id)]` est donc
purement ignoré — l'attribut n'est pas non plus traité par l'état de suppression
de Roslyn.

Supporter cet attribut signifie par conséquent supporter un catalogue
d'**avertissements trim et AOT** (`IL2026`, `IL3050`, …), et non « le même modèle
avec un autre attribut ». Cela offre aussi un diagnostic peu coûteux et utile :
signaler qu'une règle dont l'`Id` ne correspond pas à `IL####` est utilisée dans
`UnconditionalSuppressMessage` (`DCAT0009`) — un no-op silencieux qu'aucun autre
outil ne rapporte aujourd'hui.

### 9.2 Emplacements

Les suppressions peuvent être placées sur un type, une méthode, une propriété,
un champ, une assembly, ou dans un fichier `GlobalSuppressions.cs`.

### 9.3 Résolution des alias

L'analyse ne doit jamais dépendre du nom court écrit dans le code ; elle doit
résoudre le symbole réel de l'attribut. Les alias sont donc supportés :

```csharp
using Suppress = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

[Suppress(
    SomeRules.RULE001.Category,
    SomeRules.RULE001.Id,
    Justification = "...")]
```

---

## 10. Analyzer des utilisations

### 10.1 Chemin d'implémentation obligatoire

**`AttributeData` ne peut pas être utilisé.** Au moment où les arguments du
constructeur sont exposés sous forme de `TypedConstant`, les constantes ont déjà
été repliées : on obtient la valeur `"Usage"` et l'`IFieldSymbol` a disparu.
L'intégralité de cette section est irréalisable via `AttributeData`.

Le chemin requis est :

```csharp
context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
// puis, pour chaque expression d'argument :
var symbolInfo = context.SemanticModel.GetSymbolInfo(argument.Expression);
```

`IAttributeOperation` préserve bien l'`IFieldReferenceOperation` sous-jacent,
mais exige Microsoft.CodeAnalysis 4.6 ou supérieur. Le chemin syntaxique
fonctionne sur toutes les versions de Roslyn supportées ; c'est celui spécifié
ici.

### 10.2 Principe

Pour chacun des deux premiers arguments, l'analyzer résout :

* le champ constant référencé ;
* son type déclarant ;
* la présence de `[DiagnosticRule]` sur ce type.

L'utilisation est cohérente lorsque les deux champs appartiennent au même type de
règle.

### 10.3 Cas valide

```csharp
[SuppressMessage(
    SomeRules.RULE001.Category,
    SomeRules.RULE001.Id,
    Justification = "...")]
```

```text
Category → SomeRules.RULE001
Id       → SomeRules.RULE001
```

Aucun diagnostic n'est produit.

### 10.4 Cas invalide

```csharp
[SuppressMessage(
    SomeRules.RULE001.Category,
    SomeRules.RULE002.Id,
    Justification = "...")]
```

```text
Category → SomeRules.RULE001
Id       → SomeRules.RULE002
```

`DCAT0001` est signalé.

### 10.5 Formes syntaxiques acceptées

L'analyse s'appuie sur les symboles Roslyn et non sur le texte du code. La forme
canonique est l'accès de membre qualifié :

```csharp
SomeRules.RULE001.Category
```

Un alias de type est pleinement équivalent et recommandé lorsque le nom du
conteneur est long :

```csharp
using Rule = SomeRules.RULE001;

[SuppressMessage(Rule.Category, Rule.Id, Justification = "...")]
```

`using static` est **reconnu mais non recommandé** :

```csharp
using static SomeRules.RULE001;

[SuppressMessage(Category, Id, Justification = "...")]
```

Deux directives `using static` pour deux règles dans le même fichier rendent
`Category` et `Id` ambigus, ce qui est une erreur de compilation. La forme ne
fonctionne donc que pour une seule règle par fichier et casse dès qu'une seconde
suppression est nécessaire. L'analyzer doit la résoudre ; la documentation ne
doit pas la promouvoir.

### 10.6 Constantes intermédiaires

```csharp
private const string RuleId = SomeRules.RULE001.Id;

[SuppressMessage(SomeRules.RULE001.Category, RuleId, Justification = "...")]
```

Cette forme est **vérifiable**, contrairement à ce qu'une première lecture
suggère. Lorsqu'un argument résout vers un champ constant dont le type déclarant
n'est pas un type de règle, l'analyzer compare sa *valeur* constante exactement
comme pour un littéral (§11.6). Ce n'est pas la forme canonique et aucun code fix
n'est proposé, mais ce n'est pas un angle mort.

---

## 11. Diagnostics du socle

Le préfixe provisoire des diagnostics est `DCAT`.

| Id | Cible | Titre | Sévérité par défaut | MVP |
| --- | --- | --- | --- | --- |
| `DCAT0001` | utilisation | Category and Id must reference the same diagnostic rule | Warning | oui |
| `DCAT0002` | définition | A diagnostic rule must be declared as a static non-generic class | Warning | oui |
| `DCAT0003` | définition | A diagnostic rule must expose a public constant string named Id | Warning | oui |
| `DCAT0004` | définition | A diagnostic rule must expose a public constant string named Category | Warning | oui |
| `DCAT0005` | définition | The diagnostic rule type name should match its Id | Info | non |
| `DCAT0006` | utilisation | Use a diagnostic catalog reference instead of string literals | Info | **oui — cœur** |
| `DCAT0007` | utilisation | Suppression mixes a catalog reference with a string literal | Warning | oui |
| `DCAT0008` | utilisation | Suppression identifier does not resolve to a known diagnostic rule | Aucune (opt-in) | non |
| `DCAT0009` | utilisation | UnconditionalSuppressMessage only accepts IL#### identifiers | Warning | oui |
| `DCAT0010` | utilisation | Referenced diagnostic rule type is malformed | Warning | non |

Les diagnostics de définition (`DCAT0002`–`DCAT0005`) ne se déclenchent que sur
du code source visible par le compilateur. Une règle mal formée dans une
*assembly référencée* ne produit rien — c'est précisément ce que `DCAT0010`
couvre.

### 11.1 `DCAT0001` — membres issus de règles différentes

Signalé lorsque `Category` et `Id` résolvent vers des champs déclarés sur deux
types `[DiagnosticRule]` différents.

```csharp
[SuppressMessage(
    SomeRules.RULE001.Category,
    SomeRules.RULE002.Id,
    Justification = "...")]
```

### 11.2 `DCAT0002` — type de règle invalide

Signalé lorsqu'un type `[DiagnosticRule]` n'est pas une classe statique non
générique.

### 11.3 `DCAT0003` — identifiant absent ou invalide

Signalé lorsque le membre `Id` est absent, non public, n'est pas un champ, n'est
pas constant, n'est pas de type `string`, ou possède une valeur vide.

### 11.4 `DCAT0004` — catégorie absente ou invalide

Les mêmes validations que pour `Id` s'appliquent.

### 11.5 `DCAT0005` — nom de type différent de l'identifiant

```csharp
[DiagnosticRule]
public static class RuleSeven
{
    public const string Id = "JD0007";
    public const string Category = "Usage";
}
```

**Condition de déclenchement précise :** signaler uniquement lorsque
`SyntaxFacts.IsValidIdentifier(Id)` retourne `true` *et* que l'identifiant
diffère du nom du type. Sans cette clause, le diagnostic contredit le §8.2, qui
autorise explicitement `RULE_001` / `"RULE-001"`.

Ce diagnostic est de faible valeur — la forme `nameof` recommandée le rend
inviolable — et est donc exclu du MVP.

### 11.6 `DCAT0006` — chaînes littérales remplaçables

```csharp
[SuppressMessage("Usage", "JD0007", Justification = "...")]
```

**La normalisation de l'identifiant est obligatoire.** Avant comparaison,
tronquer le littéral au premier deux-points, comme le fait Roslyn lui-même
(§3.3) :

```text
"JD0007:Dummy factories should follow the convention"  →  "JD0007"
```

Omettre cette étape fait passer l'analyzer à côté de la forme que Visual Studio
génère, c'est-à-dire l'essentiel du code qui mérite d'être migré.

Règles de correspondance :

* aucune règle connue ne correspond au couple `(Category, Id)` normalisé → aucun
  diagnostic ;
* exactement une règle correspond → diagnostic et code fix déterministe ;
* plusieurs règles correspondent → diagnostic sans correction automatique
  unique.

Le code fix abandonne le suffixe lisible. C'est un compromis assumé et
documenté : la constante `Title` de la règle ou sa documentation XML le remplace.

### 11.7 `DCAT0007` — référence et littéral mélangés

```csharp
[SuppressMessage(SomeRules.RULE001.Category, "RULE001", Justification = "...")]
```

L'état de migration partielle le plus courant, et **le seul cas où la correction
est pleinement déterministe** : la règle voulue est connue par l'argument déjà
migré, il n'y a donc aucune ambiguïté à lever. Valeur pratique supérieure à celle
de `DCAT0001`.

### 11.8 `DCAT0008` — identifiant non résolu (mode strict)

Désactivé par défaut. Lorsqu'un projet y adhère, tout `checkId` doit résoudre
vers une règle de catalogue connue ; tout littéral ou identifiant inconnu est
signalé.

C'est l'aboutissement de la bibliothèque : cela transforme « mes suppressions
sont des références de catalogue » d'une convention en un invariant appliqué. Le
mode est opt-in car un projet référençant des analyzers sans catalogue
correspondant serait autrement submergé.

### 11.9 `DCAT0009` — identifiant non-IL dans `UnconditionalSuppressMessage`

Signalé lorsqu'une règle dont l'`Id` ne correspond pas à `IL####` est utilisée
dans `UnconditionalSuppressMessageAttribute` (§9.1). La suppression est un no-op
silencieux qu'aucun autre outil ne rapporte.

### 11.10 `DCAT0010` — règle référencée mal formée

Signalé au site d'utilisation lorsqu'un type `[DiagnosticRule]` référencé ne
satisfait pas le contrat structurel et est donc inutilisable. Couvre l'angle mort
laissé par `DCAT0002`–`DCAT0005` au franchissement des frontières d'assembly.

---

## 12. Code fixers

Tous les fixers doivent définir un `EquivalenceKey` explicite afin que
*Corriger toutes les occurrences* applique un choix cohérent à l'échelle d'un
document, d'un projet ou d'une solution.

### 12.1 Correction d'une paire incohérente (`DCAT0001`)

Pour :

```csharp
[SuppressMessage(
    SomeRules.RULE001.Category,
    SomeRules.RULE002.Id,
    Justification = "...")]
```

deux corrections doivent être proposées, avec des clés d'équivalence
distinctes :

```text
Use RULE001.Id        (EquivalenceKey = "AlignOnCategory")
Use RULE002.Category  (EquivalenceKey = "AlignOnId")
```

Correction fondée sur la catégorie :

```csharp
[SuppressMessage(
    SomeRules.RULE001.Category,
    SomeRules.RULE001.Id,
    Justification = "...")]
```

Correction fondée sur l'identifiant :

```csharp
[SuppressMessage(
    SomeRules.RULE002.Category,
    SomeRules.RULE002.Id,
    Justification = "...")]
```

Le code fixer ne doit jamais choisir arbitrairement quelle règle était voulue.
Lorsque les deux règles vivent dans des conteneurs ou des namespaces différents,
la correction doit aussi ajouter le `using` nécessaire.

### 12.2 Remplacement des chaînes littérales (`DCAT0006`)

Pour :

```csharp
[SuppressMessage("Usage", "JD0007", Justification = "...")]
```

lorsqu'une seule règle correspond :

```csharp
[SuppressMessage(
    JustDummiesRules.JD0007.Category,
    JustDummiesRules.JD0007.Id,
    Justification = "...")]
```

La correction ajoute la directive `using` nécessaire.

### 12.3 Complétion d'une suppression mixte (`DCAT0007`)

Une correction unique et déterministe : remplacer le littéral restant par la
référence de la règle déjà identifiée par l'autre argument.

### 12.4 Correction d'une définition

Lorsque cela peut être fait sans ambiguïté :

* rendre une classe statique ;
* rendre `Id` public ;
* rendre `Category` public ;
* remplacer `static readonly string` par `const string` lorsque l'expression est
  constante ;
* ajouter un membre manquant avec un emplacement réservé.

```csharp
public const string Category = "TODO";
```

Le code fixer ne doit jamais inventer une catégorie réelle.

---

## 13. Découverte des catalogues

L'analyzer découvre les règles dans la compilation courante et dans les
assemblies référencées. Une règle est reconnue lorsque son type porte
`DiagnosticCatalog.DiagnosticRuleAttribute` (reconnu par nom de métadonnée, §7.2)
et expose des membres `Id` et `Category` valides.

L'analyzer construit une représentation interne :

```csharp
internal sealed record DiagnosticRuleSymbol(
    INamedTypeSymbol RuleType,
    IFieldSymbol IdField,
    IFieldSymbol CategoryField,
    string Id,
    string Category);
```

Cette représentation appartient uniquement à l'implémentation de l'analyzer et ne
fait pas partie de l'API publique.

* La clé **fonctionnelle** d'une règle est `Category + Id`.
* La clé **structurelle** d'une référence est le symbole Roslyn du type
  `[DiagnosticRule]`.

### 13.1 Coût d'indexation

Parcourir tous les types de toutes les assemblies référencées est un balayage de
métadonnées coûteux, et « indexer une fois par compilation » en sous-estime le
prix. Deux mitigations obligatoires :

1. **Pré-filtrer les assemblies.** Ne visiter que celles dont
   `IAssemblySymbol.Modules.First().ReferencedAssemblies` inclut l'assembly
   `DiagnosticCatalog`, ou qui déclarent l'attribut elles-mêmes. Les autres ne peuvent
   pas contenir de règle.
2. **Construire l'index paresseusement** dans
   `RegisterCompilationStartAction`, derrière un `Lazy<T>`, pour ne payer le coût
   que si un site d'utilisation a réellement besoin d'une recherche par valeur —
   c'est-à-dire uniquement pour `DCAT0006` / `DCAT0007` / `DCAT0008`.

`DCAT0001` n'a besoin d'aucun index : il compare deux symboles résolus depuis
l'attribut lui-même.

---

## 14. Utilisation par un catalogue tiers

Un package spécialisé peut référencer `DiagnosticCatalog` et déclarer les règles
d'un analyzer qu'il ne possède pas. Trois sont implémentés, tous générés par le
même outil depuis les descriptors de leur package amont :

| Catalogue | Reflète | Règles | Catégories | Liens d'aide |
| --- | --- | --- | --- | --- |
| `DiagnosticCatalog.Sonar` | `SonarAnalyzer.CSharp 10.31.0.145097` | 456 | 13 | 0 sur 465 |
| `DiagnosticCatalog.NetAnalyzers` | `Microsoft.CodeAnalysis.NetAnalyzers 10.0.302` | 318 | 10 | 318 sur 318 |
| `DiagnosticCatalog.StyleCop` | `StyleCop.Analyzers 1.1.118` | 193 | 8 | 193 sur 193 |

```csharp
using DiagnosticCatalog;

namespace DiagnosticCatalog.Sonar;

[DiagnosticCategory]
public static class SonarCategory
{
    public const string MajorCodeSmell = "Major Code Smell";
}

public static class SonarRule
{
    [DiagnosticRule]
    public static class S1144
    {
        public const string Id = nameof(S1144);
        public const string Category = SonarCategory.MajorCodeSmell;
    }
}
```

Consommation :

```csharp
using DiagnosticCatalog.Sonar;

[SuppressMessage(
    SonarRule.S1144.Category,
    SonarRule.S1144.Id,
    Justification = "Instantiated through reflection by the DI container.")]
```

> **Exigence d'exactitude.** Un catalogue tiers doit dériver chaque catégorie du
> `DiagnosticDescriptor` réel de l'analyzer ciblé, jamais de la documentation ni
> de mémoire. Comme la plateforme ignore la catégorie (§3.2), une valeur fausse
> ne sera jamais signalée par rien — et un catalogue dont la raison d'être est
> d'être la réponse autoritative ne peut pas se permettre une inexactitude
> silencieuse. Sonar rend le point concret : ses catégories sont des paires
> `{Sévérité} {Type}`, donc `S1144` vaut `"Major Code Smell"` et `S1481` vaut
> `"Minor Code Smell"`. Aucune lecture de la documentation ne produit ces
> chaînes, et se tromper ne coûte rien et n'est jamais signalé.

### 14.1 Comment un catalogue généré est produit

Le générateur (`eng/CatalogGen`) charge l'assembly d'analyzer amont, construit
chaque `DiagnosticAnalyzer` qu'elle contient et lit les `DiagnosticDescriptor`
qu'ils déclarent. Les descriptors sont la seule source qui ne peut pas avoir
dérivé.

```text
dotnet run --project eng/CatalogGen -- \
    --package SonarAnalyzer.CSharp --version latest \
    --namespace DiagnosticCatalog.Sonar --container Sonar \
    --output src/DiagnosticCatalog.Sonar/SonarRules.g.cs
```

Règles du générateur, toutes porteuses :

1. **Lire les descriptors, pas la documentation.** Les métadonnées de règles au
   format JSON et les pages de règles publiées divergent de ce que l'analyzer
   déclare, et selon le §3.2 cette divergence est silencieuse.
2. **Produire une sortie ordonnée de façon déterministe**, pour qu'un diff de
   régénération ne montre que du changement amont réel.
3. **Signaler chaque exclusion.** Un descriptor n'est écarté que si sa catégorie
   est vide — une entrée qui n'est pas un diagnostic supprimable — ou si son
   identifiant n'est pas un identifiant C# valide. Les deux cas sont imprimés
   avec l'identifiant et le motif ; rien n'est écarté en silence. Pour
   `SonarAnalyzer.CSharp 10.31.0.145097`, cela représente neuf entrées `S9999-*`,
   qui sont des canaux internes de métriques et de télémétrie.
4. **Livrer les identifiants, les catégories et les titres.** Les deux premiers
   sont des faits portant sur le logiciel d'un tiers ; le titre est la phrase de
   cet éditeur nommant ce sur quoi la règle porte, portée par le commentaire de
   documentation de la règle, parce qu'un identifiant répété ne peut pas dire de
   quoi une règle parle. Les descriptions et les formats de message sont la
   documentation de cet éditeur et ne doivent pas être redistribués dans le
   package (ADR-0014). Un format de message n'est d'ailleurs pas une valeur unique
   par règle : 203 des 456 règles Sonar portent des emplacements remplis au moment
   de l'analyse et 37 ne portent rien d'autre, si bien qu'en publier un
   reviendrait à inventer une phrase qu'aucun descriptor ne déclare, ce que le
   point 5 interdit.
5. **Ne pas synthétiser de valeurs non lues.** `SonarAnalyzer.CSharp` renseigne
   `HelpLinkUri` sur 0 de ses 465 descriptors : le catalogue généré ne porte donc
   aucun lien d'aide, plutôt que des liens assemblés depuis un motif d'URL
   deviné.
6. **Déclarer chaque catégorie une seule fois** dans une classe
   `[DiagnosticCategory]` et faire référer les règles à elle (§7.7), plutôt que de
   répéter le littéral par règle.
7. **Prendre le langage demandé et les assemblies neutres, exclure les autres
   langages.** Les structures diffèrent et l'erreur est invisible : Sonar livre une
   assembly directement sous `analyzers/`, StyleCop utilise `analyzers/dotnet/cs/`,
   et `Microsoft.CodeAnalysis.NetAnalyzers` utilise les deux — l'essentiel des
   règles CA se trouve dans une assembly neutre à `analyzers/dotnet/`, seules les
   règles spécifiques à un langage étant sous `cs/` et `vb/`. Ne garder que
   `.../cs/` supprime silencieusement la majorité des règles CA ; tout garder
   absorbe silencieusement des règles Visual Basic dans un catalogue C#. Aucune des
   deux défaillances n'apparaît dans la sortie.
8. **Résoudre `latest` vers la dernière version *stable*.** Un catalogue reflète une
   version que les gens consomment ; `Microsoft.CodeAnalysis.NetAnalyzers` et
   `StyleCop.Analyzers` publient tous deux des préversions en avance sur le stable,
   et épingler par accident un catalogue sur une préversion est silencieux aussi.
9. **Ne jamais supprimer une règle.** Une règle que le package amont a cessé de
   déclarer est reportée depuis la sortie précédente et marquée `[Obsolete]` avec
   la version qui l'a retirée. Supprimer la constante casserait la recompilation
   de ses consommateurs, puisqu'ils en ont inliné la valeur (§23.1) ; une
   constante obsolète leur donne `CS0618` — un avertissement qui nomme la règle et
   leur dit de retirer la suppression. Si l'amont la rétablit, la marque disparaît
   automatiquement.
10. **Laisser le fichier intact quand rien n'a bougé.** Le générateur lit sa propre
   sortie précédente, compare l'ensemble des règles et la version reflétée, et ne
   réécrit que sur une différence réelle — y compris en ne touchant pas à
   `generatedOn`. Sans cela, le job planifié du §14.3 ouvrirait chaque nuit une
   pull request dont le seul contenu serait une nouvelle date.
11. **Enregistrer la provenance** avec `[assembly: CatalogSource]` (§7.6).

### 14.2 Versionnement d'un catalogue généré

Comme le §7.6 enregistre la version amont exacte dans les métadonnées, la version
du package n'a pas à l'encoder — et ne l'encode pas : la version d'un catalogue
évolue sur sa propre ligne SemVer, incrémentée depuis ce qui a changé dans le
catalogue (ADR-0015). Une correction de génération n'a donc besoin d'aucune
publication amont à laquelle s'accrocher, et une publication amont qui ne change
aucune règle publiée ne déplace aucune version. Dans les deux cas, une constante
n'est jamais supprimée (§23.1) : une règle retirée en amont devient `[Obsolete]`.

La synchronisation avec l'amont est automatisée (§14.3).

### 14.3 Synchronisation planifiée

`.github/workflows/nightly-catalogs.yml` s'exécute chaque nuit et à la demande. Il
régénère chaque catalogue listé dans `eng/catalogs.json` — la liste vit dans le
dépôt sous forme de données, elle n'est donc pas dupliquée dans la configuration
CI — puis :

1. s'arrête sans bruit quand rien n'a changé, ce qui est le cas normal ;
2. compile toute la solution, car une régénération qui ne compile plus signifie que
   l'amont a changé de forme : c'est un signal, pas quelque chose à masquer ;
3. relance le générateur une **seconde** fois et échoue si la sortie a bougé, ce
   qui attrape toute perte de déterminisme avant qu'elle ne pollue tous les diffs
   suivants ;
4. ouvre ou met à jour une pull request unique sur une branche fixe, portant dans
   son corps le diff des règles — ajoutées, recatégorisées, retirées.

**Il ne publie jamais de package.** La pull request existe parce qu'une catégorie
ou un identifiant qui a bougé en amont modifie un contrat publié, et parce que la
plateforme ne lit jamais la catégorie d'une suppression (§3.2) : une valeur fausse
fusionnée ici ne produirait aucun symptôme. Un humain doit regarder.

Le job requiert `contents: write` et `pull-requests: write`, rien de plus. Il
utilise la CLI `gh` déjà présente sur le runner plutôt qu'une action tierce, de
sorte que la seule frontière de confiance est le jeton de GitHub lui-même.

---

## 15. Utilisation par un projet propriétaire

Un projet contrôlant son propre analyzer n'est pas obligé de publier un package
de catalogue séparé.

### 15.1 Exposition directe

```csharp
namespace JustDummies.Analyzers.Suppressions;

public static class Dummies
{
    [DiagnosticRule]
    public static class JD0007
    {
        public const string Id = nameof(JD0007);
        public const string Category = "Usage";
    }
}
```

La règle architecturale : **lorsqu'un éditeur contrôle l'analyzer, le catalogue
est distribué avec cet analyzer. Lorsqu'il ne le contrôle pas, un package de
catalogue indépendant est publié.**

### 15.2 Fermer la boucle avec `DiagnosticDescriptor`

C'est la raison la plus forte pour un projet propriétaire d'adopter la
convention, et c'est la finalité du §7.5. L'analyzer devrait construire son
descriptor **depuis le catalogue** :

```csharp
private static readonly DiagnosticDescriptor Descriptor = new(
    id:                 Dummies.JD0007.Id,
    title:              Dummies.JD0007.Title,
    messageFormat:      Dummies.JD0007.MessageFormat,
    category:           Dummies.JD0007.Category,
    defaultSeverity:    Dummies.JD0007.Severity,
    isEnabledByDefault: true,
    description:        Dummies.JD0007.Description,
    helpLinkUri:        Dummies.JD0007.HelpLinkUri);
```

Une seule source de vérité pour l'analyzer *et* pour chacune de ses suppressions.
La catégorie publiée par le catalogue est alors exacte par construction et non
par diligence — ce qu'un catalogue tiers ne peut précisément pas garantir (§14).

---

## 16. Packaging NuGet

Les analyzers Roslyn sont distribués dans le dossier `analyzers` du package
NuGet. Les assemblies d'analyse ne doivent jamais devenir des dépendances
runtime de l'application consommatrice.

### 16.1 Deux packages, deux publics

Un package unique ne peut pas servir les deux publics, car leurs besoins sont
opposés :

| Public | Besoin | Mode de référence |
| --- | --- | --- |
| **Consommateur** — écrit des suppressions | les analyzers seulement | `PrivateAssets="all"`, aucune dépendance runtime |
| **Auteur de catalogue** — déclare des règles | `DiagnosticRuleAttribute` résoluble *par ses propres consommateurs* | référence `DiagnosticCatalog` ordinaire, dépendance déclarée — ou attribut embarqué en source (§7.2) |

Recommander `PrivateAssets="all"` de façon universelle produit le mode de
défaillance décrit au §7.2. D'où :

```text
DiagnosticCatalog.nupkg
├── lib/netstandard2.0/DiagnosticCatalog.dll
├── lib/netstandard2.0/DiagnosticCatalog.xml
└── README.md

DiagnosticCatalog.Analyzers.nupkg          (DevelopmentDependency = true)
├── analyzers/dotnet/cs/DiagnosticCatalog.Analyzers.dll
├── analyzers/dotnet/cs/DiagnosticCatalog.CodeFixes.dll
├── AnalyzerReleases.Shipped.md
├── README.md
└── icon.png
```

Un métapackage de confort peut dépendre des deux.

### 16.2 Référence côté consommateur

```xml
<ItemGroup>
  <PackageReference Include="DiagnosticCatalog.Analyzers"
                    Version="1.0.0"
                    PrivateAssets="all" />
</ItemGroup>
```

### 16.3 La transitivité doit être testée, pas supposée

La documentation NuGet indique que la valeur par défaut de `PrivateAssets` pour
un `PackageReference` est `contentfiles;analyzers;build`, ce qui implique que les
analyzers ne circulent pas transitivement. En pratique,
[NuGet/Home#13813](https://github.com/NuGet/Home/issues/13813) rapporte que les
analyzers transitifs circulent bel et bien. **Ne dépendre d'aucune des deux
directions.**

* `DiagnosticCatalog.Packaging.Tests` doit effectuer un restore réel des packages
  produits et vérifier si l'analyzer s'active pour le consommateur d'un package
  de catalogue.
* Le levier explicite pour un catalogue qui *souhaite* propager l'analyzer est
  `PrivateAssets="none"` sur sa propre référence.
* Jusqu'à ce que ce test existe, la documentation doit demander aux
  consommateurs de référencer explicitement `DiagnosticCatalog.Analyzers`.

---

## 17. Configuration

Toutes les règles doivent être configurables avec les mécanismes standards des
analyzers Roslyn :

```ini
dotnet_diagnostic.DCAT0001.severity = error
dotnet_diagnostic.DCAT0002.severity = error
dotnet_diagnostic.DCAT0003.severity = error
dotnet_diagnostic.DCAT0004.severity = error
dotnet_diagnostic.DCAT0005.severity = suggestion
dotnet_diagnostic.DCAT0006.severity = warning
dotnet_diagnostic.DCAT0007.severity = error
dotnet_diagnostic.DCAT0008.severity = warning   # mode strict opt-in
dotnet_diagnostic.DCAT0009.severity = error
```

Une équipe appliquant une politique zéro warning pourra ainsi transformer les
suppressions incohérentes en erreurs de compilation. Aucun format de
configuration propriétaire n'est requis pour la première version.

---

## 18. Comportement sur le code généré

Les diagnostics d'utilisation ne doivent pas être signalés dans le code généré
automatiquement. Les diagnostics de définition doivent l'être, car un catalogue
généré est lui-même du code généré.

**`ConfigureGeneratedCodeAnalysis` s'applique par analyzer, pas par
diagnostic.** Ces deux exigences ne peuvent donc pas coexister dans une seule
classe `DiagnosticAnalyzer`. L'implémentation doit se scinder en deux :

| Classe d'analyzer | Diagnostics | Flags code généré |
| --- | --- | --- |
| `DiagnosticRuleDefinitionAnalyzer` | `DCAT0002`–`DCAT0005` | `Analyze` |
| `SuppressionUsageAnalyzer` | `DCAT0001`, `DCAT0006`–`DCAT0010` | `None` |

Les définitions de règles produites par un outil externe doivent en outre être
validées par les tests du générateur, des tests de compilation et une validation
du manifeste source.

---

## 19. Performances

Les analyzers doivent :

* activer l'exécution concurrente ;
* éviter les analyses syntaxiques globales répétées ;
* indexer les règles au plus une fois par compilation, paresseusement (§13.1) ;
* pré-filtrer les assemblies référencées avant tout parcours de métadonnées
  (§13.1) ;
* comparer les symboles avec `SymbolEqualityComparer.Default` ;
* analyser uniquement les attributs pertinents ;
* mettre en cache les symboles des attributs de suppression reconnus ;
* ne réaliser aucun accès réseau ;
* ne lire aucun fichier externe à chaque analyse d'attribut.

---

## 20. Compatibilité

La fondation doit prendre en charge :

* les projets SDK-style ;
* les suppressions locales ;
* les suppressions au niveau assembly ;
* les fichiers `GlobalSuppressions.cs` ;
* les catalogues définis dans le même projet ;
* les catalogues fournis par une assembly référencée ;
* les alias de types ;
* `using static` (résolu, non recommandé — §10.5) ;
* `SuppressMessageAttribute` ;
* `UnconditionalSuppressMessageAttribute`, **pour les règles `IL####`
  uniquement** (§9.1).

La première version peut être limitée au langage C#. Le support Visual Basic
pourra être envisagé ultérieurement.

`DiagnosticCatalog` n'est actuellement **pas signé** (pas de nom fort). Une
assembly signée qui le référence produit `CS8002`, qu'un projet en
warnings-as-errors doit neutraliser via `<NoWarn>$(NoWarn);CS8002</NoWarn>`. Ce
point doit être tranché avant la première publication : ajouter *ou* retirer un
nom fort ensuite change l'identité d'assembly de toutes les références, ce qui
est une rupture binaire dans les deux sens (annexe B6).

---

## 21. Tests requis

### 21.1 Tests des définitions de règles

* une règle valide ;
* une classe non statique ;
* une classe générique ;
* un membre `Id` absent ;
* un membre `Category` absent ;
* un `Id` non constant ;
* une `Category` non constante ;
* une valeur vide ;
* un identifiant différent du nom de la classe ;
* un identifiant ne pouvant pas être un identifiant C# (ne doit **pas**
  signaler `DCAT0005`) ;
* une règle déclarée avec un attribut embarqué en source (§7.2).

### 21.2 Tests des suppressions

* une paire valide ;
* une catégorie et un identifiant issus de règles différentes ;
* des règles provenant d'une assembly référencée ;
* une forme `using static` ;
* un alias de type ;
* une suppression au niveau assembly ;
* un fichier `GlobalSuppressions.cs` ;
* une constante intermédiaire (§10.6) ;
* `SuppressMessageAttribute` ;
* `UnconditionalSuppressMessageAttribute` avec une règle `IL####` ;
* `UnconditionalSuppressMessageAttribute` avec une règle non-IL → `DCAT0009`.

### 21.3 Tests des chaînes littérales

* aucune règle correspondante ;
* exactement une règle correspondante ;
* plusieurs règles correspondantes ;
* une catégorie correcte avec un identifiant inconnu ;
* un identifiant correct avec une catégorie incorrecte ;
* **un identifiant portant un suffixe `:FriendlyName`** (§3.3) — la forme
  générée par Visual Studio ;
* une référence et un littéral → `DCAT0007`.

### 21.4 Tests des code fixers

* la correction fondée sur la catégorie ;
* la correction fondée sur l'identifiant ;
* la correction déterministe de `DCAT0007` ;
* le remplacement des chaînes littérales ;
* l'ajout d'un `using` ;
* la conservation de la justification ;
* la conservation de `Scope`, `Target` et `MessageId` ;
* l'absence de modification des autres attributs ;
* *Corriger toutes les occurrences* respectant l'`EquivalenceKey` de façon
  cohérente.

### 21.5 Tests de compilation réels

Un projet de test doit réellement compiler :

```csharp
[SuppressMessage(
    TestRules.TEST0001.Category,
    TestRules.TEST0001.Id,
    Justification = "Compilation test.")]
public sealed class Subject
{
}
```

Ce test protège la contrainte essentielle : `Id` et `Category` doivent rester
utilisables comme arguments constants d'attribut.

Une assertion par réflexion doit confirmer que le repliement des constantes a
produit les métadonnées attendues. Comme `SuppressMessageAttribute` est
`[Conditional("CODE_ANALYSIS")]` (§3.4), le projet de test **doit définir ce
symbole**, sans quoi l'attribut n'est jamais émis et l'assertion lit
silencieusement `null` :

```xml
<DefineConstants>$(DefineConstants);CODE_ANALYSIS</DefineConstants>
```

```csharp
var attribute = typeof(Subject).GetCustomAttribute<SuppressMessageAttribute>();

Assert.NotNull(attribute);   // échoue d'emblée si CODE_ANALYSIS n'est pas défini
Assert.Equal("TEST0001", attribute!.CheckId);
Assert.Equal("Usage", attribute.Category);
```

Un projet compagnon ne définissant **pas** le symbole doit vérifier l'inverse —
l'attribut est absent des métadonnées — ce qui fait de la garantie d'absence
d'empreinte du §4 une propriété testée plutôt qu'une affirmation.

### 21.6 Test de suppression de bout en bout

La prémisse de toute la bibliothèque doit être démontrée, pas supposée : un
analyzer réel émet un diagnostic, une `[SuppressMessage]` fondée sur le catalogue
est appliquée, et le diagnostic **est effectivement absent** du résultat de
compilation. Sans ce test, le §27 n'affirme que la compilation du code.

### 21.7 Tests de packaging

* un restore réel des packages produits ;
* l'analyzer s'active pour un consommateur direct ;
* le comportement de transitivité du §16.3 est vérifié, quel qu'il s'avère
  être ;
* les assemblies d'analyse n'apparaissent pas dans le dossier de sortie du
  consommateur.

---

## 22. Documentation requise

* un README présentant le problème ;
* un exemple minimal ;
* une documentation destinée aux auteurs de catalogues ;
* une documentation destinée aux consommateurs ;
* la liste des diagnostics `DCATxxxx` ;
* la procédure de configuration `.editorconfig` ;
* la matrice `PrivateAssets` du §16.1 ;
* un énoncé explicite des limites du §5.1 ;
* une politique de versionnement ;
* un guide de contribution ;
* un exemple de package spécialisé ;
* un exemple d'intégration dans un analyzer propriétaire, incluant le §15.2.

---

## 23. Versionnement

Les packages suivent Semantic Versioning.

**Version corrective** — corrections d'analyzer, corrections de code fixer,
améliorations de performance, corrections de documentation, toute modification
laissant intact le contrat public.

**Version mineure** — un nouveau diagnostic, un nouveau code fixer, une nouvelle
métadonnée facultative, la prise en charge d'un nouvel attribut compatible, une
nouvelle fonctionnalité désactivée par défaut.

**Version majeure** — renommer `DiagnosticRuleAttribute`,
`DiagnosticCategoryAttribute` ou `CatalogSourceAttribute`, modifier les noms
obligatoires `Id` ou `Category`, changer la définition structurelle d'une règle,
supprimer un diagnostic public, changer le comportement d'une règle de manière
incompatible, modifier les namespaces publics, et changer l'identité d'assembly
(nom d'assembly, identifiant de package ou clé de nom fort).

### 23.1 Note sur les packages de catalogues

Les constantes sont **inlinées à la compilation du consommateur**. Supprimer une
`const` d'un catalogue publié casse donc sa recompilation. Une règle retirée en
amont doit être marquée `[Obsolete]`, jamais supprimée.

---

## 24. Périmètre du MVP

La version `1.0` doit contenir uniquement les éléments indispensables.

**Inclus**

* `DiagnosticRuleAttribute`, avec reconnaissance par nom de métadonnée (§7.2) ;
* validation de la classe statique (`DCAT0002`) ;
* validation de `Id` (`DCAT0003`) ;
* validation de `Category` (`DCAT0004`) ;
* le contrôle de cohérence même-règle (`DCAT0001`) ;
* **la détection des littéraux et son code fix (`DCAT0006`) — cœur, et non
  facultatif** (§3.5), y compris la normalisation `:FriendlyName` ;
* la détection référence/littéral mixte et sa correction déterministe
  (`DCAT0007`) ;
* le garde-fou `IL####` pour `UnconditionalSuppressMessage` (`DCAT0009`) ;
* le support de `SuppressMessageAttribute` ;
* le support d'`UnconditionalSuppressMessageAttribute`, cadré selon le §9.1 ;
* les deux code fixers pour les paires incohérentes ;
* deux packages NuGet (§16.1) avec suivi des versions d'analyzers ;
* la documentation ;
* les tests d'analyzer, de compilation, de bout en bout et de packaging.

**Hors MVP**

* `DCAT0005`, `DCAT0008`, `DCAT0010` ;
* un source generator de catalogue ;
* l'import automatique de catalogues externes ;
* une CLI ;
* la génération de documentation ;
* la validation de `Scope` / `Target` ;
* la validation intelligente des justifications ;
* la synchronisation automatique avec un fournisseur tiers ;
* le support Visual Basic ;
* un modèle runtime de diagnostic ;
* un portail web de catalogues.

---

## 25. Évolutions possibles

Les extensions suivantes pourront être développées ultérieurement sous forme de
packages distincts :

```text
DiagnosticCatalog.Generator
DiagnosticCatalog.Tool
DiagnosticCatalog.Documentation
DiagnosticCatalog.<Éditeur>
```

### 25.1 Validation de `Scope` / `Target`

Dans `GlobalSuppressions.cs`, `Target = "~M:Ns.Type.Method(System.Int32)"` est un
identifiant de commentaire de documentation codé en dur, qui pourrit
silencieusement à chaque renommage. Rien dans la plateforme ne le signale.
`DocumentationCommentId.GetFirstSymbolForDeclarationId` permet de vérifier qu'il
résout encore. C'est la suite naturelle et elle colle exactement à la thèse de
cohérence structurelle — sans doute une douleur quotidienne plus grande que les
littéraux catégorie/identifiant eux-mêmes.

### 25.2 Mode strict

`DCAT0008` (§11.8) promu d'opt-in à configuration documentée et recommandée, dès
que l'écosystème de catalogues sera suffisamment large pour ne pas noyer un
projet de faux positifs.

### 25.3 Générateur

Un générateur pourrait transformer un manifeste en classes de constantes :

```json
{
  "rules": [
    { "id": "JD0007", "category": "Usage", "title": "Example rule" }
  ]
}
```

### 25.4 Autres catalogues générés

Les catalogues générés ne sont plus une évolution future : la méthode, le
générateur, trois catalogues et leur synchronisation planifiée sont spécifiés aux
§14.1–§14.3 et implémentés. Restent d'autres éditeurs, et une variante Visual
Basic — le générateur prend déjà `--language`, donc
`DiagnosticCatalog.NetAnalyzers.VisualBasic` est une entrée de manifeste, pas du
code.

### 25.5 Validation des justifications

Un analyzer ou un outil séparé pourrait vérifier que `Justification` est
présente, n'est pas une valeur générique, explique effectivement l'exception, et
ne contient pas seulement `TODO`, `N/A` ou `False positive`. Cette fonctionnalité
doit rester séparée du contrôle fondamental d'identité des règles.

### 25.6 CLI

```text
diagnostic-catalog validate
diagnostic-catalog generate
diagnostic-catalog list
diagnostic-catalog explain JD0007
```

---

## 26. Exemple complet

Déclaration :

```csharp
using DiagnosticCatalog;

namespace ExampleAnalyzer.Suppressions;

public static class Example
{
    [DiagnosticRule]
    public static class EXAMPLE0001
    {
        public const string Id = nameof(EXAMPLE0001);
        public const string Category = "Design";
        public const string Title = "Avoid example design";
        public const string HelpLinkUri = "https://example.org/rules/EXAMPLE0001";
    }

    [DiagnosticRule]
    public static class EXAMPLE0002
    {
        public const string Id = nameof(EXAMPLE0002);
        public const string Category = "Usage";
        public const string Title = "Avoid example usage";
        public const string HelpLinkUri = "https://example.org/rules/EXAMPLE0002";
    }
}
```

Utilisation valide :

```csharp
using System.Diagnostics.CodeAnalysis;
using ExampleAnalyzer.Suppressions;

[SuppressMessage(
    Example.EXAMPLE0001.Category,
    Example.EXAMPLE0001.Id,
    Justification = "Required by the external framework contract.")]
public sealed class FrameworkAdapter
{
}
```

Utilisation invalide :

```csharp
[SuppressMessage(
    Example.EXAMPLE0001.Category,
    Example.EXAMPLE0002.Id,
    Justification = "Required by the external framework contract.")]
```

```text
DCAT0001: Category and Id must reference the same diagnostic rule.

  Use EXAMPLE0001.Id
  Use EXAMPLE0002.Category
```

Utilisation partiellement migrée :

```csharp
[SuppressMessage(
    Example.EXAMPLE0001.Category,
    "EXAMPLE0001",
    Justification = "Required by the external framework contract.")]
```

```text
DCAT0007: Suppression mixes a catalog reference with a string literal.

  Use Example.EXAMPLE0001.Id
```

---

## 27. Critères d'acceptation de la version 1.0

La version `1.0` est considérée comme terminée lorsque :

1. une bibliothèque tierce peut déclarer une règle avec `[DiagnosticRule]`, avec
   ou sans référencer l'assembly `DiagnosticCatalog` (§7.2) ;
2. cette règle peut être utilisée dans un véritable `SuppressMessageAttribute` ;
3. **un diagnostic réellement émis par un analyzer réel est effectivement
   supprimé** par une suppression fondée sur le catalogue, démontré par le test
   du §21.6 ;
4. dans une compilation définissant `CODE_ANALYSIS`, la réflexion confirme les
   `CheckId` et `Category` attendus ; dans une compilation par ailleurs
   identique qui ne le définit pas, l'attribut est totalement absent des
   métadonnées — les deux vérifiés par le §21.5 (§3.4) ;
5. l'analyzer accepte une catégorie et un identifiant issus de la même règle ;
6. l'analyzer détecte une catégorie et un identifiant issus de règles
   différentes ;
7. deux corrections explicites sont proposées lorsque l'intention est ambiguë ;
8. les définitions de règles invalides sont détectées ;
9. une suppression par littéraux est remplacée lorsqu'une correspondance unique
   existe, **y compris sous la forme `Id:FriendlyName`** (§3.3) ;
10. une suppression partiellement migrée est détectée et corrigée de façon
    déterministe ;
11. les suppressions globales sont prises en charge ;
12. `UnconditionalSuppressMessageAttribute` est pris en charge pour les règles
    `IL####`, et le mésusage est signalé (§9.1) ;
13. les analyzers n'introduisent aucune dépendance runtime, vérifié par le
    §21.7 ;
14. les packages peuvent être installés depuis NuGet, et leur comportement de
    transitivité est documenté à partir d'un test de restore réel (§16.3) ;
15. tous les cas documentés disposent de tests automatisés ;
16. un catalogue d'exemple est fourni ;
17. la documentation permet de créer un catalogue sans connaître
    l'implémentation interne du socle.

---

## 28. Résumé architectural

```text
Une règle
    =
une classe statique marquée [DiagnosticRule]
    +
un const string Id          → la valeur qui compte réellement
    +
un const string Category    → la valeur que personne d'autre ne publie
```

```text
Le problème
    =
checkId est une chaîne magique qui échoue en silence et définitivement
```

```text
La fondation
    =
le contrat public
    +
les analyzers
    +
les code fixers
```

Les catalogues spécialisés apportent les données propres à chaque analyzer ;
`DiagnosticCatalog` fournit la convention commune et garantit leur utilisation
correcte.

---

## Annexe A — Comportements vérifiés de la plateforme

Chaque affirmation comportementale de ce document a été vérifiée sur les
sources, non restituée de mémoire. À re-vérifier avant toute révision majeure.

| # | Affirmation | Source |
| --- | --- | --- |
| A1 | `SuppressMessageAttribute` n'a qu'un constructeur `(string category, string checkId)` ; les deux paramètres sont requis et non-nullables. | [`SuppressMessageAttribute.cs`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Diagnostics/CodeAnalysis/SuppressMessageAttribute.cs) |
| A2 | Roslyn ignore la catégorie lors du matching d'une suppression : *« Ignore the category parameter because it does not identify the diagnostic… »* | [`SuppressMessageAttributeState.cs`](https://github.com/dotnet/roslyn/blob/main/src/Compilers/Core/Portable/DiagnosticAnalyzer/SuppressMessageAttributeState.cs) |
| A3 | Roslyn tronque `checkId` au premier `:`, autorisant un nom lisible facultatif. | idem A2 |
| A4 | ILLink ignore aussi la catégorie, **et n'accepte que les identifiants `IL####`** (`Length >= 6`, `StartsWith("IL")`, 4 chiffres analysés à l'offset 2). | [`UnconditionalSuppressMessageAttributeState.cs`](https://github.com/dotnet/runtime/blob/main/src/tools/illink/src/linker/Linker/UnconditionalSuppressMessageAttributeState.cs) |
| A5 | Un constructeur sans catégorie a été proposé en amont ; la demande est toujours ouverte et non tranchée. | [dotnet/runtime#68153](https://github.com/dotnet/runtime/issues/68153) |
| A6 | NuGet documente les analyzers comme non transitifs par défaut, mais un flux transitif est rapporté en pratique — le comportement doit être testé. | [NuGet/Home#13813](https://github.com/NuGet/Home/issues/13813), [documentation PackageReference](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files) |
| A7 | `SuppressMessageAttribute` est `[Conditional("CODE_ANALYSIS")]` et n'est donc pas émis en métadonnées sauf si ce symbole est défini. Confirmé empiriquement : la réflexion retourne `null` par défaut, et les valeurs attendues dès que `CODE_ANALYSIS` est défini. | [`SuppressMessageAttribute.cs`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Diagnostics/CodeAnalysis/SuppressMessageAttribute.cs) |
| A8 | `UnconditionalSuppressMessageAttribute` ne porte aucun `[Conditional]`, ce qui est sa raison d'être déclarée : *« …it doesn't have a ConditionalAttribute. So it is always preserved in the compiled assembly. »* | [`UnconditionalSuppressMessageAttribute.cs`](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Diagnostics/CodeAnalysis/UnconditionalSuppressMessageAttribute.cs) |
| A9 | `SonarAnalyzer.CSharp 10.31.0.145097` déclare 465 descriptors répartis sur 448 types d'analyzers. Les catégories sont des paires `{Sévérité} {Type}` — 13 valeurs distinctes, p. ex. `S1144` = `"Major Code Smell"`. Neuf entrées `S9999-*` portent une catégorie vide, et `HelpLinkUri` est renseigné sur **0** des 465. | Lu depuis le `DiagnosticAnalyzer.SupportedDiagnostics` du package lui-même par `eng/CatalogGen` (§14.1) |
| A10 | Une `const string` initialisée depuis une autre `const string` reste une constante de compilation : elle est acceptée comme argument d'attribut et se replie vers le littéral dans les métadonnées. Vérifié par réflexion sur un `[SuppressMessage]` dont la catégorie passait par `SonarCategory.MajorCodeSmell` — `Category` relu à `"Major Code Smell"`. | Test de compilation + réflexion (§7.7) |
| A12 | Des règles sont bel et bien retirées en amont : `CA2109` et `CA2229` sont déclarées par `Microsoft.CodeAnalysis.NetAnalyzers 6.0.0` et plus par `10.0.302`. Reportées en `[Obsolete]`, un consommateur qui en référence encore une obtient `CS0618` nommant la règle, plutôt qu'une erreur dure `CS0117` de membre supprimé. | Régénération entre les deux versions, plus compilation de la forme consommatrice (§14.1) |
| A11 | `Microsoft.CodeAnalysis.NetAnalyzers 10.0.302` déclare 318 descriptors sur 10 catégories, tous avec liens d'aide, et répartit ses analyzers entre une assembly neutre à `analyzers/dotnet/` et des assemblies par langage sous `cs/` et `vb/`. `StyleCop.Analyzers 1.1.118` déclare 193 descriptors sur 8 catégories de la forme `StyleCop.CSharp.*Rules`, tous avec liens d'aide. `StyleCop.Analyzers 1.2.0-beta.556` est un métapackage sans assembly d'analyzer ; les règles vivent dans `StyleCop.Analyzers.Unstable`. | Même méthode qu'en A9 |

## Annexe B — Décisions ouvertes

| # | Question | Position actuelle |
| --- | --- | --- |
| B1 | ~~Nom définitif du produit. `Catalog` désigne une bibliothèque qui ne contient délibérément aucun catalogue (§2.5).~~ | **Tranché** — le nom reste. Il dit ce à quoi la bibliothèque sert, non ce qu'elle contient ; les alternatives perdent chacune quelque chose, et quatre packages sont publiés. |
| B2 | Le préfixe `DCAT` est-il libre ? Les préfixes d'analyzers communautaires ne sont pas enregistrés centralement. | À vérifier contre les préfixes connus avant la 1.0. |
| B3 | Le repli purement structurel du §7.2 (sans attribut) doit-il être activé par défaut ? | Attribut seul pour la 1.0 ; repli structurel documenté mais désactivé. |
| B4 | Les alias et `using static` valent-ils la complexité d'analyse, compte tenu du §10.5 ? | Résolus, documentés, non promus. |
| B5 | L'analyzer Roslyn d'ILLink (`IL2xxx` à la compilation) partage-t-il le décodeur vérifié en A4 ? | Supposé équivalent ; à vérifier avant d'annoncer une couverture complète d'`UnconditionalSuppressMessage`. |
| B6 | ~~Signer l'assembly `DiagnosticCatalog` (nom fort) ?~~ | **Tranché** — non signée, et elle le reste au-delà de la 1.0. Le consommateur d'un catalogue n'est concerné dans aucun sens : il lit des `const`, que le compilateur inline, si bien qu'aucune référence à l'assembly du catalogue n'est émise et que l'application tourne sans lui. Seul est concerné un assembly lui-même signé **et** qui utilise les attributs marqueurs pour déclarer son propre catalogue, et seulement par un `CS8002` — un avertissement, sur n'importe quel framework cible, ce n'est pas une affaire .NET Framework. Ce n'est pas le public premier de cette bibliothèque. |
| B7 | ~~La version du package `DiagnosticCatalog.Sonar` doit-elle suivre `SonarAnalyzer.CSharp`, ou évoluer sur sa propre ligne ?~~ | **Tranché** — sa propre ligne, pour chaque catalogue : [ADR-0015](adr/0015-a-catalogues-version-runs-on-its-own-line.md). |
