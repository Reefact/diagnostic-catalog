# DiagnosticCatalog.Self

🌍 **Langues :**  
🇬🇧 [English](https://github.com/Reefact/diagnostic-catalog/blob/main/src/DiagnosticCatalog.Self/README.en.md) | 🇫🇷 Français (ce fichier)

Les règles `DCAT` — celles que signalent les analyseurs livrés dans
[`DiagnosticCatalog`](https://www.nuget.org/packages/DiagnosticCatalog) — sous forme de constantes
que vous pouvez référencer.

C'est la bibliothèque appliquée à elle-même. Les analyseurs qui vérifient *vos* suppressions publient leurs
propres règles de la manière exacte qu'ils demandent à tout le monde, et ils le font au travers du même
générateur qui produit les catalogues Sonar, analyseurs .NET et StyleCop.

## Quand vous en voulez

Quand vous supprimez un diagnostic `DCAT` et préféreriez que la suppression soit vérifiée :

```csharp
using System.Diagnostics.CodeAnalysis;
using DiagnosticCatalog.Self;

// Migration d'une grande base de code : ce fichier passe en dernier, et les littéraux ici sont voulus.
[SuppressMessage(
    DcatRule.DCAT0006.Category,
    DcatRule.DCAT0006.Id,
    Justification = "Legacy suppressions, migrated in the next pass.")]
public static class LegacyInterop
{
}
```

Sans le catalogue vous écririez `[SuppressMessage("DiagnosticCatalog", "DCAT0006")]` — deux chaînes que rien
ne vérifie, ce qui est exactement le problème que ce dépôt existe pour supprimer. Il aurait été curieux de
laisser nos propres règles comme le seul endroit où il fallait encore les écrire à la main.

La plupart des projets n'en auront pas besoin : `.editorconfig` est la façon habituelle d'abaisser un
diagnostic `DCAT`, et il prend du texte brut dans lequel aucune constante ne pourra jamais être substituée.
Tournez-vous vers le catalogue quand vous supprimez à un *endroit précis*, pour une raison qui vaut d'être
écrite.

## D'où il vient

Généré depuis les instances de `DiagnosticDescriptor` des analyseurs eux-mêmes, jamais depuis la
documentation, si bien que l'identifiant et la catégorie sont les valeurs que l'analyseur signale
réellement. Le régénérer tient en une commande :

```sh
dotnet run --project src/DiagnosticCatalog.Cli -- generate --manifest eng/catalogs.json
```

La CI le régénère à chaque pull request et échoue si le résultat diffère de ce qui est commité — ainsi un
nouvel identifiant `DCAT` ne peut pas être livré sans le catalogue qui le publie.

## Versionnage

Ce catalogue roule sur le train `lib`, avec les analyseurs qu'il reflète, et c'est délibéré : les deux sont
générés depuis une seule source dans un seul dépôt et ne doivent jamais décrire des jeux de règles
différents. Les dix autres catalogues se versionnent indépendamment parce qu'un éditeur extérieur donne leur
cadence ([ADR-0015](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/adr/0015-a-catalogues-version-runs-on-its-own-line.fr.md)) ; personne à
l'extérieur ne donne celle-ci.

Une règle retirée est reportée en `[Obsolete]` plutôt que supprimée, comme partout ailleurs ici : les
constantes sont incorporées dans votre assembly à *votre* compilation, si bien qu'en retirer une casse votre
build avec un message qui ne nomme rien d'utile.

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

- [**Les diagnostics `DCAT`**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/diagnostics.fr.md)
  — chaque règle cataloguée ici, vue du côté qui la signale.
- [**Écrire des suppressions que le compilateur vérifie**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/writing-suppressions.fr.md)
  — comment utiliser ces constantes, ce qui est identique pour n'importe quel autre catalogue.
- [**Architecture du dépôt**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/architecture.fr.md)
  — la boucle d'auto-application dont ce paquet est une moitié, et pourquoi elle tourne dans un seul sens.

La [**carte de la documentation**](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/README.fr.md)
choisit une page selon ce que vous cherchez à faire ; chaque guide existe en anglais et en français.

## Licence

Apache-2.0.
