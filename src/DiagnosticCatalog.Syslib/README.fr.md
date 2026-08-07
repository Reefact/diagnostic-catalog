# DiagnosticCatalog.Syslib

🌍 **Langues :**  
🇬🇧 [English](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.Syslib/README.en.md) | 🇫🇷 Français (ce fichier)

Les **diagnostics des générateurs de source du runtime .NET** (`SYSLIB1xxx`) sous forme de constantes
fortement référencées, pour que `SuppressMessageAttribute` prenne des références vérifiées à la
compilation plutôt que des chaînes magiques.

<!-- mirror:begin -->
> ## 🪞 Reflète `Microsoft.NETCore.App.Ref 10.0.10`
>
> **13 règles, 4 catégories**, chaque identifiant et chaque
> catégorie lus dans les analyseurs de cette version. Régénéré le 2026-08-05.
<!-- mirror:end -->

> Non officiel. Sans affiliation avec Microsoft, ni approbation ni support de sa part.

## Pourquoi

Treize règles, c'est le plus petit catalogue d'ici, et l'une d'elles en est la raison d'être.

La catégorie de `SYSLIB1090` est **`ComInterfaceGenerator`**.

Pas `Interoperability`, qui est ce qu'utilisent ses quatre voisines les plus proches. Pas `Usage`, pas
`Design` — le nom de l'assembly de génération qui se trouve la déclarer. Toutes les autres catégories
de tous les catalogues de cette famille sont un concept auquel une personne pourrait arriver :
`Usage`, `Security`, `Performance`, `Trimming`, `Assertion`. Celle-ci est un détail d'implémentation
qui a fui dans un contrat publié, porté par exactement une règle.

```csharp
[SuppressMessage("Interoperability", "SYSLIB1090:...", Justification = "…")]   // faux, et rien ne le dit
```

Trompez-vous d'identifiant et la suppression ne fait silencieusement rien — l'avertissement reste,
tout simplement. Trompez-vous de catégorie et **il ne se passe rien du tout**, jamais : la plateforme
.NET ne lit jamais cet argument, donc aucune erreur, aucun avertissement et aucun test en échec ne
vous le dira.

```csharp
using DiagnosticCatalog.Syslib;

[SuppressMessage(
    SyslibRule.SYSLIB1090.Category,
    SyslibRule.SYSLIB1090.Id,
    Justification = "The interface is only ever marshalled by the legacy path.")]
```

