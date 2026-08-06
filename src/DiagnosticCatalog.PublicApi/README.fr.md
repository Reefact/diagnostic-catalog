# DiagnosticCatalog.PublicApi

🌍 **Langues :**  
🇬🇧 [English](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.PublicApi/README.en.md) | 🇫🇷 Français (ce fichier)

Les règles de **suivi d'API publique** (`RS00xx`) sous forme de constantes fortement référencées,
pour que `SuppressMessageAttribute` prenne des références vérifiées à la compilation plutôt que des
chaînes magiques.

<!-- mirror:begin -->
> ## 🪞 Reflète `Microsoft.CodeAnalysis.PublicApiAnalyzers 5.6.0`
>
> **23 règles, 1 catégories**, chaque identifiant et chaque
> catégorie lus dans les analyseurs de cette version. Régénéré le 2026-08-05.
<!-- mirror:end -->

> Non officiel. Sans affiliation avec Microsoft, ni approbation ni support de sa part.

## Pourquoi

Vingt-trois règles, une seule catégorie — et **quatre paires qui partagent un titre identique au
caractère près**.

| Ces deux règles | Portent toutes deux le titre |
| --- | --- |
| `RS0022` et `RS0061` | *Constructor make noninheritable base class inheritable.* |
| `RS0026` et `RS0059` | *Do not add multiple public overloads with optional parameters.* |
| `RS0027` et `RS0060` | *API with optional parameter(s) should have the most parameters amongst its public overloads.* |
| `RS0037` et `RS0056` | *Enable tracking of nullability of reference types in the declared API.* |

La première de chaque paire concerne votre surface **publique** ; la seconde votre surface
**interne**. L'analyseur suit les deux — `PublicAPI.Shipped.txt` d'un côté,
`InternalAPI.Shipped.txt` de l'autre — et les titres ne disent pas laquelle est laquelle. Les liens
d'aide non plus : les deux membres de chaque paire pointent vers la même URL.

**Seul l'identifiant les distingue.** C'est-à-dire précisément la chaîne qu'on retape de mémoire,
depuis une infobulle d'IDE qui affiche un titre non unique, ou depuis un log de build deux cents
lignes plus haut.

```csharp
[SuppressMessage("ApiDesign", "RS0037:Enable tracking of nullability...", ...)]  // laquelle ?
```

Trompez-vous d'identifiant et la suppression ne fait silencieusement rien — l'avertissement reste.
Trompez-vous de catégorie et **il ne se passe rien du tout**, jamais : la plateforme .NET ne lit
jamais cet argument, donc aucune erreur, aucun avertissement et aucun test en échec ne vous le dira.

```csharp
using DiagnosticCatalog.PublicApi;

[SuppressMessage(
    PublicApiRule.RS0037.Category,
    PublicApiRule.RS0037.Id,
    Justification = "La surface publique est annotée ; l'interne est suivie mais pas annotée.")]
```

## Qui exécute ces règles, et pourquoi la réponse diffère ici

Tous les autres catalogues de cette famille reflètent un analyseur que vous n'avez pas choisi — il
arrive transitivement avec un framework de test, ou dans le SDK .NET, ou par un pack de ciblage.
**Celui-ci est différent, et autant le dire franchement :**
`Microsoft.CodeAnalysis.PublicApiAnalyzers` est un `PackageReference` explicite. Personne ne l'obtient
par accident.

Ce qui lui donne sa place ici, c'est ce qui se passe *après* ce choix. `RS0016` se déclenche **une
fois par membre absent de la surface déclarée** : activer l'analyseur sur une bibliothèque existante
ne produit pas un avertissement, il en produit des centaines, en un seul build. La plupart se
règlent en écrivant les fichiers d'API. Le reste — un membre délibérément non déclaré, un générique
que l'outil rend sous une forme que le fichier ne sait pas exprimer — se supprime en source, avec une
`Justification`, et y reste des années.

