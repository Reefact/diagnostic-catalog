# ADR-0022 | Maintenir chaque document sous `doc/` en anglais et en français

🌍 **Langues :**  
🇬🇧 [English](./0022-maintain-every-document-under-doc-in-english-and-french.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-07-31
**Accepted:** 2026-07-31
**Decision Makers:** Reefact

## Context

La langue déclarée du dépôt est l'anglais — sources, commentaires, messages de
commit, noms de branches, titres de pull requests, issues — avec une exception
documentée : [`doc/specification.en.md`](../specification.en.md) est accompagnée
de [`doc/specification.fr.md`](../specification.fr.md), et la version anglaise
est déclarée faisant foi là où les deux divergent. Cette paire est maintenue à la
main depuis sa rédaction ; elle fait respectivement 1940 et 2003 lignes.

La documentation destinée aux lecteurs est aujourd'hui le `README.md` racine (310
lignes) et trois guides sous [`doc/guide/`](../guide/), tous en anglais. Les
décisions d'architecture sous [`doc/adr/`](.) sont dix-neuf documents, tous en
anglais. Sept README de paquets sous `src/` sont livrés à l'intérieur du `.nupkg`
en `<PackageReadmeFile>` et affichés par nuget.org, qui n'offre aucun sélecteur
de langue, ne résout aucun lien relatif, et affiche le fichier unique que le
paquet déclare.

