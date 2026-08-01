# Versionner un catalogue

🌍 **Langues :**  
🇬🇧 [English](./versioning-a-catalogue.en.md) | 🇫🇷 Français (ce fichier)

Pour quiconque a publié un catalogue et s'apprête à le publier de nouveau. Une propriété des `const`
décide de presque tout ce qui suit, et elle n'est pas évidente.

## La propriété d'où tout découle

Une `const` n'est **pas** lue dans votre assemblage à l'exécution. Le compilateur substitue sa valeur
à chaque site d'utilisation, dans la compilation *du consommateur*.

```mermaid
flowchart LR
    subgraph YOURS["Contoso.Rules 1.0"]
        C["const string Id = \"CTS0001\""]
    end
    subgraph THEIRS["Acme.App, compilé contre lui"]
        U["[SuppressMessage(..., \"CTS0001\")]<br/><i>le littéral, recopié</i>"]
    end
    C -- "à la compilation d'Acme" --> U
    THEIRS -. "aucun lien retour" .-> YOURS
```

Un consommateur qui a écrit `ContosoRule.CTS0001.Id` n'a pas enregistré de référence vers votre
assemblage. Il a copié la chaîne `"CTS0001"` dans le sien, et plus rien ne relie les deux ensuite.

Deux conséquences, et elles tirent en sens inverse :

* **Livrer une nouvelle valeur n'atteint personne** tant qu'ils ne recompilent pas. Un catalogue n'est
  pas une configuration d'exécution que l'on corrige en place.
* **Supprimer un membre casse la recompilation** de tous ceux qui l'utilisaient — et la casse avec un
  `CS0117` nu qui nomme un type, un membre manquant, et n'explique rien.

## Ne supprimez jamais une règle

Quand un éditeur retire une règle, la tentation est de la sortir du catalogue. N'en faites rien :

```csharp
[DiagnosticRule]
[Obsolete("Retired in Contoso.Analyzers 4.0. No replacement.")]
public static class CTS0001
{
    public const string Id = nameof(CTS0001);
    public const string Category = ContosoCategory.Usage;
}
```

Un consommateur qui la référence encore obtient désormais `CS0618` — qui **nomme la règle et dit ce
qui s'est passé** — au lieu d'une erreur de compilation qui l'envoie chercher un espace de noms
manquant ou un mauvais `using`.

Cette différence est tout l'objet de la convention
([ADR-0010](../adr/0010-carry-a-retired-rule-forward-as-obsolete.fr.md)). L'alternative n'est pas « un
catalogue plus propre » ; c'est une montée de version qui échoue pour une raison illisible.

Nommez la version qui l'a abandonnée. La question suivante du consommateur est toujours « quand, et
y a-t-il un remplacement », et le message d'obsolescence est le seul endroit où il regardera.

## Ne renommez jamais un membre

Le même raisonnement, et il attrape les gens deux fois plus souvent parce qu'un renommage semble sans
risque.

Une constante de catégorie dont le *nom* change casse tous les consommateurs qui la référençaient,
avec le même `CS0117` inutile. Cela inclut le fait de ranger `ContosoCategory.CodeSmells` en
`ContosoCategory.CodeSmell` — une amélioration partout, sauf au seul endroit où c'est un contrat
publié.

Ce dépôt s'y tient lui-même
([ADR-0012](../adr/0012-a-catalogue-never-renames-a-member-it-published.fr.md)), et le cas qui a forcé la
décision vaut d'être connu parce que ce n'était pas une erreur humaine : une nouvelle catégorie
arrivant en amont, dont l'identifiant aplati entrait en collision avec un existant et se triait avant
lui, aurait pris ce nom et poussé le titulaire sur un suffixe numéroté — renommant un membre publié,
au cours d'une exécution nocturne sans surveillance.

**Choisissez des noms avec lesquels vous pouvez vivre.** Ils sont aussi publics que les identifiants
de règles.

## Ce qu'un numéro de version doit dire

Du SemVer ordinaire, avec la forme qu'un catalogue a réellement :

