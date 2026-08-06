# DiagnosticCatalog.NetAnalyzers

🌍 **Langues :**  
🇬🇧 [English](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.NetAnalyzers/README.en.md) | 🇫🇷 Français (ce fichier)

Les **règles d'analyse de code .NET (CA)** sous forme de constantes fortement référencées, pour que
`SuppressMessageAttribute` prenne des références vérifiées à la compilation plutôt que des chaînes magiques.

<!-- mirror:begin -->
> ## 🪞 Reflète `Microsoft.CodeAnalysis.NetAnalyzers 10.0.302`
>
> **318 règles, 10 catégories**, chaque identifiant et chaque
> catégorie lus dans les analyseurs de cette version. Régénéré le 2026-07-31.
<!-- mirror:end -->

## Pourquoi

Les deux arguments d'une suppression sont des chaînes magiques, et rien ne valide ni l'un ni l'autre :

```csharp
[SuppressMessage("Performance", "CA1822", Justification = "...")]
```

Trompez-vous d'identifiant et la suppression ne fait silencieusement rien — l'avertissement reste,
tout simplement. Trompez-vous de catégorie et **il ne se passe rien du tout**, jamais : la plateforme
.NET ne lit jamais cet argument, donc aucune compilation, aucun test et aucun outil ne vous le dira.

```csharp
using DiagnosticCatalog.NetAnalyzers;

[SuppressMessage(
    NetAnalyzersRule.CA1822.Category,
    NetAnalyzersRule.CA1822.Id,
    Justification = "Kept as an instance member for the public API contract.")]
```

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.NetAnalyzers" Version="0.1.0" />
```

C'est la seule référence dont vous avez besoin. Ce paquet dépend de `DiagnosticCatalog`, qui porte
les analyseurs `DCAT` et leurs correctifs à côté de ses attributs, si bien que référencer ce
catalogue est ce qui active les vérifications qui valident les déclarations de règles et leurs
sites d'utilisation. Une suppression littérale qu'une référence de catalogue remplacerait est une
erreur par défaut, et un correctif la réécrit pour vous.

## Ce que contient le paquet

318 règles réparties sur 10 catégories : `Design`, `Documentation`, `Globalization`,
`Interoperability`, `Maintainability`, `Naming`, `Performance`, `Reliability`,
`Security`, `Usage`.

Chaque règle porte son lien d'aide, parce que les analyseurs .NET renseignent `HelpLinkUri` sur les
318 descripteurs :

```csharp
[DiagnosticRule]
public static class CA1822
{
    public const string Id = nameof(CA1822);
    public const string Category = NetAnalyzersCategory.Performance;
    public const string HelpLinkUri = "https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1822";
}
```

Chaque règle porte son titre amont comme commentaire de documentation, lu depuis le descripteur en
culture invariante pour que le fichier généré ne dépende pas de la machine qui l'a produit. Les
descriptions de règles ne sont pas redistribuées — le lien d'aide vous y conduit.

## Une note sur les versions

Contrairement à Sonar ou StyleCop, les analyseurs CA sont **intégrés au SDK .NET**. Quelles règles
votre projet obtient réellement dépend de votre version du SDK et d'`AnalysisLevel`, pas d'un
`PackageReference` que vous contrôlez. Ce catalogue reflète
`Microsoft.CodeAnalysis.NetAnalyzers 10.0.302`, la dernière version stable au moment de la
génération ; l'assembly le consigne exactement :

```csharp
[assembly: CatalogSource(
    source:        "Microsoft.CodeAnalysis.NetAnalyzers",
    sourceVersion: "10.0.302",
    generatedOn:   "2026-07-30")]
```

Si votre SDK livre un jeu d'analyseurs plus récent, les règles ajoutées depuis 10.0.302 n'y seront pas encore.

## Catégories déclarées une seule fois

Un catalogue répète très peu de catégories distinctes sur un très grand nombre de règles. Chacune est
déclarée une fois dans `NetAnalyzersCategory` et les règles s'y réfèrent, donc il y a une source unique par valeur :

```csharp
[DiagnosticCategory]
public static class NetAnalyzersCategory
{
    public const string Performance = "Performance";
}