Reefact est une organisation francophone, et son projet frère
[`first-class-errors`](https://github.com/Reefact/first-class-errors) maintient
un ensemble bilingue complet destiné aux lecteurs — une trentaine de documents en
paires `.en.md` / `.fr.md`, chacun portant un bandeau de langue sous son titre et
un pied de navigation précédent/suivant.

Rien dans l'intégration continue de ce dépôt ne lit du Markdown. Le workflow de
lint couvre le shell et le YAML des workflows ; la build .NET couvre le C#. Un
lien relatif cassé, une page dont la traduction n'a jamais été écrite, et une
page vers laquelle rien ne pointe sont tous invisibles pour chaque vérification
qui tourne aujourd'hui.

La documentation fait des affirmations sur des artefacts compilés : quels
identifiants `DCAT` les analyseurs signalent, quelles options `dcat` accepte,
quelle version un catalogue reflète. Trois de ces affirmations sont déjà
vérifiées — `DocumentedMirrorTests` et `DocumentedSiblingsTests` lisent les README
de paquets contre le `CatalogSourceAttribute` généré et contre les déclarations
de trains de release — et le reste ne l'est pas.

Le sujet traité est une catégorie de défaut qui ne produit aucun symptôme : une
suppression dont la catégorie est fausse compile, s'exécute, et ne signale rien,
à jamais. C'est la défaillance que ce dépôt a été écrit pour supprimer, et c'est
la norme que le dépôt s'applique à lui-même — un correctif part avec un test vu en
échec, un catalogue est régénéré et comparé à chaque pull request, une release
est répétée avant d'être coupée.

## Decision

Chaque document sous `doc/` est maintenu en anglais et en français, page pour
page, la version anglaise faisant foi ; les README de paquets sous `src/`
restent en anglais uniquement.

## Rationale

L'exception existe déjà et a tenu. La spécification est maintenue en paire, à la
main, sur quatre mille lignes, avec l'anglais déclaré faisant foi — la question
n'est donc pas de savoir si ce dépôt peut soutenir un document bilingue, mais si
la règle qui n'en permet exactement qu'un décrit encore ce que le dépôt fait.
Étendre la politique au reste de `doc/` enregistre la pratique au lieu de laisser
chaque nouveau document plaider pour lui-même.

Le public plaide pour cela indépendamment de la langue du mainteneur. Cette
bibliothèque est adoptée par des équipes, pas par des individus : les guides qui
comptent le plus — adopter un catalogue sur une base de code existante, ce que
signifie un diagnostic `DCAT`, pourquoi supprimer une constante casse la build
d'un consommateur — sont lus par celui à qui l'on demande de migrer le code, pas
par celui qui a choisi la bibliothèque. Un lecteur qui suit à moitié l'argument
sur l'axe de la catégorie est un lecteur qui écrit la catégorie à la main, ce qui
est le résultat que toute la conception existe pour empêcher. La traduction achète
ici la compréhension d'un point subtil, pas un confort.

Nommer l'anglais comme faisant foi est ce qui empêche la paire de devenir la
documentation de deux bibliothèques. Une page française est une traduction : elle
peut être en retard, et quand elle l'est, on dit au lecteur quel document
l'emporte. L'alternative — deux documents indépendants — n'a aucun repli de ce
genre, et son mode de défaillance est deux pages décrivant des produits
différents sans que rien ne dise laquelle est vraie.

Les README de paquets sont exclus parce que leur moteur d'affichage décide.
nuget.org montre un fichier par paquet, sans sélecteur de langue et sans lien
relatif fonctionnel ; une page bilingue là-bas dupliquerait chaque section dans
un seul document ou renverrait à une traduction que le lecteur ne peut pas
atteindre. Leur public est aussi différent en nature — quelqu'un qui évalue un
paquet depuis un résultat de recherche, pas quelqu'un qui apprend le modèle — et
ils sont déjà contraints par deux tests, où leurs obligations sont enregistrées.

Faire respecter la paire par un test est ce qui rend la décision soutenable.
Chaque argument ci-dessus tombe dès que la moitié française prend du retard, et
le retard est le résultat normal d'une politique qui repose sur le souvenir : la
page la plus difficile à traduire est la page qui avait le plus besoin d'être
traduite, et c'est celle qu'on remet à plus tard. Rien d'autre dans ce dépôt
n'est laissé à l'attention — les règles de code sont vérifiées deux fois, le
catalogue est régénéré et comparé, la release est répétée — et un ensemble
documentaire est l'artefact où une omission est la moins visible, parce qu'aucun
lecteur incapable de lire la page n'est en position de signaler qu'elle manque.
Une vérification qui refuse une page sans sa jumelle convertit ce vide silencieux
en build rouge, ce qui est le même geste que partout ailleurs ici.

Le même raisonnement s'étend au-delà de la parité, jusqu'aux affirmations que la
documentation fait sur le code. Un identifiant `DCAT` documenté après avoir été
retiré, et un identifiant livré sans page le décrivant, sont deux erreurs
qu'aucun lecteur ne peut distinguer d'un document correct ; vérifier une page
contre les descripteurs qu'elle décrit, c'est
[ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.fr.md)
appliquée à la prose — comparer à ce que l'analyseur déclare réellement, jamais à
un autre document.

Les décisions d'architecture sont incluses plutôt qu'exemptées, et c'est la
moitié inconfortable. Une ADR est un journal historique : elle n'est jamais
modifiée sur place, sa traduction est donc écrite une fois puis laissée
tranquille, ce qui est le coût continu le plus faible possible et le coût initial
le plus élevé possible — dix-neuf documents, dont plusieurs longs. Ce qui tranche,
c'est que les ADR sont là où vit le raisonnement. Un lecteur à qui l'on dit
qu'une catégorie est un contrat publié, et qui veut savoir pourquoi un catalogue
ne renomme jamais un membre, est envoyé vers
[ADR-0012](0012-a-catalogue-never-renames-a-member-it-published.fr.md) ;
l'envoyer vers un document qu'il ne peut pas lire fait du guide qui le lie une
impasse. Un dossier `doc/` où les guides sont bilingues et le raisonnement
derrière eux ne l'est pas enseigne au lecteur que le raisonnement n'est pas pour
lui.

## Alternatives Considered

### Garder la documentation en anglais uniquement, comme la politique actuelle l'énonce

L'anglais est la langue de travail de l'outillage .NET : les éditeurs
d'analyseurs que cette bibliothèque reflète publient en anglais, la documentation
de la plateforme est en anglais, et un lecteur qui exécute déjà SonarAnalyzer ou
StyleCop lit leurs pages de règles en anglais tous les jours. La traduction
automatique est désormais assez bonne pour la prose technique, un lecteur
francophone n'est donc pas exclu. La politique n'aurait besoin d'aucun changement
et aucun document n'aurait besoin d'être écrit.

Rejeté parce que cela fait porter le coût au mauvais lecteur. La traduction
automatique s'en sort bien sur la description et mal sur les points exacts que
cette documentation existe pour faire passer — qu'un argument est lu et l'autre
non, qu'une constante est incorporée dans l'assemblage du consommateur à la
compilation du consommateur, que corriger la catégorie ne change rien à ce qui est
supprimé. Ce sont les phrases qu'un lecteur doit suivre précisément, et ce sont
celles qu'un moteur de traduction aplatit. Cela laisse en outre le raisonnement
là où il était déjà : la spécification est bilingue parce que quelqu'un a jugé
que son argument devait porter, et les guides font le même argument à davantage
de gens.

### Maintenir les pages françaises en documents indépendants plutôt qu'en traductions

Un document français écrit pour son propre lecteur peut choisir ses propres
exemples, son propre ordre et sa propre longueur. Il n'est jamais en retard sur
une traduction puisqu'il n'en est pas une, et il peut être plus court —
l'objection à la traduction est d'ordinaire qu'elle reproduit une structure
anglaise dans une langue qui aurait organisé la matière autrement.

Rejeté parce que cela double la surface qui doit rester vraie à propos du même
produit. Chaque affirmation de comportement existerait alors deux fois sans
autorité déclarée entre elles ; une affirmation corrigée dans une langue laisse
l'autre fausse et rien ne dit laquelle croire. La paire de spécifications
existante répond déjà à cela : elle déclare l'anglais faisant foi précisément pour
qu'une divergence ait une résolution.

### Générer le français depuis l'anglais dans la build

Une étape de traduction en intégration continue garderait la paire synchronisée
par construction, supprimerait la question de la parité, et rendrait une page non
traduite impossible.

Rejeté parce que cela met un service réseau sur le chemin d'un document qui fait
des affirmations précises, sans que personne n'en lise la sortie avant sa
publication. Cela contredit aussi la raison pour laquelle le workflow nocturne de
catalogue ouvre une pull request plutôt que d'en fusionner une : l'automatisation
trouve le changement, un humain l'accepte. Une traduction générée d'un paragraphe
expliquant pourquoi une catégorie fausse n'a pas de symptôme serait publiée sans
qu'aucun lecteur francophone l'ait vue, ce qui est le même geste de contrat non
relu que cette décision-là refusait.

### Étendre la politique bilingue aux guides mais exempter les décisions d'architecture

Les ADR sont internes : leur public est fait de mainteneurs et de contributeurs,
qui travaillent dans la langue du dépôt par politique, et ce sont les documents
les plus longs par unité de lecteur. Les exempter réduirait le coût initial de
moitié environ et ne toucherait rien de ce qu'un consommateur des paquets lit.

Rejeté parce que les guides y renvoient. Le raisonnement n'est délibérément pas
dupliqué dans les guides — les ADR existent pour qu'il soit enregistré une fois —
un guide bilingue dont le « pourquoi » est en anglais seulement déplace donc le
vide au lieu de le supprimer. Le coût est en outre de la forme qui plaide contre
l'exemption : une ADR acceptée n'est jamais modifiée sur place, sa traduction est
donc écrite une fois puis ne coûte plus rien, ce qui en fait la partie la *moins*
chère de cette décision à soutenir et seulement la plus chère à démarrer.

## Consequences

### Positive

* Une équipe francophone qui adopte la bibliothèque lit l'argument, et pas
  seulement les instructions — y compris les parties les plus difficiles à
  accepter sur parole.
* La politique de langue cesse d'être une règle avec une exception maintenue à la
  main, et devient une règle avec une frontière déclarée qu'un test fait
  respecter.
* Les deux dépôts de l'organisation présentent une seule convention de
  navigation ; un lecteur qui passe de l'un à l'autre ne rencontre pas une
  seconde disposition.
* Les vérifications qu'exige la paire apportent plus que la parité : la
  résolution des liens, un ordre de navigation unique sans page orpheline, et —
  les deux qui atteignent le code — chaque `DCAT` livré documenté et chaque option
  `dcat` documentée réelle.

### Negative

* Chaque changement de documentation est désormais deux éditions, et une page ne
  peut pas être fusionnée sans sa traduction. Une petite correction en anglais est
  aussi une petite correction en français, et le test refuse d'en laisser une
  atterrir seule.
* Le coût initial est élevé : dix-neuf décisions d'architecture et un jeu complet
  de guides, dans une langue dont le vocabulaire technique pour ce domaine —
  descripteur, suppression, catalogue, train — doit être arrêté une fois puis
  appliqué de façon cohérente.
* Un contributeur qui n'écrit pas le français ne peut pas mener seul un
  changement de documentation. C'est une vraie barrière à la contribution externe
  sur la documentation, et elle n'a d'autre atténuation qu'un mainteneur
  finissant la paire.

### Risks

* L'ensemble français dérive en sens tout en restant en structure. Le test de
  parité compare les titres et les blocs de code, ce qui attrape une page à
  moitié écrite et rate une page mal traduite. Rien d'autre que la revue
  n'attrape cela, et le vivier de relecteurs pour cela est d'une personne.
* Le précédent s'élargit. Un dépôt qui traduit `doc/` invite l'argument qu'il
  devrait traduire les README de paquets, les messages d'erreur que les
  analyseurs signalent, et à terme les titres de diagnostics — ces derniers ne
  pouvant pas être traduits du tout, puisqu'une `const` ne peut pas être
  localisée. La frontière posée dans la décision est l'atténuation et est énoncée
  pour cette raison.
* Deux des vérifications assertent contre des artefacts compilés ; elles portent
  donc le danger habituel d'une vérification qui lit une sortie de build : si la
  copie qui place ces artefacts à côté des tests venait à ne plus fonctionner, les
  assertions passeraient faute d'avoir quoi que ce soit à comparer.
  `DocumentedSiblingsTests` protège déjà sa propre famille contre exactement
  cela, et le même garde-fou a sa place ici.

## Follow-up Actions

* Réénoncer la règle de langue dans [`CLAUDE.md`](../../CLAUDE.md) et
  [`CONTRIBUTING.md`](../../CONTRIBUTING.md) : anglais par défaut partout, avec
  `doc/` bilingue et les README de paquets en anglais uniquement.
* Enregistrer la disposition, le bandeau, le pied de navigation et les règles de
  diagrammes dans [`doc/CONVENTIONS.fr.md`](../CONVENTIONS.fr.md), où la
  vérification peut être décrite à côté de la règle qu'elle fait respecter.
* Ajouter le projet de tests de documentation, y compris le garde-fou qui échoue
  quand il ne trouve rien à asserter.
* Traduire les dix-neuf décisions d'architecture existantes, et les renommer vers
  la paire `.en.md` / `.fr.md` que le reste de `doc/` emploie.

## References

* [ADR-0004](0004-state-the-coding-rules-where-an-agent-can-act-on-them.fr.md) —
  une règle est enregistrée là où l'outillage qui l'applique peut la lire, pour
  qu'aucune ne repose sur la seule attention.
* [ADR-0005](0005-require-an-enforcing-check-before-any-automation-merges.fr.md) —
  la même norme appliquée à ce qu'une automatisation est autorisée à fusionner.
* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.fr.md) —
  vérifier une affirmation contre ce que l'analyseur déclare, jamais contre un
  autre document.
* [`doc/CONVENTIONS.fr.md`](../CONVENTIONS.fr.md) — la disposition et les
  vérifications que cette décision exige.
* [`first-class-errors`](https://github.com/Reefact/first-class-errors) — le
  projet frère dont cette décision suit la disposition bilingue et la navigation.
