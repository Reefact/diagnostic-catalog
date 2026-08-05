# ADR-0035 | Badger avec son sujet un catalogue dont le préfixe de règle est déjà en service

🌍 **Langues :**  
🇬🇧 [English](./0035-badge-a-shared-prefix-catalogue-with-its-subject.en.md) | 🇫🇷 Français (ce fichier)

**Status:** Accepted
**Proposed:** 2026-08-05
**Accepted:** 2026-08-05
**Decision Makers:** Reefact

## Contexte

L'[ADR-0033](0033-cap-the-badge-at-three-letters.fr.md) est le document en vigueur. Sa décision
tient en une phrase qui porte deux clauses — une **source** et un **plafond** :

> Le badge d'un catalogue porte au plus trois lettres, en abrégeant le préfixe de règle lorsque
> celui-ci est plus long.

La clause de source est héritée de l'[ADR-0032](0032-badge-a-catalogues-icon-with-its-rule-prefix.fr.md),
que l'ADR-0033 remplace et dont elle a délibérément conservé le choix de ce que dit le badge :
*« le badge répond toujours depuis les règles plutôt que depuis l'éditeur »*.

**L'ADR-0033 avait déjà nommé le cas que ce document tranche, et l'a laissé ouvert sciemment.** Deux
fois, dans ses propres Conséquences :

> * Une abréviation doit être choisie pour chaque préfixe long, et deux éditeurs pourraient
>   raisonnablement abréger vers les deux mêmes lettres. Rien ne dérive l'abréviation comme le
>   préfixe lui-même était dérivé.

> * Les abréviations sont décidées une fois puis recopiées. Un futur catalogue dont le préfixe
>   s'abrège mal — **ou entre en collision avec un déjà en service** — n'a que ce document pour
>   argumenter, et la collision ne serait signalée par aucune vérification.

L'ADR-0032 avait consigné le même trou un cran plus tôt, et fermé l'échappatoire évidente :

> * Un éditeur dont les règles ne portent aucun préfixe distinctif, ou un préfixe qui entre en
>   collision avec un déjà utilisé, laisse la règle sans rien d'où dériver — et le repli est le nom
>   d'éditeur que ce document rejette.

Le trou n'est donc un oubli dans aucun des deux documents. C'est un risque énoncé qui s'est
réalisé, et ce que « n'a que ce document pour argumenter » a produit en pratique, c'est un
jugement pris deux fois, à la main, avant tout document.

**Ce qui l'a réalisé.** Trois catalogues reflètent des règles qui partagent le préfixe `RS`, et les
identifiants se répartissent proprement entre eux :

| Catalogue | Reflète | Identifiants | Règles |
| --- | --- | --- | ---: |
| `DiagnosticCatalog.Roslyn` | `Microsoft.CodeAnalysis.Analyzers` | `RS1xxx`, `RS2xxx` | 52 |
| `DiagnosticCatalog.PublicApi` | `Microsoft.CodeAnalysis.PublicApiAnalyzers` | `RS0016`–`RS0061` | 23 |
| `DiagnosticCatalog.BannedApi` | `Microsoft.CodeAnalysis.BannedApiAnalyzers` | `RS0030`, `RS0031`, `RS0035` | 3 |

Appliquer l'ADR-0033 à la lettre donne aux trois le badge `RS`. Le sigle, la plaque et le dégradé
sont ceux de la famille et ne varient pas : trois badges identiques sont donc trois icônes identiques
à l'octet près — ce que `PackageIconTests` fait échouer par conception, sur la règle qu'aucun
catalogue ne partage l'icône d'un autre. Pour le deuxième et le troisième catalogue d'un préfixe
partagé, la décision en vigueur n'est donc pas seulement muette : elle est insatisfiable.

**Ce qui a été livré à la place.** `DiagnosticCatalog.PublicApi` porte `API` et
`DiagnosticCatalog.BannedApi` porte `BAN`, chacun tranché par le mainteneur au moment d'ajouter le
catalogue. Mesuré sur les treize badges de `tools/icon/badges.py` face aux préfixes lus dans la
source générée de chaque catalogue :

* **sept** sont exactement le préfixe de règle — `S`, `CA`, `IL`, `RS`, `IDE`, `SA`, `ASP` ;
* **quatre** sont l'abréviation d'un préfixe plus long, ce qui est l'ADR-0033 telle qu'écrite —
  `XU`, `NU`, `MST`, `SYS` ;
* **deux** ne sont ni l'un ni l'autre — `API` et `BAN`, sur des catalogues dont les règles sont `RS`.

Ces deux-là sont tout ce que ce document tranche.

## Décision

