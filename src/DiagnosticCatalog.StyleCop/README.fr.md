# DiagnosticCatalog.StyleCop

🌍 **Langues :**  
🇬🇧 [English](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.StyleCop/README.en.md) | 🇫🇷 Français (ce fichier)

Les règles **StyleCop.Analyzers** sous forme de constantes fortement référencées, pour que
`SuppressMessageAttribute` prenne des références vérifiées à la compilation plutôt que des chaînes magiques.

<!-- mirror:begin -->
> ## 🪞 Reflète `StyleCop.Analyzers.Unstable 1.2.0.556`
>
> **197 règles, 8 catégories**, chaque identifiant et chaque
> catégorie lus dans les analyseurs de cette version. Régénéré le 2026-07-31.
<!-- mirror:end -->

> Non officiel. Sans affiliation avec le projet StyleCop.Analyzers ni approbation de sa part.

## Pourquoi

StyleCop illustre le propos mieux que la plupart des analyseurs : ses catégories sont des chaînes
en forme d'espace de noms que personne ne devinerait jamais.

```csharp
[SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1000", Justification = "...")]
```

Trompez-vous d'identifiant et la suppression ne fait silencieusement rien — l'avertissement reste,
tout simplement. Trompez-vous de catégorie et **il ne se passe rien du tout**, jamais : la plateforme
.NET ne lit jamais cet argument, donc rien ne vous le dira jamais. Auriez-vous écrit
`"StyleCop.CSharp.SpacingRules"` de mémoire ? Ou `"Spacing"`, ou `"Style"` ?

```csharp
using DiagnosticCatalog.StyleCop;

[SuppressMessage(
    StyleCopRule.SA1000.Category,
    StyleCopRule.SA1000.Id,
    Justification = "Generated code follows a different spacing convention.")]
```

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.StyleCop" Version="0.1.0" />
```

C'est la seule référence dont vous avez besoin. Ce paquet dépend de `DiagnosticCatalog`, qui porte
les analyseurs `DCAT` et leurs correctifs à côté de ses attributs, si bien que référencer ce
catalogue est ce qui active les vérifications qui valident les déclarations de règles et leurs
sites d'utilisation. Une suppression littérale qu'une référence de catalogue remplacerait est une
erreur par défaut, et un correctif la réécrit pour vous.

## Ce que contient le paquet

193 règles réparties sur 8 catégories, toutes de la forme `StyleCop.CSharp.*Rules` :
`DocumentationRules`, `LayoutRules`, `MaintainabilityRules`, `NamingRules`,
`OrderingRules`, `ReadabilityRules`, `SpacingRules`, `SpecialRules`.

Chaque règle porte son lien d'aide, parce que StyleCop renseigne `HelpLinkUri` sur les 193
descripteurs :

```csharp
[DiagnosticRule]
public static class SA1000
{
    public const string Id = nameof(SA1000);
    public const string Category = StyleCopCategory.StyleCopCSharpSpacingRules;
    public const string HelpLinkUri = "https://github.com/DotNetAnalyzers/StyleCopAnalyzers/blob/master/documentation/SA1000.md";
}
```

Chaque règle porte son titre amont comme commentaire de documentation. Les descriptions de règles ne
sont pas redistribuées — le lien d'aide vous y conduit.

## Une note sur les versions

Ce catalogue reflète **StyleCop.Analyzers.Unstable 1.2.0.556** — la ligne `1.2.0-beta`,
qui est ce que les projets installent réellement.

C'est un choix délibéré, et il vaut d'être énoncé clairement. La dernière version *stable* de
StyleCop.Analyzers est `1.1.118`, publiée en **avril 2019** ; le projet vit sur `1.2.0-beta`
depuis. Refléter la stable revenait à refléter une version que presque personne n'utilise, et ce
n'était pas seulement incomplet — `SA1413` est déclarée sous
`StyleCop.CSharp.ReadabilityRules` dans la stable et sous
`StyleCop.CSharp.MaintainabilityRules` dans la bêta. Un consommateur sur la bêta écrivant
`StyleCopRule.SA1413.Category` depuis un catalogue fondé sur la stable obtiendrait la mauvaise
chaîne, et rien dans sa compilation ne le lui dirait jamais. Être à jour vaut mieux qu'être
nominalement stable quand une catégorie est fausse de toute façon (ADR-0016).

Notez l'identifiant du paquet : `StyleCop.Analyzers` 1.2.0-beta est un métapaquet ne portant aucun
assembly d'analyse propre — les règles vivent dans `StyleCop.Analyzers.Unstable`, dont les versions
ne portent pas d'étiquette de préversion.

**Si vous êtes sur la stable `1.1.118`**, utilisez `DiagnosticCatalog.StyleCop` **0.2.0**, la
dernière version à la refléter. Ses 193 règles sont un sous-ensemble des 197 d'ici, aucune n'a été
retirée, et seule `SA1413` a changé de catégorie.

L'assembly consigne exactement ce qu'il reflète :

```csharp
[assembly: CatalogSource(
    source:        "StyleCop.Analyzers.Unstable",
    sourceVersion: "1.2.0.556",
    generatedOn:   "2026-07-31")]
