# ADR-0018 | Un correctif ne décide jamais ce que seul l'auteur peut décider

🌍 **Langues :**  
🇬🇧 [English](./0018-a-code-fix-never-decides-what-only-the-author-can.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-07-31
**Accepted:** 2026-07-31
**Decision Makers:** Reefact

## Context

Les analyseurs signalent quatre diagnostics sur un site de suppression, dont
trois livrent un correctif. Chacun des trois a rencontré un cas où la réparation
n'est pas déterminée de façon unique par le code :

* `DCAT0006` signale des littéraux de chaîne qu'une règle de catalogue
  remplacerait. Quand deux catalogues décrivent la même règle d'éditeur, tous
  deux correspondent à la même paire `(Category, Id)` et l'un ou l'autre pourrait
  être celui visé.
* `DCAT0007` signale une suppression à moitié migrée — une référence, un
  littéral. Quand le littéral nomme quelque chose que la règle référencée ne
  déclare pas, la suppression fait taire un diagnostic aujourd'hui et en ferait
  taire un autre une fois complétée.
* `DCAT0001` signale une catégorie et un identifiant pris à deux règles
  différentes. L'un ou l'autre argument pourrait être la faute de frappe.

La spécification décide chacun de ces cas séparément : le §11.6 donne à
l'appariement ambigu un diagnostic sans correctif automatique unique, le §11.7
décrit la complétion déterministe, et le §12.1 exige deux correctifs pour une
paire incohérente et énonce que le correctif ne doit jamais deviner quelle règle
était visée. Elle n'énonce aucune règle générale couvrant les trois, et des
diagnostics ultérieurs — `DCAT0008`, `DCAT0010`, et les correctifs de déclaration
du §12.4 — sont spécifiés sans une telle règle.

Deux faits sur la plateforme pèsent sur le comportement pratique d'un tel
correctif. Roslyn apparie une suppression sur son seul identifiant et ne consulte
jamais la catégorie ; corriger la catégorie d'une paire incohérente laisse donc
inchangé ce qui est supprimé, tandis que corriger l'identifiant le change. Et
chaque correctif ici est proposé via *Corriger toutes les occurrences*, qui
applique un choix à travers un document, un projet ou une solution sans que
l'auteur voie chaque site.

Une première tentative d'énoncer le principe partagé — qu'un correctif ne change
jamais ce qu'une suppression supprime — a été écrite puis trouvée fausse pendant
l'implémentation du §12.1 : l'une des deux corrections que cette section impose
change exactement cela.

Le raisonnement derrière chaque refus individuel vit aujourd'hui dans le
commentaire à côté. Rien n'énonce que les trois sont une seule position, et la
pression pour ajouter un choix par défaut va dans l'autre sens : un correctif qui
ne propose rien, ou qui propose deux options sans en recommander aucune, se lit
comme une fonctionnalité inachevée plutôt que comme une décision.

## Decision

Un correctif de ce dépôt n'effectue que des réparations déterminées de façon
unique par le code qu'il lit, et ne propose aucun correctif automatique — ou
propose chaque candidat sans les classer — partout où plus d'une réparation est
défendable.

## Rationale

Les trois cas du contexte sont la même situation sous des habits différents : le
code admet plus d'une réparation, et l'information qui trancherait est
l'intention de l'auteur, laquelle n'est pas dans le code. Un correctif qui en
choisit une n'est pas serviable ; il devine, et il devine silencieusement, parce
qu'un correctif appliqué ne laisse aucune trace de l'alternative qu'il a écartée.

*Corriger toutes les occurrences* est ce qui rend cette supposition coûteuse
plutôt que simplement fausse. Une suggestion erronée isolée est attrapée quand
l'auteur lit le résultat ; la même suggestion appliquée à travers une solution
réécrit des centaines de sites que personne ne lit, et les sites qu'elle abîme
sont ensuite indiscernables de ceux qu'elle a réparés. Le mécanisme que la
bibliothèque offre pour migrer une base de code en un geste est précisément celui
qui rend un choix sans fondement irrelisible.

Le silence a un coût et c'est le moindre. Un diagnostic signalé sans correctif
nomme quand même le problème et son emplacement, et l'auteur le répare avec la
connaissance qui manquait à l'outil. L'inverse — un correctif assuré bâti sur une
supposition — coûte la chose même que la bibliothèque existe pour fournir,
puisqu'une suppression qui fait taire le mauvais diagnostic est exactement la
défaillance invisible que toute la conception vise.

Le cas `DCAT0001` montre pourquoi la règle doit porter sur le fait de *décider*
plutôt que sur les conséquences, et pourquoi la formulation plus étroite était la
mauvaise. Parce que Roslyn ignore la catégorie, l'une des deux corrections
imposées est inoffensive et l'autre non, et il est tentant de ne proposer que
l'inoffensive. Ce serait encore un choix fait au nom de l'auteur, et il serait
faux chaque fois que l'identifiant était l'argument écrit correctement. Le
classement est une forme plus faible de la même erreur : une option présentée en
premier est celle qu'on accepte sans lire.

Enregistrer la position plutôt que la laisser dans trois commentaires est ce qui
la fait survivre à la pression décrite dans le contexte. Chaque refus, lu seul,
ressemble à une lacune que quelqu'un pourrait utilement combler ; lus ensemble,
ils forment une politique, et un futur diagnostic en hérite au lieu de trancher à
nouveau cas par cas. La spécification ne peut pas servir cet objet ici parce
qu'elle décide les trois instances sans énoncer ce qu'elles ont en commun.

## Alternatives Considered

### Laisser les trois décisions là où la spécification les a mises

Envisagé parce que la spécification décide déjà les trois cas, et qu'une ADR
réénonçant des décisions prises ailleurs ajoute un second endroit dont elles
peuvent diverger. La vérification qu'impose CLAUDE.md est l'habitude, pas
l'artefact, et la plupart des changements n'en produisent à juste titre aucun.

Rejeté parce que la spécification décide les instances et n'énonce jamais la
règle ; elle ne donne donc aucune orientation pour les diagnostics encore à
écrire. La preuve que la règle partagée n'est pas évidente est qu'il a fallu deux
tentatives pour l'énoncer correctement, la première n'ayant été trouvée fausse
que lorsque l'implémentation l'a contredite.

### Proposer un correctif préféré et marquer les autres en alternatives

Envisagé parce que c'est ce que font la plupart des paquets d'analyseurs, et
parce qu'un auteur face à deux options sans recommandation peut raisonnablement
demander laquelle l'outil juge la bonne.

Rejeté parce qu'une préférence est un choix, et que l'information qui la
justifierait est absente du code par définition de ces cas. Sous *Corriger toutes
les occurrences*, l'option préférée est celle qui est appliquée partout ; le
classement n'adoucit donc pas la supposition — il la met à l'échelle.

### Décider chaque cas futur sur ses propres mérites

Envisagé parce que les trois cas diffèrent dans le détail, et qu'une règle
générale risque d'interdire une réparation réellement sûre dans un cas pas encore
rencontré.

Rejeté parce que décider cas par cas est ce qui a produit les trois commentaires
sans lien, et parce que la règle telle qu'énoncée n'interdit aucune réparation
sûre : une réparation déterminée de façon unique reste entièrement automatique,
ce que la complétion déterministe de `DCAT0007` et le remplacement ordinaire de
`DCAT0006` sont déjà.

## Consequences

### Positive

* Une migration appliquée à travers une solution ne change que ce que le code
  détermine ; le résultat est donc relisible comme une transformation mécanique
  plutôt que comme un ensemble de suggestions.
* Un diagnostic sans correctif signale quand même ; aucun cas n'est donc caché
  par l'absence de réparation.
* Les diagnostics encore à écrire héritent d'une position énoncée au lieu d'en
  redériver une, et un relecteur a quelque chose à quoi confronter un nouveau
  correctif.

### Negative

* Certains cas signalés n'ont aucune réparation automatique, ce qui se lit comme
  une fonctionnalité incomplète pour qui n'en connaît pas la raison.
* Un auteur face à deux corrections non classées doit comprendre la différence
  entre elles avant de choisir, ce que le message de diagnostic et la
  documentation du paquet doivent porter.

### Risks

* La règle peut être honorée à la lettre et trahie dans l'esprit en déclarant un
  cas « déterminé de façon unique » sur un argument mince. L'atténuation est que
  chaque affirmation de ce genre est une assertion testable sur le code, et que
  le test d'un correctif refusé asserte que le diagnostic a bien été signalé — un
  correctif qui se mettrait discrètement à proposer une réparation ne peut donc
  pas passer pour un correctif qui n'en a jamais eu.
* Un futur Roslyn qui apparierait les suppressions sur la catégorie autant que
  sur l'identifiant changerait quelles réparations sont conséquentes, mais pas
  lesquelles sont déterminées. La dépendance est épinglée par un test plutôt que
  laissée en hypothèse.

## Follow-up Actions

* Appliquer la position aux correctifs de `DCAT0008` et `DCAT0010` quand ces
  diagnostics seront écrits, et aux correctifs de déclaration du §12.4 s'ils sont
  construits.
* Maintenir la documentation du paquet énonçant, pour chaque cas qui ne propose
  aucun correctif ou propose un choix non classé, ce dont l'auteur a besoin pour
  décider.

## References

* Spécification §11.6, §11.7, §12.1 — les trois décisions que celle-ci
  généralise.
* ADR-0010 — un refus voisin de laisser l'outillage retirer silencieusement
  quelque chose sur quoi un consommateur compte.
* Pull requests #35, #38, #40 et #42 — où les trois cas ont été implémentés.
