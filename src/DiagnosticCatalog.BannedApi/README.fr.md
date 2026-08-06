# DiagnosticCatalog.BannedApi

🌍 **Langues :**  
🇬🇧 [English](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.BannedApi/README.en.md) | 🇫🇷 Français (ce fichier)

Les règles d'**API bannies** (`RS0030`, `RS0031`, `RS0035`) sous forme de constantes fortement
référencées, pour que `SuppressMessageAttribute` prenne des références vérifiées à la compilation
plutôt que des chaînes magiques.

<!-- mirror:begin -->
> ## 🪞 Reflète `Microsoft.CodeAnalysis.BannedApiAnalyzers 5.6.0`
>
> **3 règles, 1 catégorie**, chaque identifiant et chaque
> catégorie lus dans les analyseurs de cette version. Régénéré le 2026-08-05.
<!-- mirror:end -->

> Non officiel. Sans affiliation avec Microsoft, ni approbation ni support de sa part.

## Pourquoi

Trois règles. Le plus petit catalogue de cette famille, et de loin — et ce nombre ne dit
absolument rien de ce que vous en verrez.

`RS0030` *Do not use banned APIs* se déclenche **une fois par site d'appel de ce que vous avez
banni**. Rien dans le paquet n'en décide ; c'est un `BannedSymbols.txt` que vous avez écrit. Bannissez
`DateTime.Now` sur une base de code qui l'appelle depuis huit ans et vous obtenez un diagnostic par
appel, tous `RS0030`. Ceux que vous migrez dans l'après-midi disparaissent. Ceux qui tiennent à un
format de sérialisation, à une signature tierce ou à une release pas encore coupée reçoivent une
suppression avec une justification, et ils restent.

C'est tout l'argument des constantes : une règle à trois identifiants et des milliers de sites est
exactement l'endroit où une faute de frappe dans l'identifiant survit à la relecture, parce que
personne ne lit la quatorzième suppression aussi attentivement que la première.

```csharp
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", ...)]
```

Trompez-vous d'identifiant et la suppression ne fait silencieusement rien — l'avertissement reste.
Trompez-vous de catégorie et **il ne se passe rien du tout**, jamais : la plateforme .NET ne lit
jamais cet argument, donc aucune erreur, aucun avertissement et aucun test en échec ne vous le dira.

```csharp
using DiagnosticCatalog.BannedApi;

[SuppressMessage(
    BannedApiRule.RS0030.Category,
    BannedApiRule.RS0030.Id,
    Justification = "Le format de sérialisation fige cette surcharge ; la migrer casse le contrat.")]
```

## Qui exécute ces règles

Personne par accident. `Microsoft.CodeAnalysis.BannedApiAnalyzers` est un `PackageReference`
explicite, et il ne fait rien du tout tant que quelqu'un n'a pas écrit le `BannedSymbols.txt` qui lui
dit quoi bannir.

