# ADR-0025 | Lier chaque commit de fonctionnalité à la documentation qu'il a changée

🌍 **Langues :**  
🇬🇧 [English](./0025-bind-every-feature-commit-to-the-documentation-it-changed.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-08-01
**Accepted:** 2026-08-01
**Decision Makers:** Reefact

## Context

Le dépôt applique une couverture documentaire sur quatre surfaces, toutes
énumérables depuis un fichier que quelque chose d'autre maintient déjà vrai :

* chaque identifiant `DCAT` que les analyseurs déclarent dans leurs fichiers
  `AnalyzerReleases` est documenté sur une page nommée, et chaque identifiant que
  cette page documente est déclaré ;
* chaque option longue que les types de configuration de `dcat` déclarent
  apparaît dans la page de référence, et chaque option que cette page mentionne
  est déclarée ;
* chaque référence de règle qu'un document montre se résout contre le catalogue
  qui la publie ;
* chaque README de catalogue nomme ses frères et porte son bandeau miroir.

Quatre vérifications supplémentaires — parité bilingue, résolution des liens,
ordre de lecture, bandeau de langue — contraignent une page une fois qu'elle
existe. Aucune ne provoque l'écriture d'une page.

Tout le reste de ce qu'un changement peut ajouter atteint une release sans
aucune vérification. Une commande `dcat`, un type public, une propriété MSBuild,
une clé de manifeste, un train de release, un workflow, un hook, un script de
`tools/`, une entrée de changelog : rien dans la build, la suite de tests ou la
chaîne n'observe si l'un d'eux a été écrit quelque part.

Le seul dispositif polyvalent en place est une case à cocher de pull request
libellée *README / documentation updated* à côté d'une autre libellée *No
documentation change required*. Rien ne lit ni l'une ni l'autre. Une pull request
est fusionnée avec les deux cochées, avec aucune, ou avec la première cochée et
aucune documentation dans le diff.

[`CONTRIBUTING.md`](../../CONTRIBUTING.md) énonce l'attente en prose : *« Une
fonctionnalité arrive avec ses tests, sa documentation d'API, son exemple : le
commit reste un `feat`. »* Le même document définit `feat` comme *« Une nouvelle
capacité, visible pour le consommateur du paquet »*, et exige un scope sur `feat`
et `fix` parce qu'un commit sans scope est silencieusement retiré du registre de
release.

Deux décisions acceptées portent déjà sur l'application.
[ADR-0004](0004-state-the-coding-rules-where-an-agent-can-act-on-them.fr.md)
enregistre qu'une règle déléguée à un artefact que rien ne lit dérive — dans le
dépôt frère qu'elle nomme, jusqu'à 203 violations.
[ADR-0005](0005-require-an-enforcing-check-before-any-automation-merges.fr.md)
enregistre qu'une garantie reposant sur autre chose qu'une vérification
bloquante n'est pas une garantie.

Le dépôt dispose déjà d'un idiome pour une exemption écrite. Un document qui
montre une référence de règle qu'aucun catalogue ne publie la déclare dans un
commentaire portant une raison, et une déclaration dont la page ne montre plus la
référence échoue ([`doc/CONVENTIONS.fr.md`](../CONVENTIONS.fr.md)).

Les messages de commit sont déjà lintés contre une convention fermée par un
script partagé par le hook local et par la CI
([ADR-0003](0003-adopt-and-enforce-a-conventional-commits-convention.fr.md)), et
cette convention porte déjà des footers dont la forme est vérifiée : `Refs:` et
`BREAKING CHANGE:`.

## Decision

Chaque commit `feat` porte un footer `Docs:` nommant la documentation qu'il a
changée, ou énonçant en toutes lettres pourquoi il n'en a changé aucune.

## Rationale

L'obligation frôle la tautologie selon les termes mêmes de ce dépôt. Un `feat`
est défini par ce que le consommateur du paquet peut observer ; une capacité que
le consommateur peut observer et sur laquelle il ne peut rien lire est soit non
documentée, soit mal typée. Faire répondre à l'auteur laquelle des deux coûte une
ligne, et c'est tout le contenu de la règle.

La couverture énumérable ne peut pas être la réponse à elle seule. Chacune des
quatre vérifications existantes fonctionne en comparant un document à un ensemble
que quelque chose d'autre maintient vrai, et c'est exactement ce qui les rend
dignes de confiance — mais c'est aussi pourquoi elles n'atteindront jamais une
propriété de build, un workflow, ou une page du guide dont rien ne dépend hormis
un lecteur. L'ensemble des choses qu'une fonctionnalité peut ajouter est ouvert ;
celui qu'une vérification peut énumérer ne l'est pas. Étendre la couverture vaut
la peine partout où l'énumération existe, et cela laisse le cas général intact.

Un footer est ce que le dépôt peut réellement faire respecter sur le cas général.
Il n'affirme pas que la documentation est bonne, ni même qu'elle est juste —
aucun mécanisme ne le peut. Il affirme que quelqu'un a décidé, et que la décision
est dans le registre permanent à côté du changement auquel elle appartient. C'est
une barre plus basse qu'une vérification de couverture et bien plus haute qu'une
case à cocher que personne ne lit, et c'est la barre qu'ADR-0004 et ADR-0005
posent déjà : énoncer la règle là où quelque chose la lit.

Le commit est le bon porteur, plutôt que la pull request. La convention traite
déjà le commit comme l'unité du changement et place déjà le registre de release,
le signal de changement cassant et la référence d'issue dans ses footers ; cela
réemploie donc un endroit que les auteurs et les outils regardent déjà tous les
deux. Une pull request est un endroit qu'un relecteur regarde une fois.

L'exemption doit être une phrase, pour la raison que les tests de documentation
donnent déjà : une exemption sans raison est un trou que personne ne peut juger.
Exiger des mots plutôt qu'un mot-clé rend aussi visible le cas malhonnête —
« aucune » avec une raison qui ne survit pas à la lecture est une chose qu'un
relecteur peut désigner, ce que « aucune » seul n'est pas.

Lier la règle à `feat` et non à `fix` découle de ce que les deux types signifient
ici. Un correctif rétablit un comportement que la documentation promet déjà ; la
réponse honnête serait donc presque toujours que rien n'a changé, et un footer
dont la valeur habituelle est « rien » entraîne tout le monde à écrire « rien »,
et la règle cesse d'être lue au moment où elle compte. C'est sur `feat` que la
dette documentaire est réellement contractée.

Scinder la vérification en deux — la forme du message là où le message est linté,
la véracité du footer là où le commit existe — suit la division que la chaîne
trace déjà entre demander si un script est bien formé et demander s'il est juste.
Cela règle aussi une limite plutôt que de la cacher : le hook se déclenche avant
que le commit existe et ne peut pas résoudre un chemin contre un diff ; cette
moitié-là tourne donc là où elle peut être répondue honnêtement, au lieu d'être
approximée là où elle ne le peut pas.

## Alternatives Considered

### Laisser la case à cocher de pull request comme toute la règle

Envisagé parce qu'elle est déjà là, qu'elle ne coûte rien, et qu'elle met la
question devant l'auteur au moment où il ouvre la demande.

Rejeté parce que rien ne la lit. Elle est satisfaite en la cochant, ce qui la
rend indiscernable d'une règle qui fonctionne, et elle disparaît du registre dès
que la pull request est fusionnée. C'est précisément la défaillance qu'ADR-0004
enregistre et qu'ADR-0005 généralise.

### Exiger que le diff de la pull request touche un fichier de documentation

Envisagé parce que cela n'exige aucune nouvelle convention ni aucun footer : un
job pourrait lire les chemins modifiés de la demande et échouer quand un `feat`
atterrit sans rien sous `doc/`.

Rejeté parce que cela mesure la mauvaise chose dans les deux sens. Une pull
request portant une fonctionnalité et une correction de coquille sans rapport
dans le guide passe sans rien documenter, et une fonctionnalité qui n'a
réellement besoin d'aucune page échoue sans moyen de le dire, sinon en écrivant
une page dont personne ne veut. Aucun des deux résultats ne laisse une trace
qu'un futur lecteur puisse peser ; le footer enregistre la réponse de l'auteur,
ce qui est la chose qui vaut la peine d'être conservée.

### Lier le footer à `fix` autant qu'à `feat`

Envisagé parce qu'un correctif peut changer un comportement documenté, et que la
règle serait alors uniforme sur les deux types qui pilotent une version.

Rejeté parce que la réponse honnête habituelle sur un correctif est que la
documentation dit déjà ce que le code fait désormais. Un champ obligatoire dont
la valeur correcte la plus courante est « rien » se remplit sans être lu, et cela
dévaloriserait le footer sur le type où il pèse. Un correctif qui change bel et
bien ce qui est documenté peut porter le footer ; rien ne l'interdit.

### N'ajouter que des tests de couverture, et aucun footer

Envisagé parce qu'un test de couverture est une vérification de vérité et qu'un
footer est une déclaration, et que les vérifications de vérité sont strictement
meilleures là où elles sont possibles.

Rejeté parce que cela répond à une autre question que celle posée. La couverture
peut être étendue à l'API publique et à l'arbre des commandes, et les deux valent
la peine, mais cela laisse chaque surface non énumérable exactement où elle est :
sans aucune vérification. Choisir seulement le mécanisme qui ne peut pas couvrir
le cas général, c'est choisir de ne pas le couvrir.

### Exiger le footer sur chaque type de commit

Envisagé parce que cela supprime un jugement — aucun auteur n'a à décider si son
changement est du genre qui l'exige.

Rejeté parce que la plupart des types ne peuvent pas contracter de dette
documentaire par construction : `style`, `test`, `refactor` et `perf` promettent
tous un comportement observable constant, et `docs` est de la documentation. Le
footer serait du bruit sur la majorité des commits et serait survolé sur la
minorité où il compte.

## Consequences

### Positive

* Une fonctionnalité ne peut plus atteindre une release sans que personne ait
  décidé si elle avait besoin de documentation, et la décision est dans
  l'historique plutôt que dans les cases d'une pull request fusionnée.
* Le cas général est couvert — une propriété de build, un workflow, un hook, une
  page du guide — là où aucune vérification énumérable ne peut atteindre.
* Le cas refusé est visible et relisible, parce que c'est une phrase plutôt
  qu'une case non cochée.
* Une pull request qui documente une page dans une seule langue est signalée, ce
  que rien ne faisait avant : les deux fichiers existent, la vérification de
  parité est donc satisfaite.

### Negative

* Chaque commit de fonctionnalité porte une ligne de plus, et un auteur qui
  l'oublie doit réécrire le message plutôt qu'ajouter un commit.
* Le footer enregistre une affirmation, pas un fait. Il peut être acquitté
  malhonnêtement en écrivant une raison qui ne résiste pas à l'examen, et aucune
  vérification ne le dira.
* La règle n'atteint le hook local que comme vérification de forme ; la moitié
  qui résout le footer contre le commit tourne en CI, un auteur qui ne pousse
  jamais l'apprend donc tard.

### Risks

* L'exemption devient un réflexe — `Docs: none` avec une raison formulaire sur
  chaque fonctionnalité. Atténuation : la raison est une phrase du registre
  permanent, relisible d'une façon qu'une case à cocher n'est pas ; les
  consignes de revue traitent déjà une étape de processus obligatoire manquante
  comme un constat bloquant.
* Le footer est lu comme remplaçant les vérifications de couverture, et une
  surface qui pourrait être énumérée est laissée à une déclaration. Atténuation :
  cet enregistrement énonce que la couverture est préférée partout où
  l'énumération existe, et les deux nouvelles vérifications arrivent avec lui.
* Un dépôt qui fusionne par commit de fusion accumule de l'histoire ; une
  convention de footer introduite maintenant ne s'applique donc à aucun commit
  déjà sur `main`, et un lecteur du journal trouvera des fonctionnalités sans
  footer. Atténuation : aucune nécessaire — la convention est datée par cet
  enregistrement.

## Follow-up Actions

* Étendre la couverture énumérable partout où existe une source que la build
  maintient déjà vraie : les fichiers d'API publique et l'arbre des commandes
  `dcat` sont couverts par le changement qui porte cet enregistrement ; les
  propriétés MSBuild sous `build/` et les clés d'`eng/catalogs.json` sont les
  candidates suivantes et ne sont pas couvertes.
* Relire la section documentation du gabarit de pull request contre cette règle,
  pour qu'elle demande ce que le footer enregistre plutôt qu'une coche.

## References

* [ADR-0003](0003-adopt-and-enforce-a-conventional-commits-convention.fr.md) — la
  convention de commit que ce footer rejoint, et le linter partagé par le hook et
  la CI.
* [ADR-0004](0004-state-the-coding-rules-where-an-agent-can-act-on-them.fr.md) —
  une règle énoncée là où rien ne la lit dérive.
* [ADR-0005](0005-require-an-enforcing-check-before-any-automation-merges.fr.md) —
  une garantie qui ne repose pas sur une vérification bloquante n'en est pas une.
* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.fr.md) — la
  norme que les vérifications de couverture atteignent : ne jamais comparer une
  affirmation à une autre affirmation.
* [ADR-0022](0022-maintain-every-document-under-doc-in-english-and-french.fr.md) —
  pourquoi le footer exige les deux moitiés d'une paire bilingue.
* [`doc/CONVENTIONS.fr.md`](../CONVENTIONS.fr.md) — ce contre quoi la
  documentation est vérifiée, et l'idiome d'exemption écrite que cette règle
  réemploie.
* `tools/commit-lint/lint-commit-message.sh` et
  `tools/commit-lint/check-docs-footer.sh` — les deux moitiés de la vérification.
