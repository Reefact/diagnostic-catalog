# DiagnosticCatalog.AspNetCore

🌍 **Langues :**  
🇬🇧 [English](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.AspNetCore/README.en.md) | 🇫🇷 Français (ce fichier)

Les règles d'analyse **ASP.NET Core et Blazor** (`ASPxxxx`, `BLxxxx`) sous forme de constantes
fortement référencées, pour que `SuppressMessageAttribute` prenne des références vérifiées à la
compilation plutôt que des chaînes magiques.

<!-- mirror:begin -->
> ## 🪞 Reflète `Microsoft.AspNetCore.App.Ref 10.0.10`
>
> **35 règles, 3 catégories**, chaque identifiant et chaque
> catégorie lus dans les analyseurs de cette version. Régénéré le 2026-08-05.
<!-- mirror:end -->

> Non officiel. Sans affiliation avec Microsoft, ni approbation ni support de sa part.

## Pourquoi

Chaque projet ASP.NET Core exécute ces analyseurs, et **personne ne les a installés** — non pas faute
d'y penser, mais parce qu'il n'y a rien à installer. Ils arrivent dans le framework partagé, et le SDK
web référence ce framework. Aucun `PackageReference` ne les nomme, et aucun ne peut être retiré.

C'est ce qui fait de leurs règles celles que les gens suppriment **dans le code source**. Une règle
que vous avez activée se règle dans `.editorconfig` ; une règle venue avec le framework reçoit une
exception à l'unique endroit où elle a tort, avec une `Justification` à côté du code qui la mérite.

```csharp
[SuppressMessage("Usage", "ASP0018:Unused route parameter", ...)]
```

Trois chaînes, et rien n'en vérifie aucune. Trompez-vous d'identifiant et la suppression ne fait
silencieusement rien — l'avertissement reste, tout simplement. Trompez-vous de catégorie et **il ne se
passe rien du tout**, jamais : la plateforme .NET ne lit jamais cet argument, donc aucune erreur,
aucun avertissement et aucun test en échec ne vous le dira.

```csharp
using DiagnosticCatalog.AspNetCore;

[SuppressMessage(
    AspNetCoreRule.ASP0018.Category,
    AspNetCoreRule.ASP0018.Id,
    Justification = "The parameter is read by the model binder, not by the handler.")]
```

Le jour où une règle passe dans une autre catégorie, la seconde version la suit et la première
reste à nommer une catégorie que la règle ne porte plus — en silence, et aussi longtemps que la
ligne survit.

## Celle qu'on ne veut surtout pas rater

`ASP0026` est la seule règle `Security` du jeu, et elle signale ceci :

> **`[Authorize]` supplanté par un `[AllowAnonymous]` plus lointain.**

Un `[AllowAnonymous]` sur une classe de base ou dans une portée extérieure l'emporte silencieusement
sur un `[Authorize]` écrit plus près du point de terminaison — l'inverse de ce que presque tout le
monde lit dans le code. Si un projet supprime un jour celle-là, la suppression est porteuse au sens le
plus fort, et l'argument qui nomme sa catégorie vaut `"Security"` — une valeur que rien dans la
plateforme ne vérifiera jamais.

## Installation

```xml
<PackageReference Include="DiagnosticCatalog.AspNetCore" Version="1.0.0" />
```

Ce paquet ne fournit que les constantes. Les vérifications qui valident les déclarations de règles et
leurs sites d'utilisation sont livrées à part dans `DiagnosticCatalog.Analyzers`.

## Ce que contient le paquet

35 règles réparties sur 3 catégories, 26 des 35 portant le lien d'aide que déclare leur descripteur.

| Catégorie | Règles | De quoi elles parlent |
| --- | --- | --- |
| `Usage` | 32 | API minimales, routage, migration de `WebApplicationBuilder`, accès aux en-têtes, arbres de rendu Blazor |
| `Encapsulation` | 2 | Des paramètres de composant Blazor qui doivent être publics, et assignables (`BL0001`, `BL0004`) |
| `Security` | 1 | `ASP0026`, ci-dessus |

