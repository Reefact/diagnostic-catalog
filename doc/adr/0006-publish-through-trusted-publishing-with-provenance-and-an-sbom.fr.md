# ADR-0006 | Publier via trusted publishing, avec provenance signée et SBOM embarqué

🌍 **Langues :**  
🇬🇧 [English](./0006-publish-through-trusted-publishing-with-provenance-and-an-sbom.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-07-30
**Accepted:** 2026-07-30
**Decision Makers:** Reefact

## Context

Le dépôt publie des paquets NuGet sur nuget.org depuis un workflow GitHub
Actions. Publier exige une clé d'API.

Une clé d'API à longue durée de vie stockée en secret de dépôt est valide jusqu'à
sa révocation, utilisable depuis n'importe où par quiconque l'obtient, et accorde
des droits de publication sur chaque paquet qu'elle couvre. nuget.org supporte
aussi le **trusted publishing** : un workflow présente un jeton OIDC GitHub, et
nuget.org l'échange contre une clé à courte durée de vie et à usage unique, à
condition qu'une politique nommant le dépôt et le workflow existe.

Un paquet publié est **immuable**. nuget.org ne permet pas de remplacer une
version ; une erreur ne se corrige qu'en publiant une autre version, et la
mauvaise reste listée ou, au mieux, délistée.

nuget.org **signe au dépôt** chaque téléversement, en ajoutant un fichier de
signature à l'intérieur du `.nupkg`. Les octets sur nuget.org ne sont donc pas
les octets que la build a produits, et leurs sommes de contrôle diffèrent.

Les consommateurs d'un catalogue de règles de diagnostic n'ont aucun moyen
pratique de dire, à partir du seul paquet, quel commit et quelle build l'ont
produit.

Les attentes en matière de chaîne d'approvisionnement logicielle incluent
désormais couramment un inventaire lisible par machine des composants d'un
paquet, et OpenSSF Scorecard — que ce dépôt exécute déjà — note les releases
signées et les builds épinglées et reproductibles.

Le workflow de release est le seul workflow qu'aucune exécution CI ordinaire
n'exerce : sa résolution de version, son empaquetage, son échange
d'identifiants et ses permissions s'exécutent pour la première fois sur un vrai
tag, en production, une seule fois.

## Decision

Les paquets sont publiés sur nuget.org via le trusted publishing OIDC, et chaque
artefact publié porte une attestation de provenance de build signée et un SBOM
SPDX embarqué.

## Rationale

Le trusted publishing supprime l'identifiant permanent plutôt que de le protéger.
Rien dans les secrets du dépôt n'accorde de droits de publication : un secret
fuité, un workflow de fork compromis ou un jeton trop large ne peuvent pas
publier ; la clé qui existe est frappée par exécution, expire, et est à usage
unique. Étant donné qu'une version publiée ne peut jamais être retirée, supprimer
l'identifiant qui pourrait en publier une mauvaise vaut plus que n'importe quelle
politique de rotation sur un identifiant stocké.

L'attestation de provenance répond à la question que le paquet lui-même ne peut
pas : quels dépôt, workflow, commit et runner ont produit ces octets. Elle est
signée par l'identité OIDC du job lui-même, elle ne peut donc pas être forgée par
quiconque n'a pas exécuté ce workflow sur ce dépôt. Elle est délibérément
produite **avant** l'une comme l'autre publication, si bien que rien n'est jamais
publié sans avoir été attesté.

L'attestation couvre les artefacts tels que construits, publiés tels quels en
actifs de GitHub Release. Elle ne peut pas couvrir la copie nuget.org, parce que
nuget.org la re-signe — c'est une propriété du registre, pas une lacune de la
décision, et c'est pourquoi les actifs de Release existent à côté de la fiche
nuget.org : ils sont la copie qu'un consommateur peut vérifier contre
l'attestation, tandis que la copie nuget.org est vérifiée par la signature du
registre.

Le SBOM est embarqué dans le paquet plutôt que publié à côté, si bien qu'il
voyage avec l'artefact et ne peut pas en être séparé par un miroir, un proxy ou
un flux hors ligne. Sa présence est assertée sur le paquet produit à chaque
empaquetage, et non supposée depuis un drapeau de build : une régression de
l'outillage SBOM laisserait sinon un empaquetage vert produire des paquets sans
inventaire.

Parce que rien de tout cela n'est exercé par la CI ordinaire, la chaîne est
rendue répétable de deux façons. La partie sans effet de bord — build,
empaquetage, SBOM, gardes d'empaquetage — s'exécute à chaque pull request, si
bien que les régressions d'empaquetage apparaissent en revue normale. Le reste
est une répétition déclenchable qui garde délibérément la connexion OIDC et
l'attestation : une politique de trusted publishing mal configurée ou une
permission manquante est exactement ce qu'une répétition doit attraper, et les
deux échouent bruyamment sans rien publier. Seules les deux étapes aux effets
irréversibles sont sautées.

## Alternatives Considered

### Stocker une clé d'API NuGet à longue durée de vie en secret de dépôt

Envisagé parce que c'est le mode par défaut, que c'est simple, et que cela
n'exige aucune politique configurée sur nuget.org.

Rejeté parce que cela crée un identifiant permanent dont le rayon d'action est
chaque paquet qu'il couvre et dont la durée de vie court jusqu'à ce que quelqu'un
pense à le faire tourner. Face à un registre immuable, le coût d'une seule
utilisation de cet identifiant est permanent.

### Trusted publishing, mais sans attestation ni SBOM

Envisagé parce que le trusted publishing seul supprime déjà le risque
d'identifiant, qui est le plus grand, et que le reste ajoute des pièces mobiles
au chemin de release.

Rejeté parce que cela laisse le consommateur sans aucun moyen de relier un paquet
à la build qui l'a produit, et sans inventaire de ce qu'il contient. Le coût
marginal est une étape et une référence de paquet ; la valeur marginale est la
seule preuve qu'un consommateur peut vérifier indépendamment.

### Publier le SBOM en actif de release séparé

Envisagé parce que cela garde le paquet plus petit et le SBOM plus facile à lire
sans dézipper.

Rejeté parce qu'un SBOM qui voyage séparément est un SBOM qui cesse de voyager :
un consommateur qui résout via un miroir, un flux proxy ou une restauration hors
ligne voit le paquet et jamais l'actif. L'embarquer lie l'inventaire à l'artefact
qu'il décrit.

### Signer les paquets avec un certificat de signature de code à la place

Envisagé parce que la signature d'auteur est le mécanisme NuGet établi et qu'elle
est vérifiable avec `dotnet nuget verify`.

Rejeté comme mécanisme *principal* parce qu'elle réintroduit exactement ce que le
trusted publishing supprime : un secret à longue durée de vie détenu par le
workflow. Elle atteste en outre d'une identité, pas d'une provenance — elle dit
qui a signé, pas quel commit et quelle build ont produit les octets. Elle reste
compatible avec cette décision si une signature d'auteur devait s'y ajouter.

## Consequences

### Positive

* Aucun identifiant du dépôt ne peut publier un paquet.
* Chaque artefact publié peut être tracé jusqu'à un commit, un workflow et un
  runner, par n'importe qui, sans faire confiance à ce projet.
* Chaque paquet porte son propre inventaire de composants.
* Le chemin de release est répétable, échange d'identifiants compris, sans rien
  publier.

### Negative

* Publier dépend d'une politique configurée hors du dépôt, sur nuget.org, pour
  chaque paquet — y compris chaque nouveau paquet de catalogue.
* Un consommateur qui vérifie la provenance doit le faire contre l'actif de
  GitHub Release, pas contre la copie nuget.org, ce qui est une distinction à
  documenter.
* Le job de release a besoin de trois portées d'écriture dont il n'aurait pas
  besoin autrement.

### Risks

* La politique de trusted publishing est absente ou mal configurée pour un
  nouveau paquet, si bien que la première vraie release d'un catalogue échoue à
  l'échange d'identifiants. Atténuation : la connexion OIDC s'exécute aussi lors
  des répétitions, la politique peut donc être validée avant qu'un tag soit
  poussé.
* L'outillage SBOM régresse et les paquets partent sans inventaire. Atténuation :
  la présence du manifeste est assertée sur le paquet produit, et l'assertion
  s'exécute à chaque pull request via la répétition.
* On suppose que l'attestation couvre les octets nuget.org et une vérification
  contre eux échoue, se lisant comme une altération. Atténuation : c'est énoncé
  ici et dans le workflow, à côté de l'étape qui la produit.

## Follow-up Actions

* Créer une politique de trusted publishing sur nuget.org pour chaque paquet
  publié, et positionner la variable de dépôt `NUGET_USER` au nom de compte
  nuget.org.
* Déclencher une répétition avant la première vraie release de chaque nouveau
  paquet.
* Documenter, pour les consommateurs, que la provenance se vérifie contre
  l'actif de GitHub Release.

## References

* [ADR-0002](0002-partition-releases-into-trains-by-commit-scope.fr.md) — ce
  qu'une release publie.
* [ADR-0007](0007-depend-across-trains-through-published-packages.fr.md).
* `.github/workflows/release.yml`, `.github/workflows/release-dryrun.yml`,
  `tools/packaging/pack.sh`.
