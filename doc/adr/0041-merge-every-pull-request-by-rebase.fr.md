# ADR-0041 | Fusionner chaque pull request par rebase, jamais par un commit de merge

🌍 **Langues :**  
🇬🇧 [English](./0041-merge-every-pull-request-by-rebase.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-08-08
**Accepted:** 2026-08-08
**Decision Makers:** Reefact

## Contexte

Ce dépôt fusionnait ses pull requests avec un commit de merge jusqu'en août 2026. L'histoire de
`main` est désormais linéaire : les commits de merge en ont disparu, et les commits de chaque pull
request y figurent en séquence, à côté de tous les autres.

GitHub propose trois manières de clore une pull request, et elles diffèrent par ce qui atteint la
branche de base. Un commit de merge ajoute un commit qui nomme la branche et tient ses commits sous
lui, sans toucher à leur identité. Un rebase rejoue chaque commit sur la base et n'ajoute rien,
donnant à chaque commit rejoué une identité neuve. Un squash remplace toute la branche par un unique
commit.

Trois décisions déjà prises dépendent de ce qui survit à une fusion :

* L'[ADR-0002](0002-partition-releases-into-trains-by-commit-scope.fr.md) partitionne les releases en
  trains par **scope** de commit : une note de release et une entrée de changelog sont donc bâties à
  partir des scopes que portent les commits individuels.
* L'[ADR-0003](0003-adopt-and-enforce-a-conventional-commits-convention.fr.md) exige un en-tête
  Conventional Commits sur chaque commit hors merge, imposé par un hook local et par un job de CI
  requis. Son contexte énonce la stratégie du commit de merge comme un fait, et sa justification en
  déduit que la vérification a sa place sur la pull request plutôt que sur le résultat de la fusion.
* L'[ADR-0025](0025-bind-every-feature-commit-to-the-documentation-it-changed.fr.md) lie un commit de
  fonctionnalité à la documentation qu'il modifie, et sa justification cite elle aussi un dépôt qui
  « fusionne avec un commit de merge ».

Un commit de merge ne porte aucun scope Conventional Commits. Le linter de commits l'exempte, le job
de CI le filtre et l'outillage des notes de release le saute — de sorte que, sous la stratégie
précédente, `main` accumulait des commits qui n'atteignaient ni note de release ni changelog.

`CONTRIBUTING.md` exige qu'une branche présente une histoire propre avant de fusionner : marqueurs
d'autosquash résorbés, en-tête conforme sur chaque commit, une intention par commit. `AGENTS.md` fait
d'atteindre cet état un devoir permanent de l'agent qui prépare la branche, `CLAUDE.md` reprend la
règle en ligne, et un hook du dépôt la signale. De ce point d'arrivée, deux parties sont imposées
mécaniquement — les marqueurs `fixup!`, `squash!` et `amend!` encore pendants, et les en-têtes que le
linter rejette — tandis qu'« une intention par commit » et les commits d'échafaudage restent un
jugement humain.

Un marqueur d'autosquash est écrit pour être réécrit : `git rebase --autosquash` le replie dans le
commit qu'il nomme, et ce rebase a lieu sur la branche, avant la fusion.

Lorsque l'histoire de ce dépôt a été relinéarisée, chaque commit de `main` a reçu une identité neuve.
Un commit référencé par son identifiant avant ce moment ne se résout plus.

## Décision

Chaque pull request est fusionnée par rebase — ses commits rejoués un à un sur `main` — et ni commit
de merge ni squash n'est utilisé.

## Justification

Le registre des releases n'est pas affecté, et c'est ce qui rend le changement adoptable. Les trains
sont bâtis à partir des scopes que portent les commits individuels, et un rebase préserve ces commits
exactement comme le faisait un commit de merge : mêmes messages, même ordre, même granularité d'une
intention par commit. L'ADR-0002 et l'ADR-0003 continuent de fonctionner sans amendement, et
l'argument de l'ADR-0003 — la vérification a sa place sur la pull request plutôt que sur le résultat
de la fusion — tient inchangé : ce sont toujours les commits individuels qui atteignent `main`.

Ce qui change, c'est ce qui survit *par ailleurs*, et cela plaide pour la décision plutôt que contre
elle. Un commit de merge était un emballage : il nommait la branche, tenait ses commits ensemble, et
permettait à un lecteur de retrouver la pull request comme une unité longtemps après. Un rebase ne
laisse aucun emballage. Les commits eux-mêmes deviennent la seule trace que la branche ait existé, ce
qui est la raison la plus forte possible de leur imposer une exigence : une branche sale n'est plus
seulement admise dans `main`, elle *devient* `main`, commit par commit, sans rien qui marque où elle
commençait ni où elle finissait. La règle qui veut qu'une branche soit nettoyée avant de fusionner
est donc plus critique sous cette décision que sous celle qu'elle remplace, et toute lecture qui
tiendrait le rebase pour la stratégie la plus indulgente prend le problème à l'envers.

Le cas de l'autosquash montre concrètement l'enjeu aiguisé. Sous un commit de merge, un marqueur
passé au travers atterrissait non linté mais restait rattaché à une branche qu'un lecteur pouvait
encore reconstituer. Rejoué par rebase, il devient un commit ordinaire de `main` nommant un commit
dans lequel il aurait dû être replié — et il ne reste plus rien où le replier. C'est pourquoi le job
de CI en refuse un d'emblée plutôt que d'avertir, et ce refus vaut plus aujourd'hui qu'hier.

En regard, une histoire linéaire est celle que `bisect` et `blame` lisent le plus proprement : une
seule séquence, aucune branche latérale où descendre, aucun commit dont le diff est l'union du
travail d'autrui. Et elle retire de `main` la seule classe de commit qui ne portait aucun scope et ne
pouvait atteindre aucune note de release.

Le coût est accepté en connaissance de cause : l'histoire cesse d'enregistrer qu'un ensemble de
commits est arrivé ensemble. Ce regroupement était une information réelle, et rien dans les commits
ne la remplace. Elle ne survit qu'en dehors de l'histoire — dans la pull request elle-même, et dans
ce que les commits choisissent de référencer.

## Alternatives envisagées

### Conserver la fusion par commit de merge

Envisagée parce que c'est ce que trois enregistrements acceptés supposent déjà, parce qu'elle
préserve la pull request comme une unité à l'intérieur de l'histoire, et parce que la conserver
n'aurait rien coûté à écrire.

Rejetée parce que le regroupement qu'elle préserve est rarement ce que quiconque lit, tandis que ses
coûts se paient à chaque fusion : un commit en histoire permanente qui ne porte aucun scope,
n'atteint aucune note de release et échappe à la convention que tous les autres commits respectent,
plus une forme branchue où `bisect` et `blame` doivent descendre. L'emballage est un piètre substitut
à des commits individuellement propres, et exiger ces derniers est déjà la règle.

### Écraser chaque pull request en un seul commit

Envisagée parce qu'elle rend l'histoire de branche jetable — une branche sale ne coûterait rien, et
un seul message par changement aurait à être conforme.

Rejetée pour la raison que l'ADR-0003 donnait déjà en pesant la même option : le squash effondre
l'unité de changement. Un commit voyage seul, et remplacer plusieurs intentions par un message écrit
au moment de la fusion rendrait une pull request à plusieurs intentions irreprésentable dans le
registre de release que l'ADR-0002 bâtit à partir des scopes. C'est aussi l'arbitrage inverse de
celui pris ici : cette décision relève ce que doit valoir un commit, quand le squash supprimerait la
question.

### Autoriser plusieurs stratégies et choisir au cas par cas

Envisagée parce que certaines pull requests sont réellement d'une seule intention et se liraient bien
écrasées, tandis que d'autres gagnent à conserver leurs commits.

Rejetée parce que la stratégie est une propriété sur laquelle l'outillage raisonne, non une
préférence de fusion. Le linter de commits, l'outillage des notes de release et la règle d'hygiène
d'historique énoncent chacun ce qui atteint `main` ; une stratégie choisie au moment de fusionner
rendrait cet énoncé conditionnel à une décision que personne n'enregistre, et le choix le plus faible
disponible fixerait la norme réelle.

## Conséquences

### Positives

* `main` se lit comme une séquence unique, la forme que `git bisect` et `git blame` parcourent le
  plus directement.
* Chaque commit de `main` porte désormais un en-tête Conventional Commits et un scope que
  l'outillage de release sait lire. La seule classe exemptée — le commit de merge — n'existe plus.
* Le registre des releases est inchangé : les trains restent bâtis à partir des scopes des commits
  individuels.
* La règle du nettoyage avant fusion gagne une justification plus nette qu'auparavant. Elle ne repose
  plus sur « une branche sale atteint `main` » mais sur le fait plus fort qu'une branche sale
  *devient* `main`.

### Négatives

* Rien dans l'histoire n'enregistre qu'un ensemble de commits est arrivé ensemble. Une pull request
  n'est plus représentée par un commit qui lui soit propre, et retrouver ses frontières impose de
  sortir de l'histoire.
* Chaque commit d'une branche est réécrit à la fusion : un identifiant cité avant celle-ci — dans une
  issue, une revue, une entrée de changelog, les notes d'un agent — ne se résout plus sur `main`
  ensuite.
* Deux enregistrements acceptés, l'ADR-0003 et l'ADR-0025, énoncent la stratégie précédente dans leur
  contexte. Aucune de leurs décisions ne change et aucun n'est modifié : la base porte donc une
  prémisse désormais historique.

### Risques

* La règle qui pèse désormais le plus lourd est celle qui est imposée le moins complètement. Les
  marqueurs et les en-têtes non conformes sont pris mécaniquement ; « une intention par commit » et
  les commits d'échafaudage ne le sont pas, et ce sont exactement ceux qu'un rebase rend définitifs.
  Atténué par `AGENTS.md`, qui fait de la relecture un devoir permanent plutôt qu'un rappel, et par
  le hook du dépôt qui la soulève sans qu'on le lui demande — mais l'atténuation est une habitude,
  pas une barrière.
* Un contributeur qui force-push sa branche après une revue voit les mêmes commits réécrits deux
  fois, une fois par son propre rebase et une fois par la fusion, ce qui rend facile à orpheliner un
  commentaire de revue épinglé sur une ligne d'un commit précis.

## Actions de suivi

* Les quatre endroits qui justifiaient la règle d'hygiène d'historique par la stratégie du commit de
  merge — `CLAUDE.md`, `AGENTS.md`, le commentaire du mode CI du linter de commits et l'en-tête du
  hook d'hygiène — ont été corrigés dans la pull request qui précède cet enregistrement.
* Décider si la prémisse historique de l'ADR-0003 et de l'ADR-0025 mérite d'être réconciliée. Aucune
  de leurs décisions n'a changé, et une ADR acceptée ne se modifie pas en place : les options sont
  donc de les laisser être les enregistrements datés qu'elles sont, ou de noter le changement depuis
  ici — pas de les réécrire.

## Références

* [ADR-0002](0002-partition-releases-into-trains-by-commit-scope.fr.md) — les trains de release
  bâtis sur les scopes que portent les commits individuels, ce qu'un rebase doit préserver.
* [ADR-0003](0003-adopt-and-enforce-a-conventional-commits-convention.fr.md) — la convention sur
  chaque commit hors merge, les couches de vérification, et l'alternative du squash pesée là en
  premier.
* [ADR-0025](0025-bind-every-feature-commit-to-the-documentation-it-changed.fr.md) — le commit de
  fonctionnalité lié à sa documentation, dont la justification cite la stratégie précédente.
* [`CONTRIBUTING.md`](../../CONTRIBUTING.md) — le point d'arrivée que l'histoire d'une branche doit
  atteindre avant de fusionner.
* [`AGENTS.md`](../../AGENTS.md) — « Tidying history before a pull request », le devoir permanent
  dont cette décision relève l'enjeu.