**Deux préfixes, un paquet.** `ASPxxxx`, c'est ASP.NET Core proprement dit — 26 règles, surtout API
minimales et routage. `BLxxxx`, ce sont les composants Blazor — 9 règles sur les paramètres, les
arbres de rendu et l'état persisté. Ils sont livrés ensemble dans le framework, ils sont donc
catalogués ensemble ; le badge de l'icône lit `ASP` parce qu'un badge porte le préfixe majoritaire
([ADR-0032](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0032-badge-a-catalogues-icon-with-its-rule-prefix.fr.md)).

```csharp
[DiagnosticRule]
public static class ASP0026
{
    public const string Id = nameof(ASP0026);
    public const string Category = AspNetCoreCategory.Security;
    public const string HelpLinkUri = "https://learn.microsoft.com/aspnet/core/diagnostics/asp0026";
}
```

## Catégories déclarées une seule fois

`AspNetCoreCategory` détient chaque catégorie une fois, et les règles la référencent — si bien que
l'orthographe d'une catégorie n'existe qu'à un seul endroit. Elle est **interne par conception** : une
suppression atteint une catégorie au travers de la règle qui la porte,
`AspNetCoreRule.ASP0026.Category`, et jamais au travers de la constante de catégorie seule. Les deux
se replient sur la même chaîne aujourd'hui et cessent de s'accorder le jour où une règle bouge
([ADR-0026](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0026-reach-a-category-only-through-the-rule-that-carries-it.fr.md)).

## Comment il est produit

Pas transcrit depuis la documentation. Le générateur lit dans les métadonnées des assemblys d'analyse
les types qu'ils marquent de `[DiagnosticAnalyzer]`, construit ceux-là, et lit les instances de
`DiagnosticDescriptor` qu'ils déclarent réellement — la seule source qui ne puisse pas avoir dérivé.

Les analyseurs sont livrés dans **`Microsoft.AspNetCore.App.Ref`**, le pack de ciblage d'ASP.NET Core,
qui est un paquet ordinaire sur nuget.org — c'est ainsi que le SDK lui-même l'acquiert. La version
reflétée est donc une version de paquet, qu'un consommateur peut consulter et installer, plutôt que ce
qui se trouvait sur la machine ayant généré le fichier.

```
dotnet run --project src/DiagnosticCatalog.Cli -- generate \
    --package Microsoft.AspNetCore.App.Ref --package-version latest \
    --namespace DiagnosticCatalog.AspNetCore --container AspNetCoreRule \
    --output src/DiagnosticCatalog.AspNetCore/AspNetCoreRules.g.cs
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

## Une note sur les versions

Les règles qu'un projet obtient réellement sont gouvernées par son **framework partagé**, que son
framework cible sélectionne — pas par une référence de paquet qu'il contrôle. Ce catalogue reflète une
version de pack de ciblage, et l'assembly consigne exactement laquelle dans `[assembly: CatalogSource]`.
Si votre application cible un ASP.NET Core plus ancien que la version qui y est consignée, les règles
ajoutées depuis seront présentes dans le catalogue et absentes de votre compilation ; en référencer
une compile quand même, et la suppression ne correspond simplement jamais à rien.

## Comment il arrive sur nuget.org

Ce catalogue roule sur le [train de release](https://github.com/Reefact/diagnostic-catalog/blob/main/CONTRIBUTING.md) `aspnetcore` et se versionne
indépendamment de la fondation, pour pouvoir suivre les versions d'ASP.NET Core sans entraîner quoi que ce soit d'autre.

La publication ne fait pas partie du nocturne. Un mainteneur pousse une étiquette `aspnetcore-vX.Y.Z`, et
le workflow de release empaquette le paquet, y intègre un SBOM SPDX, et publie via le
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) de NuGet
avec une provenance de build signée — aucune clé d'API à longue durée de vie n'existe nulle part pour
fuiter. La moitié empaquetage de ce pipeline est répétée à chaque pull request, si bien qu'une release
ne l'exerce jamais pour la première fois sur une étiquette.

## Limites

`[SuppressMessage]` ne peut pas supprimer les avertissements du **compilateur** — `CS0219` et
consorts demandent `#pragma warning disable`, qui prend des identifiants nus et ne peut donc jamais
référencer une constante. Ce paquet ne couvre que les règles d'analyse `ASPxxxx` et `BLxxxx`.

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