Il est ici pour la même raison que `DiagnosticCatalog.PublicApi` : ce qu'une équipe adopte
délibérément, elle vit ensuite des années avec. Un bannissement s'adopte précisément *parce que*
l'API est encore appelée, donc les suppressions arrivent avec le bannissement et survivent à ceux qui
les ont écrites.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.BannedApi" Version="1.0.0" />
```

Ce paquet ne fournit que les constantes. Ce qui est banni, c'est `BannedSymbols.txt`, que ce
catalogue ne lit jamais.

## Ce que contient le paquet

3 règles dans une seule catégorie, `ApiDesign` :

| Règle | Ce qu'elle signale |
| --- | --- |
| `RS0030` | Un appel à un symbole listé dans `BannedSymbols.txt`. Celle que vous rencontrerez. |
| `RS0031` | Le fichier des symboles bannis liste deux fois la même chose. |
| `RS0035` | Un symbole interne atteint depuis l'extérieur de son espace de noms restreint. |

**Deux des trois portent un lien d'aide ; `RS0035` n'en déclare aucun.** C'est le descripteur de
l'éditeur, pas un oubli d'ici — le catalogue n'émet `HelpLinkUri` que là où il en existe un, donc la
constante est absente plutôt que vide.

```csharp
[DiagnosticRule]
public static class RS0030
{
    public const string Id = nameof(RS0030);
    public const string Category = BannedApiCategory.ApiDesign;
    public const string HelpLinkUri = "https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.BannedApiAnalyzers/BannedApiAnalyzers.Help.md";
}
```

## La famille `RS`, désormais complète

Trois paquets Microsoft émettent des règles `RS`, et les trois sont catalogués :

| Paquet | Identifiants | Catalogue |
| --- | --- | --- |
| `Microsoft.CodeAnalysis.BannedApiAnalyzers` | `RS0030`, `RS0031`, `RS0035` | **celui-ci** |
| `Microsoft.CodeAnalysis.PublicApiAnalyzers` | `RS0016`–`RS0061` | [`DiagnosticCatalog.PublicApi`](https://www.nuget.org/packages/DiagnosticCatalog.PublicApi) |
| `Microsoft.CodeAnalysis.Analyzers` | `RS1xxx`, `RS2xxx` | [`DiagnosticCatalog.Roslyn`](https://www.nuget.org/packages/DiagnosticCatalog.Roslyn) |

Les identifiants se répartissent proprement, donc aucune règle n'est ambiguë quant à son catalogue.
Trois catalogues plutôt qu'un parce qu'un catalogue reflète un paquet : le manifeste prend un seul
identifiant de paquet, et `[assembly: CatalogSource]` enregistre une seule source et une seule
version. Ce sont leurs badges — `BAN`, `API`, `RS` — qui départagent les trois icônes. Un badge porte le
préfixe de règle, plafonné à trois lettres, et les trois voudraient sinon `RS` ; lorsque le préfixe
est déjà en service, le badge nomme à la place le sujet du catalogue, et le préfixe reste à celui qui
le publie
([ADR-0035](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0035-badge-a-shared-prefix-catalogue-with-its-subject.fr.md)).

**Ce catalogue et `DiagnosticCatalog.PublicApi` déclarent la même chaîne de catégorie**, `ApiDesign`,
depuis deux paquets différents. Ce sont des constantes séparées sur des conteneurs séparés, et c'est
ce qui les garde indépendants le jour où l'un des deux éditeurs bouge.

## Catégories déclarées une fois

`BannedApiCategory` porte chaque catégorie une seule fois, et les règles la référencent. C'est
**interne par conception** : une suppression atteint une catégorie par la règle qui la porte,
`BannedApiRule.RS0030.Category`, jamais par la constante de catégorie seule
([ADR-0026](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.fr.md)).

## Limites

`[SuppressMessage]` ne peut pas supprimer les avertissements du **compilateur** — `CS0219` et
consorts demandent `#pragma warning disable`, qui prend des identifiants nus et ne peut donc jamais
référencer une constante.

`RS0031` est signalée contre `BannedSymbols.txt` plutôt que contre du code, donc aucun attribut ne
l'atteint ; la réponse y est de corriger la ligne en double. Là où `[SuppressMessage]` s'applique — et
pour `RS0030` c'est chaque site d'appel — les constantes d'ici fonctionnent.

## Comment il est produit

Pas transcrit depuis la documentation. Le générateur lit les métadonnées des assemblages d'analyse
pour les types marqués `[DiagnosticAnalyzer]`, les construit, et lit les instances de
`DiagnosticDescriptor` qu'ils déclarent réellement — c'est ainsi que l'absence de lien d'aide sur
`RS0035` est un fait mesuré plutôt qu'une supposition.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package Microsoft.CodeAnalysis.BannedApiAnalyzers --package-version latest \
    --namespace DiagnosticCatalog.BannedApi --container BannedApiRule \
    --output src/DiagnosticCatalog.BannedApi/BannedApiRules.g.cs
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

Ce catalogue roule sur le [train de release](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md) `bannedapi`
et se versionne indépendamment de la fondation, pour pouvoir suivre les versions de
Microsoft.CodeAnalysis.BannedApiAnalyzers sans entraîner quoi que ce soit d'autre.

La publication ne fait pas partie du nocturne. Un mainteneur pousse une étiquette
`bannedapi-vX.Y.Z`, et le workflow de release empaquette le paquet, y intègre un SBOM SPDX, et publie
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