```

## Catégories déclarées une seule fois

Un catalogue répète très peu de catégories distinctes sur un très grand nombre de règles. Chacune est
déclarée une fois dans `StyleCopCategory` et les règles s'y réfèrent, donc il y a une source unique par valeur :

```csharp
[DiagnosticCategory]
public static class StyleCopCategory
{
    public const string StyleCopCSharpSpacingRules = "StyleCop.CSharp.SpacingRules";
}

[DiagnosticRule]
public static class SA1000
{
    public const string Id = nameof(SA1000);
    public const string Category = StyleCopCategory.StyleCopCSharpSpacingRules;
}
```

Une `const` initialisée depuis une autre `const` reste une constante de compilation, donc
`StyleCopRule.SA1000.Category` demeure valide comme argument d'attribut et se replie toujours en
`"StyleCop.CSharp.SpacingRules"` dans les métadonnées. L'indirection ne coûte rien.

`StyleCopCategory` est aussi utilisable seule — IntelliSense dessus liste exactement les 8 catégories
que cet analyseur utilise réellement.

## Comment il est produit

Pas transcrit depuis la documentation. Le générateur charge les assemblys d'analyse, construit les
analyseurs qu'ils marquent de `[DiagnosticAnalyzer]`, et lit les instances de `DiagnosticDescriptor`
qu'ils déclarent réellement — la seule source qui ne puisse pas avoir dérivé.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package StyleCop.Analyzers.Unstable --package-version latest \
    --namespace DiagnosticCatalog.StyleCop --container StyleCopRule \
    --output src/DiagnosticCatalog.StyleCop/StyleCopRules.g.cs
```

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

Ce catalogue roule sur le [train de release](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md) `stylecop` et se versionne
indépendamment de la fondation, pour pouvoir suivre les versions de StyleCop.Analyzers sans entraîner quoi que ce soit d'autre.

La publication ne fait pas partie du nocturne. Un mainteneur pousse une étiquette `stylecop-vX.Y.Z`, et
le workflow de release empaquette le paquet, y intègre un SBOM SPDX, et publie via le
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) de NuGet
avec une provenance de build signée — aucune clé d'API à longue durée de vie n'existe nulle part pour
fuiter. La moitié empaquetage de ce pipeline est répétée à chaque pull request, si bien qu'une release
ne l'exerce jamais pour la première fois sur une étiquette.

## Limites

`[SuppressMessage]` ne peut pas supprimer les avertissements du **compilateur** — `CS0219` et
consorts demandent `#pragma warning disable`, qui prend des identifiants nus et ne peut donc jamais
référencer une constante. Ce paquet ne couvre que les règles d'analyse `SAxxxx`.

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
  — la montée en sévérité, *Corriger toutes les occurrences*, le cadrage par dossier, et dans quel
  ordre convertir.
- [**Configuration**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/configuration.fr.md)
  — chaque clé de sévérité, le commutateur par catégorie, et l'erreur de `PrivateAssets` qui fait
  tout taire.
- [**Dépannage**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/troubleshooting.fr.md)
  — par symptôme : rien n'est signalé, `CS0117`, `CS0618` après une montée de version.

La [**carte de la documentation**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/README.fr.md)
choisit une page selon ce que vous cherchez à faire ; chaque guide existe en anglais et en français. La
[**spécification**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/specification.fr.md)
en est la version normative.

## Licence

Apache-2.0. Les identifiants, catégories et liens d'aide des règles sont des faits à propos d'un
analyseur tiers, lui-même sous licence MIT.
