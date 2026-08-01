# ADR-0002 | Partitionner les releases en trains par scope de commit

🌍 **Langues :**  
🇬🇧 [English](./0002-partition-releases-into-trains-by-commit-scope.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-07-30
**Accepted:** 2026-07-30
**Decision Makers:** Reefact

## Context

Le dépôt livre deux sortes d'artefacts. L'une est la **fondation** : la
bibliothèque qui définit, génère et valide les catalogues, avec ses analyseurs,
son outil en ligne de commande et son paquet de support de test. Les autres sont
les **catalogues**, un par éditeur de règles de diagnostic — SonarQube, les
analyseurs .NET de Microsoft, StyleCop.

Les deux sortes changent pour des raisons sans rapport. Un catalogue change quand
son éditeur livre, renomme, déprécie ou retire des règles ; le dépôt ne contrôle
pas ce rythme et ne l'observe pas à l'avance. La fondation change quand son
propre contrat change, ce qui devrait être rare — un catalogue est un contrat, et
la fondation est ce sur quoi le contrat repose.

Un identifiant de règle est référencé symboliquement par les consommateurs. En
retirer un d'un catalogue est un changement cassant de ce catalogue. Cela ne dit
rien de la fondation.

Le versionnage sémantique décrit la compatibilité d'**un** artefact. Un numéro de
version partagé par plusieurs artefacts n'en décrit aucun : il bouge pour des
raisons qui appartiennent à un autre.

Le dépôt exige déjà un scope Conventional Commits tiré d'une liste fermée,
vérifié au moment du commit (ADR-0003). Le scope est une déclaration que l'auteur
fait sur le composant auquel le changement appartient.

Au moment de cette décision, le dépôt ne contient aucun code : la forme choisie
ici contraint chaque projet, paquet et changelog qui suivra.

## Decision

Chaque train de release — la fondation, et un par catalogue d'éditeur —
versionne, tagge et publie indépendamment, et un commit est routé vers son train
par son scope Conventional Commits.

## Rationale

Une version unique pour tout le dépôt forcerait une release de la fondation à
chaque publication de règles par un éditeur, et rendrait le numéro de version de
la fondation non informatif : un consommateur qui l'épingle ne pourrait pas
distinguer un changement du contrat de la fondation d'un ajout à la liste de
règles de quelqu'un d'autre. La stabilité de la fondation est la propriété que la
bibliothèque vend ; coupler sa version au rythme d'un tiers reviendrait à vendre
l'inverse.

Dans l'autre sens, un éditeur qui retire des règles doit pouvoir publier une
version majeure de ce catalogue sans entraîner la majeure de la fondation avec
lui. Sous une version partagée, le ménage d'un seul éditeur annoncerait un
changement cassant à chaque consommateur de chaque paquet.

Router par scope, plutôt que par une métadonnée séparée, garde la correspondance
dérivable de la seule histoire. Le scope est déjà exigé, déjà tiré d'une liste
fermée, et déjà vérifié à l'écriture ; le routage n'a donc besoin d'aucun
artefact susceptible de se périmer, et la destination d'un commit est décidée par
la personne qui la connaît le mieux — son auteur — au moment où elle l'écrit.

Le compromis accepté est une surface de release plus grande : plus de tags, un
changelog par train, et une liste de scopes qui grandit avec chaque catalogue. Ce
coût est proportionnel au nombre de catalogues, payé une fois par catalogue, et
c'est le prix direct de l'indépendance que la décision achète.

Parce que le routage dépend de la présence du scope, les deux types de commit qui
pilotent une version ne peuvent pas rester sans scope — un commit sans scope ne
correspondrait à aucun train et disparaîtrait silencieusement du registre de
release. En faire un rejet ferme à l'écriture est ce qui empêche cette décision de
se dégrader en pratique.

## Alternatives Considered

### Une version unique pour tout le dépôt

Envisagé parce que c'est le processus de release le plus simple : un tag, un
changelog, un numéro à raisonner, et aucun routage.

Rejeté parce que cela rend la version de la fondation dénuée de sens pour les
consommateurs qui y tiennent le plus, et parce que cela force chaque paquet à
bouger dès qu'un éditeur bouge. La simplicité est réelle, mais elle s'achète en
détruisant l'information que le numéro de version existe pour porter.

### Un dépôt par catalogue

Envisagé parce que cela donne à chaque catalogue sa propre version, son
changelog, son suivi d'incidents et sa CI par construction, sans aucun mécanisme
de routage.

Rejeté parce que les catalogues sont consommateurs de la fondation et de son
paquet de support de test ; scinder maintenant dupliquerait toute la surface
CI/CD sur quatre dépôts avant même qu'un seul catalogue existe, et transformerait
chaque changement de la fondation en migration inter-dépôts. La scission reste
disponible plus tard, catalogue par catalogue, une fois que l'un d'eux aura une
vie propre.

### Router par chemin — quel projet un commit a touché

Envisagé parce qu'un chemin est un fait observable qui n'exige aucune discipline
de l'auteur et ne peut pas être mal déclaré.

Rejeté parce qu'un commit touchant du code partagé de la fondation en même temps
qu'un catalogue correspondrait à plusieurs trains sans moyen de dire à quelle
release il appartient, et parce qu'un chemin enregistre où le code se trouve
plutôt que ce dont le changement parle. Le scope est une déclaration d'intention,
qui est ce dont un registre de release a besoin ; un chemin de fichier est un
artefact d'implémentation qui bouge sous les refactorings.

### Publier tout en continu depuis `main`, sans trains

Envisagé parce que cela supprime entièrement le jugement de versionnage.

Rejeté parce que cela ne traite pas le problème : les paquets partageraient
toujours un numéro, et les consommateurs d'une fondation qui promet la stabilité
ont besoin de pouvoir l'épingler.

## Consequences

### Positive

* La version de la fondation décrit la fondation, et la version d'un catalogue
  décrit les règles de cet éditeur.
* Le changement cassant d'un éditeur n'est annoncé qu'aux consommateurs de cet
  éditeur.
* Le registre de release est dérivé de l'historique des commits, sans
  correspondance séparée à maintenir.
* Ajouter un catalogue est additif : un scope, un train, un changelog.

### Negative

* Plus de tags, plus de changelogs, et un processus de release à exécuter par
  train.
* La liste des scopes doit être étendue à chaque ajout de catalogue, dans le
  linter et dans le guide de contribution ensemble.
* Un changement unique qui couvre la fondation et un catalogue demande deux
  commits, ou un commit portant les deux scopes et atterrissant dans les notes
  des deux trains.

### Risks

* Un contributeur choisit un scope plausible mais faux et le changement est
  annoncé dans les notes du mauvais train. Atténuation : la liste des scopes est
  fermée et vérifiée, et le guide de contribution nomme chaque scope par
  l'éditeur ou le composant qu'il couvre, y compris la distinction délibérée
  entre `analyzers` et `netanalyzers`.
* L'outillage de release et la liste de scopes du linter divergent, si bien qu'un
  commit valide ne route nulle part. Atténuation : le guide de contribution
  énonce que la liste du linter est la copie vérifiable et que les deux changent
  ensemble ; un scope que le linter ne connaît pas est rejeté au moment du
  commit, ce qui échoue du bon côté.

## Follow-up Actions

* Implémenter le workflow de release par train, avec des tags préfixés du train.
* Donner à chaque projet de catalogue son propre changelog à sa création.
* Étendre ensemble la liste des scopes, le tableau des scopes du guide et le
  tableau des trains à chaque ajout de catalogue.

## References

* [ADR-0003](0003-adopt-and-enforce-a-conventional-commits-convention.fr.md) — la
  convention dont ce routage dépend.
* [CONTRIBUTING.md](../../CONTRIBUTING.md) — « Scope », et le tableau des trains.
* `tools/commit-lint/lint-commit-message.sh` — la liste de scopes vérifiée.
