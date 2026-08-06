# DiagnosticCatalog.Xunit

🌍 **Langues :**  
🇬🇧 [English](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.Xunit/README.en.md) | 🇫🇷 Français (ce fichier)

Les règles **xunit.analyzers** sous forme de constantes fortement référencées, pour que
`SuppressMessageAttribute` prenne des références vérifiées à la compilation plutôt que des chaînes magiques.

<!-- mirror:begin -->
> ## 🪞 Reflète `xunit.analyzers 1.27.0`
>
> **90 règles, 3 catégories**, chaque identifiant et chaque
> catégorie lus dans les analyseurs de cette version. Régénéré le 2026-08-05.
<!-- mirror:end -->

> Non officiel. Sans affiliation avec le projet xUnit.net, ni approbation ni support de sa part.

## Pourquoi

Chaque projet de test xUnit exécute déjà ces analyseurs, et presque personne ne les a installés
exprès : `xunit` dépend de `xunit.analyzers`, ils arrivent donc avec le framework de test. C'est ce
qui fait de leurs règles celles que les gens suppriment réellement **dans le code source** — un test
qui affirme délibérément sur un littéral, une théorie dont les données ne peuvent pas être inlinées,
une assertion que l'analyseur préférerait voir écrite autrement. Ce sont des exceptions locales avec
une raison, ce qui est le travail d'une suppression plutôt que d'une entrée `.editorconfig`.

```csharp
[SuppressMessage("Assertions", "xUnit2013:Do not use equality check to check for collection size", ...)]
```

Trois chaînes, et rien n'en vérifie aucune. Trompez-vous d'identifiant et la suppression ne fait
silencieusement rien — l'avertissement reste, tout simplement. Trompez-vous de catégorie et **il ne se
passe rien du tout**, jamais : la plateforme .NET ne lit jamais cet argument, donc aucune erreur,
aucun avertissement et aucun test en échec ne vous le dira. Auriez-vous su que `xUnit2013` est
`"Assertions"` tandis que `xUnit1013` est `"Usage"` et `xUnit3000` `"Extensibility"` ?

```csharp
using DiagnosticCatalog.Xunit;

[SuppressMessage(
    XunitRule.xUnit2013.Category,
    XunitRule.xUnit2013.Id,
    Justification = "The count is the subject of this test.")]
```

