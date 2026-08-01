# Décisions d'architecture

🌍 **Langues :**  
🇬🇧 [English](./README.en.md) | 🇫🇷 Français (ce fichier)

Enregistrements datés des décisions significatives — leur contexte, l'option retenue, et
les conséquences. Une ADR est un journal historique : une fois acceptée, elle n'est pas
modifiée sur place ; une décision se révise en écrivant une **nouvelle** ADR qui remplace
l'ancienne, et le statut de l'ancienne passe à *Superseded* avec un lien vers son
successeur.

## Quand écrit-on une ADR ?

Chaque pull request est confrontée à cette base — le moment où de nouvelles décisions
entrent dans le code. La plupart des pull requests n'embarquent aucune décision
d'architecture et n'ajoutent aucune ADR ; c'est la vérification qui est obligatoire, pas
l'artefact. Le test du « significatif » : *si l'implémentation changeait mais que la
décision tenait, l'ADR ne devrait pas avoir besoin d'être modifiée.* Une décision nouvelle
est **enregistrée** ici, une décision qui en remplace une autre s'écrit en ADR
**remplaçante**, et un changement qui **contredit** une ADR acceptée est signalé au
mainteneur. La procédure pour les agents — rédiger en *Proposed*, ne jamais changer un
statut unilatéralement — est dans [`AGENTS.md`](../../AGENTS.md).

## Une ADR est un enregistrement de décision, pas une spécification

Une ADR capture une **décision et le raisonnement qui la sous-tend** — pas la manière dont
cette décision est implémentée. La mécanique d'implémentation (code, configuration, YAML,
options exactes, extraits XML ou de commandes, parcours garde par garde ou étape par étape)
vit dans le code et dans les commentaires qui l'accompagnent — jamais dans l'ADR. En
particulier, **la section Rationale est un argument, pas un document de conception** : si un
paragraphe explique *comment quelque chose est construit* plutôt que *pourquoi la décision
est la bonne*, il appartient à côté du code, et l'ADR y renvoie. Un test utile : si
l'implémentation changeait mais que la décision tenait, l'ADR ne devrait pas avoir besoin
d'être modifiée.

## Conventions de fichiers

* Une décision par numéro, sous `doc/adr/`, nommée `NNNN-titre-court.{en,fr}.md` — un
  numéro de séquence à quatre chiffres, un titre en minuscules kebab-case, et une langue :
  `0001-floor-the-libraries-on-net-framework-4-7-2.fr.md`.
* **Un numéro n'est jamais réutilisé.** Deux enregistrements rédigés en parallèle peuvent
  entrer en collision sur un numéro ; celui accepté en premier le garde, et l'autre est
  renuméroté avant d'être accepté — un lien vers `ADR-0022` doit atteindre une seule
  décision.
* Chaque ADR existe en **anglais et en français**, et la paire arrive dans le même commit
  ([ADR-0022](0022-maintain-every-document-under-doc-in-english-and-french.fr.md)).
  **L'anglais fait foi** : là où les deux divergent, c'est la version anglaise qui a raison.
  Une traduction n'enregistre aucune décision que sa page anglaise n'enregistre pas, et une
  correction à l'une est une correction aux deux.
* Chaque ADR suit le format ci-dessous ; [`template.md`](template.md) en est un squelette
  prêt à copier.
* L'index en bas de ce fichier liste chaque ADR et son statut. Ajouter une ADR, c'est
  ajouter sa ligne — ici **et** dans son pendant anglais.

## Format

### Titre et en-tête

Le H1 vient d'abord, puis le bandeau de langue, puis le bloc d'en-tête — la
disposition que chaque page sous `doc/` suit
([`doc/CONVENTIONS.fr.md`](../CONVENTIONS.fr.md)) :

```markdown
# ADR-{number} | {Short Title}

🌍 **Languages:**
🇬🇧 English (this file) | 🇫🇷 [Français](./{number}-{short-title}.fr.md)

**Status:** Proposed | Accepted | Superseded | Deprecated
**Proposed:** YYYY-MM-DD
**Accepted:** YYYY-MM-DD
**Decision Makers:** {Names or team}
```

