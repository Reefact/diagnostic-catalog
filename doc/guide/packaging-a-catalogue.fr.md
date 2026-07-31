# Empaqueter un catalogue

🌍 **Langues :**  
🇬🇧 [English](./packaging-a-catalogue.en.md) | 🇫🇷 Français (ce fichier)

Pour quiconque s'apprête à en publier un. Ce qu'il faut référencer, ce qui se propage à vos
consommateurs que vous l'ayez voulu ou non, et ce que nuget.org fera de votre README.

## Référencez la fondation de la façon ordinaire

```xml
<PackageReference Include="DiagnosticCatalog" Version="0.1.0" />
```

Pas `PrivateAssets="all"`. Vos consommateurs ont besoin que `DiagnosticRuleAttribute` soit résoluble
dans leur propre compilation, et masquer votre dépendance est ce qui le leur retire.

> **Une correction, énoncée plutôt que corrigée en douce.** Ce guide affirmait que masquer la
> fondation laisse les analyseurs ne trouver **aucune règle** et ne rien signaler. Ce n'est pas ce qui
> se passe, et c'est désormais asserté plutôt qu'argumenté : avec la fondation absente de la
> compilation d'un consommateur et présente dans les métadonnées du catalogue, `DCAT0006` est quand
> même signalé. Deux mécanismes le font survivre — le pré-filtre admet votre assemblage parce que son
> module *liste* encore `DiagnosticCatalog` dans ses références, et le marqueur est apparié par nom
> pleinement qualifié, si bien qu'un attribut non résoluble est un type d'erreur qui conserve son nom.
> Le test est
> `MarkerRecognitionTests.A_referenced_catalogue_is_found_although_the_consumer_cannot_resolve_the_marker`.

Alors que coûte réellement `PrivateAssets="all"` ? Deux choses, et toutes deux méritent d'être
évitées :

* **Un consommateur qui déclare ses propres règles ne le peut pas.** `[DiagnosticRule]` ne résout pas
  dans sa source, et il obtient `CS0246` jusqu'à ce qu'il ajoute la fondation à la main — une
  dépendance que votre paquet avait déjà et a refusé de déclarer.
* **Tout ce qui lit votre catalogue par réflexion à l'exécution** — un générateur de documentation, un
  script d'inventaire, `dcat list` sur votre assemblage — rencontre un type d'attribut qu'il ne peut
  pas lier.

Des défaillances bruyantes plutôt que silencieuses, ce qui explique que le conseil soit « évitez »
plutôt que « jamais, sous peine de la chose même que cette bibliothèque existe pour empêcher ».

## Ne pas prendre la dépendance du tout

Si vous préférez livrer un catalogue **sans aucune** dépendance, déclarez le marqueur vous-même :

```csharp
namespace DiagnosticCatalog
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    internal sealed class DiagnosticRuleAttribute : System.Attribute { }
}
```

C'est supporté et testé, pas une astuce. Les analyseurs apparient le marqueur par son **nom
pleinement qualifié**, jamais par identité de symbole : votre copie est donc reconnue exactement comme
la vraie. C'est le même motif que PolySharp emploie pour `IsExternalInit`, et
`MarkerRecognitionTests.A_catalogue_declaring_its_own_marker_is_still_analysed` est ce qui le maintient
en état.

Le nom doit être exact — `DiagnosticCatalog.DiagnosticRuleAttribute`, dans cet espace de noms. Un
attribut du même nom court ailleurs appartient à quelqu'un d'autre, et n'est délibérément pas apparié.

`internal` est le bon choix : rien hors de votre assemblage n'a besoin de l'appliquer, et une copie
publique entrerait en collision avec la vraie pour tout consommateur qui référence les deux.

## Ce qui se propage à vos consommateurs

Si votre catalogue référence `DiagnosticCatalog.Analyzers`, les analyseurs atteignent **vos
consommateurs** aussi — référencer votre catalogue leur suffit donc pour obtenir la vérification.

Cela a été mesuré contre une vraie restauration plutôt que lu dans la documentation de NuGet, qui dit
le contraire :

| Votre référence à `DiagnosticCatalog.Analyzers` | Les analyseurs tournent pour vos consommateurs |
| --- | --- |
| pas de `PrivateAssets` | **oui** |
| `PrivateAssets="none"` | oui |
| `PrivateAssets="all"` | non |

**Le silence se propage.** Si vous préférez ne pas imposer l'analyse à tout le monde en aval,
dites-le explicitement avec `PrivateAssets="all"` — et sachez que vous le choisissez, plutôt que de
découvrir plus tard que vous l'aviez fait.

