# Glossaire

🌍 **Langues :**  
🇬🇧 [English](./glossary.en.md) | 🇫🇷 Français (ce fichier)

Pour quiconque a croisé un mot ici et veut le sens exact qu'il porte. Par ordre alphabétique ; chaque
entrée dit ce que c'est et, quand cela compte, ce que ce n'est *pas*.

## Alias

Un `using` qui donne un nom local à une règle :

```csharp
using Unused = DiagnosticCatalog.Sonar.SonarRule.S1144;
```

Vérifié exactement comme la forme longue — l'analyse travaille sur les symboles, jamais sur le texte
tapé. Le raccourci recommandé, et celui qui passe à l'échelle, contrairement à `using static`.

## Catalogue

Un assemblage plein de [règles](#règle), décrivant un analyseur. `DiagnosticCatalog.Sonar` en est un.

Un catalogue est un **instantané** de l'analyseur qu'il reflète, pas une vue vivante : il décrit la
version dont il a été généré, et sa [provenance](#provenance) enregistre laquelle. C'est pourquoi
l'âge est la première chose que `dcat list` imprime.

Pas un paquet de comportement. Un catalogue contient des constantes et leur documentation XML, et rien
qui s'exécute.

## Catégorie

Le premier argument de `[SuppressMessage(...)]`, et celui que rien dans la plateforme ne lit.

Roslyn apparie une suppression sur le seul [identifiant](#identifiant) ; la catégorie est portée dans
les métadonnées — quand elle l'est — et consultée par aucun compilateur, analyseur, test ni outil. Une
catégorie fausse ne produit donc de symptôme nulle part, ce qui est la défaillance que cette
bibliothèque existe pour éliminer.

Sa valeur faisant autorité est celle que déclare le `DiagnosticDescriptor` de l'analyseur d'origine —
pas ce que la documentation de l'éditeur en dit.

## Classe de catégories

Une classe de valeurs de catégorie en `const string`, pour qu'une même catégorie s'écrive une fois
plutôt que dans chaque règle. `SonarCategory` en est une ; le catalogue Sonar dépense 456 déclarations
de règles sur 13 de ses membres.

La marquer `[DiagnosticCategory]` est **exigé** : une règle doit atteindre sa catégorie via une
constante déclarée dans une classe marquée, ce que signale `DCAT0011`. Ce que le marqueur apporte,
c'est que l'outillage peut distinguer une constante de catégorie de n'importe quelle autre constante
`string`. Dans un catalogue
généré par ce dépôt le conteneur est `internal`, donc une suppression ne nomme une catégorie qu'à
travers la règle qui la porte — `SonarRule.S1144.Category`, jamais la catégorie seule
([ADR-0026](../adr/0026-reach-a-category-only-through-the-rule-that-carries-it.fr.md)).

## Check id

Le nom que Roslyn donne au second argument de `[SuppressMessage(...)]` — ce que cette documentation
appelle l'[identifiant](#identifiant). Il peut porter un suffixe `:NomConvivial`, que la plateforme
tronque au premier deux-points et ignore par ailleurs.

## Conteneur

La classe dans laquelle les règles sont imbriquées, et donc le premier mot de chaque site
d'utilisation : `SonarRule.S1144`.

**Au singulier**, toujours — une règle, nommée. Le pluriel casse en outre le nom dérivé : un conteneur
finissant par `Rule` nomme aussi la [classe de catégories](#classe-de-catégories), si bien que
`SonarRule` donne `SonarCategory` là où `SonarRules` donnerait `SonarRulesCategory`.

Vos utilisateurs paient ce nom deux fois par suppression et ne peuvent pas le raccourcir. Ils peuvent
l'[aliaser](#alias).

## Descripteur

Un `DiagnosticDescriptor` : l'objet qu'un analyseur déclare pour décrire une de ses règles —
identifiant, titre, format de message, catégorie, gravité, lien d'aide.

La **source de vérité** de tout ce qu'un catalogue publie. `dcat` construit chaque analyseur qu'il
trouve et lit les descripteurs qu'ils déclarent réellement, plutôt que la documentation de l'éditeur à
leur sujet ([ADR-0009](../adr/0009-generate-catalog-content-from-analyzer-descriptors.fr.md)).

## Fondation

Le paquet `DiagnosticCatalog`. Trois attributs et rien d'autre : `[DiagnosticRule]`,
`[DiagnosticCategory]`, `[assembly: CatalogSource]`.

Référencé par chaque catalogue, et par quiconque déclare ses propres règles. Un catalogue qui le masque
derrière `PrivateAssets="all"` laisse ses consommateurs incapables de déclarer eux-mêmes des règles.

## Identifiant

La valeur du `Id` d'une règle — `S1144`, `CA1822`, `SA1000`, `DCAT0006`. Le second argument d'une
suppression, et le **seul** sur lequel Roslyn apparie.

D'ordinaire le nom du type de la règle, écrit `nameof(S1144)` pour que les deux ne puissent pas
diverger. Ils diffèrent quand l'identifiant canonique du diagnostic n'est pas un identifiant C# valide :
`RULE_001` portant `"RULE-001"`.

## Marqueur

`[DiagnosticRule]`, et ce qui fait d'un type une règle.

Apparié par **nom de métadonnée pleinement qualifié** — `DiagnosticCatalog.DiagnosticRuleAttribute` —
jamais par identité de symbole. C'est ce qui permet à un catalogue de déclarer sa propre copie plutôt
que de prendre une dépendance, et ce qui garde reconnaissable un attribut non résoluble au lieu de le
rendre silencieusement invisible.

## Miroir

Un catalogue décrivant l'analyseur de quelqu'un d'autre. Les treize catalogues d'éditeurs d'ici sont des
miroirs.

Un miroir ne peut que copier ce que sa source déclare **aujourd'hui**. Il ne peut pas rendre une
catégorie exacte par construction comme le peut un catalogue de
[première partie](first-party-analyzers.fr.md) — ce qui est la seule chose que posséder les deux vous
achète.

## Provenance

L'enregistrement, au niveau de l'assemblage, de la version amont qu'un catalogue reflète et de sa date
de génération :

```csharp
[assembly: CatalogSource(source: "…", sourceVersion: "…", generatedOn: "yyyy-MM-dd")]
```

La date est une `string` parce qu'un argument d'attribut doit être une constante de compilation et
qu'aucun type de date ne peut en être une.

Un catalogue de première partie n'en a pas besoin : il ne reflète rien.

## Règle

Un diagnostic d'analyseur, exprimé en classe statique portant `const string Id` et
`const string Category`, marquée `[DiagnosticRule]`.

Un **type**, pas une ligne d'un tableau ni une clé d'un fichier. Cette forme est imposée : un argument
d'attribut doit être une constante de compilation, une `const` vit sur un type, et donner à chaque
règle son propre type est ce qui fait que `S1144.Id` se lit comme une seule chose. Les exigences
complètes sont [le contrat de règle](rule-contract.fr.md).

## Site d'utilisation

Un endroit où une règle est référencée — en pratique, une suppression. Le pendant d'une *déclaration*,
qui est là où la règle est déclarée.

La distinction traverse les diagnostics : `DCAT0001`, `DCAT0006`, `DCAT0007` et `DCAT0009` regardent
les sites d'utilisation ; `DCAT0002`, `DCAT0003` et `DCAT0004` regardent les déclarations. Ils
diffèrent aussi sur le code généré, ce qui explique qu'ils soient livrés en deux classes d'analyseur.

## Suppression

Une application de `[SuppressMessage(...)]` ou `[UnconditionalSuppressMessage(...)]` — faire taire un
avertissement, jamais effacer du code.

L'ordinaire est `[Conditional("CODE_ANALYSIS")]` et n'est pas émise dans votre assemblage.
L'inconditionnelle l'est, précisément pour que le [*trimmer*](#trimmer) puisse la lire.

## Train de release

Un groupe de scopes qui versionne, tagge et publie ensemble. `lib`, `cli`, `sonar`, `netanalyzers`,
`stylecop`.

Un projet en rejoint un en déclarant `<ReleaseTrain>` dans son propre `.csproj`, et cette déclaration
est toute l'appartenance. Les trains existent pour que suivre le rythme de SonarSource n'entraîne
jamais la version de la fondation
([ADR-0002](../adr/0002-partition-releases-into-trains-by-commit-scope.fr.md),
[ADR-0015](../adr/0015-a-catalogues-version-runs-on-its-own-line.fr.md)).

## Trimmer

ILLink, l'outil .NET qui retire le code inatteignable d'une application publiée.

Il lit `UnconditionalSuppressMessage` dans votre **assemblage compilé**, bien après que le compilateur
a fini, et son décodeur n'accepte que les identifiants de la forme `IL####` — jetant tout le reste
purement et simplement. C'est la raison d'être de `DCAT0009` : une suppression de trim nommant une
règle Sonar ou StyleCop est un no-op qu'aucun autre outil de la chaîne ne signale.

---

<div align="center">
<a href="./faq.fr.md">← FAQ</a> · <a href="./README.fr.md">↑ Table des matières</a> · <a href="./architecture.fr.md">Architecture du dépôt →</a>
</div>