Le jour où une règle passe dans une autre catégorie, la seconde version la suit et la première
reste à nommer une catégorie que la règle ne porte plus — en silence, et aussi longtemps que la
ligne survit.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.Xunit" Version="1.0.0" />
```

Ce paquet ne fournit que les constantes. Les vérifications qui valident les déclarations de règles et
leurs sites d'utilisation sont livrées à part dans `DiagnosticCatalog.Analyzers`.

## Ce que contient le paquet

90 règles réparties sur 3 catégories, et c'est le plus net des catalogues d'ici : chaque règle porte
le titre que déclare son descripteur, et **les 90 portent toutes un lien d'aide** vers les pages de
règles de xunit.net.

| Catégorie | Règles | De quoi elles parlent |
| --- | --- | --- |
| `Usage` | 54 | Comment tests, théories et leurs données sont déclarés — la plage `xUnit1xxx` |
| `Assertions` | 32 | Des assertions qui se liraient mieux écrites autrement — `xUnit2xxx` |
| `Extensibility` | 4 | Étendre le framework lui-même — `xUnit3xxx` |

```csharp
[DiagnosticRule]
public static class xUnit2013
{
    public const string Id = nameof(xUnit2013);
    public const string Category = XunitCategory.Assertions;
    public const string HelpLinkUri = "https://xunit.net/xunit.analyzers/rules/xUnit2013";
}
```

Les identifiants gardent la casse de l'éditeur, `xUnit2013` et non `XUnit2013`, parce que le nom de
membre d'un catalogue est l'identifiant que porte une suppression — le renommer pour coller à la
convention C# ferait diverger la constante et la chaîne qu'elle représente.

## Une note sur le fait que vous avez déjà ces analyseurs

Vous n'avez presque certainement pas besoin d'installer `xunit.analyzers` : `xunit` en dépend, donc un
projet de test a les règles que quelqu'un l'ait demandé ou non. Ce catalogue les nomme ; d'où elles
viennent regarde votre projet de test.

Cette arrivée transitive est aussi la raison d'être de ce catalogue. Une règle que vous avez choisi
d'activer se règle dans `.editorconfig` ; une règle qui arrive avec le framework se supprime à
l'unique endroit où elle a tort, avec une `Justification` à côté du test qui la mérite.

## Catégories déclarées une seule fois

`XunitCategory` détient chaque catégorie une fois, et les règles la référencent — si bien que
l'orthographe d'une catégorie n'existe qu'à un seul endroit. Elle est **interne par conception** : une
suppression atteint une catégorie au travers de la règle qui la porte, `XunitRule.xUnit2013.Category`,
et jamais au travers de la constante de catégorie seule. Les deux se replient sur la même chaîne
aujourd'hui et cessent de s'accorder le jour où xUnit déplace la règle
([ADR-0026](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.fr.md)).

## Comment il est produit

Pas transcrit depuis la documentation. Le générateur lit dans les métadonnées des assemblys d'analyse
les types qu'ils marquent de `[DiagnosticAnalyzer]`, construit ceux-là, et lit les instances de
`DiagnosticDescriptor` qu'ils déclarent réellement — la seule source qui ne puisse pas avoir dérivé.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package xunit.analyzers --package-version latest \
    --namespace DiagnosticCatalog.Xunit --container XunitRule \
    --output src/DiagnosticCatalog.Xunit/XunitRules.g.cs
```

## Comment il reste à jour

Un workflow nocturne régénère chaque catalogue depuis son paquet amont et ouvre une pull request
quand quoi que ce soit que le catalogue publie a bougé. Il ne
publie jamais : une catégorie ou un identifiant qui a changé en amont change un contrat publié, et
comme la plateforme ne lit jamais la catégorie d'une suppression, une valeur fausse fusionnée sans
relecture ne produirait de symptôme nulle part. Un humain lit le diff.

**Une règle retirée en amont n'est jamais supprimée.** Elle est conservée et marquée `[Obsolete]` en
nommant la version qui l'a abandonnée, pour qu'un projet qui la référence encore obtienne un
avertissement `CS0618` lui disant de retirer la suppression — plutôt qu'une erreur dure sur un membre
qui a disparu. Les consommateurs incorporent les valeurs de constantes à leur propre compilation, donc
en supprimer une casse leur recompilation.

## Comment il arrive sur nuget.org

Ce catalogue roule sur le [train de release](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md) `xunit` et se versionne
indépendamment de la fondation, pour pouvoir suivre les versions de xunit.analyzers sans entraîner quoi que ce soit d'autre.

La publication ne fait pas partie du nocturne. Un mainteneur pousse une étiquette `xunit-vX.Y.Z`, et
le workflow de release empaquette le paquet, y intègre un SBOM SPDX, et publie via le
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) de NuGet
avec une provenance de build signée — aucune clé d'API à longue durée de vie n'existe nulle part pour
fuiter. La moitié empaquetage de ce pipeline est répétée à chaque pull request, si bien qu'une release
ne l'exerce jamais pour la première fois sur une étiquette.

## Limites

`[SuppressMessage]` ne peut pas supprimer les avertissements du **compilateur** — `CS0219` et
consorts demandent `#pragma warning disable`, qui prend des identifiants nus et ne peut donc jamais
référencer une constante. Ce paquet ne couvre que les règles d'analyse `xUnitxxxx`.

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

Apache-2.0. Les identifiants, catégories, titres et liens d'aide des règles sont lus depuis un
analyseur tiers, lui-même sous licence Apache-2.0.
