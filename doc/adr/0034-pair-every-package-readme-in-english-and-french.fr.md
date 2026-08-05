# ADR-0034 | Apparier chaque README de paquet en anglais et en français

🌍 **Langues :**  
🇬🇧 [English](./0034-pair-every-package-readme-in-english-and-french.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-08-05
**Accepted:** 2026-08-05
**Decision Makers:** Reefact

## Contexte

L'[ADR-0022](0022-maintain-every-document-under-doc-in-english-and-french.fr.md) a rendu bilingue
chaque document sous [`doc/`](..) et a nommé une exclusion dans sa Décision : *les READMEs de
paquet sous `src/` restent en anglais uniquement*. L'argument était le moteur de rendu.
nuget.org montre un fichier par paquet, n'offre aucun sélecteur de langue et ne résout aucun
lien relatif — une page bilingue y dupliquerait donc chaque section dans un seul document,
ou pointerait vers une traduction que le lecteur ne peut pas atteindre.

L'[ADR-0029](0029-pair-the-project-readme-across-the-doc-boundary.fr.md) a ensuite rencontré la
même forme de contrainte chez GitHub et l'a tranchée autrement. GitHub compose la page d'accueil
d'un dépôt depuis un `README.md` à la racine et depuis rien d'autre ; cela retire le *suffixe* et
fixe l'*emplacement* d'une moitié, et cela ne retire pas la *paire*. Sa section Risques a nommé
la suite : *« L'exception appelle de la compagnie … la raison — un moteur de rendu qui fixe le nom
et la place d'une page qui est de la documentation — est ce que tout futur candidat doit
argumenter. »*

La seconde moitié de l'argument d'ADR-0022 ne décrit plus ces fichiers. Chaque README de paquet
pointe déjà vers l'extérieur avec des adresses absolues
`https://github.com/Reefact/diagnostic-catalog/blob/main/...`, et
`LinkTests.A_package_readme_carries_no_relative_link` fait échouer celui qui n'en fait pas autant :
là-bas un lien relatif est toujours cassé, si soigneusement écrit soit-il, si bien que l'exigence
est qu'ils n'en portent aucun. Un lecteur sur nuget.org *peut* donc atteindre une page de ce
dépôt — c'est ainsi que les guides, la spécification et les catalogues frères lui sont déjà offerts.

Ce qui n'a pas changé, c'est la première moitié. nuget.org rend un seul fichier. Quoi qu'un paquet
emporte, il l'emporte seul, sans sélecteur et sans frère à côté.

Le public est celui à propos duquel ADR-0022 a le plus argumenté. Ce record justifiait de traduire
les guides parce qu'ils sont lus par celui à qui l'on demande de migrer le code plutôt que par celui
qui a choisi la bibliothèque — et un README de paquet est la page que ce lecteur rencontre en
premier, avant tout guide, souvent depuis un résultat de recherche. Onze des quatorze sont des pages
de catalogue dont le sujet entier est la défaillance que ce dépôt existe pour supprimer : une
suppression dont la catégorie est fausse compile, s'exécute, et ne signale rien, pour toujours. Cet
argument ne vaut rien pour un lecteur qui ne le suit pas.

Les vérifications de parité de `tests/DiagnosticCatalog.Documentation.UnitTests` s'appuient sur le
suffixe de langue, et elles lisent déjà `src/` : `Repository` parcourt `doc/` et `src/` pareillement,
si bien qu'une page nommée `README.en.md` y est dans le jeu bilingue sans aucune liste où l'ajouter.
Ce qu'il faut à ces fichiers, c'est plutôt que deux vérifications apprennent la nouvelle forme —
celle qui sélectionne les READMEs de paquet par leur nom, et celle qui résout le lien d'une bannière
de langue.

Le générateur écrit dans ces fichiers. `CatalogEmitter` réécrit le bloc marqué qui indique quelle
version amont un catalogue reflète, dans le README et dans le changelog, et `DocumentedMirrorTests`
fait échouer un document dont le bloc contredit l'`[assembly: CatalogSource]` du catalogue. Ce même
générateur est livré dans `dcat`, où il s'exécute contre des dépôts qui gardent un unique
`README.md` et n'ont jamais entendu parler d'un suffixe de langue.

## Décision

Chaque README de paquet sous `src/` est maintenu en anglais et en français, sous les noms
`README.en.md` et `README.fr.md`, la version anglaise étant canonique et livrée : le
`<PackageReadmeFile>` d'un paquet nomme la moitié anglaise, et les deux moitiés écrivent chaque
adresse en entier, bannière de langue comprise. Le `CHANGELOG.md` par paquet reste en anglais
uniquement.

## Justification

Le moteur de rendu décide quelle moitié un paquet emporte — il ne décide pas si une traduction
existe. Tout est là. ADR-0022 a lu une contrainte comme deux : nuget.org montre un fichier *et* ne
résout aucun lien relatif, et c'est la seconde qui rendait une paire inutile, parce qu'une bannière
offrant l'autre langue aurait pointé vers rien. Mais ces pages avaient déjà cessé d'écrire des liens
relatifs pour exactement cette raison, et une adresse absolue n'y est pas seulement tolérée — c'est
la seule sorte qui ait jamais fonctionné. La bannière est un lien de plus du genre dont la page
entière est déjà faite.

C'est le geste d'ADR-0029 appliqué à l'autre moteur de rendu, et la symétrie vaut d'être énoncée
parce que c'est elle qui empêche la politique linguistique de devenir une liste de lieux. GitHub fixe
le *nom et l'emplacement* d'une moitié et la paire survit ; nuget.org fixe *combien de moitiés
voyagent dans un paquet* et la paire survit. Aucun des deux moteurs n'a jamais été consulté sur le
droit du dépôt à détenir une traduction.

L'argument du public porte plus fort ici qu'au README racine. Une page de paquet est atteinte depuis
un résultat de recherche, depuis un `PackageReference` écrit par quelqu'un d'autre, depuis une
dépendance transitive que personne n'a choisie — le lecteur arrive en utilisant déjà l'analyseur, ce
qui est précisément le lecteur dont ADR-0022 disait qu'il devait suivre l'argument et qu'il n'avait
pas choisi la bibliothèque. Le README racine appartient au moins à quelqu'un qui évalue ; ces
pages-ci sont lues par celui qui doit réparer la compilation.

Déclarer la paire aux vérifications plutôt que l'en exempter est le geste que les deux records ont
déjà fait, et pour la même raison : chaque argument ci-dessus échoue dès l'instant où la moitié
française prend du retard, et prendre du retard est le résultat normal d'une politique qui repose sur
le souvenir. Le suffixe est ce qui l'achète — pas de liste, pas d'exception, pas de décision fichier
par fichier. Un `README.en.md` sous `src/` est dans le jeu à cause de son nom, exactement comme
`getting-started.en.md`.

Maintenir le générateur écrivant les deux moitiés est ce qui fait survivre la paire à un nocturne. La
version reflétée est le seul énoncé d'un README de catalogue que personne n'édite à la main, et une
traduction que rien ne rafraîchit annonce la version du mois dernier au lecteur le moins outillé pour
s'en apercevoir — ni l'attribut d'assembly qu'elle contredit ni les guides qui la corrigeraient ne
sont dans sa langue. N'écrire que de l'anglais dans une page française aurait été pire que de la
laisser périmée, donc chaque moitié reçoit sa propre bannière et seule la prose diffère ;
l'identifiant du paquet et la version sont la même phrase dans les deux.

Que le générateur écrive dans les orthographes de README qui existent, plutôt que dans un nom fixe,
est ce qui empêche la convention de ce dépôt d'entrer chez les autres. Le dossier de catalogue d'un
utilisateur de `dcat` contient `README.md`, et il doit continuer d'obtenir sa bannière ; le nôtre
contient une paire, et les deux moitiés le doivent. Une orthographe absente est la convention d'un
autre dépôt plutôt qu'un document manquant, elle n'est donc pas signalée — une note à chaque
exécution pour un fichier que personne n'a voulu garder, c'est ainsi qu'un lecteur apprend à ne plus
lire les notes.

Les changelogs par paquet sont laissés tranquilles parce que l'argument ci-dessus ne les atteint pas.
Un changelog est un journal de versions publiées plutôt qu'une page que l'on lit pour comprendre la
bibliothèque ; il n'est livré dans aucun paquet, donc aucun moteur de rendu ne le contraint, et
l'argument du public — le lecteur qui arrive en utilisant déjà l'analyseur — porte sur la page qui
explique ce que sont les règles.

## Alternatives envisagées

### Garder les READMEs de paquet en anglais uniquement, comme ADR-0022 l'a décidé

Cela ne demande aucun ADR, aucune traduction, et aucun changement au générateur, à l'empaquetage ni
aux vérifications. Leur public évalue sans doute plutôt qu'il n'apprend, et un lecteur qui en veut
plus est à un lien d'un jeu entièrement bilingue.

Rejeté parce que « à un lien » est l'affirmation que la paire rend vraie plutôt qu'un argument contre
elle : ces liens existent et sont absolus, ce qui est exactement pourquoi une bannière peut en être
un. Et la description du public ne survit pas au contact de la façon dont ces pages sont atteintes —
une page de catalogue est rencontrée par quelqu'un qui a déjà l'analyseur, au travers d'une dépendance
qu'il n'a pas choisie.

### Garder `README.md` et ajouter `README.fr.md` à côté

Le fichier livré garderait le nom que l'empaquetage, le générateur et chaque utilisateur de `dcat` en
aval emploient déjà, et rien d'autre que le nouveau fichier ne bougerait.

Rejeté parce que cela met une moitié hors des vérifications de parité et l'autre dedans. Un document
sans suffixe de langue n'est pas dans le jeu bilingue, donc `README.fr.md` serait vérifié contre un
`README.en.md` qui n'existe pas — la paire devrait être déclarée fichier par fichier, ce qui est la
liste d'exceptions qu'ADR-0022 a refusée, cette fois avec quatorze entrées au lieu d'une.

### Replier les deux langues dans l'unique fichier que nuget.org rend

Un document par paquet, l'anglais puis le français, avec une ancre en haut. Rien ne bouge, rien n'est
renommé, et un lecteur sur nuget.org n'a besoin d'aucun lien.

Rejeté pour la raison qu'ADR-0022 a donnée en le rejetant : cela duplique chaque section dans un seul
document. Cela double aussi la page que chaque lecteur fait défiler pour ne lui en servir que la
moitié, et les vérifications de parité — qui comparent deux documents — n'auraient rien à comparer.

### Apparier aussi le `CHANGELOG.md` par paquet

Il est sous `src/` à côté du README, le générateur y écrit déjà une bannière, et le laisser
monolingue rend le dossier incohérent.

Rejeté parce que la cohérence du dossier n'est pas l'argument. Un changelog se lit pour savoir ce qui
a changé dans une version, n'est livré dans aucun paquet, et est complété à chaque release — le seul
document ici dont le coût de traduction revient selon un calendrier, pour la prose la moins
explicative du dépôt.

## Conséquences

### Positives

* La page qu'un lecteur de catalogue rencontre en premier existe dans sa langue, y compris
  l'argument sur le fait qu'une catégorie fausse ne produit aucun symptôme.
* La politique linguistique cesse de nommer un dossier et se met à nommer un moteur de rendu :
  `doc/` est bilingue, le README racine est bilingue avec son nom fixé par GitHub, et les READMEs de
  paquet sont bilingues avec leur moitié livrée fixée par nuget.org.
* La paire est vérifiée par les mêmes théories que toute autre page — une moitié manquante, une
  section abandonnée, une ligne de tableau ajoutée d'un seul côté, une bannière qui ne pointe nulle
  part — parce que le suffixe la met dans le jeu sans liste à maintenir.
* La version reflétée ne peut pas se périmer dans une seule langue, parce que le générateur écrit les
  deux moitiés et que `DocumentedMirrorTests` lit les deux.

### Négatives

* Quatorze pages de plus à garder vraies, et ce sont les pages les plus susceptibles de changer : le
  README d'un catalogue énonce son nombre de règles, son tableau de catégories et la version qu'il
  reflète.
* Un README de paquet ne peut plus être édité seul, et le linter de commits l'impose —
  `check-docs-footer.sh` refuse un pied `Docs:` qui ne nomme qu'une moitié d'une paire.
* Parcourir un dossier de paquet sur GitHub ne rend aucun README, parce que GitHub rend `README.md`
  dans un listing de répertoire et qu'aucune moitié ne porte ce nom. `doc/guide/` vit déjà avec cela.
* Le `.nupkg` contient désormais un fichier nommé `README.en.md`, ce qui se lit comme s'il manquait
  un `README.fr.md` à un paquet qui n'emporte délibérément qu'un fichier.

### Risques

* La moitié française dérive de sens tout en gardant sa forme. Les théories de parité comptent les
  titres, les exemples, les items de liste et les lignes de tableau ; elles ne lisent pas le
  français, et un README de catalogue est l'endroit où un chiffre périmé a le plus de chances d'être
  cru.
* Un futur catalogue est ajouté avec une seule moitié. Rien dans le générateur ne crée un README, la
  paire est donc créée à la main, et les vérifications qui attraperaient une moitié manquante sont
  les tests de documentation plutôt que quoi que ce soit que le générateur dise sur le moment.
* L'exception appelle de la compagnie, à nouveau. Ce record répond à la question d'ADR-0029 pour les
  READMEs de paquet, et la raison qu'il argumente — un moteur de rendu qui décide comment une page
  est montrée plutôt que si elle existe — est ce que le prochain candidat devra argumenter.

## Actions de suivi

* Réénoncer la règle dans [`doc/CONVENTIONS.fr.md`](../CONVENTIONS.fr.md),
  [`CONTRIBUTING.md`](../../CONTRIBUTING.md), [`CLAUDE.md`](../../CLAUDE.md) et le README du projet,
  où chacun dit actuellement que les READMEs de paquet sont en anglais uniquement.
* Apprendre à `LinkTests` à sélectionner les READMEs de paquet par leurs nouveaux noms, et à
  `Repository` à résoudre une adresse vers ce dépôt pour qu'une bannière de langue écrite en entier
  soit vérifiée comme n'importe quelle autre.
* Apprendre à `CatalogEmitter` à écrire la bannière de reflet dans les orthographes de README que
  détient un dossier de catalogue, et à `DocumentedMirrorTests` à lire les deux moitiés.
* Apprendre à `check-docs-footer.sh` que `src/*/README.en.md` et `src/*/README.fr.md` sont
  homologues, dans les deux sens.

## Références

* [ADR-0022](0022-maintain-every-document-under-doc-in-english-and-french.fr.md) — la politique
  bilingue, et l'exclusion que ce record remplace.
* [ADR-0029](0029-pair-the-project-readme-across-the-doc-boundary.fr.md) — la même question posée à
  GitHub, et la réponse que ce record suit.
* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.fr.md) — pourquoi la version
  reflétée est écrite par le générateur plutôt qu'à la main.
* [ADR-0017](0017-publish-the-generator-as-a-cli-on-its-own-release-train.fr.md) — pourquoi le
  générateur est l'outil de quelqu'un d'autre autant que le nôtre.
* [`doc/CONVENTIONS.fr.md`](../CONVENTIONS.fr.md) — la disposition, et ce que vérifient les tests de
  documentation.
