# ADR-0007 | Dépendre d'un autre train par paquet publié, jamais par référence de projet

🌍 **Langues :**  
🇬🇧 [English](./0007-depend-across-trains-through-published-packages.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-07-30
**Accepted:** 2026-07-30
**Decision Makers:** Reefact

## Context

Le dépôt publie plusieurs trains de release qui versionnent et taggent
indépendamment (ADR-0002) : la fondation, et un catalogue par éditeur de règles
de diagnostic. Tous les trains vivent dans une seule solution.

Chaque catalogue est bâti sur la fondation. Un projet de catalogue a donc besoin
des types de la fondation à la compilation.

`dotnet pack` convertit une `<ProjectReference>` en dépendance de paquet
estampillée à **la version en cours d'empaquetage**. Empaqueter le catalogue
Sonar en `4.0.0` avec une référence de projet vers la fondation déclarerait donc
une dépendance sur la version `4.0.0` de la fondation — une version d'un autre
train, que rien n'a jamais publiée et que le train `lib` n'atteindra peut-être
jamais.

Un consommateur qui restaure un tel paquet obtient `NU1102` : la dépendance ne
peut être résolue depuis aucun flux. Les paquets sur nuget.org étant immuables,
le paquet cassé reste publié ; seule une nouvelle version le corrige.

Au sein d'un même train, la situation est différente : tous ses projets sont
empaquetés dans la même invocation à la même version, si bien qu'une référence de
projet entre eux est estampillée à une version qui est publiée à l'instant même.

Une référence de projet est aussi la manière ordinaire d'embarquer quelque chose
dans un paquet sans en dépendre — un analyseur livré à l'intérieur de la
bibliothèque qu'il accompagne, par exemple — et ces références visent des projets
qui ne publient rien en propre.

Au moment de cette décision, aucun projet de catalogue n'existe ; aucune
référence de ce genre n'a donc pu être écrite.

## Decision

Un projet d'un train de release ne dépend d'un autre train que par une
`PackageReference` vers une version publiée, jamais par une `ProjectReference`.

## Rationale

La défaillance que cela empêche est à la fois silencieuse et permanente. Rien
d'une référence de projet inter-trains n'est visible à la compilation : la
solution compile, les tests passent, l'empaquetage réussit, et le défaut n'existe
qu'à l'intérieur du `.nuspec` produit. Il apparaît à la première restauration
d'un consommateur, sur un artefact qu'on ne peut pas retirer. Face à cette
asymétrie, interdire la construction purement et simplement est proportionné — il
n'en existe aucune version qui fonctionne.

Dépendre par paquet publié est aussi l'expression honnête de ce que signifient
des trains indépendants. Un catalogue n'est pas livré *avec* une copie de travail
particulière de la fondation ; il est livré contre une version de la fondation
qui existe sur nuget.org, ce qui est précisément ce que son paquet doit déclarer.
Faire résoudre à la build le même artefact que celui que le paquet déclare
supprime l'écart entre ce qui a été compilé et ce qu'un consommateur restaurera.

Le coût est qu'un catalogue ne récupère pas automatiquement un changement non
publié de la fondation : un changement couvrant les deux doit publier la
fondation d'abord, puis monter la référence du catalogue. Ce n'est pas une
friction que la décision ajoute, c'est l'ordre de release que des trains
indépendants impliquent déjà, rendu explicite plutôt que découvert.

La règle est vérifiée sur les fichiers projet plutôt que sur le paquet produit,
parce que c'est là que la réponse est exacte. Un `.nuspec` ne peut pas distinguer
une dépendance venue d'une référence de projet d'une référence de paquet
légitime qui porterait la même version ; le fichier projet énonce la construction
directement. La vérifier à chaque empaquetage — répétition de pull request
comprise — est ce qui fait arriver la règle au moment où la référence est écrite
plutôt qu'au moment où une release est tentée.

Une référence vers un projet qui ne déclare aucun train est délibérément laissée
tranquille. Ces projets ne publient rien, aucune dépendance n'est donc
estampillée pour eux ; les signaler casserait le motif d'embarquement ordinaire
et produirait des échecs sans défaut derrière.

## Alternatives Considered

### Autoriser la référence de projet et surcharger la version de dépendance émise

Envisagé parce que MSBuild peut surcharger ce qu'une référence de projet apporte
au paquet : la bonne version publiée pourrait être estampillée pendant que la
build compile toujours contre les sources locales.

Rejeté parce que cela fait déclarer au paquet une dépendance sur un artefact que
la build n'a jamais utilisé. Le code compilé et la dépendance déclarée seraient
libres de diverger, ce qui convertit un échec de restauration — bruyant,
immédiat — en une incompatibilité d'exécution qui n'apparaît que dans
l'application du consommateur.

### Mettre chaque catalogue sur le train de la fondation

Envisagé parce que cela supprime entièrement le cas inter-trains : tout est
copublié à une seule version, et les références de projet sont toujours valides.

Rejeté parce que c'est ADR-0002 à l'envers. Cela rétablit exactement le couplage
que cette décision existe pour supprimer — la mise à jour de règles d'un éditeur
ferait bouger la version de la fondation, et la promesse de stabilité de la
fondation serait à la merci de quatre rythmes de release.

### Compter sur la revue pour attraper la construction

Envisagé parce que la règle est simple à énoncer et qu'un relecteur qui la
connaît verra la référence dans un diff.

Rejeté parce que la construction est écrite une fois, à la création d'un projet
de catalogue, et devient invisible ensuite. La défaillance apparaît des mois plus
tard, à une release, le contexte de l'auteur ayant depuis longtemps disparu. Une
vérification qui tourne à chaque pull request ne coûte rien et n'oublie jamais.

### Scinder chaque catalogue dans son propre dépôt

Envisagé parce que des dépôts séparés rendent impossible l'écriture d'une
référence de projet inter-trains.

Rejeté pour les raisons déjà enregistrées dans ADR-0002 : les catalogues
partagent la fondation et ses aides de test, et la scission multiplierait la
surface CI/CD avant qu'un seul catalogue existe. Une scission de dépôts reste
disponible plus tard et rendrait cette règle redondante plutôt que fausse.

## Consequences

### Positive

* Un paquet publié ne peut jamais déclarer une dépendance sur une version qui n'a
  jamais été publiée.
* Ce contre quoi un catalogue compile et ce que son paquet déclare sont le même
  artefact.
* L'ordre de release impliqué par des trains indépendants est énoncé, pas
  découvert.

### Negative

* Un changement couvrant la fondation et un catalogue exige deux releases, dans
  l'ordre.
* Un catalogue ne peut pas être développé contre des sources non publiées de la
  fondation sans un flux local ou une version de préversion.
* Les contributeurs rencontrent une règle que la structure de la solution leur
  laisserait sinon enfreindre naturellement.

### Risks

* La friction pousse quelqu'un à ajouter la référence interdite
  « temporairement ». Atténuation : la vérification fait échouer l'empaquetage à
  chaque pull request, une version temporaire ne survit donc pas à la revue.
* Une version de préversion de la fondation est référencée puis jamais publiée,
  laissant un catalogue épinglé à une version inexistante. Atténuation : la
  référence se résout à la restauration, la build échoue donc immédiatement
  plutôt que celle du consommateur.

## Follow-up Actions

* Établir comment un catalogue se développe contre une fondation non publiée —
  une version de préversion sur nuget.org, ou un flux local — à la création du
  premier catalogue.

## References

* [ADR-0002](0002-partition-releases-into-trains-by-commit-scope.fr.md) —
  pourquoi les trains sont indépendants.
* [ADR-0006](0006-publish-through-trusted-publishing-with-provenance-and-an-sbom.fr.md).
* `tools/packaging/pack.sh` — la vérification.
* [CONTRIBUTING.md](../../CONTRIBUTING.md) — « Cross-train dependencies ».
