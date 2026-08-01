# ADR-0019 | Résoudre les paquets via la configuration NuGet de l'utilisateur

🌍 **Langues :**  
🇬🇧 [English](./0019-resolve-packages-through-the-users-own-nuget-configuration.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-07-31
**Accepted:** 2026-07-31
**Decision Makers:** Reefact

## Context

Ce dépôt publie `dcat` (ADR-0017), et la raison de cette publication y est
énoncée : la méthode enregistrée dans ADR-0009 — dériver un catalogue des
descripteurs que les analyseurs déclarent, jamais de la documentation — vaut la
peine pour quiconque livre des analyseurs, pas seulement pour les trois éditeurs
reflétés ici.

Jusqu'à cette décision, l'outil atteignait exactement un flux. `api.nuget.org`
était écrit en constante dans les sources, et le protocole flat-container était
appelé à la main en HTTP.

Les analyseurs sont fréquemment *non* publics. Les règles maison d'une entreprise
sont livrées en paquet sur un flux que seule cette entreprise peut atteindre, et
atteindre un tel flux est ce que l'outil ne savait pas faire : aucune source
configurée n'était consultée, et aucun identifiant n'était jamais envoyé.

La configuration de NuGet n'est pas un fichier unique mais une hiérarchie —
niveau machine, niveau utilisateur, et chaque dossier depuis le répertoire de
travail en remontant — et `dotnet restore` sur la même machine résout déjà contre
elle. Les identifiants qui y sont déclarés existent en plusieurs sortes. Deux
d'entre elles, les valeurs chiffrées au repos et celles fournies par un
fournisseur d'identifiants, ne sont pas du tout lisibles depuis les fichiers de
configuration : les obtenir signifie demander au client NuGet lui-même.

Le dépôt épingle chaque dépendance centralement et demande qu'une nouvelle porte
une raison claire (`CLAUDE.md`, « Change guidelines »). `dcat` est un artefact
publié dont un consommateur paie la taille une fois, à l'installation ; avant
cette décision son paquet pesait 6,4 Mo.

## Decision

`dcat` résout et télécharge les paquets via la configuration NuGet de
l'utilisateur — les sources et les identifiants que sa machine déclare — jamais
via un flux que ce dépôt aurait choisi pour lui.

## Rationale

La décision découle de l'*objet* de la publication de l'outil. ADR-0017 soutient
que la méthode devrait être disponible pour quiconque livre des analyseurs ; un
outil qui atteint un seul flux public n'est disponible que pour les gens dont les
analyseurs sont déjà publics, ce qui est à peu près l'opposé de la population qui
a le plus besoin d'un catalogue de règles maison. La lacune n'était pas une
commodité manquante, c'était l'argument de la publication discrètement vidé de sa
substance.

Choisir le flux à la place de l'utilisateur est aussi une décision que l'outil
n'a pas qualité pour prendre. Sa machine répond déjà à « d'où viennent les
paquets » pour chaque autre outil de la chaîne .NET, et elle y répond à un
endroit qu'il contrôle et que son organisation audite. Un outil en désaccord avec
`dotnet restore` sur la même machine est surprenant de la manière précise qui
coûte le plus de temps : il échoue là où tout le reste réussit, et la raison est
invisible parce que la configuration qu'il a ignorée est celle que l'utilisateur
regardait.

Les identifiants sont ce qui fait de ceci une décision portant sur une dépendance
plutôt que seulement sur un comportement. Honorer une configuration dont les
secrets ne sont pas lisibles hors du client NuGet n'est pas une chose qu'une
implémentation maison puisse faire à moitié bien — elle serait correcte pour le
cas en clair et discrètement fausse pour les cas chiffré et fourni par un
fournisseur. La défaillance produite est de la pire forme disponible : le paquet
paraît ne pas exister, sur un flux dont l'utilisateur sait qu'il le contient.
Décider d'honorer la configuration décide donc contre l'implémentation maison ;
les deux ne sont pas des choix indépendants.

Le coût accepté est une grosse dépendance à l'intérieur d'un artefact publié, et
un outil dont le comportement dépend désormais de la machine sur laquelle il
tourne. Le second n'est pas un défaut à atténuer mais la décision elle-même :
« résoudre comme cette machine est configurée pour résoudre » est précisément
l'affirmation que deux machines configurées différemment résoudront
différemment. Ce que cela oblige en échange, c'est qu'un échec à trouver un
paquet doive dire quelles sources ont été consultées, faute de quoi l'utilisateur
ne peut pas voir la configuration que l'outil a employée.

## Alternatives Considered

### Garder le flux codé en dur et ajouter une option d'URL de source

Envisagé parce que cela n'exige aucune nouvelle dépendance et couvre le cas de
flux privé le plus simple : un serveur interne en lecture anonyme.

Rejeté parce qu'un flux privé est d'ordinaire privé, c'est-à-dire avec
identifiants, et que ceci couvre exactement les flux qui ne le sont pas. Cela
laisse en outre l'outil ignorer les sources que la machine déclare, il
continuerait donc d'être en désaccord avec `dotnet restore` sur la même machine —
la surprise que cette décision existe pour supprimer — tout en paraissant l'avoir
traitée.

### Lire la configuration à la main

Envisagé parce que cela évite entièrement la dépendance et traiterait la
hiérarchie de configuration et les identifiants en clair, ce qui représente
l'essentiel du mécanisme.

Rejeté parce que la partie qu'il ne peut pas traiter est celle qui échoue en
silence. Les identifiants chiffrés et les fournisseurs d'identifiants ne sont pas
lisibles hors du client NuGet ; cette implémentation fonctionnerait donc pour
certains utilisateurs et, pour d'autres, rapporterait qu'un paquet n'existe pas
sur un flux qui le contient. Un outil correct pour un sous-ensemble de ses
utilisateurs et trompeur pour le reste est pire qu'un outil honnêtement limité,
parce que rien ne distingue les deux cas de l'extérieur.

### Exiger que l'utilisateur récupère le paquet lui-même

Envisagé parce que l'outil accepte déjà un fichier de paquet sur disque ; cela ne
coûte donc rien à construire.

Rejeté parce que cela déplace le travail vers l'utilisateur à chaque
régénération, et qu'un job planifié — le cas que les catalogues d'ici existent
pour servir — ne peut pas le faire du tout. Cela répond à « comment lire ce
paquet une fois » et non à « comment mon catalogue reste à jour ».

## Consequences

### Positive

* Un paquet sur un flux privé est lu sans aucun drapeau supplémentaire : l'outil
  emploie ce que la machine déclare déjà.
* L'outil s'accorde avec `dotnet restore` sur la même machine, y compris là où la
  configuration propre à un dépôt surcharge celle de l'utilisateur.
* La sélection de version devient un ordre SemVer plutôt qu'une position dans la
  réponse d'un flux, ce que l'implémentation précédente ne réussissait que parce
  qu'un flux la triait ainsi.

### Negative

* Une grosse dépendance voyage dans un artefact publié : le paquet de l'outil
  passe de 6,4 Mo à 7,7 Mo.
* Le comportement dépend de la configuration de la machine ; reproduire un échec
  de résolution exige donc la configuration et pas seulement la ligne de
  commande.

### Risks

* Le client est une large surface avec son propre historique d'avis de sécurité —
  la version à laquelle cette décision a été prise a été choisie plutôt qu'une
  antérieure portant `GHSA-g4vj-cjjj-v7hg`. Les avertissements d'audit NuGet sont
  délibérément des avertissements plutôt que des erreurs dans ce dépôt ; un futur
  avis ne fera donc pas échouer une build, et le remarquer relève de l'habitude de
  revue plutôt que d'une barrière.
* Une configuration qui résout sur la machine d'un mainteneur et pas sur un
  runner ressemblera à une défaillance de l'outil. Rapporter les sources
  consultées est ce qui garde cela diagnosticable, et c'est donc porteur plutôt
  que cosmétique.

## Follow-up Actions

* Aucune contraignante. Surveiller les avis de sécurité du client quand sa
  version est montée, puisque les avertissements d'audit qui les annonceraient
  autrement ne sont pas promus en erreurs ici.

## References

* [ADR-0009](0009-generate-catalog-content-from-analyzer-descriptors.fr.md) — la
  méthode dont la disponibilité est la raison même de publier l'outil.
* [ADR-0017](0017-publish-the-generator-as-a-cli-on-its-own-release-train.fr.md) —
  la décision de publier, dont celle-ci exécute l'argument.
* [`CLAUDE.md`](../../CLAUDE.md), « Change guidelines » — l'exigence qu'une
  nouvelle dépendance porte une raison claire.
