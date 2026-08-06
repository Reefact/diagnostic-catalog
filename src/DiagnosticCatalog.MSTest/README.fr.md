# DiagnosticCatalog.MSTest

🌍 **Langues :**  
🇬🇧 [English](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.MSTest/README.en.md) | 🇫🇷 Français (ce fichier)

Les règles **MSTest.Analyzers** sous forme de constantes fortement référencées, pour que
`SuppressMessageAttribute` prenne des références vérifiées à la compilation plutôt que des chaînes magiques.

<!-- mirror:begin -->
> ## 🪞 Reflète `MSTest.Analyzers 4.3.3`
>
> **62 règles, 3 catégories**, chaque identifiant et chaque
> catégorie lus dans les analyseurs de cette version. Régénéré le 2026-08-05.
<!-- mirror:end -->

> Non officiel. Sans affiliation avec Microsoft, ni approbation ni support de sa part.

## Pourquoi

Chaque projet MSTest exécute ces analyseurs, et presque personne ne les a installés :
`MSTest.TestFramework` dépend de `MSTest.Analyzers`, si bien que le métapaquet `MSTest` et
`MSTest.TestAdapter` les entraînent tous les deux. Ils arrivent avec le framework de test.

C'est ce qui fait de leurs règles celles que les gens suppriment **dans le code source**. Une règle
que vous avez activée se règle dans `.editorconfig` ; une règle arrivée avec le framework reçoit une
exception à l'unique endroit où elle a tort, avec une `Justification` à côté du test qui la mérite.

MSTest aiguise cela plus que les autres frameworks de test, parce qu'il livre des règles qui **se
contredisent exprès** : `MSTEST0019` préfère les méthodes `TestInitialize` aux constructeurs et
`MSTEST0020` préfère les constructeurs aux méthodes `TestInitialize` ; `MSTEST0021` préfère `Dispose`
à `TestCleanup` et `MSTEST0022` préfère l'inverse. Vous choisissez un camp, et l'autre règle de la
paire est une règle dont vous répondrez toute la vie du projet.

```csharp
[SuppressMessage("Usage", "MSTEST0037:Use proper 'Assert' methods", ...)]
```

Trois chaînes, et rien n'en vérifie aucune. Trompez-vous d'identifiant et la suppression ne fait
silencieusement rien — l'avertissement reste, tout simplement. Trompez-vous de catégorie et **il ne se
passe rien du tout**, jamais : la plateforme .NET ne lit jamais cet argument, donc aucune erreur,
aucun avertissement et aucun test en échec ne vous le dira.

```csharp
using DiagnosticCatalog.MSTest;

[SuppressMessage(
    MSTestRule.MSTEST0037.Category,
    MSTestRule.MSTEST0037.Id,
    Justification = "The overload this rule suggests does not exist for this comparer.")]
```