Lorsque le préfixe de règle d'un catalogue est déjà porté par le badge d'un autre catalogue, son
badge nomme à la place le sujet du paquet qu'il reflète, dans la limite des trois lettres, et le
préfixe reste au catalogue qui le publie déjà.

## Justification

**Le plafond n'est pas touché ; seule la clause de source bouge.** La mesure de l'ADR-0033 — la
hauteur de capitale aux 128px auxquels une liste rend l'icône, et un mot de six lettres réduit à une
tache de 4,8px — porte sur la longueur et n'est pas affectée par la provenance des lettres. Onze des
treize badges se lisent exactement comme l'ADR-0033 le dit. Ce document change la réponse pour le
cas auquel l'ADR-0033 ne pouvait pas répondre, et la remplace parce que les deux clauses vivent dans
une seule phrase : un badge de trois lettres qui n'est pas le préfixe satisfait le plafond et
contredit la source.

**Le préfixe reste au titulaire parce que le déplacer coûte plus cher que le garder.** Un badge
publié est sur nuget.org, dans une liste, à côté d'un paquet qu'un consommateur a déjà installé ;
le changer change une icône que quelqu'un reconnaît. Le premier-en-service ne demande par ailleurs
aucun jugement et aucune mesure — c'est un fait sur le dépôt, vérifiable en lisant
`tools/icon/badges.py`. Tous les autres départages envisagés (le plus de règles, la plage
d'identifiants la plus étroite, le paquet le plus ancien) bougent quand l'amont bouge, ce qui
ferait d'une icône publiée une fonction des releases de quelqu'un d'autre.

**Le sujet, parce que c'est ce qu'il faut départager.** Dès lors que le préfixe ne distingue plus
deux catalogues, il ne reste au badge qu'un travail, et `PublicApi` contre `BannedApi` est
exactement la distinction dont a besoin un lecteur qui tient `RS0030`. C'est aussi dérivable, au
sens où l'ADR-0032 y tenait : le sujet vient du paquet nommé dans `eng/catalogs.json`, donc un
relecteur vérifie le badge contre le manifeste plutôt que contre le goût — relation plus faible que
de le lire dans un identifiant de règle, et relation réelle.

**Le prix que cela paie, dit plutôt qu'escamoté.** L'ADR-0032 rejetait le nom de l'éditeur en
partie parce qu'il *« répète ce que nuget.org imprime déjà à côté de l'icône »*, et `API` à côté
d'un paquet nommé `DiagnosticCatalog.PublicApi` le répète en partie. C'est une perte réelle, et
elle est acceptée ici pour deux raisons. L'alternative, ce sont trois icônes identiques, ce qui est
pire et ne compile pas. Et ce qui est dépensé, c'est le **sujet du catalogue**, non le nom de
l'éditeur : `Microsoft` n'apparaît dans aucun des deux badges, donc l'interdit écrit par
l'ADR-0032 et repris par l'ADR-0033 reste intact — un lecteur apprend toujours du badge quelque
chose que l'identifiant du paquet ne crie pas, à savoir lequel des trois catalogues `RS` il tient.

## Alternatives envisagées

### Laisser les trois catalogues `RS` partager le badge `RS`

Rejetée parce que cela ne compile pas. Le badge est la seule partie variable de l'icône, donc trois
badges `RS` sont trois fichiers identiques et `PackageIconTests` les fait échouer. Même en suspendant
cette vérification, un badge identique sur trois paquets ne répond à rien de ce pour quoi il existe.

### Allonger le préfixe jusqu'à ce qu'il les sépare — `RS1`, `RS0`, `RS2`

Séduisante, et mesurée comme fausse. Cela marche pour `DiagnosticCatalog.Roslyn`, dont les règles
sont `RS1xxx` et `RS2xxx`, mais `PublicApiAnalyzers` émet `RS0016`–`RS0061` et `BannedApiAnalyzers`
émet `RS0030`, `RS0031` et `RS0035` : les deux sont en `RS0`, donc les deux catalogues qui entrent
réellement en collision sont exactement ceux que cela ne sépare pas. Cela ferait de plus d'un badge
une fonction de la numérotation amont — une seule nouvelle règle `RS00xx` dans le paquet d'écriture
d'analyseurs et le partage à trois est faux.

### Badger les catalogues en collision avec le nom de l'éditeur

Déjà rejetée, et inutile ici de surcroît. L'ADR-0032 a rejeté les badges au nom de l'éditeur et
l'ADR-0033 l'a conservé ; et les trois paquets sont de Microsoft, donc l'éditeur ne sépare rien.

### Fusionner les trois en un seul catalogue

Rejetée sur la forme plutôt que sur la préférence. Un catalogue reflète un paquet : `package` dans
`eng/catalogs.json` est une chaîne unique, contrairement à `projects` et `assemblies`, et
`[assembly: CatalogSource]` enregistre une source et une version. Un catalogue lisant trois paquets
ne peut pas s'exprimer, et ce qu'il déclarerait comme source est une question ouverte à laquelle
ce document n'a pas besoin de répondre.

### Laisser le mainteneur trancher au cas par cas

Rejetée parce que c'est l'état que ce document existe pour quitter, et parce que l'ADR-0033 a
rejeté la même forme une clause plus loin : *« une règle qui s'en remet au jugement produit
exactement l'étalement mesuré ci-dessus, et ne donne au relecteur rien à vérifier »*. Le jugement a
maintenant été exercé deux fois sans rien d'écrit, et c'est ainsi qu'`API` et `BAN` ne sont
défendables qu'en demandant.

## Conséquences

### Positives

* Le deuxième et le troisième catalogue d'un préfixe partagé sont décidables sans demander, ce qui
  est la propriété que l'ADR-0032 défendait et qu'aucun des deux documents n'assurait pour ce cas.
* `API` et `BAN` cessent d'être deux jugements non documentés et deviennent les conséquences d'un
  document qu'un relecteur peut vérifier.
* Le préfixe garde son sens pour le catalogue qui le porte : `RS` sur `DiagnosticCatalog.Roslyn`
  correspond toujours aux identifiants que le lecteur tient.
* Rien de déjà publié ne bouge. La règle est écrite pour que le titulaire garde son badge, donc
  l'adopter ne redessine aucune icône.

### Négatives

* Pour un catalogue en collision, le badge n'est plus lu dans les règles du catalogue. Un relecteur
  le vérifie contre `eng/catalogs.json` au lieu de la source générée — relation plus faible que celle
  décrite par l'ADR-0032, et plus faible que celle que gardent les onze autres badges.
* `API` et `BAN` répètent en partie l'identifiant de paquet imprimé à côté d'eux, ce qui est le
  coût dont l'ADR-0032 se servait pour rejeter les badges au nom de l'éditeur. Deux badges sur
  treize le paient désormais.
* « Le sujet du paquet » demande un jugement comme une abréviation en demande un. Que
  `PublicApiAnalyzers` donne `API` plutôt que `PUB` a été décidé, non dérivé, et ce document ne rend
  pas cette étape mécanique.

### Risques

* **Rien ne vérifie tout cela.** `PackageIconTests` n'affirme que la distinction des icônes, et
  `tools/icon/check-icon-template.py` lit le sigle et le dégradé, et délibérément pas le lettrage. Un
  badge qui n'est ni le préfixe ni le sujet passe au vert, exactement comme l'ADR-0033 le
  consignait pour la collision qu'elle prévoyait.
* Le premier-en-service n'est stable que tant que les badges ne sont pas renommés. Un catalogue
  renommé, retiré ou fusionné dans un autre laisse ouverte la question de qui hérite du préfixe nu,
  et ce document n'y répond pas.
* Un quatrième catalogue de règles `RS` demanderait un troisième sujet distinct en trois lettres. Le
  gisement est fini, et rien ici ne dit ce qui se passe lorsqu'il est épuisé.

## Actions de suivi

* Énoncer la règle là où un lecteur qui publie son propre catalogue la rencontre —
  [`doc/guide/packaging-a-catalogue.fr.md`](../guide/packaging-a-catalogue.fr.md) et sa moitié
  anglaise.
* Citer ce document à côté de l'ADR-0033 dans la docstring de `tools/icon/badges.py`, qui est la
  table où la règle s'applique.
* Une vérification de ce que dit un badge reste absente, et mérite sa propre décision plutôt qu'une
  assertion improvisée — l'argument sur l'endroit où une telle vérification peut vivre est celui
  que l'issue #149 fait pour le relevé des paquets : une tâche planifiée qui lit l'arbre, pas une
  barrière par pull request.

## Références

* [ADR-0033](0033-cap-the-badge-at-three-letters.fr.md) — le document que celui-ci remplace, dont le
  plafond est conservé et dont la clause de source ne pouvait pas répondre à un préfixe partagé,
  comme ses propres Risques le prévoyaient.
* [ADR-0032](0032-badge-a-catalogues-icon-with-its-rule-prefix.fr.md) — là où le badge a été lié pour
  la première fois au préfixe de règle, et où la collision a été consignée comme risque pour la
  première fois.
* `tests/DiagnosticCatalog.Catalogs.UnitTests/PackageIconTests.cs` — la vérification qui fait de
  trois badges identiques un échec de build plutôt qu'une affaire de goût.
* `tools/icon/badges.py` — le registre où la règle s'applique, et le seul endroit où un badge est
  déclaré.
* [`eng/catalogs.json`](../../eng/catalogs.json) — là où est enregistré le paquet dont un badge tire
  son sujet.
