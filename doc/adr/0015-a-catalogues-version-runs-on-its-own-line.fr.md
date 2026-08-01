# ADR-0015 | La version d'un catalogue suit sa propre ligne, jamais celle de l'amont

🌍 **Langues :**  
🇬🇧 [English](./0015-a-catalogues-version-runs-on-its-own-line.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-07-31
**Accepted:** 2026-07-31
**Decision Makers:** Reefact

## Context

Chaque catalogue reflète un paquet d'analyseur amont et publie sur son propre
train de release
([ADR-0002](0002-partition-releases-into-trains-by-commit-scope.fr.md)). Deux
numéros de version existent donc pour chaque catalogue : celui que ce dépôt
publie, et celui qu'il reflète.

La version amont est déjà enregistrée, catalogue par catalogue, dans les
métadonnées d'assemblage par `[assembly: CatalogSource]` (§7.6). Le §14.2 observe
que la version du paquet n'a pas à l'encoder et que les deux peuvent bouger
indépendamment, mais s'arrête avant de choisir — *« quel que soit le schéma
retenu »*. L'annexe B7 enregistrait ce choix en question ouverte à trancher avant
la première release publique. Les changelogs propres aux trois catalogues, eux,
énoncent déjà la réponse comme un fait dans leur préambule : la version amont se
lit dans l'assemblage plutôt qu'elle ne se déduit du numéro de paquet.

Les trois éditeurs reflétés numérotent leurs versions différemment et aucun
n'emploie le versionnage sémantique tel que NuGet l'entend :
`SonarAnalyzer.CSharp 10.31.0.145097` porte quatre segments,
`Microsoft.CodeAnalysis.NetAnalyzers 10.0.302` trois,
`StyleCop.Analyzers 1.1.118` trois.

Les deux numéros bougent de façon démontrée à des moments différents, dans les
deux sens :

* La `0.2.0` des trois catalogues a livré un changement de la documentation de
  chaque règle tout en reflétant exactement les versions que la `0.1.0` reflétait
  déjà. Rien n'avait bougé en amont.
* La synchronisation planifiée (§14.3) trouve régulièrement une version amont qui
  ne change aucune règle publiée par ce dépôt, et laisse le catalogue intact par
  conception — une version amont bouge, le catalogue non.

Le workflow de release rejette purement et simplement les métadonnées de build
SemVer (`+…`), parce que NuGet les retire de l'identité d'un paquet.

## Decision

La version de paquet d'un catalogue est sa propre ligne de versionnage
sémantique, incrémentée d'après ce qui a changé dans le catalogue ; la version
amont qu'il reflète est portée dans les métadonnées d'assemblage et n'est jamais
encodée dans la version du paquet.

## Rationale

Les deux numéros répondent à des questions différentes, et un numéro unique ne
peut pas répondre aux deux. Une version de paquet dit à un consommateur si monter
est sûr — si une constante qu'il référence a pu bouger, ce que les §23 et §23.1
définissent précisément. La version amont lui dit quelle version d'éditeur le
catalogue reflète. Replier la seconde dans la première laisserait la première
incapable de dire quoi que ce soit, ce qui est la défaillance qu'ADR-0002
partitionne les trains pour éviter : le numéro d'un train doit dire quelque chose
de ce train.

La démonstration est plus forte que l'argument. La `0.2.0` a dû sortir pendant
que l'amont ne bougeait pas, et un schéma de suivi n'avait aucun numéro
disponible pour elle : la version reflétée n'avait pas changé, donc tout numéro
qui en dérivait était déjà pris. L'inverse arrive plus souvent encore — le job
nocturne trouve une version amont ne portant rien que ce catalogue publie et
n'écrit correctement rien. Un schéma qui lie les deux doit inventer un numéro
dans le premier cas et en supprimer un dans le second, et les deux inventions
sont un catalogue qui ment sur sa propre provenance.

Les schémas propres aux éditeurs règlent le reste. Une version à quatre segments
n'est pas une valeur SemVer, et le workflow de release accepte exactement trois
segments ; `10.31.0.145097` ne peut donc pas être une version que ce dépôt
publie — NuGet, lui, en accepte bien quatre, comme le montre
`StyleCop.Analyzers.Unstable 1.2.0.556`, mais ce n'est pas la contrainte qui lie
ici. Les trois éditeurs ne s'accordent en outre sur aucune forme ; aucune
correspondance unique ne sert donc les trois catalogues. Encoder la version amont
à côté d'un cœur SemVer est également fermé : les métadonnées de build sont
rejetées par le workflow de release parce que NuGet les retire de l'identité du
paquet, et une étiquette de préversion marquerait chaque release comme une
préversion.

Les métadonnées sont le bon foyer pour la version reflétée parce qu'elles ne
peuvent pas être tronquées pour entrer. `[assembly: CatalogSource]` porte la
chaîne de version de l'éditeur exactement comme l'éditeur l'a écrite, quatre
segments compris, à côté de la date de génération — ce qu'aucune version de
paquet ne pourrait contenir, quel que soit le schéma retenu.

## Alternatives Considered

### Suivre la version amont

Envisagé parce que c'est instantanément lisible : un consommateur lisant
`DiagnosticCatalog.Sonar 10.31.0` saurait ce qu'il reflète sans rien ouvrir, et
la question « ce catalogue est-il à jour ? » se répondrait d'elle-même.

Rejeté parce que cela laisse le catalogue incapable de livrer ses propres
changements. La `0.2.0` est le cas d'espèce : rien n'avait bougé en amont, et
chaque numéro dérivé de l'amont était déjà publié. Cela ne peut pas non plus se
faire fidèlement — les quatre segments de Sonar ne sont pas une valeur SemVer —
et cela ferait suivre au `MAJOR` d'un catalogue la numérotation d'un éditeur
plutôt que la rupture de contrat que le §23 lui réserve.

### Porter la version amont à côté d'un cœur SemVer, en métadonnées de build ou en étiquette de préversion

Envisagé parce que cela garderait une ligne indépendante tout en montrant la
version reflétée dans le numéro qu'un consommateur lit en premier.

Rejeté parce que le workflow de release refuse déjà les métadonnées de build :
NuGet retire `+…` de l'identité d'un paquet, deux versions amont différentes
produiraient donc le même paquet. Une étiquette de préversion la porterait, mais
au prix de marquer chaque release de chaque catalogue comme une préversion.

### Ne lier que la majeure : le `MAJOR` du catalogue suit celui de l'éditeur

Envisagé comme voie médiane — lisible d'un coup d'œil, tout en laissant `MINOR`
et `PATCH` libres pour les mouvements propres au catalogue.

Rejeté parce que `MAJOR` est le seul segment à avoir ici une signification
définie : les §23 et §23.1 en font le signal qu'une constante référencée a pu
bouger. Le dépenser pour la numérotation sans rapport d'un éditeur ferait tirer
le seul signal de changement cassant dont dispose un consommateur sur des
releases qui ne cassent rien, et le ferait taire sur celles qui cassent.

## Consequences

### Positive

* Un catalogue peut livrer un changement qui lui est propre — une correction du
  générateur, un changement de documentation — sans attendre une version de
  l'éditeur.
* Une version de paquet garde la signification que le §23 lui donne ; un
  consommateur qui en lit une apprend donc si la montée peut casser sa
  compilation.
* Les trois catalogues suivent une seule règle malgré trois schémas d'éditeurs
  incompatibles.

### Negative

* Un consommateur ne peut pas déduire de la version de paquet quelle version
  amont un catalogue reflète ; il lit `[assembly: CatalogSource]`, ou l'entrée de
  changelog, qui l'énoncent tous deux.
* Deux numéros doivent être gardés en tête pour chaque catalogue plutôt qu'un.

### Risks

* Un consommateur suppose que la version de paquet suit l'amont et en conclut que
  le catalogue est périmé. Atténuation : le préambule du changelog de chaque
  catalogue énonce la règle et renvoie aux métadonnées, et chaque entrée de
  release nomme la version reflétée.

## Follow-up Actions

* Clore l'annexe B7 de la spécification contre cet enregistrement, dans les deux
  langues.

## References

* [ADR-0002](0002-partition-releases-into-trains-by-commit-scope.fr.md) —
  pourquoi les trains versionnent indépendamment.
* [doc/specification.fr.md](../specification.fr.md) — §7.6, §14.2, §14.3, §23 et
  §23.1, et annexe B7.
* `src/DiagnosticCatalog.Sonar/CHANGELOG.md` et ses homologues — où la règle est
  énoncée aux consommateurs.
