# ADR-0003 | Adopter et faire respecter une convention Conventional Commits

🌍 **Langues :**  
🇬🇧 [English](./0003-adopt-and-enforce-a-conventional-commits-convention.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-07-30
**Accepted:** 2026-07-30
**Decision Makers:** Reefact

## Context

L'historique des commits est le seul enregistrement d'un changement qui survive à
la branche, à la pull request et à la mémoire du relecteur. Celui qui prépare une
release le lit pour décider de ce que la release contient et du numéro de version
qui en sort.

Les releases de ce dépôt sont partitionnées en trains par le scope de chaque
commit (ADR-0002). Un commit dont le scope est absent ne correspond à aucun
train, il n'atteint donc jamais aucune note de version ni aucun changelog — et il
échoue silencieusement, produisant une build verte et un registre de release
incomplet.

Ce dépôt fusionne les pull requests avec un **commit de fusion**. Chaque commit
qu'une branche porte atterrit donc dans l'historique permanent de `main` ; une
branche en désordre n'est pas écrasée à la fusion.

Un hook git local se contourne d'un seul drapeau, et n'est pas installé par
défaut sur un clone neuf.

Dependabot écrit ses propres en-têtes de commit. Leur longueur est dictée par le
nom du paquet, si bien qu'un nom long suffit à dépasser une limite de longueur
d'en-tête, et le bot ne peut pas amender le message qu'il a écrit.

Une part significative des commits de ce dépôt est écrite par des agents
automatisés, qui lisent les fichiers du dépôt pour en apprendre les règles et ne
peuvent pas inférer une convention du seul historique environnant.

## Decision

Chaque commit hors fusion suit une convention Conventional Commits à liste fermée
de types et liste fermée de scopes, validée par un unique linter partagé par le
hook local `commit-msg` et une vérification CI sur pull request.

## Rationale

Les listes fermées sont ce qui empêche la convention de se dégrader. Une liste de
types ouverte finit par un fourre-tout qui absorbe tout et ne signifie rien ; une
liste de scopes ouverte finit par des scopes qui nomment des fichiers ou des
classes, qui bougent, plutôt que des composants, qui ne bougent pas. Une liste
fermée rend aussi la convention vérifiable, et une convention qu'on ne peut pas
vérifier est une convention qui dérive.

Un **unique** linter, plutôt qu'un hook et un job CI implémentant chacun les
règles, est ce qui rend les deux verdicts identiques par construction. Deux
implémentations d'une même prose finissent par diverger, et la divergence se
découvre au pire moment — quand un commit passé localement échoue sur la pull
request.

Les deux couches sont nécessaires, et aucune ne remplace l'autre. Le hook donne
le verdict à l'auteur tant que le message est encore bon marché à corriger, avant
que le commit existe. La vérification CI est celle qu'on ne peut pas contourner,
et elle est requise précisément parce que le hook, lui, peut l'être : il n'est
pas installé sur un clone neuf et il cède à un simple drapeau. Faire respecter la
règle sur la pull request plutôt que sur le résultat de la fusion est ce
qu'exige la stratégie de commit de fusion — ce sont les commits individuels qui
atteignent `main`, ce sont donc eux qu'il faut vérifier.

Exiger un scope sur les deux types qui pilotent une version découle directement
d'ADR-0002 : ce sont les commits à partir desquels un registre de release est
construit, et le mode de défaillance d'un commit sans scope est le silence.
Transformer une omission silencieuse en rejet bruyant à l'écriture est le seul
point de la chaîne où le coût de la correction est quasi nul.

Exempter Dependabot n'affaiblit pas la règle : cela reconnaît que la règle traite
d'une paternité que le bot n'a pas. Ses en-têtes sont mécaniques, il ne peut pas
les amender, et l'alternative est une mise à jour de dépendance de routine qui
vire au rouge pour une raison sur laquelle personne ne peut agir.

## Alternatives Considered

### Aucune convention ; compter sur la revue pour attraper les mauvais messages

Envisagé parce que cela n'ajoute ni outillage ni friction, et qu'un relecteur
attentif remarque bel et bien un message peu informatif.

Rejeté parce que la qualité des messages est exactement ce qu'un relecteur
plongé dans un diff cesse de remarquer, et parce que le routage d'ADR-0002 a
besoin d'un scope lisible par machine, pas d'un scope bien intentionné. La
défaillance est en outre cumulative et invisible : personne ne découvre une
décennie d'historique inutilisable avant d'en avoir besoin.

### Fusionner en squash, et ne linter que le titre de la pull request

Envisagé parce que cela rend l'historique de branche jetable : une branche en
désordre ne coûte rien et une seule ligne par changement doit se conformer.

Rejeté parce que cela écrase l'unité de changement. Un commit voyage seul — il
est cherry-piqué, listé dans un log, relu isolément plus tard — et le squash
remplace plusieurs intentions par un message écrit par celui qui a appuyé sur
« merge » plutôt que par celui qui a fait chaque changement. Cela rendrait aussi
une pull request à intentions multiples impossible à représenter dans le registre
de release.

### Utiliser un linter du commerce comme commitlint

Envisagé parce qu'il est largement utilisé, configurable, et n'aurait ni à être
écrit ni à être maintenu ici.

Rejeté parce que cela introduirait une chaîne d'outils Node dans un dépôt .NET
pour une seule vérification de texte — une étape d'installation dans le chemin du
hook, une dépendance dans la chaîne d'approvisionnement, et un second écosystème
à suivre pour Dependabot. Les règles qui pèsent le plus ici (la liste de scopes
fermée, le couplage aux trains de release, l'exemption Dependabot) sont sur
mesure de toute façon, et un script POSIX n'a aucune étape d'installation et se
comporte identiquement dans le hook et sur le runner.

### Ne faire respecter la règle qu'en CI, sans hook local

Envisagé parce que c'est la couche qui bloque réellement une fusion, et qu'elle
ne demande aucune installation par clone.

Rejeté parce que cela déplace chaque verdict après l'existence des commits, où
corriger un message signifie un rebase interactif et un force-push plutôt qu'une
simple édition. La vérification CI reste l'autorité ; le hook est ce qui rend la
conformité bon marché.

## Consequences

### Positive

* L'historique répond à ce qu'une branche contient et à l'incrément de version
  qu'elle implique, sans ouvrir un diff.
* Les trains de release routent depuis le seul historique (ADR-0002).
* La convention est énoncée une fois, dans le guide de contribution, et vérifiée
  par un seul script que le hook et la CI appellent tous deux.
* Un agent qui lit le dépôt trouve la règle écrite et le vérificateur à côté.

### Negative

* Les contributeurs doivent exécuter une commande par clone pour installer le
  hook.
* Un message rejeté ne peut pas être corrigé par un commit ultérieur ; il exige
  de réécrire l'historique de la branche avant la fusion.
* La liste des scopes est un fichier partagé qui doit être mis à jour de concert
  avec le guide.

### Risks

* Le linter et le guide en prose divergent, si bien qu'un message que le guide
  autorise est rejeté. Atténuation : le guide énonce que le linter le reflète et
  nomme le fichier ; les deux changent dans le même commit.
* L'exemption de Dependabot est identifiée par l'auteur du commit, si bien qu'un
  message Dependabot réécrit perd l'exemption. Atténuation : c'est le
  comportement voulu — dès qu'un humain ou un agent réécrit le message, c'est un
  travail d'auteur et il est linté comme n'importe quel autre.

## Follow-up Actions

* Maintenir la vérification CI requise pour les fusions une fois la protection de
  branche configurée (ADR-0005).
* Étendre la liste de scopes du linter en même temps que le guide à chaque ajout
  de composant ou de catalogue.

## References

* [ADR-0002](0002-partition-releases-into-trains-by-commit-scope.fr.md) —
  pourquoi un scope est exigé sur `feat` et `fix`.
* [ADR-0005](0005-require-an-enforcing-check-before-any-automation-merges.fr.md).
* [CONTRIBUTING.md](../../CONTRIBUTING.md) — « Commit messages ».
* [Conventional Commits 1.0.0](https://www.conventionalcommits.org/fr/v1.0.0/).