L'en-tête porte **une ligne datée par état que la décision a réellement atteint dans ce
dépôt**, et aucune date n'est jamais écrasée. Un enregistrement rédigé en *Proposed* porte
`Proposed:` seul ; l'accepter ajoute `Accepted:` en dessous et laisse la première ligne
intacte. Les deux dates restent alors pour de bon : le moment où la réflexion a eu lieu et
celui où elle a été ratifiée sont deux faits différents, et un journal qui ne garde que le
second ne peut pas dire combien de temps une décision a attendu, ni lesquelles ont été
ratifiées à vue.

Une supersession n'ajoute rien — **elle ne déplace aucune date et n'en introduit aucune**.
La décision a été prise quand elle a été prise, et c'est ce que l'enregistrement conserve ;
la nouvelle date appartient au successeur. Ce qui relie les deux est le lien, pas la date :
une ADR *Superseded* renvoie à l'ADR qui la remplace, à côté du statut.

### Context

Décrivez toute l'information qui a mené à la décision. L'objectif est que quelqu'un qui ne
connaît pas le projet puisse comprendre pourquoi cette décision devait être prise.

Incluez chaque aspect pertinent le cas échéant :

* contexte métier ;
* exigences fonctionnelles ;
* contraintes techniques ;
* contraintes d'architecture ;
* contraintes opérationnelles ;
* exigences de sécurité ;
* exigences de performance ;
* considérations de coût ;
* compétences et expérience de l'équipe ;
* limitations du système existant ;
* contraintes organisationnelles ou politiques ;
* dépendances externes ;
* délais ou contraintes de livraison ;
* risques connus.

Cette section contient **des faits uniquement**. Elle ne justifie ni n'explique la solution
retenue.

### Decision

Décrivez la décision en **une seule phrase**.

Règles :

* une phrase, pas plus ;
* aucune justification ;
* aucune alternative ;
* aucune explication historique ;
* aucun détail d'implémentation, sauf s'il fait partie de la décision elle-même.

Exemple :

> L'application utilisera PostgreSQL comme base de données relationnelle principale.

### Rationale

Expliquez pourquoi cette décision est le meilleur choix compte tenu du contexte. Chaque
argument doit être traçable jusqu'à une information déjà décrite dans la section Context ;
si un argument manque au Context, ajoutez-y d'abord le fait manquant.

Cette section explique :

* pourquoi la décision satisfait les exigences ;
* quelles contraintes elle adresse ;
* quels compromis ont été acceptés ;
* pourquoi les bénéfices attendus l'emportent sur les inconvénients.

C'est **de l'argument uniquement**. Elle **ne contient pas** de détail d'implémentation —
ni code, ni configuration, ni YAML, ni options exactes, ni extraits XML ou de commandes, ni
« comment c'est construit » garde par garde ou étape par étape. Cela, c'est de la
spécification : renvoyez à l'endroit où elle vit réellement plutôt que de la recopier ici.
Nommer le *rôle* d'un garde et *pourquoi il existe* est de l'argument et a sa place ici ;
documenter *comment le garde est câblé* est de la spécification et n'en a pas.

### Alternatives Considered

Documentez chaque alternative sérieuse évaluée. Chacune explique **pourquoi elle a été
envisagée** et **pourquoi elle a finalement été rejetée** — pas simplement qu'elle l'a été.

```markdown
### {Alternative 1}

Why it was considered.

Why it was ultimately rejected.
```

### Consequences

Décrivez les conséquences de l'adoption de cette décision — impacts positifs comme
négatifs — sous trois sous-titres :

* **Positive** — les bénéfices que la décision apporte ;
* **Negative** — les coûts et limitations acceptés avec elle ;
* **Risks** — ce qui pourrait mal tourner plus tard, et les mesures d'atténuation en place.

### Follow-up Actions

Listez le travail rendu nécessaire par cette décision. Exemples :

* mettre à jour la documentation ;
* migrer des composants existants ;
* créer des lignes directrices techniques ;
* surveiller les performances après déploiement ;
* ajouter des tests automatisés ;
* planifier une revue future.

### References

Matériel de support, optionnel :

* ADR liées ;
* RFC ;
* spécifications ;
* benchmarks ;
* documents de conception ;
* pull requests ;
* suivis d'incidents ;
* diagrammes.

## Index