[DiagnosticRule]
public static class CA1822
{
    public const string Id = nameof(CA1822);
    public const string Category = NetAnalyzersCategory.Performance;
}
```

Une `const` initialisée depuis une autre `const` reste une constante de compilation, donc
`NetAnalyzersRule.CA1822.Category` demeure valide comme argument d'attribut et se replie toujours en
`"Performance"` dans les métadonnées. L'indirection ne coûte rien.

`NetAnalyzersCategory` est aussi utilisable seule — IntelliSense dessus liste exactement les 10 catégories
que cet analyseur utilise réellement.

## Comment il est produit

Pas transcrit depuis la documentation. Le générateur charge les assemblys d'analyse, construit les
analyseurs qu'ils marquent de `[DiagnosticAnalyzer]`, et lit les instances de `DiagnosticDescriptor`
qu'ils déclarent réellement — la seule source qui ne puisse pas avoir dérivé.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package Microsoft.CodeAnalysis.NetAnalyzers --package-version latest \
    --namespace DiagnosticCatalog.NetAnalyzers --container NetAnalyzersRule \
    --output src/DiagnosticCatalog.NetAnalyzers/NetAnalyzersRules.g.cs
```

Le paquet répartit ses analyseurs entre `analyzers/dotnet/` (neutre du point de vue du langage, la
plupart des règles), `analyzers/dotnet/cs/` et `analyzers/dotnet/vb/`. Le générateur prend les deux
premiers et exclut le troisième, pour qu'aucune règle Visual Basic ne fuite dans un catalogue C#.

## Comment il reste à jour

Un workflow nocturne régénère chaque catalogue depuis son paquet amont et ouvre une pull request
quand quoi que ce soit que le catalogue publie a bougé. Il ne
publie jamais : une catégorie ou un identifiant qui a changé en amont change un contrat publié, et
comme la plateforme ne lit jamais la catégorie d'une suppression, une valeur fausse fusionnée sans
relecture ne produirait de symptôme nulle part. Un humain lit le diff.

Les nuits où l'amont n'a pas bougé ne produisent rien du tout : le générateur compare sa propre
sortie précédente et laisse le fichier intact, `generatedOn` compris.

**Une règle retirée en amont n'est jamais supprimée.** Elle est conservée et marquée `[Obsolete]` en
nommant la version qui l'a abandonnée, pour qu'un projet qui la référence encore obtienne un
avertissement `CS0618` lui disant de retirer la suppression — plutôt qu'une erreur dure sur un membre
qui a disparu. Les consommateurs incorporent les valeurs de constantes à leur propre compilation, donc
en supprimer une casse leur recompilation.

Pour régénérer tous les catalogues d'un coup :

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate --manifest eng/catalogs.json
```

## Comment il arrive sur nuget.org

Ce catalogue roule sur le [train de release](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md) `netanalyzers` et se versionne indépendamment de
la fondation, pour pouvoir suivre les versions d'analyseurs du SDK .NET sans entraîner quoi que ce soit d'autre.

La publication ne fait pas partie du nocturne. Un mainteneur pousse une étiquette `netanalyzers-vX.Y.Z`, et le
workflow de release empaquette le paquet, y intègre un SBOM SPDX, et publie via le
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) de NuGet avec une provenance de build signée — aucune clé d'API à longue
durée de vie n'existe nulle part pour fuiter. La moitié empaquetage de ce pipeline est répétée à chaque
pull request, si bien qu'une release ne l'exerce jamais pour la première fois sur une étiquette.

## Limites

`[SuppressMessage]` ne peut pas supprimer les avertissements du **compilateur** — `CS0219` et
consorts demandent `#pragma warning disable`, qui prend des identifiants nus et ne peut donc jamais
référencer une constante. Ce paquet ne couvre que les règles d'analyse `CAxxxx`, ni `CSxxxx` ni `IDExxxx`.

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
  — la version complète, migration des littéraux que vous avez déjà comprise.
- [**Adopter un catalogue sur une base de code existante**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/adopting-a-catalogue.fr.md)
  — la montée en sévérité, *Corriger toutes les occurrences*, le cadrage par dossier, et dans quel ordre convertir.
- [**Configuration**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/configuration.fr.md)
  — chaque clé de sévérité, le commutateur par catégorie, et l'erreur de `PrivateAssets` qui
  fait tout taire.
- [**Dépannage**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/troubleshooting.fr.md)
  — par symptôme : rien n'est signalé, `CS0117`, `CS0618` après une montée de version.

La [**carte de la documentation**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/README.fr.md)
choisit une page selon ce que vous cherchez à faire ; chaque guide existe en anglais et en français. La
[**spécification**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/specification.fr.md)
en est la version normative.

## Licence

Apache-2.0. Les identifiants, catégories et liens d'aide des règles sont des faits à propos d'un
analyseur tiers, lui-même sous licence MIT.
