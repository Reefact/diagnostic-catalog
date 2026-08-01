# ADR-0004 | Énoncer les règles de code là où un agent peut les appliquer

🌍 **Langues :**  
🇬🇧 [English](./0004-state-the-coding-rules-where-an-agent-can-act-on-them.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-07-30
**Accepted:** 2026-07-30
**Decision Makers:** Reefact

## Context

La pratique établie du mainteneur consigne le style de code dans un fichier
`.DotSettings` ReSharper/Rider. Ce fichier est lu par Rider et par rien d'autre :
aucun compilateur ne le lit, aucun job CI ne le lit, et aucun agent automatisé ne
peut l'ouvrir utilement.

Dans le dépôt frère dont proviennent les conventions de celui-ci, le guide de
projet déléguait ses règles de style à ce fichier — « suivez-le ». Résultat
mesuré : la règle de type explicite a dérivé jusqu'à 203 violations.
L'instruction se lisait comme une instruction sans être actionnable pour aucun
lecteur incapable d'ouvrir le fichier.

Une part significative du code ici est écrite par des agents automatisés. Ils
n'ouvrent jamais d'IDE, et ils apprennent les règles du dépôt en lisant ses
fichiers.

Roslyn peut exprimer un sous-ensemble de ces règles en diagnostics `IDE*`
configurés via `.editorconfig` — mais seulement quand la propriété du SDK qui
exécute les analyseurs de style pendant une build est activée. Sans elle,
`.editorconfig` est lu et rien ne signale quoi que ce soit à la compilation.

Un autre sous-ensemble n'a aucun équivalent Roslyn : l'alignement en colonnes de
déclarations consécutives, les motifs de disposition de fichier, les conventions
de régions. Aucun outil disponible pour un contributeur sans Rider ne peut les
reproduire.

Le dépôt promeut déjà chaque avertissement en erreur en CI.

## Decision

Une règle de code à laquelle les contributeurs sont tenus est énoncée dans le
guide de projet et vérifiée par au moins un mécanisme qui s'exécute hors d'un
IDE ; aucune règle de ce genre ne repose sur un artefact propre à Rider.

## Rationale

Une règle qu'un seul outil peut lire n'est vérifiée que tant que cet outil est
ouvert. Ce n'est pas une hypothèse ici : c'est l'histoire mesurée de la règle
exacte dont ce dépôt hérite. Écrire la règle là où chaque lecteur peut la
trouver — un humain sans Rider, un agent, un relecteur qui lit le guide — est ce
qui en fait une règle plutôt qu'une préférence que l'IDE applique par hasard.

L'application est en couches parce que les couches ont des latences différentes
et attrapent des manques différents. Un hook au moment de l'édition signale la
violation pendant que l'auteur est encore dans le fichier. La build signale la
même chose à quiconque compile, y compris à un contributeur sans hook installé.
La CI la transforme en erreur, et c'est là qu'elle cesse d'être négociable.
Chaque couche couvre le lecteur que la précédente n'atteint pas, et les trois
signalent la même règle.

Garder le rapport de la build en avertissement localement, et en erreur seulement
en CI, est délibéré : une boucle interne qui refuse de compiler un refactoring à
moitié fait rend l'itération hostile, tandis qu'un avertissement que la CI
promouvra ne coûte rien à ignorer dix minutes et rien à corriger avant de
pousser. Le cliquet est là où la règle devient contraignante, et il est placé
après que l'auteur a cessé de travailler, pas pendant.

Le coût accepté est la duplication : la règle est énoncée en prose et configurée
dans `.editorconfig`, et si un fichier `.DotSettings` est ajouté, il l'énoncera
une troisième fois. La duplication reste honnête parce que toutes les copies
disent la même chose, et parce que le guide nomme le fichier qui fait foi pour la
vérification.

Les règles sans équivalent Roslyn ne sont pas abandonnées ; elles sont
rétrogradées. Elles peuvent vivre dans un artefact Rider, mais ce sont alors des
mises en forme que l'IDE applique, pas des règles auxquelles un contributeur est
tenu — d'où le fait que le guide associe cette décision à une consigne permanente
de ne pas reformater du code qu'on n'a pas modifié. Sans cet appariement, un
contributeur sans Rider ferait dériver la mise en page à chaque fichier touché,
et un formateur enfouirait les vrais changements sous des reflows.

## Alternatives Considered

### Garder le `.DotSettings` comme référence et y renvoyer les contributeurs

Envisagé parce que c'est la pratique existante, que cela n'exige aucun nouveau
fichier, et que Rider reproduit exactement la disposition du dépôt — ce qu'aucun
autre outil ne fait.

Rejeté parce que c'est l'arrangement dont l'échec est déjà mesuré. Il laisse
chaque lecteur sans Rider — y compris chaque agent — incapable de se conformer,
et transforme l'instruction du guide en une instruction impossible à suivre.

### Exécuter un formateur en CI et le laisser réécrire le code

Envisagé parce que cela supprime entièrement la question : le code est normalisé
quoi qu'on écrive.

Rejeté pour deux raisons. Un formateur qui rattrape derrière l'auteur soustrait
la production de l'auteur à la correction : rien n'est appris et le commit
suivant répète l'erreur. Et le formateur disponible ne peut pas reproduire les
conventions de disposition que le style du dépôt encode ; il ne convergerait donc
pas vers le style du dépôt — il s'en éloignerait tout en paraissant le faire
respecter.

### Ne faire respecter la règle qu'en CI, et ne rien énoncer dans le guide

Envisagé comme le plus petit mécanisme qui empêche encore une violation d'être
fusionnée.

Rejeté parce que le retour arrive alors une fois la pull request ouverte, sur du
code déjà écrit, ce qui est précisément l'arrangement qui a laissé la règle
dériver. Une règle qu'on découvre par une vérification rouge est une règle que
personne n'a énoncée.

### Ne faire respecter la règle que par le hook d'édition

Envisagé parce qu'il donne le retour le plus rapide et atteint les agents qui
écrivent une grande partie du code.

Rejeté parce qu'il n'atteint que les agents dont le harnais exécute le hook. Un
contributeur humain, un autre outil, ou un hook contourné ne rencontreraient
rien, et le hook ne bloque rien à l'entrée.

## Consequences

### Positive

* Chaque lecteur — humain, agent, compilateur, CI — peut trouver la règle et
  l'appliquer.
* Une violation est signalée à l'édition, à la build et à la barrière de fusion,
  dans les mêmes termes.
* Ajouter une règle est une opération définie : l'énoncer dans le guide, et
  nommer comment elle est vérifiée.

### Negative

* La même règle est énoncée dans plus d'un fichier, et les copies doivent changer
  ensemble.
* Les règles que Roslyn ne peut pas exprimer ne sont vérifiées nulle part, et
  reposent sur la consigne de non-reformatage pour rester stables.
* Les contributeurs qui compilent localement voient un avertissement que la CI
  traitera comme fatal ; une build locale propre n'est donc pas la preuve d'une
  build CI propre.

### Risks

* La règle en prose et la gravité dans `.editorconfig` divergent, si bien que le
  guide décrit une règle que la build ne signale pas. Atténuation : le guide
  énonce, règle par règle, comment elle est vérifiée, si bien qu'une règle sans
  vérification énoncée est visiblement incomplète.
* La liste enfle jusqu'à devenir un manuel de style que personne ne lit.
  Atténuation : le guide n'admet une règle que lorsqu'elle énonce son mécanisme
  d'application, ce qui borne la liste à ce qui est réellement vérifié.

## Follow-up Actions

* Énoncer le mécanisme d'application à côté de chaque règle ajoutée au guide.
* Réévaluer si un fichier `.DotSettings` est introduit : il peut porter de la
  mise en page, jamais une règle à laquelle les contributeurs sont tenus.

## References

* [CLAUDE.md](../../CLAUDE.md) — « Coding rules ».
* `.editorconfig`, `Directory.Build.props` — l'application à la compilation.
* `.claude/hooks/coding-rules.sh` — le rapport à l'édition.