Le jour où cette catégorie est corrigée en amont — et cela ressemble au genre de chose qui finit par
être corrigé — la seconde version la suit et la première continue de compiler pendant qu'elle cesse
discrètement de correspondre.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.Syslib" Version="1.0.0" />
```

C'est la seule référence dont vous avez besoin. Ce paquet dépend de `DiagnosticCatalog`, qui porte
les analyseurs `DCAT` et leurs correctifs à côté de ses attributs, si bien que référencer ce
catalogue est ce qui active les vérifications qui valident les déclarations de règles et leurs
sites d'utilisation. Une suppression littérale qu'une référence de catalogue remplacerait est une
erreur par défaut, et un correctif la réécrit pour vous.

## Ce que contient le paquet

13 règles réparties sur 4 catégories, et **les 13 portent toutes le lien d'aide que déclare leur
descripteur**.

| Catégorie | Règles | De quoi elles parlent |
| --- | --- | --- |
| `Usage` | 6 | Forme et validité du marshaller pour `LibraryImport` — `SYSLIB1055`–`SYSLIB1061` |
| `Interoperability` | 5 | La conversion vers `LibraryImport` et vers l'interface COM générée, et l'hébergement COM |
| `Performance` | 1 | `SYSLIB1045`, *Convertir en `GeneratedRegexAttribute`* |
| `ComInterfaceGenerator` | 1 | `SYSLIB1090`, ci-dessus |

```csharp
[DiagnosticRule]
public static class SYSLIB1045
{
    public const string Id = nameof(SYSLIB1045);
    public const string Category = SyslibCategory.Performance;
    public const string HelpLinkUri = "https://learn.microsoft.com/dotnet/fundamentals/syslib-diagnostics/syslib1045";
}
```

## De quelles règles SYSLIB il s'agit

Le préfixe `SYSLIB` couvre deux choses sans rapport, et une seule est ici.

* **`SYSLIB1xxx` — les diagnostics des générateurs de source.** Ce que contient ce paquet. Ils
  viennent de vrais analyseurs avec de vraies instances de `DiagnosticDescriptor`, et
  `[SuppressMessage]` les fait taire.
* **`SYSLIB0xxx` — les avertissements d'obsolescence.** `SYSLIB0001` et ses semblables sont levés par
  le compilateur depuis l'`[Obsolete]` posé sur l'API elle-même. Aucun analyseur ne les déclare, donc
  aucun descripteur n'existe à lire et aucune n'apparaît ici.

Les identifiants ne sont pas contigus pour la même raison que ceux d'un éditeur ne le sont jamais — le
runtime les alloue entre générateurs, et seuls ceux qui ont survécu jusqu'à une version livrée sont
déclarés.

## Catégories déclarées une seule fois

`SyslibCategory` détient chaque catégorie une fois, et les règles la référencent — si bien que
l'orthographe d'une catégorie n'existe qu'à un seul endroit. Elle est **interne par conception** : une
suppression atteint une catégorie au travers de la règle qui la porte,
`SyslibRule.SYSLIB1090.Category`, et jamais au travers de la constante de catégorie seule. Les deux se
replient sur la même chaîne aujourd'hui et cessent de s'accorder le jour où une règle bouge
([ADR-0026](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.fr.md)).

## Comment il est produit

Pas transcrit depuis la documentation. Le générateur lit dans les métadonnées des assemblys d'analyse
les types qu'ils marquent de `[DiagnosticAnalyzer]`, construit ceux-là, et lit les instances de
`DiagnosticDescriptor` qu'ils déclarent réellement — la seule source qui ne puisse pas avoir dérivé.

Les générateurs sont livrés dans **`Microsoft.NETCore.App.Ref`**, le pack de ciblage du runtime .NET,
qui est un paquet ordinaire sur nuget.org — c'est ainsi que le SDK lui-même l'acquiert. La version
reflétée est donc une version de paquet qu'un consommateur peut consulter, plutôt que ce qui se
trouvait installé sur la machine ayant généré le fichier.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package Microsoft.NETCore.App.Ref --package-version latest \
    --namespace DiagnosticCatalog.Syslib --container SyslibRule \
    --output src/DiagnosticCatalog.Syslib/SyslibRules.g.cs
```

Six assemblys de génération sont lus et dix de leurs types déclarent une règle. Le pack entier est lu
plutôt qu'un sous-ensemble choisi à la main, si bien qu'un générateur qui gagne sa première règle est
attrapé par le nocturne au lieu d'attendre que quelqu'un le remarque.

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

## Une note sur les versions

Les règles qu'un projet obtient réellement sont gouvernées par son **framework partagé**, que son
framework cible sélectionne — pas par une référence de paquet qu'il contrôle. Ce catalogue reflète une
version de pack de ciblage, et l'assembly consigne exactement laquelle dans `[assembly: CatalogSource]`.
Si votre application cible un runtime plus ancien que la version qui y est consignée, les règles
ajoutées depuis seront présentes dans le catalogue et absentes de votre compilation ; en référencer
une compile quand même, et la suppression ne correspond simplement jamais à rien.

## Comment il arrive sur nuget.org

Ce catalogue roule sur le [train de release](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md) `syslib` et se versionne
indépendamment de la fondation, pour pouvoir suivre les versions du runtime sans entraîner quoi que ce soit d'autre.

La publication ne fait pas partie du nocturne. Un mainteneur pousse une étiquette `syslib-vX.Y.Z`, et
le workflow de release empaquette le paquet, y intègre un SBOM SPDX, et publie via le
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) de NuGet
avec une provenance de build signée — aucune clé d'API à longue durée de vie n'existe nulle part pour
fuiter. La moitié empaquetage de ce pipeline est répétée à chaque pull request, si bien qu'une release
ne l'exerce jamais pour la première fois sur une étiquette.

## Limites

`[SuppressMessage]` ne peut pas supprimer les avertissements du **compilateur** — `CS0219` et
consorts demandent `#pragma warning disable`, qui prend des identifiants nus et ne peut donc jamais
référencer une constante. C'est aussi pourquoi les obsolescences `SYSLIB0xxx` sont hors de portée :
ce sont des avertissements du compilateur levés depuis `[Obsolete]`, pas des diagnostics d'analyse.
Ce paquet ne couvre que les règles d'analyse `SYSLIB1xxx`.

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
analyseur Microsoft, lui-même sous licence MIT.