C'est une population durable d'identifiants de règle tapés à la main, dans une famille où quatre
titres sont ambigus et où les identifiants font `RS0016`, `RS0017`, `RS0022`, `RS0024` — proches,
non contigus, et faciles à intervertir.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.PublicApi" Version="1.0.0" />
```

Les constantes sont tout ce que ce paquet déclare — il ne suit pas votre API, c'est le travail de
l'analyseur et ce catalogue ne l'exécute jamais. Cette référence reste malgré tout la seule dont
vous avez besoin pour être vérifié : ce paquet dépend de `DiagnosticCatalog`, qui porte les
analyseurs `DCAT` et leurs correctifs à côté de ses attributs, si bien que les vérifications des
déclarations de règles et de leurs sites d'utilisation arrivent avec lui. Une suppression littérale
qu'une référence de catalogue remplacerait est une erreur par défaut, et un correctif la réécrit
pour vous.

## Ce que contient le paquet

23 règles dans une seule catégorie, `ApiDesign`. Chaque règle porte un lien d'aide, mais il n'existe
que **deux destinations distinctes** : dix-neuf pointent vers la page d'aide partagée de l'analyseur,
et les quatre règles sur les paramètres optionnels vers un document de conception.

L'ensemble se sépare nettement en deux, et c'est la forme à connaître :

| Surface | Règles | |
| --- | ---: | --- |
| Publique | 11 | `RS0016`, `RS0017`, `RS0022`, `RS0024`, `RS0025`, `RS0026`, `RS0027`, `RS0036`, `RS0037`, `RS0041`, `RS0048` |
| Interne | 11 | `RS0051`–`RS0061` |
| L'une ou l'autre | 1 | `RS0050` *API is marked as removed but it exists in source code* |

```csharp
[DiagnosticRule]
public static class RS0016
{
    public const string Id = nameof(RS0016);
    public const string Category = PublicApiCategory.ApiDesign;
    public const string HelpLinkUri = "https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/PublicApiAnalyzers/PublicApiAnalyzers.Help.md";
}
```

## Pas les autres règles `RS`

Trois paquets Microsoft émettent des règles `RS`, et les identifiants se répartissent proprement
entre eux :

| Paquet | Identifiants | Catalogue |
| --- | --- | --- |
| `Microsoft.CodeAnalysis.PublicApiAnalyzers` | `RS0016`–`RS0061` | **celui-ci**, les 23 |
| `Microsoft.CodeAnalysis.Analyzers` | `RS1xxx`, `RS2xxx` | [`DiagnosticCatalog.Roslyn`](https://www.nuget.org/packages/DiagnosticCatalog.Roslyn) |
| `Microsoft.CodeAnalysis.BannedApiAnalyzers` | `RS0030`, `RS0031`, `RS0035` | pas catalogué |

Deux catalogues plutôt qu'un parce qu'un catalogue reflète un paquet : le manifeste prend un seul
identifiant de paquet, et `[assembly: CatalogSource]` enregistre une seule source et une seule
version. Ce qui les a tenus séparés un temps, c'est l'icône — un badge porte le préfixe de règle du
catalogue, plafonné à trois lettres, donc les deux voulaient `RS` et aucune icône n'aurait pu les
départager. Un badge dont le préfixe est déjà en service nomme à la place le sujet du catalogue, et
le préfixe reste à celui qui le publie
([ADR-0035](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0035-badge-a-shared-prefix-catalogue-with-its-subject.fr.md)) :
`RS` est à `DiagnosticCatalog.Roslyn`, et celui-ci porte `API`.

## Catégories déclarées une fois

`PublicApiCategory` porte chaque catégorie une seule fois, et les règles la référencent. Avec une
seule catégorie cela rapporte peu aujourd'hui ; cela ne coûte rien et c'est ce que fait chaque
catalogue ici, donc le jour où l'amont en ajoutera une seconde, rien de la forme ne changera. C'est
**interne par conception** : une suppression atteint une catégorie par la règle qui la porte,
`PublicApiRule.RS0016.Category`, jamais par la constante de catégorie seule
([ADR-0026](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.fr.md)).

## Limites

`[SuppressMessage]` ne peut pas supprimer les avertissements du **compilateur** — `CS0219` et
consorts demandent `#pragma warning disable`, qui prend des identifiants nus et ne peut donc jamais
référencer une constante.

Deux de ces règles sont signalées contre un **projet** plutôt que contre un nœud de syntaxe :
`RS0048` *Missing shipped or unshipped public API file* et `RS0058`, son jumeau interne. On y répond
en ajoutant le fichier ou par une entrée `.editorconfig`, dont aucune ne peut prendre une constante.
Là où `[SuppressMessage]` s'applique, les constantes d'ici fonctionnent ; là où il ne s'applique pas,
aucun catalogue ne peut aider.

