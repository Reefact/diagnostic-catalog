# ADR-0037 | Embarquer les analyzers dans le paquet de la fondation

🌍 **Langues :**  
🇬🇧 [English](./0037-ship-the-analyzers-inside-the-foundation-package.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Proposed
**Proposed:** 2026-08-06
**Decision Makers:** Reefact

## Contexte

Le train de publication `lib` porte trois projets : `DiagnosticCatalog`, qui contient les attributs
marqueurs ; `DiagnosticCatalog.Analyzers`, qui contient les diagnostics `DCAT` et, empaquetés à
l'intérieur, les correcteurs ; et `DiagnosticCatalog.Self`, les règles `DCAT` exprimées comme un
catalogue. Un train est tagué une fois et empaquette tout projet qui le déclare
([ADR-0002](0002-partition-releases-into-trains-by-commit-scope.fr.md)) : les trois portent donc
toujours la même version et ne peuvent jamais être publiés séparément.

Ce train a été tagué une seule fois, en `lib-v0.1.0`. À ce commit, `src/` contenait quatre projets
et `DiagnosticCatalog.Analyzers` n'en faisait pas partie — il a été écrit plus tard.
`DiagnosticCatalog` 0.1.0 est donc sur nuget.org, et le paquet d'analyzers n'a jamais été publié.

Le §16.1 de la [spécification](../specification.fr.md) sépare les deux paquets par public : un
consommateur qui écrit des suppressions veut les analyzers et aucune dépendance à l'exécution,
tandis qu'un auteur de catalogue a besoin que l'attribut atteigne ses propres consommateurs. Il note
qu'un métapaquet de commodité peut dépendre des deux.

Chaque paquet de catalogue dépend de `DiagnosticCatalog`, et n'a pas le droit de le masquer :
l'attribut doit atteindre quiconque consomme le catalogue, à la fois pour la réflexion sur les types
de règles et pour qu'il puisse déclarer ses propres règles. Cette dépendance est obligatoire, déjà
déclarée et déjà ouverte.

Aucun catalogue ne référence `DiagnosticCatalog.Analyzers`. Un projet qui référence un catalogue
reçoit les constantes de règles et l'assembly d'attributs, et aucun diagnostic `DCAT` d'aucune
sorte — rien ne signale donc les suppressions encore écrites en littéraux, qui sont la migration que
`DCAT0006` existe pour conduire. Le README du projet l'énonce et nomme la publication manquante
comme la raison.

Le §16.3 a mesuré le passage transitif des analyzers contre de vrais paquets, et
`tools/packaging/verify-consumption.sh` le remesure à chaque pull request : un catalogue qui
référence l'analyzer sans le masquer le livre bien à ses propres consommateurs, ce qui est
l'inverse de ce que NuGet documente. La mesure couvre un saut — un projet référençant un catalogue.
Elle ne couvre pas un projet référençant une bibliothèque qui référence un catalogue.

La dépendance d'un catalogue empaqueté vers `DiagnosticCatalog` porte la liste d'actifs privés par
défaut de NuGet, qui nomme les analyzers parmi les actifs exclus. Le passage mesuré au §16.3 tient
donc malgré cette liste plutôt que grâce à elle.

`DCAT0006` est livré en erreur par défaut
([ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.fr.md)), au motif énoncé que référencer un
paquet de catalogue est en soi la déclaration d'intention.

Les diagnostics de définition ne rapportent que sur les types marqués par l'attribut de règle ;
l'analyzer sort immédiatement sur tout le reste. Un projet qui consomme un catalogue et ne déclare
aucune règle propre n'est donc concerné que par les diagnostics de site d'usage, quel que soit le
paquet qui les a livrés.

`DiagnosticCatalog.CodeFixes` ne déclare aucun train de publication et est empaqueté dans le paquet
d'analyzers — la seule forme de projet que
l'[ADR-0007](0007-depend-across-trains-through-published-packages.fr.md) bénit pour du code qui
n'est pas un paquet à part entière.

## Décision

Les analyzers et leurs correcteurs sont embarqués dans le paquet `DiagnosticCatalog`, qui porte à la
fois l'assembly d'attributs et les assemblys d'analyse, et `DiagnosticCatalog.Analyzers` cesse
d'être une identité de paquet distincte.

## Justification

On sépare un paquet d'un autre pour le versionner indépendamment. Ces deux-là sont sur le même
train : il n'y a donc aucune indépendance à acheter, puisque chaque tag qui publie l'un publie
l'autre, au même numéro, pour toujours. Ce que la séparation livre réellement, c'est un deuxième nom
à découvrir et une deuxième référence à écrire — et le constat de ce coût, c'est l'état où se trouve
le dépôt aujourd'hui : treize catalogues publiés ou empaquetables, dont aucun n'est vérifié par quoi
que ce soit.

Replier les analyzers dans la fondation fait de *utiliser un catalogue, c'est être vérifié* une
propriété du graphe de dépendances qui existe déjà, plutôt que de treize références que quelqu'un
doit ajouter et tenir alignées. Cette distinction est tout l'argument. Une garantie qui repose sur
le fait que chaque auteur de catalogue se souvienne d'une ligne est une garantie que rien n'énumère
et qu'aucun test ne peut affirmer ; l'appartenance à un train a enseigné la même leçon, et c'est
pourquoi elle se déclare dans le projet et non dans une liste ailleurs.

Les deux publics du §16.1 ne survivent pas au contact de l'empaquetage. Un consommateur de catalogue
reçoit déjà l'assembly d'attributs, qu'il le veuille ou non, puisqu'il est interdit au catalogue de
le masquer — le public qui voulait les analyzers sans dépendance à l'exécution n'existe donc pas
parmi les consommateurs de catalogues, et chez les autres il demande à éviter un assembly
d'attributs marqueurs dont les tests d'empreinte nulle garantissent déjà qu'il ne laisse aucune
trace dans la compilation d'un consommateur.

L'ADR-0027 justifie de livrer `DCAT0006` en erreur au motif que référencer un catalogue est une
déclaration d'intention. Ce motif est actuellement faux : référencer un catalogue ne livre rien qui
puisse rapporter, et celui qui encaisse l'erreur est celui qui est allé chercher séparément le
paquet d'analyzers, ce qui est une intention plus forte que celle dont l'enregistrement argumente.
Cette décision n'affaiblit pas l'ADR-0027 — elle fait du paquet qu'il nomme le paquet qui porte la
conséquence.

La séparation que certains lecteurs voudront à la place — les vérifications de site d'usage pour les
consommateurs, celles de définition pour les auteurs de catalogues — est déjà obtenue, par le
comportement plutôt que par l'empaquetage. Rien dans l'ensemble de définition ne se déclenche sur un
projet qui ne déclare aucune règle : l'assembly d'analyse est donc déjà limité au site d'usage pour
exactement le public qui aurait demandé la séparation, et un consommateur qui déclare bien des
règles est un auteur de catalogue qui veut le reste.

Le calendrier est la part qui n'attend pas. `DiagnosticCatalog.Analyzers` n'a jamais été publié :
aucun `.csproj` au monde ne le nomme, et le replier ne coûte rien à personne. Le prochain tag `lib`
en fait une identité de paquet publique, et retirer une identité publiée est un changement cassant
pour ceux qui l'ont adoptée en premier — les lecteurs les plus susceptibles d'avoir été attentifs.

## Alternatives envisagées

### Publier le paquet d'analyzers et le référencer depuis chaque catalogue

Le plus petit changement : le §16.1 tient, l'empaquetage tient, et chaque catalogue gagne une
référence — sans version, sous la gestion centralisée des paquets, si bien que la version elle-même
est une modification unique.

Rejetée parce qu'elle achète la propriété par répétition. Treize références aujourd'hui et une de
plus à chaque catalogue ajouté, dont chacune doit être écrite, doit refuser de masquer l'analyzer,
et doit être rappelée à qui ajoutera le quatorzième. Rien n'énumère les références de paquets :
aucune vérification ne peut donc tenir l'ensemble vrai, et le mode de défaillance est le silence —
un catalogue dont les consommateurs ne sont pas vérifiés ressemble exactement à un catalogue dont
ils le sont.

### Replier les analyzers dans chaque paquet de catalogue

La lecture la plus directe de l'objectif : un catalogue devient autosuffisant, et aucun consommateur
n'a besoin de savoir qu'une fondation existe.

Rejetée parce que les catalogues roulent sur des trains différents, à des rythmes différents. Un
projet en référençant deux chargerait deux copies de l'assembly d'analyse en deux versions, et
Roslyn rapporte depuis chaque analyzer qu'il charge — la même suppression serait donc diagnostiquée
deux fois, par deux versions qui peuvent diverger.

### Publier un métapaquet de commodité dépendant des deux, comme le suggère §16.1

Rien d'existant ne change, aucune identité n'est retirée, et le lecteur qui veut tout n'a qu'un nom
à référencer.

Rejetée parce qu'elle répond à un problème de découverte par un troisième nom à découvrir. Le
lecteur qui n'a jamais appris que les vérifications vivent dans un deuxième paquet est précisément
celui qui n'apprendra pas qu'elles vivent aussi dans un troisième.

### Laisser l'empaquetage tel quel et documenter davantage

Cela ne coûte rien, et la documentation est déjà en place : la page de dépannage s'ouvre sur un
diagramme dont la première question est de savoir si le paquet d'analyzers est référencé.

Rejetée parce qu'elle accepte le silence comme état par défaut, ce qui est précisément la
défaillance que ce dépôt existe pour supprimer. Une suppression dont la catégorie est fausse
compile, s'exécute et ne rapporte rien ; une base de code dont les suppressions ne sont pas
vérifiées compile, se publie et ne rapporte rien. Répondre à la seconde par une page que le lecteur
doit déjà soupçonner utile, c'est le pari que la première fait perdre.

## Conséquences

### Positives

* Référencer n'importe quel catalogue livre les vérifications, sans aucune déclaration par projet à
  écrire, à relire, ou à se rappeler le jour où le quatorzième catalogue est ajouté.
* Les analyzers ne peuvent jamais être en retard d'une publication sur l'attribut qu'ils lisent,
  puisqu'il y a un paquet et une version là où il y en avait deux de chaque sur un seul train.
* La justification de l'ADR-0027 devient vraie du paquet qu'elle nomme : la référence qui énonce
  l'intention est celle qui livre le diagnostic.
* Aucun catalogue ne gagne une dépendance inter-trains qu'il ne porte pas déjà : l'
  [ADR-0007](0007-depend-across-trains-through-published-packages.fr.md) n'est même pas sollicitée.

### Négatives

* Le §16.1 cesse de décrire l'empaquetage et doit être réécrit, avec le tableau d'état du README du
  projet et le diagramme de dépannage, qui nomment le paquet d'analyzers comme la chose à
  référencer.
* La fondation cesse d'être publiable comme bibliothèque pure : elle porte des assemblys d'analyse,
  et leur dépendance à Roslyn devient une contrainte sur un paquet que reçoit tout consommateur de
  tout catalogue.
* Un consommateur qui veut les attributs sans les vérifications ne peut plus l'exprimer en refusant
  une référence de paquet, et doit faire taire les diagnostics dans `.editorconfig` à la place.
* La première publication de catalogue après le changement fait échouer des compilations qui
  passaient, partout où une suppression littérale correspond à une règle cataloguée, puisque
  `DCAT0006` est une erreur.

### Risques

* Le passage transitif dont dépend cette décision contredit la documentation de NuGet, et la liste
  d'actifs privés par défaut nomme les analyzers parmi les actifs exclus. Une version de NuGet
  rétablissant le comportement documenté refermerait le chemin en silence. L'atténuation est que ce
  passage est remesuré contre de vrais paquets à chaque pull request plutôt que supposé.
* Le chemin à deux sauts n'est pas mesuré. Une bibliothèque qui référence un catalogue pour ses
  propres suppressions peut imposer des diagnostics de sévérité erreur à ses consommateurs, qui
  n'ont choisi ni le catalogue ni l'analyzer, et cette décision rend ce chemin vivant avant que quoi
  que ce soit ne le mesure.
* Le repli n'est gratuit que tant que l'identité n'est pas publiée. Si un tag `lib` passe d'abord,
  la même décision coûte une dépréciation et une note de migration au lieu d'un renommage que
  personne ne peut observer.

## Actions de suivi

* Réécrire les §16.1 et §16.3 de la [spécification](../specification.fr.md), qui décrivent les deux
  paquets et les leviers de transitivité dont un paquet unique n'a plus besoin.
* Étendre `tools/packaging/verify-consumption.sh` au cas à deux sauts, et le faire avant le prochain
  tag `lib` plutôt qu'après.
* Mettre à jour le tableau d'état du README du projet et
  [`doc/guide/troubleshooting`](../guide/troubleshooting.fr.md), qui envoient tous deux le lecteur
  vers un paquet qui n'existerait plus.
* Énoncer dans les notes de publication du premier catalogue portant le changement que l'adopter
  fait échouer la compilation sur chaque suppression littérale correspondant à une règle cataloguée.
* Décider si `DiagnosticCatalog.Self`, également sur le train `lib` et également non publié, conserve
  sa propre identité de paquet — la même question, à laquelle la même réponse n'est pas évidemment
  la bonne.

## Références

* [ADR-0002](0002-partition-releases-into-trains-by-commit-scope.fr.md) — pourquoi c'est le train,
  et non le paquet, qui versionne.
* [ADR-0007](0007-depend-across-trains-through-published-packages.fr.md) — la règle inter-trains que
  cette décision évite de solliciter, et la forme de projet qu'elle bénit pour du code qui n'est pas
  son propre paquet.
* [ADR-0027](0027-ship-the-use-site-diagnostics-as-errors.fr.md) — la sévérité, et la déclaration
  d'intention que cet enregistrement rend vraie.
* [`doc/specification.fr.md`](../specification.fr.md) — le §16, l'empaquetage qu'il décrit et la
  transitivité qu'il a mesurée.
* `tools/packaging/verify-consumption.sh` — ce qui remesure le passage à chaque pull request, et ce
  qui doit apprendre le second saut.
