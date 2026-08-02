# ADR-0028 | Apparier le README du projet par-delà la frontière de `doc/`

🌍 **Langues :**  
🇬🇧 [English](./0028-pair-the-project-readme-across-the-doc-boundary.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-08-02
**Accepted:** 2026-08-02
**Decision Makers:** Reefact

## Context

L'[ADR-0022](0022-maintain-every-document-under-doc-in-english-and-french.fr.md) a
rendu bilingue chaque document sous [`doc/`](..), l'anglais faisant foi, et a
laissé les README de paquets sous `src/` en anglais seul. Cette seconde exclusion
était argumentée depuis le moteur de rendu : nuget.org affiche un fichier par
paquet, n'offre aucun sélecteur de langue et ne résout aucun lien relatif, si bien
qu'une page bilingue y dupliquerait chaque section dans un seul document ou
renverrait vers une traduction que le lecteur ne peut pas atteindre.

Elle n'a produit aucun argument sur le README du projet. Le *Context* de cet
enregistrement le comptait pourtant parmi la documentation destinée aux lecteurs
de l'époque, aux côtés des guides ; la *Decision* a ensuite nommé `doc/` et `src/`
sans y revenir. La règle qui le maintenait en anglais était une ligne de tableau
dans [`doc/CONVENTIONS.en.md`](../CONVENTIONS.en.md), qui énonçait la frontière —
hors de `doc/` — plutôt qu'une raison.

L'argument du moteur de rendu ne se transpose pas. GitHub affiche le README du
projet sur la page d'accueil du dépôt et y résout les liens relatifs, ce qui est
précisément pourquoi cette page pointait déjà deux fois dans l'ensemble bilingue,
en proposant la moitié française de la carte de la documentation et celle de sa
porte d'entrée. Un lecteur francophone était donc introduit dans un ensemble
traduit par la seule page du parcours qui, elle, n'avait pas de traduction.

Ce que GitHub impose, en revanche, c'est le nom et l'emplacement. Il compose la
page d'accueil d'un dépôt à partir d'un fichier nommé `README.md` à la racine, et
d'aucun autre : un `README.en.md` n'est pas retenu, un README vivant sous `doc/`
non plus. C'est ce même fichier que la page du dépôt présente à quiconque arrive
depuis un résultat de recherche, une page de paquet ou un lien.

Les contrôles de parité de `tests/DiagnosticCatalog.Documentation.UnitTests`
s'appuient sur le suffixe de langue : un document nommé `<nom>.en.md` ou
`<nom>.fr.md` appartient à l'ensemble et est confronté à son homologue, un
document sans suffixe n'y est simplement pas. Rien n'est exempté par une liste, et
c'est délibéré — un contrôle avec une liste d'exceptions dérive vers un contrôle
qui n'a plus que des exceptions. Le README du projet ne portait aucun suffixe : il
n'avait donc jamais fait partie de l'ensemble, et ces tests ne le lisaient que pour
ses liens.

Deux pages faisaient un seul travail. `doc/README.en.md` était la porte d'entrée de
la documentation — un panneau indicateur nommant les quatre sortes de documents qui
vivent sous `doc/` et la question à laquelle chacune répond — et le README du projet
portait une section `Documentation` listant les quatre mêmes. La porte d'entrée
n'avait qu'un seul lien entrant, depuis cette section ; le pied de navigation du
guide, lui, la contournait déjà pour revenir au README du projet.

Le projet frère [`first-class-errors`](https://github.com/Reefact/first-class-errors),
dont l'ADR-0022 a suivi la mise en page, apparie son `README.md` racine avec une
moitié française tenue à l'intérieur de son dossier de documentation, la moitié
anglaise portant la bannière de langue et la moitié française renvoyant vers la
racine.

## Decision

La porte d'entrée de la documentation est fusionnée dans le README du projet, dont
la moitié anglaise reste à la racine du dépôt parce que GitHub l'y affiche et dont
la moitié française est `doc/README.fr.md`, et les contrôles de documentation
traitent les deux comme homologues par-delà la frontière de dossier.

## Rationale

L'exclusion était un effet de bord d'une frontière tracée par dossier, non un
jugement porté sur le document. Toute autre exclusion de l'ADR-0022 était
argumentée depuis quelque chose de la page — les README de paquets depuis le
moteur qui les affiche — tandis que celle-ci l'était depuis l'endroit où le
fichier se trouve. Dès lors que le README racine est reconnu comme une page de
documentation que GitHub maintient hors de `doc/`, la frontière cesse de décrire
une décision pour décrire un système de fichiers.

L'argument d'audience de l'ADR-0022 porte ici plus fort que partout où il a déjà
été accepté. Cet enregistrement justifiait la traduction des guides parce qu'ils
sont lus par celui à qui l'on demande de migrer le code, non par celui qui a
choisi la bibliothèque. Le README est la page que ce lecteur rencontre en premier,
et la seule que beaucoup liront jamais : c'est elle qui porte l'argument expliquant
pourquoi une catégorie fausse ne produit aucun symptôme, le point unique que toute
la conception existe pour établir. Traduire les guides en laissant cette page en
anglais inverse la priorité que l'enregistrement avait posée.

La contrainte imposée par GitHub est réelle mais plus étroite que celle de
nuget.org, et cette différence est ce qui fixe la forme. nuget.org ne peut afficher
aucune traduction : le choix y était entre une langue et un document plié en deux.
GitHub peut en afficher une ; il exige seulement que la moitié anglaise s'appelle
`README.md` et siège à la racine. La contrainte retire donc le *suffixe* et fixe
l'*emplacement* d'une moitié — elle ne retire pas la *paire*.

Fusionner la porte d'entrée est ce qui rend la paire gratuite, et cela vaut d'être
fait pour soi-même. Un panneau indicateur vers quatre documents et une section de
README listant les quatre mêmes sont une page écrite deux fois, et le doublon se
voyait déjà : la porte d'entrée n'était atteignable que par un lien unique, et la
navigation du guide la contournait. La replier dans le README retire une étape du
parcours de chaque lecteur et laisse une page à tenir à jour au lieu de deux — et
l'eût-on gardée, le dépôt aurait dû tenir côte à côte une porte d'entrée française
et un README français, chacun disant l'essentiel de ce que dit l'autre.

La moitié française garde donc le nom `README.fr.md`, et ce nom signifie toujours ce
qu'il signifie dans `doc/guide/` et `doc/adr/` : l'index du dossier où il siège. Le
README du projet *est* désormais cet index — c'est là que le guide, la
spécification, les décisions et les conventions sont nommés — si bien que `doc/`
gagne son index français, et que son index anglais vit à la racine, déplacé par le
moteur de rendu plutôt que par un choix.

Déclarer la paire aux contrôles, plutôt que d'en exempter la page, est le geste
même de l'ADR-0022, et pour la même raison. Chacun des arguments ci-dessus tombe dès
l'instant où la moitié française prend du retard, et prendre du retard est l'issue
normale d'une politique qui repose sur le fait de s'en souvenir. Une page bilingue
par convention et non vérifiée par construction est exactement la défaillance que
cet enregistrement voulait supprimer, et le README du projet est la pire page du
dépôt à laisser dans cet état, parce que c'est celle que l'on modifie le plus
souvent pour des raisons sans rapport avec la traduction.

## Alternatives Considered

### Garder le README du projet en anglais seul, comme la convention précédente l'énonçait

Le README est une vitrine plutôt qu'un document à étudier, son public évalue au lieu
d'apprendre, et le lecteur qui veut davantage est à un clic d'un ensemble
entièrement bilingue. Le laisser tel quel ne demanderait ni ADR, ni traduction, ni
modification des contrôles.

Rejeté parce que cela fait de la porte d'entrée la seule étape unilingue du
parcours. L'ensemble situé derrière est bilingue, le README y renvoyait déjà deux
fois en français, et l'argument que la page porte est celui dont l'ADR-0022 disait
qu'il devait être compris précisément. Un lecteur qui ne peut pas suivre le README
n'atteint pas les guides dont la traduction se justifiait par ce besoin même.

### Garder la porte d'entrée de la documentation et nommer autrement le README français

La porte d'entrée aurait pu rester `doc/README.en.md` et `doc/README.fr.md`, et la
moitié française du README du projet prendre un nom distinct tel que
`doc/project-readme.fr.md`. Rien n'aurait été fusionné, aucun lien entrant n'aurait
bougé, et le changement aurait été purement additif.

Rejeté parce que cela garde deux pages faisant un seul travail et en ajoute une
troisième. Le doublon existait déjà en anglais ; une politique bilingue l'aurait
doublé, laissant une porte d'entrée française et un README français côte à côte
disant l'essentiel des mêmes choses, avec une seconde graphie de « readme » inventée
pour les distinguer.

### Placer la moitié française à la racine du dépôt, sous le nom `README.fr.md`

Cela garde la paire dans un seul dossier, rend la relation évidente à quiconque
liste la racine, et ne demande aucun changement au calcul des homologues.

Rejeté parce que la racine est la contrainte de GitHub sur un fichier, non un
domicile pour l'ensemble documentaire. Une seconde page Markdown de premier niveau
concurrence celle que la page du dépôt affiche, et place une page de documentation
hors du dossier dont les conventions la régissent — une page qui serait alors
bilingue par politique tout en siégeant là où cette politique ne dit rien.

### Réduire le README racine à un résumé et tenir la paire entièrement sous `doc/`

La paire ne demanderait alors aucun homologue inter-dossiers et suivrait la
convention existante sans la moindre exception, la racine ne portant qu'un titre et
un lien vers l'ensemble.

Rejeté parce que le README racine est ce que nuget.org, les moteurs de recherche et
la page du dépôt affichent réellement. Dégrader la page que la plupart des lecteurs
voient, afin de protéger une règle de nommage, échange le public contre la
convention — et le garder entier à côté d'une copie sous `doc/` créerait au
contraire deux pages anglaises portant les mêmes affirmations, sans que rien ne dise
laquelle fait foi.

## Consequences

### Positive

* La page que la plupart des lecteurs voient en premier existe dans les deux
  langues, et l'argument qu'elle porte — une catégorie fausse ne produit aucun
  symptôme — atteint le lecteur pour qui l'ADR-0022 a été écrit.
* La politique linguistique cesse de reposer sur une frontière qui décrit un système
  de fichiers, et énonce à la place quel moteur de rendu impose quelle exception.
* Une page en remplace deux. Le panneau indicateur vers le guide, la spécification,
  les décisions et les conventions est une section du README : un lecteur qui arrive
  sur le dépôt est à une page de tout, au lieu de deux.
* La paire est vérifiée par les mêmes théories que toute autre page : une moitié
  manquante, un titre disparu, une ligne de tableau ajoutée d'un seul côté, une
  bannière qui ne pointe nulle part.

### Negative

* Le README du projet ne peut plus être modifié seul. Une ligne de badge, un paquet
  ajouté à un tableau, une phrase corrigée : chacun compte désormais deux éditions,
  et la théorie de parité refuse qu'une seule atterrisse.
* La relation d'homologie n'est plus déductible du nom de fichier. Une paire du dépôt
  est déclarée au lieu d'être calculée, et le lecteur des contrôles doit rencontrer
  cette déclaration pour comprendre pourquoi.
* `doc/` est désormais le seul dossier dont l'index est scindé : `README.fr.md` y
  siège et son homologue anglais non, ce qui se lit comme un oubli tant qu'on n'en
  connaît pas la raison.
* Un contributeur qui n'écrit pas le français ne peut plus mener seul un changement
  du README — la barrière que l'ADR-0022 acceptait pour `doc/`, étendue à la page la
  plus susceptible d'attirer une contribution extérieure.

### Risks

* La moitié française dérive de sens tout en gardant sa forme. Les théories de parité
  comptent les titres, les exemples, les puces et les lignes de tableau ; elles ne
  lisent pas le français, et le README est la page où une phrase périmée est la plus
  visible du plus grand nombre.
* Le README grossit. Absorber la porte d'entrée a ajouté une section à une page déjà
  longue, et chaque ajout futur à l'ensemble documentaire plaidera pour une ligne de
  plus — avec désormais le coût de traduction attaché à chacun.
* L'exception appelle de la compagnie. Un dépôt qui apparie un fichier par-delà la
  frontière peut se voir demander d'apparier `CONTRIBUTING.md`, `SECURITY.md` et le
  reste de la racine ; la *Decision* nomme un document, et la raison — un moteur de
  rendu qui fixe le nom et la place d'une page de documentation — est ce que tout
  candidat futur devra plaider.

## Follow-up Actions

* Réénoncer la règle dans [`doc/CONVENTIONS.en.md`](../CONVENTIONS.en.md),
  [`CONTRIBUTING.md`](../../CONTRIBUTING.md) et [`CLAUDE.md`](../../CLAUDE.md), où
  chacun affirme aujourd'hui que `doc/` est le seul endroit bilingue.
* Apprendre aux contrôles de documentation l'unique paire inter-dossiers déclarée, et
  porter la bannière de langue sur les deux moitiés.
* Fusionner la porte d'entrée dans le README du projet, traduire le résultat, et
  garder les deux moitiés dans le même commit que toute autre paire.

## References

* [ADR-0022](0022-maintain-every-document-under-doc-in-english-and-french.fr.md) — la
  politique bilingue que cet enregistrement étend, et le raisonnement dont il
  argumente.
* [ADR-0004](0004-state-the-coding-rules-where-an-agent-can-act-on-them.fr.md) — une
  règle est consignée là où l'outillage qui l'applique peut la lire.
* [ADR-0005](0005-require-an-enforcing-check-before-any-automation-merges.fr.md) — le
  même standard appliqué à ce qu'une automatisation a le droit de fusionner.
* [`doc/CONVENTIONS.fr.md`](../CONVENTIONS.fr.md) — la mise en page, et ce que les
  tests de documentation vérifient.
* [`first-class-errors`](https://github.com/Reefact/first-class-errors) — le projet
  frère, dont le README racine est apparié de la même façon.