Le choix est réel. Imposer l'analyse donne à vos utilisateurs le correctif de migration et les
vérifications de cohérence sans qu'ils sachent que le paquet existe ; cela met aussi des
avertissements dans des builds qui n'en demandaient pas, le jour où ils montent votre catalogue. Quel
que soit votre choix, dites lequel dans votre README.

## Le tableau récapitulatif

| Qui vous êtes | Ce que vous référencez | Comment |
| --- | --- | --- |
| **Consommateur** — écrit des suppressions | un catalogue | référence ordinaire |
| **Consommateur** — veut les vérifications | `DiagnosticCatalog.Analyzers` | `PrivateAssets="all"` |
| **Auteur de catalogue** | `DiagnosticCatalog` | **référence ordinaire** |
| **Auteur de catalogue** — veut ses consommateurs vérifiés aussi | `DiagnosticCatalog.Analyzers` | référence ordinaire, délibérément |
| **Auteur d'analyseur** — possède les deux | `DiagnosticCatalog` dans le projet catalogue ; le catalogue dans le projet analyseur | voir [boucler la boucle](first-party-analyzers.fr.md) |

## Votre README est votre page de paquet

`<PackageReadmeFile>` est rendu par nuget.org, et ce rendu a deux contraintes que la plupart des gens
découvrent à leurs dépens.

**Il ne résout aucun lien relatif.**
`[le guide de l'auteur](../../doc/guide/authoring-a-catalogue.en.md)` est un lien mort sur la page du
paquet, aussi correct soit-il dans le dépôt. Pointez vers l'extérieur en adresses absolues :

```markdown
[le guide de l'auteur](https://github.com/Reefact/diagnostic-catalog/blob/main/doc/guide/authoring-a-catalogue.en.md)
```

Ce dépôt en avait cinq, en ligne sur des pages publiées, jusqu'à ce qu'un test se mette à les refuser.

**Il n'offre aucun sélecteur de langue.** Un fichier par paquet, dans une langue. C'est pourquoi
`doc/` est bilingue ici et pas les READMEs de paquet
([ADR-0022](../adr/0022-maintain-every-document-under-doc-in-english-and-french.md)).

Deux choses que le README d'un catalogue doit porter et que rien d'autre ne dira au lecteur :

* **La version amont qu'il reflète, et sa date de génération.** C'est la première chose dont a besoin
  quiconque évalue le paquet, et une page de paquet n'a aucun voisin à côté d'elle. Dans ce dépôt, le
  générateur l'écrit entre des marqueurs `<!-- mirror:begin -->`, et `DocumentedMirrorTests` fait
  échouer un document dont le bandeau ne correspond pas à l'attribut `CatalogSource` que le générateur
  a écrit — un bandeau que rien ne peut atteindre n'énonce rien.
* **Les autres catalogues que vous publiez, par identifiant de paquet.** Un lecteur arrivé d'une
  recherche voit ce catalogue et rien d'autre.

## Ce que l'empaquetage vous donne ici

Pour référence, si vous regardez les projets de ce dépôt : un projet rejoint un train de release en
déclarant `<ReleaseTrain>` dans son propre `.csproj`, et cette unique déclaration est toute
l'appartenance — elle rend le projet empaquetable et lui donne un SBOM SPDX embarqué. Rien ne liste
les projets une seconde fois : un projet renommé ou déplacé ne peut donc pas disparaître
silencieusement de sa propre release.

La règle qui va avec : un projet d'un train ne doit pas porter de `<ProjectReference>` vers un projet
d'un autre, parce que `dotnet pack` estamperait une dépendance vers une version jamais publiée
([ADR-0007](../adr/0007-depend-across-trains-through-published-packages.md)). C'est pourquoi les
catalogues d'ici prennent la fondation en `PackageReference` alors même que sa source est dans le même
dépôt.

## Où aller ensuite

* [**Versionner un catalogue**](versioning-a-catalogue.fr.md) — la règle sur les `const` qui décide de
  ce qu'une release peut et ne peut pas changer.
* [**Les diagnostics `DCAT`**](diagnostics.fr.md) — ce qu'on dira à vos utilisateurs, et quand.
* [**CONTRIBUTING.md**](../../CONTRIBUTING.md) — les trains de release, et comment un catalogue est
  ajouté ici.

---

<div align="center">
<a href="./versioning-a-catalogue.fr.md">← Versionner un catalogue</a> · <a href="./README.fr.md">↑ Table des matières</a> · <a href="./dcat.fr.md">L'outil dcat →</a>
</div>
