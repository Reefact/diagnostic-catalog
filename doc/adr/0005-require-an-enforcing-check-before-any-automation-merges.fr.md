# ADR-0005 | Exiger une vérification bloquante avant toute fusion automatisée

🌍 **Langues :**  
🇬🇧 [English](./0005-require-an-enforcing-check-before-any-automation-merges.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-07-30
**Accepted:** 2026-07-30
**Decision Makers:** Reefact

## Context

L'auto-merge de GitHub est armé par un workflow mais effectué par GitHub, et
seulement une fois que les vérifications de statut **requises** de la branche
passent. Qu'une vérification soit requise est une propriété des règles de
protection de branche du dépôt, pas du workflow qui a armé la fusion.

Là où aucune règle de protection de branche ne marque une vérification comme
requise, armer l'auto-merge fusionne la pull request immédiatement — avant, et
indépendamment de, toute vérification que le dépôt exécute.

Ce dépôt est neuf. Il porte des workflows mais aucune règle de protection de
branche, et l'écosystème de dépendances GitHub Actions ouvre des pull requests de
mise à jour dès qu'il est configuré, ce qui est désormais le cas. La fenêtre
pendant laquelle un fusionneur armé agirait sans vérification s'ouvre donc
aussitôt et reste ouverte jusqu'à ce qu'un humain la remarque.

Le mainteneur est la seule autorité qui fusionne une pull request ; aucun agent
ne fusionne ni n'arme une fusion sur son propre travail.

Les vérifications du dépôt sont le mécanisme d'application de plusieurs autres
décisions : la convention de commit (ADR-0003), le plancher .NET Framework
(ADR-0001) et le cliquet des règles de code (ADR-0004) reposent tous sur la
capacité d'une vérification à bloquer une fusion.

## Decision

Aucune automatisation de ce dépôt ne peut fusionner ni armer une fusion tant
qu'une vérification de statut requise et bloquante n'est pas en place, et une
automatisation capable de fusionner est livrée désarmée jusqu'à ce que cette
vérification existe.

## Rationale

La sûreté d'une fusion automatisée réside entièrement dans les vérifications
requises. Le workflow qui l'arme décide seulement *quelles* pull requests sont
éligibles ; il n'a pas voix au chapitre sur le fait que quoi que ce soit ait été
vérifié d'abord. Traiter le workflow comme le mécanisme de sûreté inverse
l'origine réelle de la garantie — et c'est ainsi qu'un dépôt non protégé finit
par fusionner des changements non vérifiés à travers un workflow qui a l'air
prudent.

Le danger est à son maximum précisément maintenant. Un jeune dépôt paraît calme,
les pull requests sont des montées de version mécaniques, et personne ne surveille
une voie que personne n'a encore empruntée. Une fenêtre silencieuse et non bornée
n'est pas un risque à accepter en pariant que la règle de protection sera créée
bientôt.

Livrer l'automatisation **désarmée**, plutôt que ne pas la livrer, garde sa
logique — quelles mises à jour sont éligibles, comment l'identité est établie,
quand une fusion est retirée — écrite, relue et versionnée pendant que le
raisonnement est frais. Cette logique est la partie coûteuse ; l'interrupteur ne
l'est pas. Différer tout le workflow signifierait l'écrire plus tard, sous la
pression de vouloir ouvrir la voie.

La règle est énoncée pour *toute* automatisation plutôt que pour le seul
metteur à jour de dépendances, parce que la même inversion est offerte à chaque
automatisation future capable de fusionner ou d'armer une fusion. L'enregistrer
comme politique signifie que le prochain workflow de ce genre hérite de la
réponse au lieu de la rejouer.

Le sens du désarmement est délibérément laissé sans condition. Retirer une fusion
est toujours sûr, et un chemin de repli qui dépend d'un interrupteur positionné
n'est pas un chemin de repli.

## Alternatives Considered

### Livrer l'automatisation armée et créer la règle de protection rapidement

Envisagé parce que la règle de protection représente quelques minutes de travail
et que l'écart serait en pratique court.

Rejeté parce que l'écart est non borné en principe et silencieux en pratique : la
défaillance ne produit ni erreur, ni notification, ni artefact — des commits
fusionnés que rien n'a vérifiés, indiscernables après coup de fusions que tout a
vérifiées. « Rapidement » n'est pas une propriété que le dépôt peut tenir.

### Ne pas livrer l'automatisation du tout tant que la protection n'existe pas

Envisagé, et tout aussi sûr. C'est le plus petit diff, et cela supprime
entièrement l'interrupteur.

Rejeté parce que cela abandonne la revue de la logique propre au workflow, qui
est la partie qu'il vaut la peine de bien faire et la plus difficile à écrire
plus tard. Cela ne laisse en outre aucune trace de la décision au moment où elle
a réellement été prise.

### Détecter la règle de protection à l'exécution plutôt qu'un interrupteur manuel

Envisagé parce que cela supprime un interrupteur qui peut être mal positionné, et
rend la garantie auto-vérifiable plutôt que procédurale.

Rejeté parce que lire la protection de branche exige une portée de jeton plus
large que ce dont le workflow a besoin par ailleurs — élargir des permissions
pour vérifier une propriété de sûreté est un mauvais échange — et parce que la
présence d'une règle ne prouve pas l'application : une règle peut exister sans
marquer aucune vérification comme requise. La détection répondrait à une question
voisine de celle qui compte.

### Se reposer sur l'exigence d'une approbation humaine plutôt qu'une vérification requise

Envisagé parce qu'une approbation est aussi une barrière, et que c'est le
jugement du mainteneur plutôt que celui d'une machine.

Rejeté parce que cela ruine l'objet de l'automatisation : une voie dont le but
est de fusionner des mises à jour de routine sans attention humaine ne peut pas
être conditionnée à l'attention humaine. La vérification est ce qui peut être à
la fois automatique et bloquant.

## Consequences

### Positive

* Aucune mise à jour de dépendance ne peut fusionner avant que les vérifications
  propres au dépôt aient tourné et aient été rendues déterminantes.
* La logique de l'automatisation existe, relue, et n'est qu'à un réglage de dépôt
  d'être utilisable.
* Les futures automatisations capables de fusionner héritent d'une réponse
  énoncée plutôt que de répéter l'analyse.

### Negative

* Les mises à jour de dépendances doivent être fusionnées à la main jusqu'à ce
  que la protection de branche soit configurée, ce qui est de la friction sur
  exactement les pull requests que la voie existe pour éliminer.
* Le dépôt porte un workflow qui ne fait pour l'instant presque rien, ce qu'un
  lecteur doit s'entendre dire plutôt que découvrir.

### Risks

* L'interrupteur est positionné avant que la règle de protection existe,
  recréant le danger que la décision élimine. Atténuation : l'en-tête du workflow
  lui-même énonce l'ordre requis et nomme les deux étapes ; l'interrupteur est
  documenté comme le second de deux, pas comme un commutateur de fonctionnalité.
* La décision est lue comme portant spécifiquement sur les mises à jour de
  dépendances, et une future automatisation capable de fusionner est livrée
  armée. Atténuation : la décision est énoncée pour toute automatisation, et cet
  enregistrement est ce qu'une vérification ADR sur pull request fait remonter.

## Follow-up Actions

* Protéger `main` et marquer les vérifications CI comme requises, puis armer
  l'automatisation.
* Faire entrer dans cet ensemble requis les vérifications qui appliquent
  ADR-0001, ADR-0003 et ADR-0004.

## References

* [ADR-0001](0001-floor-the-libraries-on-net-framework-4-7-2.fr.md),
  [ADR-0003](0003-adopt-and-enforce-a-conventional-commits-convention.fr.md),
  [ADR-0004](0004-state-the-coding-rules-where-an-agent-can-act-on-them.fr.md) —
  décisions dont l'application dépend d'une vérification requise.
* `.github/workflows/dependabot-automerge.yml` — l'automatisation désarmée et la
  procédure d'armement.
