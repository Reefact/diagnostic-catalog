# ADR-0013 | Écrire l'outillage shell pour POSIX sh, pas bash

🌍 **Langues :**  
🇬🇧 [English](./0013-write-the-shell-tooling-for-posix-sh-not-bash.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-07-31
**Accepted:** 2026-07-31
**Decision Makers:** Reefact

## Context

Ce dépôt porte des scripts shell à trois endroits. `tools/` contient l'outillage
de release — le tableau des trains que chaque étape d'empaquetage et de notes de
version source, le linter de commit partagé par le hook local et la CI, et un
petit harnais de tests pour eux. `.claude/hooks/` contient les hooks d'agent.
`.githooks/` contient les hooks git, qui ne portent aucune extension parce que
git exige le nom de hook exact.

Ce à quoi `tools/trains.sh` répond, c'est quels projets une release publie. Un
projet que sa découverte manque est silencieusement absent de sa propre release ;
un projet qu'elle trouve à tort est publié alors qu'il ne le devrait pas. Aucune
des deux erreurs n'apparaît en build rouge.

Ces scripts tournent à des endroits qui ne partagent aucun installeur : un runner
hébergé par GitHub, la machine d'un mainteneur, et un hook git invoqué par le git
dont dispose le contributeur. Rien dans le dépôt n'installe de shell, et le
harnais de tests sous `tools/tests/` ne dépend délibérément de rien au-delà d'un
shell — ni bats, ni gestionnaire de paquets.

`local`, `[[ ]]` et les tableaux sont des extensions que bash et ksh
fournissent ; POSIX n'en définit aucune. Les comportements diffèrent
concrètement plutôt que stylistiquement : dash, qui est `/bin/sh` sur les images
de runner Ubuntu, rejette `[[` purement et simplement, tandis qu'il accepte
`local` comme extension propre. Le code de sortie d'une fonction shell est, dans
tous les dialectes, celui de sa dernière commande.

Deux vérifications lisent déjà ces fichiers à chaque pull request : shellcheck,
qui prend le dialecte dans le shebang et est tenu à zéro constat à toutes les
gravités, et la suite de tests shell, exécutée avec `sh` plutôt que bash pour
qu'un bashisme échoue ici plutôt que sur une machine plus légère.

Ces mêmes fichiers sont aussi soumis à un service d'analyse statique tiers, dont
les règles shell sont écrites pour bash et ne tiennent aucun compte d'un shebang.
Il demande `local` à la place d'un paramètre positionnel, `[[` à la place de
`[`, et un `return` explicite à la fin de chaque fonction. À la première analyse
de ce dépôt, ces trois règles représentaient 34 des 55 constats ouverts.

## Decision

L'outillage shell du dépôt est écrit en POSIX sh, et chaque outil qui le lit est
configuré pour ce dialecte plutôt que pour bash.

## Rationale

Les scripts sous `tools/` décident de ce qu'une release publie ; ils doivent donc
tourner partout où une release tourne, sans que quiconque ait installé un shell
au préalable. POSIX sh est le seul dialecte pour lequel cette hypothèse tient.
Tout le reste de l'offre — `local`, tableaux, `[[` — est un confort acheté en
ajoutant une dépendance d'exécution au code qui la tolère le moins.

Le confort est réellement minime à cette taille. Il s'agit de quelques centaines
de lignes de manipulation de chaînes sur un tableau séparé par des barres
verticales ; ce que `local` apporterait est une portée qu'une convention de
nommage approche déjà, et ce que les tableaux apporteraient est une structure de
données dont les scripts n'ont pas besoin.

La contrainte mérite d'être enregistrée parce qu'elle n'est pas évidente à la
lecture d'un script, et parce qu'elle lie désormais plus que les scripts. Un
outil qui rapporte un conseil bash contre un fichier POSIX ne rapporte pas un
défaut ; il rapporte une discordance de dialecte, et une discordance qui ne peut
jamais être résolue — le constat est permanent, et il y en a plus que de constats
actionnables sur les mêmes fichiers. Un rapport majoritairement composé de bruit
cesse d'être lu, ce qui est déjà le raisonnement derrière le maintien de
shellcheck à zéro constat à toutes les gravités. L'analyse suit donc la décision
plutôt que la décision ne plie devant l'analyse.

L'un de ces trois conseils est pire que du bruit. Une fonction se terminant par
un `return 0` inconditionnel rapporte un succès quoi qu'ait fait sa dernière
commande, et la dernière commande est là où ces fonctions font leur travail.
Appliqué à la découverte qui décide quels projets une release publie, cela
convertit une chaîne cassée en réponse vide réussie — la défaillance en succès
silencieux que l'outillage de ce dépôt est écrit pour empêcher. Prendre un
conseil de dialecte pour argent comptant, dans un fichier dont tout l'objet est
de rendre une défaillance silencieuse impossible, est l'erreur précise que l'on
cherche à éliminer.

L'enregistrer règle aussi la question pour le prochain outil. La décision est une
propriété du code, pas d'un analyseur en particulier ; un analyseur ajouté plus
tard est donc configuré depuis l'enregistrement plutôt que depuis une discussion
rejouée.

## Alternatives Considered

### Écrire l'outillage en bash et le déclarer dans le shebang

Bash est présent sur chaque runner hébergé par GitHub et sur la plupart des
machines de développeurs, et `local`, les tableaux et `[[` rendraient les scripts
plus courts et leur portée explicite. Les constats de dialecte bash seraient
alors des conseils à suivre, et aucun analyseur n'aurait besoin d'être configuré.

Rejeté parce que cela pose une dépendance d'exécution sur le code qui décide de
ce qu'une release publie, en échange d'une économie minime à cette taille. La
dépendance est invisible jusqu'au jour où quelque chose tourne dans un conteneur
ne livrant qu'un shell POSIX, et la défaillance apparaît sous forme d'une release
ayant publié le mauvais ensemble de projets plutôt que d'un interpréteur
manquant.

### Garder POSIX sh et laisser les constats de dialecte ouverts

Chaque suppression est une affirmation que quelqu'un doit maintenir, et derrière
laquelle un vrai défaut peut se cacher. Laisser les constats ouverts ne coûte
rien mécaniquement et garde la configuration d'analyse vide.

Rejeté parce que ces constats-là ne peuvent ni être traités ni disparaître. Ils
sont permanents, ils sont plus nombreux que les constats actionnables sur les
mêmes fichiers, et ils seraient retriés par chaque futur lecteur qui ne connaît
pas déjà le dialecte. C'est le mode de défaillance que la barre de zéro constat
sur shellcheck existe pour éviter, arrivant par une autre porte.

### Remplacer l'outillage shell par un programme dans un langage que le dépôt construit

La logique de release pourrait être du C# dans la chaîne d'outils propre à ce
dépôt, analysée par les mêmes analyseurs que tout le reste, sans aucune question
de dialecte.

Rejeté parce que les scripts tournent avant et autour de la build .NET — un hook
git se déclenche avant toute restauration, et une étape de workflow lit le
tableau des trains pour décider de ce qu'il faut construire. Exiger un SDK
restauré pour répondre à « quels projets ce train publie-t-il ? » inverse la
dépendance, et échange une contrainte de dialecte contre une contrainte
d'amorçage plus difficile à satisfaire.

## Consequences

### Positive

* L'outillage de release tourne partout où existe un shell POSIX, y compris sur
  les images plus légères qu'une étape de release peut recevoir, sans
  interpréteur à installer d'abord.
* La contrainte est vérifiée à chaque pull request par deux moyens indépendants —
  le dialecte que shellcheck applique, et le shell avec lequel la suite de tests
  est exécutée — elle ne repose donc pas sur la mémoire d'un contributeur.
* Les rapports d'analyse sur ces fichiers ne portent que des constats
  actionnables, ce qui est ce qui les garde dignes d'être lus.

### Negative

* Pas de `local`, pas de tableaux, pas de `[[`. Les paramètres de fonction sont
  nommés en les affectant à des globales préfixées, ce qui est plus verbeux et
  repose sur une convention de nommage là où une fonctionnalité du langage aurait
  fait le travail.
* Chaque analyseur qui lit le shell de ce dépôt doit s'entendre dire le dialecte.
  Un analyseur ajouté plus tard commence par répéter la discordance, et la
  configuration est par outil plutôt que déclarée une fois.

### Risks

* dash accepte `local` ; la seule chose entre ce dépôt et un bashisme qu'il ne
  remarquerait pas est donc le dialecte POSIX de shellcheck. La suite de tests
  passerait. Si la barre de gravité du job de lint venait à être relâchée, ce
  garde-fou cesserait de s'appliquer **silencieusement**.
* Une exclusion cadrée par motif de fichier couvre aussi des fichiers qui
  n'existent pas encore. Un script ajouté plus tard dans un autre dialecte, dans
  la même arborescence, hériterait de l'exclusion sans que personne ne l'ait
  décidé.

## Follow-up Actions

* Aucune. Le dialecte est appliqué par le workflow de lint existant et par la
  suite de tests shell ; la configuration par analyseur vit à côté du workflow
  qui exécute l'analyse, avec la raison de chaque règle exclue énoncée sur place.

## References

* [ADR-0004](0004-state-the-coding-rules-where-an-agent-can-act-on-them.fr.md) —
  le principe frère : une règle est enregistrée là où l'outillage qui l'applique
  peut la lire, pour qu'aucune ne repose sur la seule attention.
* `.github/workflows/lint.yml` — la barre shellcheck et le shell avec lequel la
  suite de tests est exécutée.
* `.github/workflows/sonar.yml` — les règles de dialecte bash exclues, chacune
  avec la raison pour laquelle elle ne peut pas s'appliquer ici.