## Comment il est produit

Pas transcrit depuis la documentation. Le générateur lit les métadonnées des assemblages d'analyse
pour les types marqués `[DiagnosticAnalyzer]`, les construit, et lit les instances de
`DiagnosticDescriptor` qu'ils déclarent réellement — la seule source qui ne peut pas avoir dérivé, et
ce qui a fait apparaître les quatre titres dupliqués ci-dessus. L'ensemble provient d'**un seul type
d'analyseur** déclarant vingt-trois descripteurs.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package Microsoft.CodeAnalysis.PublicApiAnalyzers --package-version latest \
    --namespace DiagnosticCatalog.PublicApi --container PublicApiRule \
    --output src/DiagnosticCatalog.PublicApi/PublicApiRules.g.cs
```

## Comment il reste à jour

Un workflow nocturne régénère chaque catalogue depuis son paquet amont et ouvre une pull request
quand quoi que ce soit que le catalogue publie a bougé. Il ne
publie jamais : une catégorie ou un identifiant qui change en amont change un contrat publié, et
puisque la plateforme ne lit jamais la catégorie d'une suppression, une valeur fausse fusionnée sans
relecture ne produirait aucun symptôme nulle part. Un humain lit le diff.

**Une règle retirée en amont n'est jamais supprimée.** Elle est conservée et marquée `[Obsolete]` en
nommant la version qui l'a retirée, pour qu'un projet qui la référence encore reçoive un
avertissement `CS0618` lui disant d'enlever la suppression — plutôt qu'une erreur dure venant d'un
membre disparu. Les consommateurs incorporent les valeurs des constantes à leur propre compilation,
donc en supprimer une casse leur recompilation.

## Comment il arrive sur nuget.org

Ce catalogue roule sur le [train de release](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md) `publicapi`
et se versionne indépendamment de la fondation, pour pouvoir suivre les versions de
Microsoft.CodeAnalysis.PublicApiAnalyzers sans entraîner quoi que ce soit d'autre.

La publication ne fait pas partie du nocturne. Un mainteneur pousse une étiquette
`publicapi-vX.Y.Z`, et le workflow de release empaquette le paquet, y intègre un SBOM SPDX, et publie
via le [Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) de
NuGet avec une provenance de build signée — aucune clé d'API à longue durée de vie n'existe nulle
part pour fuiter.

## Voir aussi

Chaque catalogue publié par ce dépôt est listé au même endroit — choisissez celui qui correspond à
un analyseur que vous exécutez :

**[Les catalogues disponibles](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/README.fr.md#-les-catalogues-disponibles)**

**Vous voulez un catalogue à vous ?** Les règles de votre analyseur, ou un jeu de règles interne, se
déclarent exactement comme celles-ci : une classe statique de constantes marquée `[DiagnosticRule]`,
référencée par les consommateurs au lieu d'être retapée. Ce marqueur est livré par
[`DiagnosticCatalog`](https://www.nuget.org/packages/DiagnosticCatalog), la fondation sur laquelle ce catalogue est
bâti, et son README est le guide.

## Documentation

Pour utiliser un catalogue, dans l'ordre où le travail se fait :

- [**Démarrage**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/getting-started.fr.md)
  — dix minutes : référencer ce paquet, réécrire une suppression, la casser exprès et regarder le
  compilateur l'attraper.
- [**Écrire des suppressions que le compilateur vérifie**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/writing-suppressions.fr.md)
  — la version complète, migration des littéraux existants comprise.
- [**Publier un catalogue**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/authoring-a-catalogue.fr.md)
  — le contrat structurel, et comment en livrer un pour les règles de votre propre analyseur.
- [**Configuration**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/configuration.fr.md)
  — chaque clé de sévérité, l'interrupteur par catégorie, et l'erreur de `PrivateAssets` qui fait
  tout taire.
- [**Dépannage**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/troubleshooting.fr.md)
  — par symptôme : rien n'est signalé, `CS0117`, `CS0618` après une montée de version.

La [**carte de la documentation**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/README.fr.md)
choisit une page selon ce que vous cherchez à faire ; chaque guide existe en anglais et en français.
La [**spécification**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/specification.fr.md)
en est la version normative.

## Licence

Apache-2.0. Les identifiants de règle, catégories, titres et liens d'aide sont lus dans un analyseur
Microsoft, lui-même sous licence MIT.