Le jour où une règle passe dans une autre catégorie, la seconde version la suit et la première
reste à nommer une catégorie que la règle ne porte plus — en silence, et aussi longtemps que la
ligne survit.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.MSTest" Version="1.0.0" />
```

C'est la seule référence dont vous avez besoin. Ce paquet dépend de `DiagnosticCatalog`, qui porte
les analyseurs `DCAT` et leurs correctifs à côté de ses attributs, si bien que référencer ce
catalogue est ce qui active les vérifications qui valident les déclarations de règles et leurs
sites d'utilisation. Une suppression littérale qu'une référence de catalogue remplacerait est une
erreur par défaut, et un correctif la réécrit pour vous.

## Ce que contient le paquet

62 règles réparties sur 3 catégories, et **les 62 portent toutes à la fois le titre que déclare leur
descripteur et un lien d'aide** vers Microsoft Learn — une complétude que seuls les catalogues xUnit
et NUnit égalent ici.

| Catégorie | Règles | De quoi elles parlent |
| --- | --- | --- |
| `Usage` | 46 | Utiliser correctement le framework — assertions, attributs, sources de données, async |
| `Design` | 14 | La forme d'une classe de test : fixtures, cycle de vie, ce qui est public |
| `Performance` | 2 | La parallélisation, et les appels bloquants dans le code de test |

```csharp
[DiagnosticRule]
public static class MSTEST0037
{
    public const string Id = nameof(MSTEST0037);
    public const string Category = MSTestCategory.Usage;
    public const string HelpLinkUri = "https://learn.microsoft.com/dotnet/core/testing/mstest-analyzers/mstest0037";
}
```

## Où MSTest classe une règle d'assertion, et pourquoi vous ne pouvez pas le deviner

Si vous exécutez plus d'un framework de test dans une solution — une migration, un monorepo mixte —
voici le piège qui vaut d'être nommé. Prenez un seul concept, « cette assertion est fausse », et
demandez à chaque framework de quelle catégorie elle relève :

| | La catégorie d'une règle d'assertion |
| --- | --- |
| xUnit | `Assertions` |
| NUnit | `Assertion` |
| **MSTest** | **`Usage` *ou* `Design`, selon la règle** |

xUnit et NUnit diffèrent d'une lettre. MSTest n'a pas de catégorie d'assertion du tout : il les
répartit selon le genre d'erreur qu'elles attrapent. `MSTEST0037` *Use proper 'Assert' methods* est
`Usage` ; `MSTEST0032` *Assertion condition is always true* est `Design` ; `MSTEST0025` *Use
'Assert.Fail' instead of an always-failing assert* est `Design` aussi.

Ainsi, même connaître le framework ne vous donne pas la réponse — il faut connaître la règle. Et rien
dans la plateforme ne lit cet argument, donc une valeur fausse ne coûte aucune erreur ni aucun
avertissement, jamais. L'atteindre au travers de `MSTestRule.MSTEST0037.Category` supprime la question.

## Catégories déclarées une seule fois

`MSTestCategory` détient chaque catégorie une fois, et les règles la référencent — si bien que
l'orthographe d'une catégorie n'existe qu'à un seul endroit. Elle est **interne par conception** : une
suppression atteint une catégorie au travers de la règle qui la porte,
`MSTestRule.MSTEST0037.Category`, et jamais au travers de la constante de catégorie seule. Les deux se
replient sur la même chaîne aujourd'hui et cessent de s'accorder le jour où MSTest déplace la règle
([ADR-0026](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.fr.md)).

## Comment il est produit

Pas transcrit depuis la documentation. Le générateur lit dans les métadonnées des assemblys d'analyse
les types qu'ils marquent de `[DiagnosticAnalyzer]`, construit ceux-là, et lit les instances de
`DiagnosticDescriptor` qu'ils déclarent réellement — la seule source qui ne puisse pas avoir dérivé.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package MSTest.Analyzers --package-version latest \
    --namespace DiagnosticCatalog.MSTest --container MSTestRule \
    --output src/DiagnosticCatalog.MSTest/MSTestRules.g.cs
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

Ce catalogue roule sur le [train de release](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md) `mstest` et se versionne
indépendamment de la fondation, pour pouvoir suivre les versions de MSTest.Analyzers sans entraîner quoi que ce soit d'autre.

La publication ne fait pas partie du nocturne. Un mainteneur pousse une étiquette `mstest-vX.Y.Z`, et
le workflow de release empaquette le paquet, y intègre un SBOM SPDX, et publie via le
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) de NuGet
avec une provenance de build signée — aucune clé d'API à longue durée de vie n'existe nulle part pour
fuiter. La moitié empaquetage de ce pipeline est répétée à chaque pull request, si bien qu'une release
ne l'exerce jamais pour la première fois sur une étiquette.

## Limites

`[SuppressMessage]` ne peut pas supprimer les avertissements du **compilateur** — `CS0219` et
consorts demandent `#pragma warning disable`, qui prend des identifiants nus et ne peut donc jamais
référencer une constante. Ce paquet ne couvre que les règles d'analyse `MSTESTxxxx`.

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
analyseur tiers, lui-même sous licence MIT.