| Changement | Version |
| --- | --- |
| Une nouvelle règle | **mineur** — additif ; le code de personne ne cesse de compiler |
| Une règle retirée en amont, reportée en `[Obsolete]` | **mineur** — un avertissement n'est pas une rupture |
| La *catégorie* d'une règle a changé en amont | **mineur**, et cela mérite une note de version — la valeur que vos consommateurs incorporent change |
| Retirer quoi que ce soit de publié | **majeur** |
| Renommer quoi que ce soit de publié | **majeur** |
| Régénérer contre une nouvelle version amont, rien n'a bougé | **aucune release** |

La troisième ligne est celle qui piège. Une recatégorisation change ce que vos consommateurs
compilent dans leurs assemblages et ne produit d'erreur nulle part — c'est exactement la classe de
changement silencieux que cette bibliothèque existe pour faire remonter, alors faites-la remonter
dans vos notes même si le SemVer ne vous y oblige pas.

La dernière ligne n'est pas de la paresse. Le générateur compare sa propre sortie précédente et laisse
le fichier intact quand rien n'a bougé, estampille `generatedOn` comprise : une nuit où l'amont n'a
pas bougé ne produit donc ni diff ni release.

## Votre version n'est pas celle de l'éditeur

Un catalogue reflétant `SonarAnalyzer.CSharp 10.31.0` n'est **pas** en version `10.31.0`.

Elle court sur sa propre ligne
([ADR-0015](../adr/0015-a-catalogues-version-runs-on-its-own-line.fr.md)), pour une raison qui devient
évidente la première fois qu'on en a besoin : vous publierez un correctif au catalogue — une
correction de métadonnée, un changement d'empaquetage, un titre perdu — sans que la version amont
bouge. Si les numéros sont liés, cette release n'a aucun numéro disponible.

La version amont qu'un catalogue reflète appartient à l'assemblage, pas au numéro de version :

```csharp
[assembly: CatalogSource(
    source:        "Contoso.Analyzers",
    sourceVersion: "4.2.1",
    generatedOn:   "2026-07-31")]
```

C'est aussi ce qui rend la paire lisible de l'extérieur : `dcat list` et `dcat explain` annoncent la
version reflétée et la date de génération **avant** de répondre à quoi que ce soit, parce que l'âge
d'un instantané décide si sa réponse est digne de confiance.

Dans ce dépôt, chaque catalogue roule sur son propre [train de release](../../CONTRIBUTING.md) : une
release Sonar n'entraîne donc jamais la version de la fondation, et réciproquement.

## Les préversions, quand l'éditeur y est

Si l'analyseur que vous reflétez publie son vrai travail sur une ligne de préversion, reflétez cette
ligne plutôt qu'une étiquette stable périmée. StyleCop est le cas qui a tranché ici
([ADR-0016](../adr/0016-mirror-stylecops-prerelease-line.fr.md)) : sa release stable a des années de
retard sur ce que tout le monde exécute réellement, et un catalogue la reflétant décrirait des règles
que ses utilisateurs n'ont pas et omettrait celles qu'ils ont.

Dites-le dans le README plutôt que de le laisser découvrir. Un consommateur qui choisit entre deux
paquets a besoin de savoir lequel décrit l'analyseur qu'il exécute.

## Où aller ensuite

* [**Empaqueter un catalogue**](packaging-a-catalogue.fr.md) — comment référencer la fondation, et ce
  qui atteint vos consommateurs que vous l'ayez voulu ou non.
* [**Boucler la boucle avec votre propre analyseur**](first-party-analyzers.fr.md) — si vous possédez
  les deux, les valeurs peuvent cesser d'être deux transcriptions d'une même chaîne.
* [**Concepts**](concepts.fr.md#provenance--un-catalogue-est-un-instantané) — ce que la provenance
  enregistre et pourquoi la date est une `string`.

---

<div align="center">
<a href="./first-party-analyzers.fr.md">← Boucler la boucle avec votre propre analyseur</a> · <a href="./README.fr.md">↑ Table des matières</a> · <a href="./packaging-a-catalogue.fr.md">Empaqueter un catalogue →</a>
</div>