| ADR | Titre | Statut |
|---|---|---|
| [ADR-0001](0001-floor-the-libraries-on-net-framework-4-7-2.fr.md) | Plancher le support .NET Framework des bibliothèques à 4.7.2 | Accepted |
| [ADR-0002](0002-partition-releases-into-trains-by-commit-scope.fr.md) | Partitionner les releases en trains par scope de commit | Accepted |
| [ADR-0003](0003-adopt-and-enforce-a-conventional-commits-convention.fr.md) | Adopter et faire respecter une convention Conventional Commits | Accepted |
| [ADR-0004](0004-state-the-coding-rules-where-an-agent-can-act-on-them.fr.md) | Énoncer les règles de code là où un agent peut les appliquer | Accepted |
| [ADR-0005](0005-require-an-enforcing-check-before-any-automation-merges.fr.md) | Exiger une vérification bloquante avant toute fusion automatisée | Accepted |
| [ADR-0006](0006-publish-through-trusted-publishing-with-provenance-and-an-sbom.fr.md) | Publier via trusted publishing, avec provenance signée et SBOM embarqué | Accepted |
| [ADR-0007](0007-depend-across-trains-through-published-packages.fr.md) | Dépendre d'un autre train par paquet publié, jamais par référence de projet | Accepted |
| [ADR-0008](0008-express-a-rule-as-a-marked-static-class-of-constants.fr.md) | Exprimer une règle en classe statique de constantes marquée, jamais en interface | Accepted |
| [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.fr.md) | Générer le contenu d'un catalogue depuis les descripteurs, jamais depuis la documentation | Accepted |
| [ADR-0010](0010-carry-a-retired-rule-forward-as-obsolete.fr.md) | Reporter une règle retirée en obsolète, ne jamais supprimer sa constante | Accepted |
| [ADR-0011](0011-redistribute-rule-facts-only-never-the-vendors-prose.fr.md) | Redistribuer les faits d'une règle uniquement, jamais la prose de l'éditeur | Superseded by [ADR-0014](0014-ship-the-vendors-rule-title-as-a-catalogues-documentation.fr.md) |
| [ADR-0012](0012-a-catalogue-never-renames-a-member-it-published.fr.md) | Un catalogue ne renomme jamais un membre qu'il a publié | Accepted |
| [ADR-0013](0013-write-the-shell-tooling-for-posix-sh-not-bash.fr.md) | Écrire l'outillage shell pour POSIX sh, pas bash | Accepted |
| [ADR-0014](0014-ship-the-vendors-rule-title-as-a-catalogues-documentation.fr.md) | Livrer le titre de règle de l'éditeur comme documentation d'un catalogue | Accepted |
| [ADR-0015](0015-a-catalogues-version-runs-on-its-own-line.fr.md) | La version d'un catalogue suit sa propre ligne, jamais celle de l'amont | Accepted |
| [ADR-0016](0016-mirror-stylecops-prerelease-line.fr.md) | Refléter la ligne de préversion de StyleCop, pas sa version stable périmée | Accepted |
| [ADR-0017](0017-publish-the-generator-as-a-cli-on-its-own-release-train.fr.md) | Publier le générateur en CLI, sur son propre train de release | Accepted |
| [ADR-0018](0018-a-code-fix-never-decides-what-only-the-author-can.fr.md) | Un correctif ne décide jamais ce que seul l'auteur peut décider | Accepted |
| [ADR-0019](0019-resolve-packages-through-the-users-own-nuget-configuration.fr.md) | Résoudre les paquets via la configuration NuGet de l'utilisateur | Accepted |
| [ADR-0020](0020-a-catalogue-is-generated-for-c-sharp-only.fr.md) | Un catalogue est généré pour C# uniquement | Accepted |
| [ADR-0021](0021-derive-the-build-rule-set-from-the-quality-profile.fr.md) | Dériver le jeu de règles Sonar du build depuis le profil qualité du serveur | Accepted |
| [ADR-0022](0022-maintain-every-document-under-doc-in-english-and-french.fr.md) | Maintenir chaque document sous `doc/` en anglais et en français | Accepted |
| [ADR-0023](0023-acquire-a-solutions-analyzers-by-declaration.fr.md) | Acquérir les analyseurs d'une solution par déclaration, jamais par découverte | Accepted |
| [ADR-0024](0024-fail-on-any-diagnostic-the-ratchet-cannot-see.fr.md) | Échouer sur tout diagnostic que le cliquet d'avertissements ne voit pas | Accepted |
| [ADR-0025](0025-bind-every-feature-commit-to-the-documentation-it-changed.fr.md) | Lier chaque commit de fonctionnalité à la documentation qu'il a changée | Accepted |
