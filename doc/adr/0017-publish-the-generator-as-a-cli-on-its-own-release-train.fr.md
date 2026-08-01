# ADR-0017 | Publier le générateur en CLI, sur son propre train de release

🌍 **Langues :**  
🇬🇧 [English](./0017-publish-the-generator-as-a-cli-on-its-own-release-train.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-07-31
**Accepted:** 2026-07-31
**Decision Makers:** Reefact

## Context

Ce dépôt publie des catalogues qui reflètent des analyseurs qu'il ne possède pas,
et il les génère avec un outil qu'il garde pour lui. `eng/CatalogGen` est marqué
non empaquetable et vit hors de `src/` : il produit les catalogues et ne livre
rien.

Cela a une conséquence enregistrée. Les commits sont partitionnés en trains de
release par scope (ADR-0002), et `cataloggen` est le seul scope n'appartenant à
aucun train — `CONTRIBUTING.md` en énonce la raison sans détour : le générateur ne
livre rien, donc rien de ce qu'il fait ne peut faire bouger une version publiée.
C'est le seul endroit de la convention où un `feat` ou un `fix` n'atteignant
aucune note de version est correct plutôt qu'accidentel.

Trois faits ont bougé depuis que cela a été écrit.

**Le générateur n'est plus spécifique aux catalogues d'ici.** Jusqu'à récemment
il ne pouvait atteindre des analyseurs que d'une seule façon : nommer un paquet,
et il est téléchargé depuis nuget.org. Il lit désormais aussi des assemblages
d'analyseurs déjà sur disque ; ce qu'il fait pour `SonarAnalyzer.CSharp`, il le
fait pour un analyseur qu'un développeur a construit il y a cinq secondes. La
capacité qui le rendait interne — savoir aller chercher les trois paquets que ce
dépôt reflète — n'est plus ce qu'il est.

**Un outil en ligne de commande est déjà prévu, et a déjà un scope.** La
spécification liste `DiagnosticCatalog.Tool` parmi les évolutions possibles et
esquisse ses verbes, dont `generate`. Le scope `cli` existe dans la liste fermée
des scopes et route aujourd'hui vers le train `lib`, alors qu'aucun projet de CLI
n'existe pour l'employer.

**Le train `lib` est délibérément très stable**, parce qu'un contrat de catalogue
repose dessus. `CONTRIBUTING.md` donne cela comme raison de ne pas scoper
`cataloggen` en `core` : monter dans le train `lib` ferait bouger la version de la
fondation pour un travail que ses consommateurs ne voient jamais.

Deux faits supplémentaires pèsent sur l'endroit où un générateur publié se
situerait. Son rythme est fixé par des choses extérieures à ce dépôt — les
versions de Roslyn contre lesquelles les analyseurs amont sont compilés, et les
dispositions de dossiers que leurs paquets emploient — et non par le contrat de la
fondation, qui change pour des raisons sans rapport et bien plus rarement. Et il
ne détient aucune référence vers la fondation : il émet `using DiagnosticCatalog;`
sous forme de texte, rien ne lie donc les deux assemblages et la règle
inter-trains d'ADR-0007 n'a rien à lier.

Enfin, le dépôt `first-class-errors` du même mainteneur publie déjà un outil en
ligne de commande ainsi : un train `cli`, portant à la fois le scope de la CLI et
celui du générateur de documentation derrière elle, versionnant à part du train
`lib` de ce dépôt.

## Decision

Le générateur de catalogues est publié en outil .NET sur un train de release
`cli` qui lui est propre, vers lequel routent les scopes `cli` et `cataloggen` et
qui versionne indépendamment de `lib`.

## Rationale

La décision découle de ce qu'un numéro de version est censé dire. La version du
train `lib` parle pour la fondation sur laquelle repose chaque contrat de
catalogue, et le travail sur le générateur n'atteint aucun consommateur de cette
fondation : une correction sur la façon dont un paquet d'analyseur est dépaqueté
ne change rien qu'un projet référençant `DiagnosticCatalog` puisse observer.
Publier le générateur sur `lib` mettrait du mouvement dans un numéro dont la
stabilité est tout l'objet, et le ferait à un rythme que la fondation ne contrôle
pas — chaque version de Roslyn et chaque réempaquetage amont devenant une release
de la fondation. C'est l'argument que `CONTRIBUTING.md` fait déjà pour tenir le
générateur hors de `core`, et le publier ne l'affaiblit pas ; cela l'aiguise,
parce que le mouvement serait désormais visible aux consommateurs sous forme de
montée de version plutôt que simplement enregistré.

La raison de publier tout court est que l'utilité du générateur a cessé d'être
spécifique à ce dépôt. La valeur qu'il porte est la méthode enregistrée dans
ADR-0009 — lire les descripteurs, jamais la documentation, parce que la plateforme
ne valide jamais la catégorie d'une suppression et qu'une transcription qui
dérive ne produit de symptôme nulle part. Ce raisonnement ne porte pas sur
SonarSource, Microsoft ou StyleCop ; il vaut pour quiconque livre des analyseurs
et veut un catalogue que ses consommateurs puissent référencer symboliquement.
Garder privée la seule implémentation revient à ce que chacune de ces personnes
transcrive ses propres règles à la main — le mode de défaillance qu'ADR-0009
existe pour refuser — ou reconstruise l'outil. Maintenant qu'atteindre leurs
analyseurs n'exige plus qu'ils soient un paquet public, rien d'autre que
l'empaquetage ne se tient entre la méthode et les gens qu'elle sert.

Un train à lui, plutôt qu'une quatrième position d'un autre genre, est ce que
l'architecture existante prévoit déjà pour cela : un train est précisément un
paquet qui versionne et publie à son propre rythme, et le générateur a un rythme
propre. Nommer ce train `cli` plutôt qu'inventer un autre nom découle du fait que
la spécification a déjà décidé qu'il y a un outil en ligne de commande à
plusieurs verbes, dont la génération. Un train `cataloggen` séparé versionnerait
deux fois le même exécutable.

Y router `cataloggen`, plutôt que retirer le scope au profit de `cli`, conserve
une distinction que le dépôt trouve déjà utile. La liste des scopes sépare
ailleurs aussi la coquille de l'outil du moteur derrière, et le registre de
release s'en lit mieux : un changement dans la façon dont les descripteurs sont
lus et un changement dans la façon dont la commande analyse ses arguments sont
deux faits différents à propos du même paquet. Ce qui change, c'est seulement
qu'ils atteignent désormais une note de version, ce qui est tout le contenu de
cette décision pour ce qui concerne `cataloggen`.

L'absence de toute référence du générateur vers la fondation est ce qui rend cela
bon marché plutôt que délicat. ADR-0007 interdit à un projet d'un train de porter
une référence de projet vers un projet d'un autre, parce que `dotnet pack`
estampillerait une dépendance sur une version jamais publiée. Il n'y a aucune
référence de ce genre à retirer : le générateur produit du texte nommant les
attributs de la fondation et ne s'y lie jamais. Le décalage de versions que cela
crée normalement — l'outil construit contre une version d'une bibliothèque, le
projet du consommateur en détenant une autre — ne peut pas survenir, pour la même
raison que l'outil du dépôt frère ne détient aucune référence vers sa propre
bibliothèque. Cette propriété mérite d'être énoncée parce qu'elle est facile à
perdre : ajouter une référence plus tard, pour le confort de faire vérifier par
le compilateur l'usage de l'API par l'émetteur, échangerait une garantie
structurelle contre une garantie vérifiée.

## Alternatives Considered

### Garder le générateur privé, tel qu'aujourd'hui

Envisagé sérieusement, parce que cela a un avantage réel : rien de l'outil n'est
un contrat public. Sa ligne de commande, son format de manifeste, son
comportement quand un analyseur ne peut pas être construit et la plage de
versions de Roslyn qu'il tolère sont tous libres de changer en un seul commit,
parce que le seul appelant est le job nocturne de ce dépôt.

Rejeté parce que le coût de cette liberté est désormais payé par d'autres.
L'argument d'ADR-0009 — qu'un catalogue dérivé de la documentation a tort avec
assurance et que rien dans la build d'un consommateur ne le contredit — vaut pour
chaque auteur d'analyseurs, et le seul outil qui agit dessus est celui gardé ici.
La liberté vaut quelque chose ; elle ne vaut pas d'être la raison pour laquelle
la méthode reste indisponible.

### Le publier sur le train `lib`, avec le scope `cli` là où il route déjà

Envisagé parce que cela n'exige aucun nouveau train : le scope existe et pointe
déjà vers `lib`, c'est donc le plus petit changement possible.

Rejeté parce que cela inverse le raisonnement que `CONTRIBUTING.md` emploie pour
tenir le générateur hors de `core`. La version de la fondation bougerait pour un
travail que ses consommateurs ne peuvent pas voir, à un rythme dicté par les
versions d'analyseurs amont, et un projet dépendant de la fondation verrait une
agitation qui ne dit rien du contrat dont il dépend.

### Donner au générateur un train propre, distinct de celui de la CLI

Envisagé parce que le générateur et une coquille en ligne de commande autour de
lui sont réellement des composants différents, et parce que cela laisserait le
moteur bouger sans republier l'outil.

Rejeté parce qu'ils sont un seul artefact publié. La spécification prévoit une
commande unique à plusieurs verbes, dont la génération ; deux trains versionnant
le même exécutable produiraient deux numéros de version pour un paquet et aucun
moyen de dire lequel un utilisateur a installé.

### Publier le générateur en bibliothèque plutôt qu'en outil

Envisagé parce que cela permettrait à une build d'intégrer la génération
directement, et parce qu'une bibliothèque est la forme que ce dépôt sait déjà
publier.

Rejeté parce que cela met la mauvaise chose dans le processus du consommateur.
Lire des descripteurs signifie charger des assemblages d'analyseurs tiers et les
construire — exécuter du code que le consommateur n'a pas écrit — et une forme
bibliothèque fait que cela se produit dans sa build plutôt que dans un outil
qu'il a invoqué. Le besoin est une étape de build, pas une API.

## Consequences

### Positive

* La version de la fondation continue de dire quelque chose de la fondation, sur
  le train dont la stabilité porte les contrats de catalogues.
* La méthode enregistrée dans ADR-0009 devient disponible pour quiconque livre
  des analyseurs, plutôt que pour les seuls catalogues que ce dépôt reflète.
* Le travail sur le générateur atteint un registre de release pour la première
  fois : les commits `cataloggen` cessent d'être corrects-mais-invisibles et
  commencent à décrire un artefact publié.
* Le rythme de l'outil devient honnête — il bouge quand Roslyn et les
  dispositions de paquets amont bougent, ce qui est ce qui le pilote réellement.

### Negative

* La ligne de commande, le format de manifeste et la forme du fichier généré
  deviennent des contrats publics, modifiables seulement par une montée de
  version plutôt que par un commit.
* Le générateur cible actuellement un unique runtime récent, ce qu'un outil
  publié ne peut pas supposer des machines qui l'installent.
* Un comportement d'émission écrit pour ce dépôt — la provenance enregistrée dans
  le catalogue, et les bandeaux rafraîchis dans les fichiers voisins — atteint des
  dépôts qui ne l'ont pas demandé et n'en veulent peut-être pas.

### Risks

* **Le chargement se produit dans le processus de l'outil.** Le lecteur résout
  chaque requête Roslyn sur la version qu'il détient déjà, ce qui fonctionne
  parce que ce dépôt contrôle les trois paquets qu'il lit. Publiée, la plage
  tolérée devient une chose que les utilisateurs découvrent par l'échec.
* **Une identité de paquet coûte cher à renommer après adoption.** L'identité
  sous laquelle ce train publie devrait être arrêtée et réservée avant la
  première release plutôt qu'après.
* **La provenance peut ne pas convenir à un catalogue de première partie.** La
  documentation propre à la fondation énonce qu'un catalogue maintenu à côté de
  son propre analyseur n'a besoin d'aucun enregistrement de provenance, ce qui est
  exactement le cas que crée une source lue localement.

## Follow-up Actions

* Arrêter l'identité publiée et le nom de commande, et réserver l'identité avant
  la première release.
* Décider du plancher de runtime que l'outil cible, et si une build sur ce
  plancher est autorisée à tourner sur des majeures plus récentes.
* Décider si la provenance et la réécriture des bandeaux sont toujours actives ou
  optionnelles, maintenant qu'une source peut être de première partie.
* Déplacer le scope `cli` hors du train `lib` et router `cataloggen` vers le
  nouveau, dans la source de vérité unique que l'outillage de release lit.
* Envisager de lire les descripteurs hors processus, pour que le runtime auquel
  le lecteur se lie puisse suivre les assemblages qu'on lui donne plutôt que
  celui de l'outil.

## References

* [ADR-0002](0002-partition-releases-into-trains-by-commit-scope.fr.md) — la
  partition en trains à laquelle cette décision ajoute.
* [ADR-0007](0007-depend-across-trains-through-published-packages.fr.md) — la
  règle de dépendance inter-trains, à laquelle ce train n'a rien à lier.
* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.fr.md) — la
  méthode dont la disponibilité est la raison de publier.
* [`CONTRIBUTING.md`](../../CONTRIBUTING.md) — les tableaux des scopes et des
  trains, et le raisonnement pour tenir le générateur hors de `core`.
* [`doc/specification.fr.md`](../specification.fr.md) §25, §25.6 — l'outil prévu
  et ses verbes.
* [`Reefact/first-class-errors`](https://github.com/Reefact/first-class-errors) —
  un dépôt frère publiant un outil en ligne de commande sur un train `cli` propre.
